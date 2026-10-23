"""Shortens level 6 programs with CP-SAT.

Usage: python3 polish.py <input> <output> full|local <seconds per model> [jobs] [workers per job]

Mode full models each whole program, seeded with the program as a solution hint,
and minimises the ticks before the last required submission.
Proven optimal patterns land in <output>.optimal and get skipped by later full runs.

Mode local keeps most of a program fixed and re-plans one piece of it.
A window has to reach the emitter state, pattern matter and submission count that its old ticks reached, in one tick less.
A suffix starts from the state that the fixed ticks before it leave and only has to finish the remaining submissions.

A shorter program replaces the old one once a replay confirms it.
"""

import json
import os
import sys
import time
from multiprocessing import Pool

from ortools.sat.python import cp_model

FIRE = [(0, 2), (1, 1), (1, -1), (0, -2), (-1, -1), (-1, 1)]
RAIL_WALK = [(-1, 1), (0, 2), (1, 1), (1, -1), (0, -2), (-1, -1)]
WINDOW = 10
SUFFIX = 16


def read_patterns(path):
    lines = open(path).read().split('\n')
    count = int(lines[0])
    i = 1
    patterns = []
    for _ in range(count):
        while not lines[i].strip():
            i += 1
        radius, rows, _, required = map(int, lines[i].split())
        patterns.append((radius, lines[i + 1:i + 1 + rows], required))
        i += 1 + rows
    return patterns


def geometry(radius, grid):
    rail = []
    cell = (radius, 0)
    for dr, dc in RAIL_WALK:
        for _ in range(radius):
            rail.append(cell)
            cell = (cell[0] + dr, cell[1] + dc)

    def in_grid(r, c):
        return 0 <= r < len(grid) and 0 <= c < len(grid[0]) and grid[r][c] != '-'

    target = sorted((r, c) for r in range(len(grid)) for c in range(len(grid[0])) if grid[r][c] == 'M')
    skirt = sorted({(r + dr, c + dc) for r, c in target for dr, dc in FIRE
                    if in_grid(r + dr, c + dc) and grid[r + dr][c + dc] == 'O'})
    span = 2 * radius + 1
    # Interior cells that emitter e's beam crosses from rail offset o.
    cover = [[[] for _ in range(span)] for _ in range(6)]
    for e in range(6):
        for o in range(span):
            r, c = rail[(e * radius + o - radius) % (6 * radius)]
            dr, dc = FIRE[e]
            r, c = r + dr, c + dc
            while in_grid(r, c):
                if grid[r][c] != '*':
                    cover[e][o].append((r, c))
                r, c = r + dr, c + dc
    return target, skirt, cover


def states(radius, grid, program):
    """State before the first tick and after each tick: offsets, on bits, target matter, submissions so far.

    The offsets go negative or past the rail for a program that leaves its emitters' useful range."""
    target, skirt, cover = geometry(radius, grid)
    offset, active = [radius] * 6, [False] * 6
    matter, submitted = set(), 0
    trail = [(tuple(offset), tuple(active), frozenset(), 0)]
    for line in program:
        for e, token in enumerate(line.split()):
            if token == 'F':
                offset[e] += 1
            elif token == 'B':
                offset[e] -= 1
            elif token == 'T':
                active[e] = not active[e]
        if any(o < 0 or o > 2 * radius for o in offset):
            return None
        hits = {}
        for e in range(6):
            if active[e]:
                for cell in cover[e][offset[e]]:
                    hits[cell] = hits.get(cell, 0) + 1
        for cell, n in hits.items():
            if n == 2:
                matter.add(cell)
            elif n >= 3:
                matter.discard(cell)
        if all(cell in matter for cell in target) and not any(cell in matter for cell in skirt):
            matter.difference_update(target)
            submitted += 1
        trail.append((tuple(offset), tuple(active), frozenset(matter & set(target)), submitted))
    return trail


def replay(radius, grid, program):
    """Number of submissions that the program produces, following the contest sandbox."""
    trail = states(radius, grid, program)
    return -1 if trail is None else trail[-1][3]


