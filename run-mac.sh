#!/bin/bash
# Run patched Zombie Estate on Mac using system Mono + FNA
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
GAME_DIR="$ROOT/game"
LIB_DIR="$ROOT/lib"
TOOLS_DIR="$ROOT/tools"
MONO="/Library/Frameworks/Mono.framework/Versions/Current/bin/mono"

if [ ! -x "$MONO" ]; then
  echo "Mono not found at $MONO"
  echo "Install Mono 6.x: https://www.mono-project.com/download/"
  exit 1
fi

# Mono resolves managed assemblies from MONO_PATH and the exe directory.
export MONO_PATH="$LIB_DIR${MONO_PATH:+:$MONO_PATH}"

# patch-xna expects FNA.NetStub.dll beside the exe; symlink port libs into game/
for dll in "$LIB_DIR"/*.dll "$LIB_DIR"/FNA.dll.config; do
  [ -e "$dll" ] || continue
  base="$(basename "$dll")"
  if [ ! -e "$GAME_DIR/$base" ]; then
    ln -sf "../lib/$base" "$GAME_DIR/$base"
  fi
done

cd "$GAME_DIR"

if [ ! -f "ZombieEstate.exe" ]; then
  echo "ZombieEstate.exe missing in game/"
  exit 1
fi

# Re-apply Xbox -> FNA patch if exe still matches the backup
if [ -f "ZombieEstate.exe.xbox.backup" ] && [ -f "$TOOLS_DIR/patch-xna.exe" ]; then
  if cmp -s "ZombieEstate.exe" "ZombieEstate.exe.xbox.backup" 2>/dev/null; then
    echo "Patching Xbox build -> FNA..."
    "$MONO" "$TOOLS_DIR/patch-xna.exe" "$GAME_DIR/ZombieEstate.exe"
  fi
fi

exec "$MONO" "$GAME_DIR/ZombieEstate.exe" "$@"
