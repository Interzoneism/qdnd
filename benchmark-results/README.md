# Benchmark Results

This directory contains performance benchmark results and the baseline used for regression detection in CI.

## Files

- `baseline.json` - The committed baseline against which all benchmarks are compared. This file should be updated deliberately when expected performance changes occur.
- `benchmark_YYYYMMDD_HHMMSS.json` - Timestamped benchmark run results, automatically created by the benchmark gate test.

## CI Benchmark Gate

The `scripts/ci-benchmark.sh` script runs a performance regression gate that:
1. Executes all benchmarks in Release mode
2. Compares results against `baseline.json` (if it exists)
3. Fails CI if any benchmark regresses by more than 20% (P95 metric)

### Baseline Policy (Option B - Soft)

**Missing Baseline:** If `baseline.json` does not exist, the gate passes with a warning. This allows development to proceed but disables regression detection.

**With Baseline:** All benchmarks must stay within 20% of the baseline P95 performance, or CI fails.

## Updating the Baseline

When legitimate performance changes occur (optimization or expected degradation), update the baseline:

```bash
# Run benchmarks
./scripts/ci-benchmark.sh

# Copy the latest result as the new baseline
cp benchmark-results/benchmark_YYYYMMDD_HHMMSS.json benchmark-results/baseline.json

# Commit the new baseline
git add benchmark-results/baseline.json
git commit -m "chore: update performance baseline"
```

## Interpreting Results

Each benchmark JSON contains:
- `OperationName` - Name of the benchmarked operation
- `Iterations` - Number of times the operation was executed
- `MeanMs`, `MedianMs` - Average and median execution time
- `P95Ms`, `P99Ms` - 95th and 99th percentile times (regression checks use P95)
- `MinMs`, `MaxMs` - Fastest and slowest execution times

The P95 metric is used for regression detection because it's more stable than max while still catching performance degradation.
