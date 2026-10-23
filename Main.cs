using CodingContest;

if (args.Length == 3 && args[0] == "--check")
{
    Console.WriteLine($"{Path.GetFileName(args[2])}: score {ChallengeSechs.Check(File.ReadAllLines(args[1]), File.ReadAllText(args[2]))}");
    return;
}

string[] inputPaths = args.Length > 0
    ? args
    : ["/home/bjoern/git/coding-contest/level-6/in_level-6_0-example.txt", "/home/bjoern/git/coding-contest/level-6/in_level-6_1-small.txt"];

foreach (var inputPath in inputPaths)
{
    var outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, Path.GetFileName(inputPath).Replace("in_", "out_"));
    var lines = File.ReadAllLines(inputPath);

    var started = DateTime.Now;
    var result = ChallengeSechs.Solve(lines);
    int score = ChallengeSechs.Check(lines, result);
    Console.WriteLine($"{Path.GetFileName(inputPath)}: score {score} in {(DateTime.Now - started).TotalSeconds:F1} s");

    bool isExample = Path.GetFileName(inputPath).Contains("example");
    if (isExample && File.Exists(outputPath))
    {
        // The example's output file holds the contest's reference program.
        Console.WriteLine($"  reference program scores {ChallengeSechs.Check(lines, File.ReadAllText(outputPath))}");
        outputPath = Path.ChangeExtension(outputPath, ".solution.txt");
    }

    File.WriteAllText(outputPath, result);
}
