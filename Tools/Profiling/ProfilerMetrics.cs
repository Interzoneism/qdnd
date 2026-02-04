#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Tools.Profiling;

/// <summary>
/// Performance metrics from a profiling run.
/// </summary>
public class ProfilerMetrics
{
    public string OperationName { get; set; } = "";
    public int Iterations { get; set; }
    public double TotalMs { get; set; }
    public double MeanMs { get; set; }
    public double MedianMs { get; set; }
    public double MinMs { get; set; }
    public double MaxMs { get; set; }
    public double StdDevMs { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public List<double> RawSamples { get; set; } = new();

    public static ProfilerMetrics Compute(string name, List<double> samples)
    {
        if (samples.Count == 0)
        {
            return new ProfilerMetrics { OperationName = name };
        }

        var sorted = samples.OrderBy(x => x).ToList();
        var mean = samples.Average();
        var variance = samples.Average(x => Math.Pow(x - mean, 2));

        return new ProfilerMetrics
        {
            OperationName = name,
            Iterations = samples.Count,
            TotalMs = samples.Sum(),
            MeanMs = mean,
            MedianMs = sorted[sorted.Count / 2],
            MinMs = sorted.First(),
            MaxMs = sorted.Last(),
            StdDevMs = Math.Sqrt(variance),
            P95Ms = GetPercentile(sorted, 0.95),
            P99Ms = GetPercentile(sorted, 0.99),
            RawSamples = samples
        };
    }

    private static double GetPercentile(List<double> sorted, double percentile)
    {
        var index = (int)Math.Ceiling(sorted.Count * percentile) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }

    public override string ToString()
    {
        return $"{OperationName}: Mean={MeanMs:F3}ms, P95={P95Ms:F3}ms, P99={P99Ms:F3}ms, Max={MaxMs:F3}ms ({Iterations} iterations)";
    }
}
