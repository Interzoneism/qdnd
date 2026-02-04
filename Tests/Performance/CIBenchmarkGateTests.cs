#nullable enable
using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Tests.Performance;

/// <summary>
/// Collection definition to ensure benchmark tests run without parallelism.
/// </summary>
[CollectionDefinition("BenchmarkTests", DisableParallelization = true)]
public class BenchmarkTestCollection
{
}

/// <summary>
/// CI benchmark gate tests that enforce performance regression prevention.
/// </summary>
[Collection("BenchmarkTests")]
public class CIBenchmarkGateTests
{
    private readonly ITestOutputHelper _output;

    public CIBenchmarkGateTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BenchmarkGate_NoRegressions_PassesOrWarnsIfNoBaseline()
    {
        // Arrange: Get output directory from env var or use default
        var outputDir = Environment.GetEnvironmentVariable("QDND_BENCH_OUTPUT_DIR")
                        ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "benchmark-results");

        // Ensure absolute path
        outputDir = Path.GetFullPath(outputDir);

        _output.WriteLine($"Benchmark output directory: {outputDir}");
        Directory.CreateDirectory(outputDir);

        var runner = new CIBenchmarkRunner(outputDir);

        // Act: Run benchmarks with regression check
        var (results, regressions) = runner.RunWithRegressionCheck();

        // Log results
        _output.WriteLine("");
        _output.WriteLine("=== Benchmark Results ===");
        foreach (var result in results.Results)
        {
            _output.WriteLine($"  {result.OperationName}: P95={result.P95Ms:F3}ms, Mean={result.MeanMs:F3}ms");
        }

        // Save current results with timestamp
        runner.SaveResults(results, updateBaseline: false);
        _output.WriteLine("");
        _output.WriteLine($"Results saved to: {outputDir}");

        // Check for baseline
        var baselinePath = Path.Combine(outputDir, "baseline.json");
        if (!File.Exists(baselinePath))
        {
            // Option B (soft): Missing baseline passes but warns
            _output.WriteLine("");
            _output.WriteLine("WARNING: No baseline.json found. Benchmark gate passes but cannot detect regressions.");
            _output.WriteLine($"To establish a baseline, copy the latest benchmark JSON to: {baselinePath}");
            _output.WriteLine("");

            // Pass the test
            Assert.True(true, "Baseline missing - gate passes with warning");
            return;
        }

        // Assert: No regressions should exceed threshold
        if (regressions.Count > 0)
        {
            _output.WriteLine("");
            _output.WriteLine("=== PERFORMANCE REGRESSIONS DETECTED ===");
            foreach (var regression in regressions)
            {
                _output.WriteLine($"  {regression}");
            }
            _output.WriteLine("");

            var regressionDetails = string.Join("\n", regressions.Select(r => r.ToString()));
            Assert.Fail($"Performance regressions detected:\n{regressionDetails}");
        }

        _output.WriteLine("");
        _output.WriteLine("✓ All benchmarks within acceptable performance range");
    }
}
