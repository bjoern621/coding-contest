using CodingContest;

var inputPath = "/home/bjoern/git/coding-contest/level-2/in_level-2_1-small.txt";
var outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, Path.GetFileName(inputPath).Replace("in_", "out_"));

var lines = File.ReadAllLines(inputPath);

// var result = ChallengeOne.Solve(lines);
var result = ChallengeTwo.Solve(lines);

File.WriteAllText(outputPath, result + "\n");
Console.WriteLine(result);
