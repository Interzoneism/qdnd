#!/usr/bin/env bash
set -euo pipefail
SLN="$(ls *.sln | head -n 1)"
dotnet build "$SLN" -c Release
