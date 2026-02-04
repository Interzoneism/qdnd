#nullable enable
using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Tests.Performance;

public class CIBenchmarkTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;
    private readonly CIBenchmarkRunner _runner;

    public CIBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"qdnd_bench_{Guid.NewGuid()}");
        _runner = new CIBenchmarkRunner(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void RunAllBenchmarks_Completes()
    {
        var results = _runner.RunAll();

        _output.WriteLine($"Suite: {results.SuiteName}");
        _output.WriteLine($"Duration: {results.Duration.TotalSeconds:F2}s");
        _output.WriteLine($"Benchmarks: {results.Results.Count}");

        results.PrintSummary(msg => _output.WriteLine(msg));

        Assert.Empty(results.Errors);
        Assert.True(results.Results.Count >= 7, "Expected at least 7 benchmarks");
    }

    [Fact]
    public void SaveResults_CreatesFile()
    {
        var results = _runner.RunAll();
        _runner.SaveResults(results);

        var files = Directory.GetFiles(_tempDir, "benchmark_*.json");
        Assert.Single(files);

        _output.WriteLine($"Saved to: {files[0]}");
    }

    [Fact]
    public void RegressionCheck_NoRegressionsOnFirstRun()
    {
        var (results, regressions) = _runner.RunWithRegressionCheck();

        // No baseline exists, so no regressions
        Assert.Empty(regressions);
        _output.WriteLine("No regressions detected (no baseline)");
    }

    [Fact]
    public void RegressionCheck_DetectsSignificantRegression()
    {
        // Create a baseline with fast times
        var baselinePath = Path.Combine(_tempDir, "baseline.json");
        var fakeBaseline = @"{
            ""SuiteName"": ""Test"",
            ""Benchmarks"": [
                { ""OperationName"": ""SlowOp"", ""P95Ms"": 0.1 }
            ]
        }";
        File.WriteAllText(baselinePath, fakeBaseline);

        // Create current results with slow times (simulated regression)
        var results = _runner.RunAll();

        // Add a fake slow result that would be a regression
        results.Results.Add(new QDND.Tools.Profiling.ProfilerMetrics
        {
            OperationName = "SlowOp",
            P95Ms = 1.0  // 10x slower than baseline
        });

        var regressions = BenchmarkReporter.CompareToBaseline(results, baselinePath);

        Assert.Single(regressions);
        Assert.Contains("SlowOp", regressions[0].OperationName);

        _output.WriteLine(regressions[0].ToString());
    }

    [Fact]
    public void UpdateBaseline_CreatesBaselineFile()
    {
        var results = _runner.RunAll();
        _runner.SaveResults(results, updateBaseline: true);

        var baselinePath = Path.Combine(_tempDir, "baseline.json");
        Assert.True(File.Exists(baselinePath));

        _output.WriteLine("Baseline created successfully");
    }

    [Fact]
    public void AllBenchmarks_MeetTargets()
    {
        var results = _runner.RunAll();

        foreach (var metric in results.Results)
        {
            _output.WriteLine($"{metric.OperationName}: P95={metric.P95Ms:F3}ms");

            // Define targets per operation
            var target = metric.OperationName switch
            {
                "DiceRoller.100xRollD20" => 1.0,
                "Snapshot.Serialize" => 5.0,
                "Snapshot.Deserialize" => 5.0,
                "Simulation.Turn" => 1.0,
                "InvariantChecker.CheckAll" => 1.0,
                "Simulation.FullCombat50Turns" => 50.0,
                "DeterministicExporter.Export" => 10.0,
                _ => 100.0
            };

            Assert.True(metric.P95Ms < target,
                $"{metric.OperationName} exceeded target: {metric.P95Ms:F3}ms > {target}ms");
        }
    }
}
