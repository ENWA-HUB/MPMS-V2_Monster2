#!/bin/zsh
set -e
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT/frontend"
echo "Installing frontend dependencies..."
npm install
echo "Building R6 frontend..."
npm run build
rm -rf "$ROOT/backend/MAIPT.PM.Api/wwwroot"
mkdir -p "$ROOT/backend/MAIPT.PM.Api/wwwroot"
cp -R dist/* "$ROOT/backend/MAIPT.PM.Api/wwwroot/"
echo "R6 frontend copied to backend/wwwroot."
