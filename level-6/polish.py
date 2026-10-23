"""Shortens level 6 programs with CP-SAT.

Usage: python3 polish.py <input> <output> <seconds per pattern> [jobs] [workers per job]

Each program in <output> seeds a CP-SAT model as a solution hint.
The model spans as many ticks as the program holds and minimises the ticks before the last required submission.
A shorter program replaces the old one once a replay confirms it.
Proven optimal patterns land in <output>.optimal and get skipped on later runs.
"""

import json
import os
import sys
import time
from multiprocessing import Pool

from ortools.sat.python import cp_model

FIRE = [(0, 2), (1, 1), (1, -1), (0, -2), (-1, -1), (-1, 1)]
RAIL_WALK = [(-1, 1), (0, 2), (1, 1), (1, -1), (0, -2), (-1, -1)]


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


def replay(radius, grid, program):
    """Number of submissions that the program produces, following the contest sandbox."""
    target, skirt, cover = geometry(radius, grid)
    offset, active = [radius] * 6, [False] * 6
    matter, submitted = set(), 0
    for line in program:
        for e, token in enumerate(line.split()):
            if token == 'F':
                offset[e] += 1
            elif token == 'B':
                offset[e] -= 1
            elif token == 'T':
                active[e] = not active[e]
        if any(o < 0 or o > 2 * radius for o in offset):
            return -1
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
    return submitted


def shorten(radius, grid, required, hint, seconds, workers):
    """Returns (program, proven optimal) for the best program found within the hint's tick count."""
    target, skirt, cover = geometry(radius, grid)
    span = 2 * radius + 1
    ticks = len(hint)
    model = cp_model.CpModel()

    at = [[[model.NewBoolVar('') for _ in range(span)] for _ in range(ticks + 1)] for _ in range(6)]
    on = [[model.NewBoolVar('') for _ in range(ticks + 1)] for _ in range(6)]
    for e in range(6):
        model.Add(at[e][0][radius] == 1)
        model.Add(on[e][0] == 0)
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

    submit = [None] + [model.NewBoolVar('') for _ in range(ticks)]
    held = {}
    for cell in target:
        before = None
        for t in range(1, ticks + 1):
            h = hits(cell, t)
            create, destroy, now = model.NewBoolVar(''), model.NewBoolVar(''), model.NewBoolVar('')
            model.Add(h == 2).OnlyEnforceIf(create)
            model.Add(h != 2).OnlyEnforceIf(create.Not())
            model.Add(h >= 3).OnlyEnforceIf(destroy)
            model.Add(h <= 2).OnlyEnforceIf(destroy.Not())
            if before is None:
                model.Add(now == create)
            else:
                # Matter carries over unless the previous tick submitted the pattern.
                kept = model.NewBoolVar('')
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

    done = [model.NewBoolVar('') for _ in range(ticks + 1)]
    model.Add(done[0] == 0)
    for t in range(1, ticks + 1):
        model.Add(sum(submit[1:t + 1]) >= required).OnlyEnforceIf(done[t])
        model.Add(sum(submit[1:t + 1]) <= required - 1).OnlyEnforceIf(done[t].Not())
    model.Add(done[ticks] == 1)
    model.Minimize(sum(1 - done[t] for t in range(1, ticks + 1)))

    offset, active = [radius] * 6, [0] * 6
    for t, line in enumerate(hint, 1):
        for e, token in enumerate(line.split()):
            if token == 'F':
                offset[e] += 1
            elif token == 'B':
                offset[e] -= 1
            elif token == 'T':
                active[e] ^= 1
            for o in range(span):
                model.AddHint(at[e][t][o], o == offset[e])
            model.AddHint(on[e][t], active[e])

    solver = cp_model.CpSolver()
    solver.parameters.max_time_in_seconds = seconds
    solver.parameters.num_workers = workers
    status = solver.Solve(model)
    if status not in (cp_model.OPTIMAL, cp_model.FEASIBLE):
        return hint, False

    length = int(round(solver.ObjectiveValue())) + 1
    program = []
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
        program.append(' '.join(tokens))
    return program, status == cp_model.OPTIMAL


def job(task):
    index, (radius, grid, required), hint, seconds, workers = task
    started = time.time()
    program, optimal = shorten(radius, grid, required, hint, seconds, workers)
    if len(program) < len(hint) and replay(radius, grid, program) < required:
        program, optimal = hint, False
    return index, program, optimal, time.time() - started


def main():
    input_path, output_path, seconds = sys.argv[1], sys.argv[2], float(sys.argv[3])
    jobs = int(sys.argv[4]) if len(sys.argv) > 4 else 4
    workers = int(sys.argv[5]) if len(sys.argv) > 5 else 4

    patterns = read_patterns(input_path)
    programs = [block.split('\n') for block in open(output_path).read().strip('\n').split('\n\n')]
    optimal_path = output_path + '.optimal'
    optimal = set(json.load(open(optimal_path))) if os.path.exists(optimal_path) else set()

    # Long programs first: they hold the most lines to win.
    tasks = [(i, patterns[i], programs[i], seconds, workers)
             for i in sorted(range(len(patterns)), key=lambda i: -len(programs[i])) if i not in optimal]
    before = sum(map(len, programs))

    with Pool(jobs) as pool:
        for done, (i, program, proven, elapsed) in enumerate(pool.imap_unordered(job, tasks), 1):
            if len(program) < len(programs[i]):
                print(f'{done}/{len(tasks)} pattern {i}: {len(programs[i])} -> {len(program)} lines in {elapsed:.0f} s', flush=True)
                programs[i] = program
            if proven:
                optimal.add(i)
            with open(output_path, 'w') as f:
                f.write('\n\n'.join('\n'.join(p) for p in programs) + '\n')
            with open(optimal_path, 'w') as f:
                json.dump(sorted(optimal), f)

    print(f'{before} -> {sum(map(len, programs))} lines, {len(optimal)} of {len(patterns)} proven optimal')


if __name__ == '__main__':
    main()