def plan(radius, grid, start, ticks, required, end, hint, seconds, workers):
    """Plans `ticks` ticks from `start` that submit `required` patterns.

    With an end state the plan has to reach it after the last tick; without one it minimises
    the ticks before the last submission. Returns (lines or None, proven)."""
    target, skirt, cover = geometry(radius, grid)
    span = 2 * radius + 1
    model = cp_model.CpModel()
    start_offset, start_active, start_matter = start

    at = [[[model.NewBoolVar('') for _ in range(span)] for _ in range(ticks + 1)] for _ in range(6)]
    on = [[model.NewBoolVar('') for _ in range(ticks + 1)] for _ in range(6)]
    for e in range(6):
        model.Add(at[e][0][start_offset[e]] == 1)
        model.Add(on[e][0] == int(start_active[e]))
        for t in range(ticks + 1):
            model.AddExactlyOne(at[e][t])
        for t in range(1, ticks + 1):
            for o in range(span):
                model.AddBoolOr([at[e][t][q] for q in (o - 1, o, o + 1) if 0 <= q < span]).OnlyEnforceIf(at[e][t - 1][o])
                # A move uses the emitter's instruction, so its state stays.
                for q in (o - 1, o + 1):
                    if 0 <= q < span:
                        model.Add(on[e][t] == on[e][t - 1]).OnlyEnforceIf([at[e][t - 1][o], at[e][t][q]])

    firing = {}

    def fires(e, t, o):
        if (e, t, o) not in firing:
            v = model.NewBoolVar('')
            model.AddImplication(v, at[e][t][o])
            model.AddImplication(v, on[e][t])
            model.AddBoolOr([v, at[e][t][o].Not(), on[e][t].Not()])
            firing[e, t, o] = v
        return firing[e, t, o]

    beams = {}
    for e in range(6):
        for o in range(span):
            for cell in cover[e][o]:
                beams.setdefault(cell, []).append((e, o))

    def hits(cell, t):
        return sum(fires(e, t, o) for e, o in beams.get(cell, []))

    for cell in skirt:
        for t in range(1, ticks + 1):
            model.AddLinearExpressionInDomain(hits(cell, t), cp_model.Domain.FromValues([0, 1, 3, 4, 5, 6]))

    def fixed(value):
        v = model.NewBoolVar('')
        model.Add(v == value)
        return v

    submit = [fixed(0)] + [model.NewBoolVar('') for _ in range(ticks)]
    held = {}
    for cell in target:
        before = fixed(int(cell in start_matter))
        for t in range(1, ticks + 1):
            h = hits(cell, t)
            create, destroy, now, kept = (model.NewBoolVar('') for _ in range(4))
            model.Add(h == 2).OnlyEnforceIf(create)
            model.Add(h != 2).OnlyEnforceIf(create.Not())
            model.Add(h >= 3).OnlyEnforceIf(destroy)
            model.Add(h <= 2).OnlyEnforceIf(destroy.Not())
            # Matter carries over unless the previous tick submitted the pattern.
            model.AddImplication(kept, before)
            model.AddImplication(kept, submit[t - 1].Not())
            model.AddBoolOr([kept, before.Not(), submit[t - 1]])
            model.AddImplication(now, destroy.Not())
            model.AddImplication(create, now)
            model.AddBoolOr([now, kept.Not(), destroy])
            model.AddBoolOr([create, kept]).OnlyEnforceIf(now)
            held[cell, t] = now
            before = now
    for t in range(1, ticks + 1):
        for cell in target:
            model.AddImplication(submit[t], held[cell, t])
        model.AddBoolOr([submit[t]] + [held[cell, t].Not() for cell in target])

    if end is not None:
        end_offset, end_active, end_matter = end
        model.Add(sum(submit) == required)
        for e in range(6):
            model.Add(at[e][ticks][end_offset[e]] == 1)
            model.Add(on[e][ticks] == int(end_active[e]))
        for cell in target:
            if cell in end_matter:
                model.AddBoolAnd([held[cell, ticks], submit[ticks].Not()])
            else:
                model.AddBoolOr([held[cell, ticks].Not(), submit[ticks]])
    else:
        done = [model.NewBoolVar('') for _ in range(ticks + 1)]
        model.Add(done[0] == int(required <= 0))
        for t in range(1, ticks + 1):
            model.Add(sum(submit[1:t + 1]) >= required).OnlyEnforceIf(done[t])
            model.Add(sum(submit[1:t + 1]) <= required - 1).OnlyEnforceIf(done[t].Not())
        model.Add(done[ticks] == 1)
        model.Minimize(sum(1 - done[t] for t in range(1, ticks + 1)))

    offset, active = list(start_offset), [int(a) for a in start_active]
    for t, line in enumerate(hint[:ticks], 1):
        for e, token in enumerate(line.split()):
            if token == 'F':
                offset[e] += 1
            elif token == 'B':
                offset[e] -= 1
            elif token == 'T':
                active[e] ^= 1
            if 0 <= offset[e] < span:
                for o in range(span):
                    model.AddHint(at[e][t][o], o == offset[e])
            model.AddHint(on[e][t], active[e])

    solver = cp_model.CpSolver()
    solver.parameters.max_time_in_seconds = seconds
    solver.parameters.num_workers = workers
    status = solver.Solve(model)
    if status not in (cp_model.OPTIMAL, cp_model.FEASIBLE):
        return None, status == cp_model.INFEASIBLE

    length = ticks if end is not None else int(round(solver.ObjectiveValue())) + 1
    lines = []
    for t in range(1, length + 1):
        tokens = []
        for e in range(6):
            before = next(o for o in range(span) if solver.Value(at[e][t - 1][o]))
            after = next(o for o in range(span) if solver.Value(at[e][t][o]))
            if after != before:
                tokens.append('F' if after > before else 'B')
            elif solver.Value(on[e][t]) != solver.Value(on[e][t - 1]):
                tokens.append('T')
            else:
                tokens.append('-')
        lines.append(' '.join(tokens))
    return lines, status == cp_model.OPTIMAL


