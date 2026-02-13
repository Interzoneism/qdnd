#!/usr/bin/env bash
set -euo pipefail
SLN="$(ls *.sln | head -n 1)"
# Exclude CIBenchmarkGateTests (run separately via ci-benchmark.sh)
dotnet test "$SLN" -c Release --no-build --filter "FullyQualifiedName!~CIBenchmarkGateTests&FullyQualifiedName!~ParityValidationTests"

# Mandatory parity validation gate.
./scripts/ci-parity-validate.sh
