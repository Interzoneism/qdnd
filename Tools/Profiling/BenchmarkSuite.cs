#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QDND.Tools.Profiling;

/// <summary>
/// A suite of benchmarks that can be run together.
/// </summary>
public class BenchmarkSuite
{
    private readonly ProfilerHarness _harness;
    private readonly List<BenchmarkDefinition> _benchmarks = new();
    
    public string SuiteName { get; set; } = "Benchmarks";
    
    public BenchmarkSuite(int warmupIterations = 10)
    {
        _harness = new ProfilerHarness(warmupIterations);
    }
    
    public void AddBenchmark(string name, Action operation, int iterations = 100)
    {
        _benchmarks.Add(new BenchmarkDefinition(name, operation, iterations));
    }
    
    public BenchmarkResults Run()
    {
        var results = new BenchmarkResults
        {
            SuiteName = SuiteName,
            StartTime = DateTime.UtcNow
        };
        
        foreach (var benchmark in _benchmarks)
        {
            try
            {
                var metrics = _harness.Measure(benchmark.Name, benchmark.Operation, benchmark.Iterations);
                results.Results.Add(metrics);
            }
            catch (Exception ex)
            {
                results.Errors.Add($"{benchmark.Name}: {ex.Message}");
            }
        }
        
        results.EndTime = DateTime.UtcNow;
        return results;
    }
    
    private record BenchmarkDefinition(string Name, Action Operation, int Iterations);
}

/// <summary>
/// Results from a benchmark suite run.
/// </summary>
public class BenchmarkResults
{
    public string SuiteName { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public List<ProfilerMetrics> Results { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    
    public void SaveToJson(string path)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var summary = new
        {
            SuiteName,
            Timestamp = StartTime.ToString("o"),
            DurationMs = Duration.TotalMilliseconds,
            Benchmarks = Results.Select(r => new
            {
                r.OperationName,
                r.Iterations,
                r.MeanMs,
                r.MedianMs,
                r.P95Ms,
                r.P99Ms,
                r.MinMs,
                r.MaxMs
            }),
            Errors
        };
        
        File.WriteAllText(path, JsonSerializer.Serialize(summary, options));
    }
    
    public void PrintSummary(Action<string>? writer = null)
    {
        writer ??= Console.WriteLine;
        
        writer($"=== {SuiteName} ===");
        writer($"Duration: {Duration.TotalSeconds:F2}s");
        writer("");
        
        foreach (var result in Results)
        {
            writer(result.ToString());
        }
        
        if (Errors.Count > 0)
        {
            writer("");
            writer("Errors:");
            foreach (var error in Errors)
            {
                writer($"  - {error}");
            }
        }
    }
}
