#!/usr/bin/env bash
# Launch the game with Hedge Maze pre-selected on character select.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

python3 scripts/generate-hedge-maze.py

export ZOMBIE_ESTATE_DEFAULT_MAP="Hedge Maze"
export ZOMBIE_ESTATE_AI_P2=0
export ZOMBIE_ESTATE_KEYBOARD_P2=1
export ZOMBIE_ESTATE_DUAL_SCREEN=0

exec ./run-mac.sh
