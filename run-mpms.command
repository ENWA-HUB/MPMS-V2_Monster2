#!/bin/zsh
set -e
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT/backend/MAIPT.PM.Api"
echo "Starting MAIPT Project Management R6 on http://localhost:5088"
dotnet run --urls http://localhost:5088
