#!/usr/bin/env bash
set -euo pipefail

# CI Benchmark Runner Script
# Runs performance benchmarks and gates on regressions

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
RESULTS_DIR="${PROJECT_ROOT}/benchmark-results"

echo "=== QDND Benchmark Gate ==="
echo "Project Root: ${PROJECT_ROOT}"
echo "Results Dir: ${RESULTS_DIR}"
echo ""

# Create results directory
mkdir -p "${RESULTS_DIR}"

# Build in Release mode for accurate benchmarks
echo "Building in Release mode..."
dotnet build "${PROJECT_ROOT}/Tests/QDND.Tests.csproj" -c Release -v quiet

# Set output directory environment variable for tests
export QDND_BENCH_OUTPUT_DIR="${RESULTS_DIR}"

# Run benchmark gate test
echo ""
echo "Running benchmark gate test..."
dotnet test "${PROJECT_ROOT}/Tests/QDND.Tests.csproj" \
    -c Release \
    --filter "FullyQualifiedName~CIBenchmarkGateTests" \
    --logger "console;verbosity=detailed" \
    --no-build

# Capture exit code
EXIT_CODE=$?

echo ""
if [ $EXIT_CODE -eq 0 ]; then
    echo "✓ Benchmark gate passed"
else
    echo "✗ Benchmark gate failed"
    echo ""
    echo "Performance regression detected or test failure."
    echo "Review output above for details."
fi

echo "Results saved to: ${RESULTS_DIR}"

# Exit with test result code
exit $EXIT_CODE
