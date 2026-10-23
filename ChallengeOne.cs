namespace CodingContest;

public static class ChallengeOne
{
    public static string Solve(string[] lines)
    {
        var numbers = string.Join(' ', lines)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToArray();

        var boards = numbers.Skip(1).Take(numbers[0]).Select(Board);

        return string.Join("\n\n", boards);
    }

    internal static string Board(int radius)
    {
        var rows = new List<string>();

        for (int row = -radius; row <= radius; row++)
        {
            rows.Add(Row(radius, Math.Abs(row)));
        }

        return string.Join('\n', rows);
    }

    static string Row(int radius, int distance)
    {
        int cellCount = 2 * radius + 1 - distance;
        bool isBorderRow = distance == radius;

        var cells = Enumerable.Range(0, cellCount)
            .Select(i => isBorderRow || i == 0 || i == cellCount - 1 ? '*' : 'O');

        string indent = new('-', distance);

        return indent + string.Join('-', cells) + indent;
    }
}
