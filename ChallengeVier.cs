namespace CodingContest;

public static class ChallengeVier
{
    public static string Solve(string[] lines)
    {
        var boards = ReadPrograms(lines).Select(RunProgram);

        return string.Join("\n\n", boards.Select(Format));
    }

    record FabProgram(int Radius, char[][] Instructions);

    static char[][] RunProgram(FabProgram program, int index)
    {
        var map = ChallengeOne.Board(program.Radius).Split('\n').Select(row => row.ToCharArray()).ToArray();
        var active = new bool[6];
        // Steps along the rail from the emitter's corner, clockwise positive.
        var position = new int[6];

        Console.WriteLine($"Program {index + 1} (radius {program.Radius}, {program.Instructions.Length} ticks)");
        Console.WriteLine("  Emitter  1 2 3 4 5 6");

        for (int tick = 0; tick < program.Instructions.Length; tick++)
        {
            var line = program.Instructions[tick];
            Console.WriteLine($"  Tick {tick + 1,-3} {string.Join(' ', line)}");

            for (int emitter = 0; emitter < 6; emitter++)
            {
                switch (line[emitter])
                {
                    case 'F':
                        position[emitter]++;
                        break;
                    case 'B':
                        position[emitter]--;
                        break;
                    case 'T':
                        active[emitter] = !active[emitter];
                        break;
                }
            }
        }

        Console.WriteLine();

        return map;
    }

    static int[][]? hitCount;

    static char[][] SolveMap(char[][] map)
    {
        // empty map zero
        hitCount = new int[map.Length][];
        for (int i = 0; i < map.Length; i++)
        {
            hitCount[i] = new int[map[i].Length];
        }

        // Find number
        for (int row = 0; row < map.Length; row++)
        {
            for (int col = 0; col < map[row].Length; col++)
            {
                if (map[row][col] == '1'){
                    Console.WriteLine($"Found 1 at {row}, {col}");
                    make1(map, row, col);
                }else if (map[row][col] == '2'){
                    Console.WriteLine($"Found 2 at {row}, {col}");
                    make2(map, row, col);
                }else if (map[row][col] == '3'){
                    Console.WriteLine($"Found 3 at {row}, {col}");
                    make3(map, row, col);
                }else if (map[row][col] == '4'){
                    Console.WriteLine($"Found 4 at {row}, {col}");
                    make4(map, row, col);
                }else if (map[row][col] == '5'){
                    Console.WriteLine($"Found 5 at {row}, {col}");
                    make5(map, row, col);
                }else if (map[row][col] == '6'){
                    Console.WriteLine($"Found 6 at {row}, {col}");
                    make6(map, row, col);
                }
            }
        }

        // output hit count map
        for (int i = 0; i < hitCount.Length; i++)
        {
            Console.WriteLine(string.Join(" ", hitCount[i]));
        }

        // make transformed map
        for (int row = 0; row < map.Length; row++)
        {
            for (int col = 0; col < map[row].Length; col++)
            {
                if (hitCount[row][col] ==2 && map[row][col] == 'O')
                {
                    map[row][col] = 'M';
                }else if (hitCount[row][col] >= 3)
                {
                    map[row][col] = 'O';
                }
            }
        }

        return map;
    }

    static void make1(char[][] map, int row, int col)
    {
        col += 2;

        while (map[row][col] is not ('*' or (>= '1' and <= '6')))
        {
            // map[row][col] = 'M';
            hitCount[row][col]++;
            col+=2;
        }
    }

       static void make2(char[][] map, int row, int col)
    {
        row += 1;
        col += 1;

        while  (map[row][col] is not ('*' or (>= '1' and <= '6')))
        {
            // map[row][col] = 'M';
            hitCount[row][col]++;
            row++;
            col+=1;
        }
    }

    static void make3(char[][] map, int row, int col)
    {
        row += 1;
        col -= 1;

        while (map[row][col] is not ('*' or (>= '1' and <= '6')))
        {
            // map[row][col] = 'M';
            hitCount[row][col]++;
            row += 1;
            col -= 1;
        }
    }

    static void make4(char[][] map, int row, int col)
    {
        col -= 2;

        while  (map[row][col] is not ('*' or (>= '1' and <= '6')))
        {
            // map[row][col] = 'M';
            hitCount[row][col]++;
            col-=2;
        }
    }

    static void make5(char[][] map, int row, int col)
    {
        row -= 1;
        col -= 1;

        while  (map[row][col] is not ('*' or (>= '1' and <= '6')))
        {
            // map[row][col] = 'M';
            hitCount[row][col]++;
            row--;
            col-=1;
        }
    }

    static void make6(char[][] map, int row, int col)
    {
        row -= 1;
        col += 1;

        while  (map[row][col] is not ('*' or (>= '1' and <= '6')))
        {
            // map[row][col] = 'M';
            hitCount[row][col]++;
            row--;
            col+=1;
        }
    }

    static List<FabProgram> ReadPrograms(string[] lines)
    {
        var rows = lines.Where(row => !string.IsNullOrWhiteSpace(row)).ToArray();
        int count = int.Parse(rows[0]);
        var programs = new List<FabProgram>();
        int line = 1;

        for (int i = 0; i < count; i++)
        {
            int radius = int.Parse(rows[line]);
            int lineCount = int.Parse(rows[line + 1]);

            var instructions = rows.Skip(line + 2).Take(lineCount)
                .Select(row => row.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(cell => cell[0]).ToArray())
                .ToArray();

            programs.Add(new FabProgram(radius, instructions));
            line += lineCount + 2;
        }

        return programs;
    }

    static string Format(char[][] map)
    {
        return string.Join('\n', map.Select(row => new string(row)));
    }
}
