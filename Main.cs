using CodingContest;

var inputPath = "/home/bjoern/Downloads/level-1/in_level-1_1-small.txt";
var outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, Path.GetFileName(inputPath).Replace("in_", "out_"));

var lines = File.ReadAllLines(inputPath);

using (var output = new StreamWriter(outputPath))
{
    Console.SetOut(output);
    ChallengeOne.Solve(lines);
    // ChallengeTwo.Solve(lines);
}
