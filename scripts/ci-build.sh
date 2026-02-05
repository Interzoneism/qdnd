#!/usr/bin/env bash
set -euo pipefail
SLN="$(ls *.sln | head -n 1)"

# Build Release for production
dotnet build "$SLN" -c Release

# Also build Debug for Godot headless mode (used by simulation tests)
dotnet build QDND.csproj -c Debug -v q
