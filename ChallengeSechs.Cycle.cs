using System.Numerics;

namespace CodingContest;

public static partial class ChallengeSechs
{
    sealed partial class Solver
    {
        /// Searches for a cycle of instruction lines that builds the whole pattern once per pass,
        /// returning the program that travels to the cycle's start and repeats it,
        /// or null when no cycle shorter than <paramref name="maxLength"/> lines turns up.
        public List<int>? RunCycle(int maxLength, int attempts, Random random)
        {
            for (int length = 1; length < maxLength; length++)
            {
                var walks = Walks(length);
                var choice = Enumerable.Range(0, 6).Select(_ => random.Next(walks.Count)).ToArray();
                long score = Descend(walks, length, choice, random);

                for (int attempt = 0; attempt < attempts && score >= 1000; attempt++)
                {
                    var trial = (int[])choice.Clone();
                    for (int kick = random.Next(1, 3); kick > 0; kick--)
                        trial[random.Next(6)] = random.Next(walks.Count);

                    long trialScore = Descend(walks, length, trial, random);
                    if (trialScore <= score)
                        (choice, score) = (trial, trialScore);
                }

                if (score < 1000 && Unroll(walks, choice, length) is { } program)
                    return program;
            }
            return null;
        }

        /// Closed walks over one emitter's (offset, on) states, holding the state after each tick;
        /// the last entry doubles as the state that the walk starts from.
        List<int[]> Walks(int length)
        {
            var walks = new List<int[]>();
            var path = new int[length];

            void Extend(int start, int state, int tick)
            {
                if (tick == length)
                {
                    if (state == start)
                        walks.Add((int[])path.Clone());
                    return;
                }
                int o = state >> 1, on = state & 1;
                if (Math.Abs(o - (start >> 1)) > length - tick) return;

                foreach (int next in (int[])[state, state ^ 1, (o + 1) << 1 | on, (o - 1) << 1 | on])
                {
                    if ((next >> 1) < 0 || (next >> 1) >= span) continue;
                    path[tick] = next;
                    Extend(start, next, tick + 1);
                }
            }

            for (int start = 0; start < 2 * span; start++)
                Extend(start, start, 0);

            // A walk that never switches on covers nothing, wherever it sits; one parked at the start corner suffices.
            return walks
                .Where(walk => walk.Any(state => (state & 1) == 1) || walk.All(state => state == radius << 1))
                .Distinct(new WalkComparer())
                .ToList();
        }

        sealed class WalkComparer : IEqualityComparer<int[]>
        {
            public bool Equals(int[]? a, int[]? b) => a!.AsSpan().SequenceEqual(b);
            public int GetHashCode(int[] walk) => walk.Aggregate(17, (h, state) => h * 31 + state);
        }

        int Travel(int[] walk) => Math.Abs((walk[^1] >> 1) - radius) + (walk[^1] & 1);

        /// Lets each emitter in turn take the walk that best fits the other five, until none improves.
        /// Returns the score of the final choice; below 1000 the pattern forms every pass with a clean skirt.
        long Descend(List<int[]> walks, int length, int[] choice, Random random)
        {
            var one = new ulong[length * words];
            var two = new ulong[length * words];
            var three = new ulong[length * words];
            long current = long.MaxValue;

            for (int pass = 0; pass < 30; pass++)
            {
                bool improved = false;
                foreach (int e in Enumerable.Range(0, 6).OrderBy(_ => random.Next()))
                {
                    Array.Clear(one);
                    Array.Clear(two);
                    Array.Clear(three);
                    for (int other = 0; other < 6; other++)
                        if (other != e)
                            Accumulate(other, walks[choice[other]], length, one, two, three);

                    int best = choice[e];
                    long bestScore = Score(e, walks[best], length, one, two, three);
                    for (int i = 0; i < walks.Count; i++)
                    {
                        long score = Score(e, walks[i], length, one, two, three);
                        if (score < bestScore)
                            (best, bestScore) = (i, score);
                    }

                    if (bestScore < current)
                    {
                        current = bestScore;
                        improved = true;
                    }
                    choice[e] = best;
                }
                if (!improved) break;
            }

            return current;
        }

        void Accumulate(int e, int[] walk, int length, ulong[] one, ulong[] two, ulong[] three)
        {
            for (int t = 0; t < length; t++)
            {
                if ((walk[t] & 1) == 0) continue;
                var ray = beam[e][walk[t] >> 1];
                for (int w = 0, i = t * words; w < words; w++, i++)
                {
                    three[i] |= two[i] & ray[w];
                    two[i] |= one[i] & ray[w];
                    one[i] |= ray[w];
                }
            }
        }

        /// Skirt hits weigh most, then target cells missing after one pass from an empty pattern, then travel.
        long Score(int e, int[] walk, int length, ulong[] one, ulong[] two, ulong[] three)
        {
            Span<ulong> present = stackalloc ulong[words];
            long hits = 0;
            for (int t = 0; t < length; t++)
            {
                bool on = (walk[t] & 1) == 1;
                var ray = beam[e][walk[t] >> 1];
                for (int w = 0, i = t * words; w < words; w++, i++)
                {
                    ulong m = on ? ray[w] : 0;
                    ulong th = three[i] | (two[i] & m);
                    ulong exactlyTwo = (two[i] | (one[i] & m)) & ~th;
                    hits += BitOperations.PopCount(exactlyTwo & skirt[w]);
                    present[w] = (present[w] | (exactlyTwo & targetFull[w])) & ~th;
                }
            }

            int missing = targetCount;
            foreach (var word in present)
                missing -= BitOperations.PopCount(word);
            return hits * 1_000_000 + missing * 1000L + Travel(walk);
        }

        /// Travels to the cycle's start with every emitter off, switches the needed ones on in the last travel tick,
        /// then repeats the cycle until a replay confirms the required submissions.
        List<int>? Unroll(List<int[]> walks, int[] choice, int length)
        {
            var chosen = choice.Select(i => walks[i]).ToArray();
            int travel = chosen.Max(Travel);
            var program = new List<int>();

            for (int tick = 1; tick <= travel; tick++)
            {
                int action = 0;
                for (int e = 0; e < 6; e++)
                {
                    int target = chosen[e][^1] >> 1;
                    int moves = Math.Abs(target - radius);
                    if (tick <= moves)
                        action |= (target > radius ? 2 : 3) << (2 * e);
                    else if (tick == travel && (chosen[e][^1] & 1) == 1)
                        action |= 1 << (2 * e);
                }
                program.Add(action);
            }

            var cycle = new int[length];
            for (int t = 0; t < length; t++)
                for (int e = 0; e < 6; e++)
                {
                    int from = chosen[e][(t + length - 1) % length], to = chosen[e][t];
                    int code = from == to ? 0 : (from ^ to) == 1 ? 1 : (to >> 1) > (from >> 1) ? 2 : 3;
                    cycle[t] |= code << (2 * e);
                }

            for (int pass = 0; pass < pattern.Count + 2; pass++)
            {
                program.AddRange(cycle);
                if (pass + 1 < pattern.Count) continue;

                var lines = program.Select(FormatLine).ToList();
                int submitted = Simulate(pattern, lines);
                if (submitted >= pattern.Count)
                {
                    while (program.Count > 0 && Simulate(pattern, lines.Take(program.Count - 1).ToList()) >= pattern.Count)
                        program.RemoveAt(program.Count - 1);
                    return program;
                }
            }
            return null;
        }
    }
}
