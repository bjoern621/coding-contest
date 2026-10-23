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
        return map;
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
