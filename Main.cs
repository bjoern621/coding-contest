using CodingContest;

var inputPath = "/home/bjoern/git/coding-contest/level-2/in_level-2_0-example.txt";
var outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, Path.GetFileName(inputPath).Replace("in_", "out_"));

var lines = File.ReadAllLines(inputPath);

using (var output = new StreamWriter(outputPath))
{
    Console.SetOut(output);
    // ChallengeOne.Solve(lines);
    ChallengeTwo.Solve(lines);
}
