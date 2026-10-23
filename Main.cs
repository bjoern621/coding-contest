using CodingContest;

var inputPath = "/home/bjoern/git/coding-contest/level-4/in_level-4_1-small.txt";
var outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, Path.GetFileName(inputPath).Replace("in_", "out_"));

var lines = File.ReadAllLines(inputPath);

// var result = ChallengeOne.Solve(lines);
var result = ChallengeVier.Solve(lines);

File.WriteAllText(outputPath, result + "\n");
Console.WriteLine(result);
