#!/usr/bin/env bash
set -euo pipefail

SLN="$(ls *.sln | head -n 1)"

echo "=== QDND parity-validate ==="
dotnet test "$SLN" -c Release --no-build --filter "FullyQualifiedName~ParityValidationTests"
