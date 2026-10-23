namespace CodingContest;

public static class ChallengeVier
{
    public static string Solve(string[] lines)
    {
        var boards = ReadPrograms(lines).Select(RunProgram);

        return string.Join("\n\n", boards.Select(Format));
    }

    record FabProgram(int Radius, char[][] Instructions);

    static int countStarInMap(char[][] map)
    {
        int count = 0;
        for (int row = 0; row < map.Length; row++)
        {
            for (int col = 0; col < map[row].Length; col++)
            {
                if (map[row][col] == '*')
                {
                    count++;
                }
            }
        }
        return count;
    }

    static char[] emitterStates = new char[6]; // 0 is off; default, 1 is on

    static char[][] RunProgram(FabProgram program, int index)
    {
        var map = ChallengeOne.Board(program.Radius).Split('\n').Select(row => row.ToCharArray()).ToArray();

        List<(int x, int y, int e)> stars = GetStarsPositions(map);
        // Console.WriteLine($"Stars found at: {string.Join(", ", stars.Select(s => $"({s.x}, {s.y})"))}");

        // liste pro * position welche emitter dort sind
        var emitterPositionsList = new List<List<char>>();
        for (int i = 0; i < countStarInMap(map); i++)
        {
            emitterPositionsList.Add(new List<char>());
        }

        for (int emitter = 0; emitter < 6; emitter++)
        {
            emitterPositionsList[emitter * program.Radius].Add((char)('1' +((   emitter + 1 + 6) % 6))); 
        }

        // Console.WriteLine($"Initial emitter positions: {string.Join(", ", emitterPositionsList.Select(list => string.Join("", list)))}");

        Console.WriteLine(stars.Count == emitterPositionsList.Count);

        // 0 ist off; default
        // 1 is on
        for (int i = 0; i < emitterStates.Length; i++)
        {
            emitterStates[i] = '0';
        }


        // Console.WriteLine($"Program {index + 1} (radius {program.Radius}, {program.Instructions.Length} ticks)");
        // Console.WriteLine("  Emitter  1 2 3 4 5 6");

        for (int tick = 0; tick < program.Instructions.Length; tick++)
        {
            var line = program.Instructions[tick];
            Console.WriteLine($"  Tick {tick + 1,-3} {string.Join(' ', line)}");

            // change emitter states and positions based on instructions
            for (int emitter = 0; emitter < 6; emitter++)
            {
                char instruction = line[emitter];

                switch (instruction)
                {
                    case 'F':
                        int emitterListIndexF = findListIndexOfEmitter(emitterPositionsList, (char)('1' + emitter));
                        emitterPositionsList[emitterListIndexF].Remove((char)('1' + emitter));
                        emitterPositionsList[(emitterListIndexF + 1) % emitterPositionsList.Count].Add((char)('1' + emitter));
                        break;
                    case 'B':
                        int emitterListIndexB = findListIndexOfEmitter(emitterPositionsList, (char)('1' + emitter));
                        emitterPositionsList[emitterListIndexB].Remove((char)('1' + emitter));
                        emitterPositionsList[(emitterListIndexB - 1 + emitterPositionsList.Count) % emitterPositionsList.Count].Add((char)('1' + emitter));
                        break;
                    case 'T':
                        emitterStates[emitter] = emitterStates[emitter] == '1' ? '0' : '1';
                        break;
                    case '-':
                        break;
                }
            }

             // empty map zero
            hitCount = new int[map.Length][];
            for (int i = 0; i < map.Length; i++)
            {
                hitCount[i] = new int[map[i].Length];
            }

            // for each emitter make the calculation
            for (int emitter = 0; emitter < 6; emitter++)
            {
                // set one emitter at correct position:
                int emitterListIndex = findListIndexOfEmitter(emitterPositionsList, (char)('1' + emitter));

                // Console.WriteLine($"Emitter {emitter + 1} is at position {emitterListIndex} in the list of emitter positions.");

                map[stars[emitterListIndex].x][stars[emitterListIndex].y] = (char)('1' + emitter);

                // Console.WriteLine($"Emitter {emitter + 1} at position ({stars[emitterListIndex].x}, {stars[emitterListIndex].y}) with state {emitterStates[emitter]}");

                // print map
                // Console.WriteLine("Current map:");
                // Console.WriteLine(Format(map));

                map = SolveMap(map, hitCount);

                map[stars[emitterListIndex].x][stars[emitterListIndex].y] = '*';
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

        }

        Console.WriteLine();

        return map;
    }

private static List<(int row, int col)> directions = [(0,2), (1, 1), (1, -1), (0, -2), (-1, -1), (-1, 1)];


    static List<(int row, int col, int e)> GetStarsPositions(char[][] map)
    {

        List<(int row, int col, int e)> result = [];

        int curRow = 0;
        int curCol = -1;
        for (int col = 0; col < map[0].Length; col++)
        {
            if (map[0][col] == '*')
            {
                curCol = col;
                // result.Add((curRow, curCol, 0));
                break;
            }
        }

        Console.WriteLine($"Starting position of first star: ({curRow}, {curCol})");

        for (int i = 0; i < directions.Count; i++)
        {
            (int row, int col) curDirection = directions[i];
          
            try
            {
                 while(map[curRow][curCol] == '*')
                {
                    result.Add((curRow, curCol, 0));

                    curRow = curRow += curDirection.row;
                    curCol = curCol += curDirection.col;
                }

                curRow = curRow -= curDirection.row;
                curCol = curCol -= curDirection.col;

               curRow += directions[(i + 1) % directions.Count].row;
               curCol += directions[(i + 1) % directions.Count].col;
            }
            catch (IndexOutOfRangeException)
            {
                // Console.WriteLine($"Reached the edge of the map at ({curRow}, {curCol}) while searching for stars.");
                curRow = curRow -= curDirection.row;
                curCol = curCol -= curDirection.col;

               curRow += directions[(i + 1) % directions.Count].row;
               curCol += directions[(i + 1) % directions.Count].col;
                continue;
            }

                
           
        }

        // remove last element
        result.RemoveAt(result.Count - 1);

        return result;
    }


    static int findListIndexOfEmitter(List<List<char>> emitterPositionsList, char emitter)
    {
        for (int i = 0; i < emitterPositionsList.Count; i++)
        {
            if (emitterPositionsList[i].Contains(emitter))
            {
                return i;
            }
        }
        return -1;
    }

    static int[][]? hitCount;

    static char[][] SolveMap(char[][] map, int[][] hitCount)
    {
        // ein board mit genau einer zahl

        // Find number
        for (int row = 0; row < map.Length; row++)
        {
            for (int col = 0; col < map[row].Length; col++)
            {
                if (map[row][col] == '1' && emitterStates[0] == '1'){
                    Console.WriteLine($"Found 1 at {row}, {col}");
                    make1(map, row, col);
                }else if (map[row][col] == '2' && emitterStates[1] == '1'){
                    Console.WriteLine($"Found 2 at {row}, {col}");
                    make2(map, row, col);
                }else if (map[row][col] == '3' && emitterStates[2] == '1'){
                    Console.WriteLine($"Found 3 at {row}, {col}");
                    make3(map, row, col);
                }else if (map[row][col] == '4' && emitterStates[3] == '1'){
                    Console.WriteLine($"Found 4 at {row}, {col}");
                    make4(map, row, col);
                }else if (map[row][col] == '5' && emitterStates[4] == '1'){
                    Console.WriteLine($"Found 5 at {row}, {col}");
                    make5(map, row, col);
                }else if (map[row][col] == '6' && emitterStates[5] == '1'){
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

        try{

        while (map[row][col] is not ('*' or (>= '1' and <= '6')))
        {
            // map[row][col] = 'M';
            hitCount[row][col]++;
            col+=2;
        }}
        catch (IndexOutOfRangeException)
        {
        }
    }

       static void make2(char[][] map, int row, int col)
    {
        row += 1;
        col += 1;

        try{

        while  (map[row][col] is not ('*' or (>= '1' and <= '6')))
        {
            // map[row][col] = 'M';
            hitCount[row][col]++;
            row++;
            col+=1;
        }}
        catch (IndexOutOfRangeException)
        {
        }
    }

    static void make3(char[][] map, int row, int col)
    {
        row += 1;
        col -= 1;

        try {

        while (map[row][col] is not ('*' or (>= '1' and <= '6')))
        {
            // map[row][col] = 'M';
            hitCount[row][col]++;
            row += 1;
            col -= 1;
        }}catch (IndexOutOfRangeException)
        {
        }
    }

    static void make4(char[][] map, int row, int col)
    {
        col -= 2;

        try
        {
             while  (map[row][col] is not ('*' or (>= '1' and <= '6')))
        {
            // map[row][col] = 'M';
            hitCount[row][col]++;
            col-=2;
        }
        }catch (IndexOutOfRangeException)
        {
        }

       
    }

    static void make5(char[][] map, int row, int col)
    {
        row -= 1;
        col -= 1;

        try {

        while  (map[row][col] is not ('*' or (>= '1' and <= '6')))
        {
            // map[row][col] = 'M';
            hitCount[row][col]++;
            row--;
            col-=1;
        }
        }
        catch (IndexOutOfRangeException)
        {
        }
    }

    static void make6(char[][] map, int row, int col)
    {
        row -= 1;
        col += 1;

        try {

        while  (map[row][col] is not ('*' or (>= '1' and <= '6')))
        {
            // map[row][col] = 'M';
            hitCount[row][col]++;
            row--;
            col+=1;
        }}

        catch (IndexOutOfRangeException)
        {
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
