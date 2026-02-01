#!/usr/bin/env bash
set -euo pipefail

# CI Benchmark Runner Script
# Runs performance benchmarks and outputs results

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
RESULTS_DIR="${PROJECT_ROOT}/benchmark-results"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

echo "=== QDND Benchmark Runner ==="
echo "Project Root: ${PROJECT_ROOT}"
echo "Results Dir: ${RESULTS_DIR}"
echo ""

# Create results directory
mkdir -p "${RESULTS_DIR}"

# Build in Release mode for accurate benchmarks
echo "Building in Release mode..."
dotnet build "${PROJECT_ROOT}/Tests/QDND.Tests.csproj" -c Release -v quiet

# Run benchmark tests
echo ""
echo "Running benchmarks..."
dotnet test "${PROJECT_ROOT}/Tests/QDND.Tests.csproj" \
    -c Release \
    --filter "FullyQualifiedName~PerformanceBenchmarks" \
    --logger "console;verbosity=detailed" \
    --results-directory "${RESULTS_DIR}" \
    --logger "trx;LogFileName=benchmark_${TIMESTAMP}.trx" \
    --no-build

# Check for baseline and compare
BASELINE_FILE="${RESULTS_DIR}/baseline.json"
CURRENT_FILE="${RESULTS_DIR}/benchmark_${TIMESTAMP}.json"

if [ -f "${BASELINE_FILE}" ]; then
    echo ""
    echo "Comparing against baseline..."
    # Simple comparison would go here
    # In a real system, you'd parse both JSON files and compare P95 values
    echo "Note: Manual baseline comparison required"
fi

echo ""
echo "Benchmarks complete!"
echo "Results saved to: ${RESULTS_DIR}"

# Exit successfully
exit 0
