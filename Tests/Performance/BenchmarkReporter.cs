#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using QDND.Tools.Profiling;

namespace Tests.Performance;

/// <summary>
/// Generates benchmark reports for CI integration.
/// </summary>
public class BenchmarkReporter
{
    public const double DefaultRegressionThreshold = 0.50; // 50% - increased to account for system variance in development
    public const double MinAbsoluteDiffMs = 0.05; // 50 microseconds - ignore regressions smaller than this

    /// <summary>
    /// Save benchmark results to JSON file.
    /// </summary>
    public static void SaveResults(BenchmarkResults results, string path)
    {
        results.SaveToJson(path);
    }

    /// <summary>
    /// Compare current results against a baseline file.
    /// Returns list of regressions that exceed threshold.
    /// </summary>
    public static List<BenchmarkRegression> CompareToBaseline(
        BenchmarkResults current,
        string baselinePath,
        double threshold = DefaultRegressionThreshold,
        double minAbsoluteDiffMs = MinAbsoluteDiffMs)
    {
        var regressions = new List<BenchmarkRegression>();

        if (!File.Exists(baselinePath))
            return regressions;

        var baselineJson = File.ReadAllText(baselinePath);
        var baseline = JsonSerializer.Deserialize<BaselineData>(baselineJson);

        if (baseline?.Benchmarks == null)
            return regressions;

        foreach (var currentMetric in current.Results)
        {
            var baselineMetric = baseline.Benchmarks
                .Find(b => b.OperationName == currentMetric.OperationName);

            if (baselineMetric == null)
                continue;

            // Compare P95 values
            var absoluteDiff = currentMetric.P95Ms - baselineMetric.P95Ms;
            var percentChange = absoluteDiff / baselineMetric.P95Ms;

            // Only flag regressions if BOTH percentage AND absolute thresholds are exceeded
            // This prevents false positives from tiny baseline values (e.g., 0.001ms -> 0.0013ms = 30% but only 0.0003ms diff)
            if (percentChange > threshold && absoluteDiff > minAbsoluteDiffMs)
            {
                regressions.Add(new BenchmarkRegression
                {
                    OperationName = currentMetric.OperationName,
                    BaselineP95Ms = baselineMetric.P95Ms,
                    CurrentP95Ms = currentMetric.P95Ms,
                    PercentChange = percentChange * 100,
                    Threshold = threshold * 100
                });
            }
        }

        return regressions;
    }

    /// <summary>
    /// Update baseline file with current results.
    /// </summary>
    public static void UpdateBaseline(BenchmarkResults results, string baselinePath)
    {
        results.SaveToJson(baselinePath);
    }
}

public class BaselineData
{
    public string? SuiteName { get; set; }
    public string? Timestamp { get; set; }
    public List<BaselineMetric>? Benchmarks { get; set; }
}

public class BaselineMetric
{
    public string OperationName { get; set; } = "";
    public int Iterations { get; set; }
    public double MeanMs { get; set; }
    public double MedianMs { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public double MinMs { get; set; }
    public double MaxMs { get; set; }
}

public class BenchmarkRegression
{
    public string OperationName { get; set; } = "";
    public double BaselineP95Ms { get; set; }
    public double CurrentP95Ms { get; set; }
    public double PercentChange { get; set; }
    public double Threshold { get; set; }

    public override string ToString()
    {
        return $"REGRESSION: {OperationName} - P95 increased from {BaselineP95Ms:F3}ms to {CurrentP95Ms:F3}ms ({PercentChange:F1}% > {Threshold}% threshold)";
    }
}
