namespace CodingContest;

public static class ChallengeTwo
{
    public static string Solve(string[] lines)
    {
        var maps = ReadMaps(lines).Select(SolveMap);

        return string.Join("\n\n", maps.Select(Format));
    }

    static char[][] SolveMap(char[][] map)
    {
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

        return map;
    }

    static void make1(char[][] map, int row, int col)
    {
        col += 2;

        while (map[row][col] != '*')
        {
            map[row][col] = 'M';
            col+=2;
        }
    }

       static void make2(char[][] map, int row, int col)
    {
        row += 1;
        col += 1;

        while (map[row][col] != '*')
        {
            map[row][col] = 'M';
            row++;
            col+=1;
        }
    }

    static void make3(char[][] map, int row, int col)
    {
        row += 1;
        col -= 1;

        while (map[row][col] != '*')
        {
            map[row][col] = 'M';
            
        row += 1;
        col -= 1;
        }
    }

    static void make4(char[][] map, int row, int col)
    {
        col -= 2;

        while (map[row][col] != '*')
        {
            map[row][col] = 'M';
            col-=2;
        }
    }

    static void make5(char[][] map, int row, int col)
    {
        row -= 1;
        col -= 1;

        while (map[row][col] != '*')
        {
            map[row][col] = 'M';
            row--;
            col-=1;
        }
    }

    static void make6(char[][] map, int row, int col)
    {
        row -= 1;
        col += 1;

        while (map[row][col] != '*')
        {
            map[row][col] = 'M';
            row--;
            col+=1;
        }
    }

    static List<char[][]> ReadMaps(string[] lines)
    {
        int count = int.Parse(lines[0]);
        var maps = new List<char[][]>();
        int line = 1;

        for (int i = 0; i < count; i++)
        {
            while (string.IsNullOrWhiteSpace(lines[line]))
            {
                line++;
            }

            int rows = int.Parse(lines[line].Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]);

            maps.Add(lines.Skip(line + 1).Take(rows).Select(row => row.ToCharArray()).ToArray());
            line += rows + 1;
        }

        return maps;
    }

    static string Format(char[][] map)
    {
        return string.Join('\n', map.Select(row => new string(row)));
    }
}