def full(radius, grid, required, program, seconds, workers):
    start = ((radius,) * 6, (False,) * 6, frozenset())
    lines, proven = plan(radius, grid, start, len(program), required, None, program, seconds, workers)
    return lines or program, proven


def local(radius, grid, required, program, seconds, workers):
    """Re-plans windows from the program's start to its end, then suffixes of growing length."""
    a = 0
    while a + WINDOW <= len(program):
        trail = states(radius, grid, program)
        first, last = trail[a], trail[a + WINDOW]
        lines, _ = plan(radius, grid, first[:3], WINDOW - 1, last[3] - first[3], last[:3],
                        program[a:a + WINDOW], seconds, workers)
        if lines is not None and replay(radius, grid, program[:a] + lines + program[a + WINDOW:]) >= required:
            program = program[:a] + lines + program[a + WINDOW:]
        else:
            a += WINDOW // 2

    for length in range(SUFFIX // 2, SUFFIX + 1, SUFFIX // 4):
        a = max(0, len(program) - length)
        first = states(radius, grid, program)[a]
        lines, _ = plan(radius, grid, first[:3], len(program) - a, required - first[3], None,
                        program[a:], seconds, workers)
        if lines is not None and replay(radius, grid, program[:a] + lines) >= required:
            program = program[:a] + lines
    return program, False


def job(task):
    index, (radius, grid, required), program, mode, seconds, workers = task
    started = time.time()
    improved, proven = (full if mode == 'full' else local)(radius, grid, required, program, seconds, workers)
    if len(improved) >= len(program) or replay(radius, grid, improved) < required:
        improved = program
    return index, improved, proven, time.time() - started


def main():
    input_path, output_path, mode, seconds = sys.argv[1], sys.argv[2], sys.argv[3], float(sys.argv[4])
    jobs = int(sys.argv[5]) if len(sys.argv) > 5 else 4
    workers = int(sys.argv[6]) if len(sys.argv) > 6 else 4

    patterns = read_patterns(input_path)
    programs = [block.split('\n') for block in open(output_path).read().strip('\n').split('\n\n')]
    optimal_path = output_path + '.optimal'
    optimal = set(json.load(open(optimal_path))) if os.path.exists(optimal_path) else set()

    # Long programs first: they hold the most lines to win.
    tasks = [(i, patterns[i], programs[i], mode, seconds, workers)
             for i in sorted(range(len(patterns)), key=lambda i: -len(programs[i])) if i not in optimal]
    before = sum(map(len, programs))

    with Pool(jobs) as pool:
        for done, (i, program, proven, elapsed) in enumerate(pool.imap_unordered(job, tasks), 1):
            if len(program) < len(programs[i]):
                print(f'{done}/{len(tasks)} pattern {i}: {len(programs[i])} -> {len(program)} lines in {elapsed:.0f} s', flush=True)
                programs[i] = program
            if proven:
                optimal.add(i)
            temporary = output_path + '.tmp'
            with open(temporary, 'w') as f:
                f.write('\n\n'.join('\n'.join(p) for p in programs) + '\n')
            os.replace(temporary, output_path)
            with open(optimal_path, 'w') as f:
                json.dump(sorted(optimal), f)

    print(f'{before} -> {sum(map(len, programs))} lines, {len(optimal)} of {len(patterns)} proven optimal')


if __name__ == '__main__':
    main()
