#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace QDND.Tools.Profiling;

/// <summary>
/// Profiling harness for measuring operation performance.
/// </summary>
public class ProfilerHarness
{
    private readonly int _warmupIterations;

    public ProfilerHarness(int warmupIterations = 10)
    {
        _warmupIterations = warmupIterations;
    }

    /// <summary>
    /// Measure the performance of an operation.
    /// </summary>
    public ProfilerMetrics Measure(string operationName, Action operation, int iterations = 100)
    {
        // Warmup
        for (int i = 0; i < _warmupIterations; i++)
        {
            operation();
        }

        // Force GC to reduce noise
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var samples = new List<double>();
        var stopwatch = new Stopwatch();

        for (int i = 0; i < iterations; i++)
        {
            stopwatch.Restart();
            operation();
            stopwatch.Stop();

            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return ProfilerMetrics.Compute(operationName, samples);
    }

    /// <summary>
    /// Measure async operation performance.
    /// </summary>
    public async Task<ProfilerMetrics> MeasureAsync(
        string operationName,
        Func<Task> operation,
        int iterations = 100)
    {
        // Warmup
        for (int i = 0; i < _warmupIterations; i++)
        {
            await operation();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var samples = new List<double>();
        var stopwatch = new Stopwatch();

        for (int i = 0; i < iterations; i++)
        {
            stopwatch.Restart();
            await operation();
            stopwatch.Stop();

            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return ProfilerMetrics.Compute(operationName, samples);
    }

    /// <summary>
    /// Measure operation with setup and teardown.
    /// </summary>
    public ProfilerMetrics MeasureWithSetup<T>(
        string operationName,
        Func<T> setup,
        Action<T> operation,
        Action<T>? teardown = null,
        int iterations = 100)
    {
        var samples = new List<double>();
        var stopwatch = new Stopwatch();

        // Warmup
        for (int i = 0; i < _warmupIterations; i++)
        {
            var setupResult = setup();
            operation(setupResult);
            teardown?.Invoke(setupResult);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        for (int i = 0; i < iterations; i++)
        {
            var setupResult = setup();

            stopwatch.Restart();
            operation(setupResult);
            stopwatch.Stop();

            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            teardown?.Invoke(setupResult);
        }

        return ProfilerMetrics.Compute(operationName, samples);
    }
}
