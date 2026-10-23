using System.Numerics;

namespace CodingContest;

public static class ChallengeSechs
{
    public record Pattern(int Radius, char[][] Cells, int Count);

    static readonly (int dr, int dc)[] Fire = [(0, 2), (1, 1), (1, -1), (0, -2), (-1, -1), (-1, 1)];
    static readonly (int dr, int dc)[] RailWalk = [(-1, 1), (0, 2), (1, 1), (1, -1), (0, -2), (-1, -1)];
    const string Instructions = "-TFB";

    // Tuning knobs, overridable through environment variables of the same name.
    static readonly int BeamWidth = (int)Setting("BEAM", 64);
    static readonly double Decay = Setting("DECAY", 0.6);
    static readonly double PotentialWeight = Setting("WEIGHT", 0.8);

    static double Setting(string name, double fallback) =>
        double.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.CultureInfo.InvariantCulture, out double value)
            ? value
            : fallback;

    public static string Solve(string[] lines)
    {
        var patterns = ReadPatterns(lines);
        var programs = new string[patterns.Count];

        Parallel.For(0, patterns.Count, i =>
        {
            programs[i] = string.Join('\n', new Solver(patterns[i]).Run().Select(FormatLine));
        });

        return string.Join("\n\n", programs) + "\n";
    }

    static string FormatLine(int action) =>
        string.Join(' ', Enumerable.Range(0, 6).Select(e => Instructions[(action >> (2 * e)) & 3]));

    public static List<Pattern> ReadPatterns(string[] lines)
    {
        var rows = lines.Select(row => row.TrimEnd()).Where(row => row.Length > 0).ToArray();
        int count = int.Parse(rows[0]);
        var patterns = new List<Pattern>();
        int line = 1;

        for (int i = 0; i < count; i++)
        {
            var header = rows[line].Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
            if (header.Length != 4)
                throw new FormatException($"Pattern {i + 1}: header \"{rows[line]}\" needs radius, rows, columns and count");

            var (radius, patternRows, patternCols, required) = (header[0], header[1], header[2], header[3]);
            var cells = rows.Skip(line + 1).Take(patternRows).Select(row => row.ToCharArray()).ToArray();
            line += patternRows + 1;

            var pattern = new Pattern(radius, cells, required);
            var problem = Describe(pattern, patternRows, patternCols);
            if (problem != null)
                throw new FormatException($"Pattern {i + 1}: {problem}");

            patterns.Add(pattern);
        }

        if (line != rows.Length)
            throw new FormatException($"Input holds {rows.Length - line} lines after the last pattern");

        return patterns;
    }

    static string? Describe(Pattern pattern, int patternRows, int patternCols)
    {
        if (pattern.Count < 1)
            return $"count {pattern.Count} below 1";
        if (patternRows != 2 * pattern.Radius + 1 || patternCols != 4 * pattern.Radius + 1)
            return $"{patternRows}x{patternCols} does not fit radius {pattern.Radius}";
        if (pattern.Cells.Length != patternRows || pattern.Cells.Any(row => row.Length != patternCols))
            return "grid size differs from its header";

        var board = ChallengeOne.Board(pattern.Radius).Split('\n');
        for (int r = 0; r < patternRows; r++)
            for (int c = 0; c < patternCols; c++)
            {
                char cell = pattern.Cells[r][c];
                char expected = board[r][c];
                if (cell != expected && !(cell == 'M' && expected == 'O'))
                    return $"'{cell}' at row {r + 1}, column {c + 1} where the board holds '{expected}'";
            }

        var matter = Cells(pattern, 'M').ToList();
        if (matter.Count == 0)
            return "no matter cell";

        var reached = new HashSet<(int r, int c)> { matter[0] };
        var queue = new Queue<(int r, int c)>([matter[0]]);
        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            foreach (var (dr, dc) in Fire)
                if (IsCell(pattern, r + dr, c + dc, 'M') && reached.Add((r + dr, c + dc)))
                    queue.Enqueue((r + dr, c + dc));
        }

        return reached.Count == matter.Count ? null : "matter cells not connected";
    }

    static IEnumerable<(int r, int c)> Cells(Pattern pattern, char kind)
    {
        for (int r = 0; r < pattern.Cells.Length; r++)
            for (int c = 0; c < pattern.Cells[r].Length; c++)
                if (pattern.Cells[r][c] == kind)
                    yield return (r, c);
    }

    static bool IsCell(Pattern pattern, int r, int c, char kind) =>
        r >= 0 && r < pattern.Cells.Length && c >= 0 && c < pattern.Cells[r].Length && pattern.Cells[r][c] == kind;

    static bool InGrid(Pattern pattern, int r, int c) =>
        r >= 0 && r < pattern.Cells.Length && c >= 0 && c < pattern.Cells[r].Length && pattern.Cells[r][c] != '-';

    static (int r, int c)[] Rail(Pattern pattern)
    {
        var rail = new List<(int r, int c)>();
        var (r, c) = (pattern.Radius, 0);
        foreach (var (dr, dc) in RailWalk)
            for (int i = 0; i < pattern.Radius; i++)
            {
                rail.Add((r, c));
                (r, c) = (r + dr, c + dc);
            }
        return rail.ToArray();
    }

    /// Interior cells that a beam of emitter <paramref name="emitter"/> crosses from that rail cell.
    static List<(int r, int c)> Ray(Pattern pattern, (int r, int c) start, int emitter)
    {
        var cells = new List<(int r, int c)>();
        var (dr, dc) = Fire[emitter];
        var (r, c) = (start.r + dr, start.c + dc);
        while (InGrid(pattern, r, c))
        {
            if (pattern.Cells[r][c] != '*')
                cells.Add((r, c));
            (r, c) = (r + dr, c + dc);
        }
        return cells;
    }

    /// Runs a program the way the contest sandbox does and returns the number of submitted patterns.
    public static int Simulate(Pattern pattern, IReadOnlyList<string> program)
    {
        var rail = Rail(pattern);
        var grid = pattern.Cells.Select(row => row.Select(cell => cell == 'M' ? 'O' : cell).ToArray()).ToArray();
        var target = Cells(pattern, 'M').ToHashSet();
        var skirt = Cells(pattern, 'O')
            .Where(cell => Fire.Any(d => target.Contains((cell.r + d.dr, cell.c + d.dc))))
            .ToList();
        var position = Enumerable.Range(0, 6).Select(e => e * pattern.Radius).ToArray();
        var active = new bool[6];
        int submitted = 0;

        foreach (var line in program)
        {
            var tokens = line.Split(' ');
            if (tokens.Length != 6 || tokens.Any(token => token.Length != 1 || !Instructions.Contains(token[0])))
                throw new FormatException($"Instruction line \"{line}\" needs six of '-', 'T', 'F' and 'B'");

            for (int e = 0; e < 6; e++)
                switch (tokens[e][0])
                {
                    case 'F': position[e] = (position[e] + 1) % rail.Length; break;
                    case 'B': position[e] = (position[e] + rail.Length - 1) % rail.Length; break;
                    case 'T': active[e] = !active[e]; break;
                }

            var hits = new Dictionary<(int r, int c), int>();
            for (int e = 0; e < 6; e++)
                if (active[e])
                    foreach (var cell in Ray(pattern, rail[position[e]], e))
                        hits[cell] = hits.GetValueOrDefault(cell) + 1;

            foreach (var ((r, c), n) in hits)
                if (n == 2) grid[r][c] = 'M';
                else if (n >= 3) grid[r][c] = 'O';

            if (target.All(cell => grid[cell.r][cell.c] == 'M') && skirt.All(cell => grid[cell.r][cell.c] != 'M'))
            {
                foreach (var (r, c) in target)
                    grid[r][c] = 'O';
                submitted++;
            }
        }

        return submitted;
    }

    /// Checks every program of an output against its pattern and returns the total number of instruction lines.
    public static int Check(string[] inputLines, string output)
    {
        var patterns = ReadPatterns(inputLines);
        var programs = output.Replace("\r", "").TrimEnd('\n').Split("\n\n");
        if (programs.Length != patterns.Count)
            throw new FormatException($"Output holds {programs.Length} programs for {patterns.Count} patterns");

        int score = 0;
        for (int i = 0; i < patterns.Count; i++)
        {
            var program = programs[i].Split('\n');
            int submitted = Simulate(patterns[i], program);
            if (submitted < patterns[i].Count)
                throw new InvalidOperationException($"Program {i + 1} submits {submitted} of {patterns[i].Count} patterns");
            score += program.Length;
        }
        return score;
    }

    /// Beam search over ticks.
    /// A state keeps emitter rail offsets, their on/off bits and which target cells hold matter;
    /// matter outside the target and its skirt never decides a submission and stays untracked.
    sealed class Solver
    {
        readonly Pattern pattern;
        readonly int radius, span, targetCount, words;
        // Beam mask per emitter and rail offset, over target cells first, skirt cells after.
        readonly ulong[][][] beam;
        readonly ulong[] targetFull, skirt;
        // Rail offset at which an emitter's beam crosses a target cell, -1 where none does.
        readonly int[][] reach;

        public Solver(Pattern pattern)
        {
            this.pattern = pattern;
            radius = pattern.Radius;
            span = 2 * radius + 1;

            var target = Cells(pattern, 'M').ToList();
            var targetSet = target.ToHashSet();
            var skirtCells = Cells(pattern, 'O')
                .Where(cell => Fire.Any(d => targetSet.Contains((cell.r + d.dr, cell.c + d.dc))))
                .ToList();
            targetCount = target.Count;

            var index = new Dictionary<(int r, int c), int>();
            foreach (var cell in target.Concat(skirtCells))
                index[cell] = index.Count;
            words = (index.Count + 63) / 64;

            targetFull = new ulong[words];
            skirt = new ulong[words];
            for (int i = 0; i < index.Count; i++)
                (i < targetCount ? targetFull : skirt)[i / 64] |= 1UL << (i % 64);

            var rail = Rail(pattern);
            beam = new ulong[6][][];
            reach = new int[6][];
            for (int e = 0; e < 6; e++)
            {
                beam[e] = new ulong[span][];
                reach[e] = Enumerable.Repeat(-1, targetCount).ToArray();
                for (int o = 0; o < span; o++)
                {
                    beam[e][o] = new ulong[words];
                    foreach (var cell in Ray(pattern, rail[RailIndex(e, o)], e))
                        if (index.TryGetValue(cell, out int i))
                        {
                            beam[e][o][i / 64] |= 1UL << (i % 64);
                            if (i < targetCount)
                                reach[e][i] = o;
                        }
                }
            }
        }

        int RailIndex(int emitter, int offset) =>
            (emitter * radius + offset - radius + 6 * radius) % (6 * radius);

        record Node(int Parent, int Action, ulong Config, ulong[] Matter, int Submitted);

        static int Offset(ulong config, int e) => (int)((config >> (5 * e)) & 31);
        static bool Active(ulong config, int e) => ((config >> (30 + e)) & 1) != 0;

        public List<int> Run()
        {
            ulong start = 0;
            for (int e = 0; e < 6; e++)
                start |= (ulong)radius << (5 * e);

            var layers = new List<Node[]> { new[] { new Node(-1, 0, start, new ulong[words], 0) } };
            double bestProgress = 0;
            int bestLayer = 0;

            while (true)
            {
                double progress = layers[^1].Max(Progress);
                if (progress > bestProgress)
                    (bestProgress, bestLayer) = (progress, layers.Count);
                else if (layers.Count - bestLayer > 2 * radius + 4)
                {
                    BuildMissingCell(layers);
                    if (layers[^1][0].Submitted == pattern.Count)
                        return Trace(layers);
                    (bestProgress, bestLayer) = (Progress(layers[^1][0]), layers.Count);
                }

                var beamNodes = layers[^1];
                var candidates = new List<(double score, int parent, int action)>();

                for (int p = 0; p < beamNodes.Length; p++)
                    Expand(beamNodes[p], p, candidates);

                if (candidates.Count == 0)
                    throw new InvalidOperationException("Beam search ran out of states");

                candidates.Sort((a, b) => b.score.CompareTo(a.score));

                var next = new List<(double score, Node node)>();
                var seen = new HashSet<ulong>();
                int shortlist = BeamWidth * 4;
                foreach (var (_, parent, action) in candidates)
                {
                    if (next.Count >= shortlist) break;
                    var child = Apply(beamNodes[parent], parent, action);
                    if (child == null || !seen.Add(Hash(child))) continue;

                    if (child.Submitted == pattern.Count)
                    {
                        layers.Add([child]);
                        return Trace(layers);
                    }
                    next.Add((Progress(child) + PotentialWeight * PairPotential(child), child));
                }

                layers.Add(next.OrderByDescending(entry => entry.score).Take(BeamWidth).Select(entry => entry.node).ToArray());

                if (layers.Count > 20000)
                    throw new InvalidOperationException("Beam search found no program within 20000 ticks");
            }
        }

        /// Leaves a stalled beam by building the closest missing target cell from the best state:
        /// every emitter switches off, two emitters on different axes line up on the cell and switch on together.
        /// Two crossing beams alone put matter on their crossing cell and nowhere else.
        void BuildMissingCell(List<Node[]> layers)
        {
            int index = Enumerable.Range(0, layers[^1].Length).MaxBy(i => Progress(layers[^1][i]));
            var node = layers[^1][index];

            var (first, second, cellOf, cost) = (0, 0, 0, int.MaxValue);
            for (int cell = 0; cell < targetCount; cell++)
            {
                if (Holds(node.Matter, cell)) continue;
                for (int a = 0; a < 6; a++)
                    for (int b = a + 1; b < 6; b++)
                    {
                        if (a % 3 == b % 3) continue;
                        int c = Math.Max(Math.Abs(Offset(node.Config, a) - reach[a][cell]), Math.Abs(Offset(node.Config, b) - reach[b][cell]));
                        if (c < cost)
                            (first, second, cellOf, cost) = (a, b, cell, c);
                    }
            }

            void Step(int action)
            {
                node = Apply(node, index, action) ?? throw new InvalidOperationException("Fallback step hit the skirt");
                index = 0;
                layers.Add([node]);
            }

            while (true)
            {
                int action = 0;
                for (int e = 0; e < 6; e++)
                {
                    int o = Offset(node.Config, e);
                    if (Active(node.Config, e))
                        action |= 1 << (2 * e);
                    else if ((e == first || e == second) && o != reach[e][cellOf])
                        action |= (o < reach[e][cellOf] ? 2 : 3) << (2 * e);
                }
                if (action == 0) break;
                Step(action);
            }
            Step((1 << (2 * first)) | (1 << (2 * second)));
        }

        static List<int> Trace(List<Node[]> layers)
        {
            var actions = new List<int>();
            int index = 0;
            for (int layer = layers.Count - 1; layer > 0; layer--)
            {
                var node = layers[layer][index];
                actions.Add(node.Action);
                index = node.Parent;
            }
            actions.Reverse();
            return actions;
        }

        double Progress(Node node)
        {
            int count = 0;
            foreach (var word in node.Matter)
                count += BitOperations.PopCount(word);
            return node.Submitted * targetCount + count;
        }

        bool Holds(ulong[] matter, int cell) => ((matter[cell / 64] >> (cell % 64)) & 1) != 0;

        int Distance(ulong config, int e, int cell)
        {
            int need = reach[e][cell];
            if (need < 0) return 1000;
            return Math.Abs(Offset(config, e) - need) + (Active(config, e) ? 0 : 1);
        }

        /// Sum over missing target cells of Decay^t, where t counts the ticks until two emitters cross there.
        double PairPotential(Node node)
        {
            double sum = 0;
            for (int cell = 0; cell < targetCount; cell++)
            {
                if (Holds(node.Matter, cell)) continue;
                int best = 1000, second = 1000;
                for (int e = 0; e < 6; e++)
                {
                    int d = Distance(node.Config, e, cell);
                    if (d < best) (best, second) = (d, best);
                    else if (d < second) second = d;
                }
                if (second < 1000)
                    sum += Math.Pow(Decay, second);
            }
            return sum;
        }

        /// Per emitter and rail state, the summed closeness to the parent's missing target cells.
        double[][] EmitterPotential(Node node)
        {
            var table = new double[6][];
            for (int e = 0; e < 6; e++)
            {
                table[e] = new double[2 * span];
                for (int cell = 0; cell < targetCount; cell++)
                {
                    int need = reach[e][cell];
                    if (need < 0 || Holds(node.Matter, cell)) continue;
                    for (int o = 0; o < span; o++)
                    {
                        int d = Math.Abs(o - need);
                        table[e][2 * o] += Math.Pow(Decay, d + 1);
                        table[e][2 * o + 1] += Math.Pow(Decay, d);
                    }
                }
            }
            return table;
        }

        void Expand(Node node, int parent, List<(double score, int parent, int action)> candidates)
        {
            var potential = EmitterPotential(node);
            double scale = 0.05 / Math.Max(1, targetCount);
            double baseProgress = node.Submitted * targetCount;

            var options = new (int action, int offset, bool active)[6][];
            for (int e = 0; e < 6; e++)
            {
                int o = Offset(node.Config, e);
                bool on = Active(node.Config, e);
                var list = new List<(int, int, bool)> { (0, o, on), (1, o, !on) };
                if (o + 1 < span) list.Add((2, o + 1, on));
                if (o > 0) list.Add((3, o - 1, on));
                options[e] = list.ToArray();
            }

            var one = new ulong[7][];
            var two = new ulong[7][];
            var three = new ulong[7][];
            for (int i = 0; i < 7; i++)
            {
                one[i] = new ulong[words];
                two[i] = new ulong[words];
                three[i] = new ulong[words];
            }

            void Visit(int e, int action, double hint)
            {
                if (e == 6)
                {
                    int count = 0;
                    bool complete = true;
                    for (int w = 0; w < words; w++)
                    {
                        ulong exactlyTwo = two[6][w] & ~three[6][w];
                        if ((exactlyTwo & skirt[w]) != 0) return;
                        ulong matter = (node.Matter[w] | (exactlyTwo & targetFull[w])) & ~three[6][w];
                        count += BitOperations.PopCount(matter);
                        if (matter != targetFull[w]) complete = false;
                    }
                    double progress = complete ? baseProgress + 2 * targetCount : baseProgress + count;
                    candidates.Add((progress + scale * hint, parent, action));
                    return;
                }

                foreach (var (code, o, on) in options[e])
                {
                    if (on)
                    {
                        var ray = beam[e][o];
                        for (int w = 0; w < words; w++)
                        {
                            three[e + 1][w] = three[e][w] | (two[e][w] & ray[w]);
                            two[e + 1][w] = two[e][w] | (one[e][w] & ray[w]);
                            one[e + 1][w] = one[e][w] | ray[w];
                        }
                    }
                    else
                    {
                        Array.Copy(one[e], one[e + 1], words);
                        Array.Copy(two[e], two[e + 1], words);
                        Array.Copy(three[e], three[e + 1], words);
                    }
                    Visit(e + 1, action | (code << (2 * e)), hint + potential[e][2 * o + (on ? 1 : 0)]);
                }
            }

            Visit(0, 0, 0);
        }

        Node? Apply(Node node, int parent, int action)
        {
            ulong config = node.Config;
            var one = new ulong[words];
            var two = new ulong[words];
            var three = new ulong[words];

            for (int e = 0; e < 6; e++)
            {
                int o = Offset(config, e);
                bool on = Active(config, e);
                switch ((action >> (2 * e)) & 3)
                {
                    case 1: on = !on; break;
                    case 2: o++; break;
                    case 3: o--; break;
                }
                config &= ~((31UL << (5 * e)) | (1UL << (30 + e)));
                config |= ((ulong)o << (5 * e)) | (on ? 1UL << (30 + e) : 0);

                if (!on) continue;
                var ray = beam[e][o];
                for (int w = 0; w < words; w++)
                {
                    three[w] |= two[w] & ray[w];
                    two[w] |= one[w] & ray[w];
                    one[w] |= ray[w];
                }
            }

            var matter = new ulong[words];
            bool complete = true;
            for (int w = 0; w < words; w++)
            {
                ulong exactlyTwo = two[w] & ~three[w];
                if ((exactlyTwo & skirt[w]) != 0) return null;
                matter[w] = (node.Matter[w] | (exactlyTwo & targetFull[w])) & ~three[w];
                if (matter[w] != targetFull[w]) complete = false;
            }

            if (complete)
                return new Node(parent, action, config, new ulong[words], node.Submitted + 1);
            return new Node(parent, action, config, matter, node.Submitted);
        }

        static ulong Hash(Node node)
        {
            ulong h = node.Config * 0x9E3779B97F4A7C15UL ^ (ulong)node.Submitted;
            foreach (var word in node.Matter)
                h = (h ^ word) * 0xBF58476D1CE4E5B9UL + 0x94D049BB133111EBUL;
            return h;
        }
    }
}
