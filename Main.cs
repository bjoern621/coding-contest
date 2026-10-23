using CodingContest;

var inputPath = "/home/bjoern/git/coding-contest/level-5/in_level-5_2-large.txt";
var outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, Path.GetFileName(inputPath).Replace("in_", "out_"));

var lines = File.ReadAllLines(inputPath);

// var result = ChallengeOne.Solve(lines);
var result = ChallengeFuenf.Solve(lines);

Console.WriteLine(result);

bool isExample = Path.GetFileName(inputPath).Contains("example");
if (isExample && File.Exists(outputPath))
{
    // The example's output file holds the expected answer from the contest.
    var expected = File.ReadAllText(outputPath).TrimEnd();
    Console.WriteLine(result == expected ? "Matches the expected output." : $"Differs from the expected output:\n{expected}");
}
else
{
    File.WriteAllText(outputPath, result + "\n");
}
