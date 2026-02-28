#!/usr/bin/env bash
set -euo pipefail
SLN="$(ls *.sln | head -n 1)"

# Build export release (solution has ExportRelease, not Release)
dotnet build "$SLN" -c ExportRelease

# Also build Debug for Godot headless mode (used by simulation tests)
dotnet build QDND.csproj -c Debug -v q
