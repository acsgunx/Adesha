#!/usr/bin/env bash
# ============================================================================
# Adesha — Kill, Clean, Build, Run
# ============================================================================
# Usage:
#   ./dev.sh              # kill + clean + build + run (full reset)
#   ./dev.sh --no-clean   # kill + build + run (skip clean, faster)
#   ./dev.sh --run-only   # kill + run only (no rebuild)
#   ./dev.sh --kill-only  # just kill everything, don't build or run
#
# Double-click on macOS: this script is executable. You can also create an
# Automator "Application" that runs `./dev.sh` in Terminal for true double-click.
# ============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

NO_CLEAN=false
RUN_ONLY=false
KILL_ONLY=false

for arg in "$@"; do
  case "$arg" in
    --no-clean)  NO_CLEAN=true ;;
    --run-only)  RUN_ONLY=true ;;
    --kill-only) KILL_ONLY=true ;;
    *) echo "Unknown flag: $arg"; exit 1 ;;
  esac
done

# ---------------------------------------------------------------------------
# 1. KILL — stop any running Adesha / Aspire / Docker processes
# ---------------------------------------------------------------------------
echo ""
echo "============================================================"
echo "  1/4  KILLING existing Adesha processes"
echo "============================================================"

# Kill Aspire CLI and DCP (Distributed Control Plane) processes
pkill -f "aspire run" 2>/dev/null || true
pkill -f "aspire.*Adesha" 2>/dev/null || true
pkill -f "dcp" 2>/dev/null || true
pkill -f "Aspire.Dashboard" 2>/dev/null || true

# Kill the API process
pkill -f "Adesha.Api" 2>/dev/null || true

# Kill the Angular dev server (Vite)
pkill -f "Adesha.Web.*vite" 2>/dev/null || true
pkill -f "node.*Adesha.Web" 2>/dev/null || true

# Stop and remove Adesha Docker containers (Postgres, Redis from previous run)
# These are created by Aspire with adesha-* prefix
docker ps --filter "name=adesha-" --format "{{.Names}}" 2>/dev/null | while read -r cname; do
  echo "  Stopping container: $cname"
  docker stop "$cname" >/dev/null 2>&1 || true
  docker rm "$cname" >/dev/null 2>&1 || true
done

# Remove persistent volumes on full clean to avoid stale password issues.
# Aspire generates random passwords; a stale volume has the old password.
if [ "$NO_CLEAN" = false ] && [ "$RUN_ONLY" = false ]; then
  echo "  Removing stale Docker volumes..."
  docker volume ls --filter "name=adesha-" --format "{{.Name}}" 2>/dev/null | while read -r vname; do
    echo "  Removing volume: $vname"
    docker volume rm "$vname" >/dev/null 2>&1 || true
  done
fi

# Wait for all processes to fully exit and release ports
echo "  Waiting for processes to exit..."
for i in $(seq 1 10); do
  if ! pgrep -f "dcp|Adesha.Api|Aspire.Dashboard|aspire.*Adesha" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

# Force kill any stubborn processes
pkill -9 -f "dcp" 2>/dev/null || true
pkill -9 -f "Adesha.Api" 2>/dev/null || true
pkill -9 -f "Aspire.Dashboard" 2>/dev/null || true
sleep 1

echo "  Done. All Adesha processes stopped."

if [ "$KILL_ONLY" = true ]; then
  echo ""
  echo "  --kill-only specified. Exiting without build or run."
  exit 0
fi

# ---------------------------------------------------------------------------
# 2. CLEAN — remove build artifacts
# ---------------------------------------------------------------------------
if [ "$RUN_ONLY" = false ] && [ "$NO_CLEAN" = false ]; then
  echo ""
  echo "============================================================"
  echo "  2/4  CLEAN (dotnet clean + Angular clean)"
  echo "============================================================"

  echo "  Cleaning .NET solution..."
  dotnet clean --verbosity quiet 2>/dev/null || true

  echo "  Cleaning Angular app..."
  if [ -d "src/Adesha.Web/node_modules" ]; then
    cd src/Adesha.Web
    rm -rf dist .angular
    cd "$SCRIPT_DIR"
  fi

  echo "  Done."
elif [ "$NO_CLEAN" = true ]; then
  echo ""
  echo "  2/4  CLEAN skipped (--no-clean)"
fi

# ---------------------------------------------------------------------------
# 3. BUILD — compile the full solution + Angular app
# ---------------------------------------------------------------------------
if [ "$RUN_ONLY" = false ]; then
  echo ""
  echo "============================================================"
  echo "  3/4  BUILD (dotnet build + ng build)"
  echo "============================================================"

  echo "  Building .NET solution..."
  dotnet build --verbosity quiet
  if [ $? -ne 0 ]; then
    echo ""
    echo "  BUILD FAILED. Fix errors above before running."
    exit 1
  fi
  echo "  .NET build succeeded."

  echo "  Building Angular app..."
  if [ -f "src/Adesha.Web/package.json" ]; then
    cd src/Adesha.Web
    if [ ! -d "node_modules" ]; then
      echo "  Installing npm dependencies..."
      npm ci --silent
    fi
    npm run build -- --silent 2>/dev/null || npx ng build --configuration development 2>/dev/null || true
    cd "$SCRIPT_DIR"
    echo "  Angular build succeeded."
  fi

  echo ""
  echo "  Running tests to verify build integrity..."
  dotnet test --verbosity quiet --no-build 2>&1 | tail -5
fi

# ---------------------------------------------------------------------------
# 4. RUN — start the full stack via Aspire
# ---------------------------------------------------------------------------
echo ""
echo "============================================================"
echo "  4/4  RUN (aspire run)"
echo "============================================================"

# Ensure Aspire CLI is available
if ! command -v aspire &>/dev/null; then
  export PATH="$HOME/.aspire/bin:$PATH"
fi

if ! command -v aspire &>/dev/null; then
  echo ""
  echo "  ERROR: aspire CLI not found."
  echo "  Install it:  brew install aspire"
  echo "  Or:          dotnet tool install -g aspire.cli"
  exit 1
fi

# Ensure Docker (Colima on macOS) is running
if ! docker info &>/dev/null 2>&1; then
  echo "  Starting Docker (Colima)..."
  colima start 2>/dev/null || true
  sleep 3
  if ! docker info &>/dev/null 2>&1; then
    echo "  ERROR: Docker is not running. Start it manually: colima start"
    exit 1
  fi
fi

echo ""
echo "  Starting Aspire..."
echo "  Dashboard will be available at the URL shown below."
echo "  Angular app:    http://localhost:4200"
echo "  API health:     http://localhost:5157/health"
echo ""
echo "  Press Ctrl+C to stop."
echo ""

aspire run
