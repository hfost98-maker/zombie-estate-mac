#!/bin/bash
# One-time Xbox 360 -> FNA conversion using XnaToFna timemachine branch.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GAME_DIR="$ROOT/game"
LIB_DIR="$ROOT/lib"
TOOLS_DIR="$ROOT/tools"
MONO="/Library/Frameworks/Mono.framework/Versions/Current/bin/mono"
XTF="$TOOLS_DIR/XnaToFna.exe"

if [ ! -x "$MONO" ]; then
  echo "Mono not found at $MONO"
  exit 1
fi

if [ ! -f "$XTF" ]; then
  echo "Missing $XTF — build XnaToFna timemachine branch first."
  exit 1
fi

if [ ! -f "$GAME_DIR/ZombieEstate.exe.xbox.backup" ]; then
  echo "Missing Xbox backup at game/ZombieEstate.exe.xbox.backup"
  exit 1
fi

echo "Restoring Xbox backup..."
cp "$GAME_DIR/ZombieEstate.exe.xbox.backup" "$GAME_DIR/ZombieEstate.exe"

echo "Patching mscorlib references for Cecil..."
"$MONO" "$TOOLS_DIR/patch-xna.exe" --mscorlib-only "$GAME_DIR/ZombieEstate.exe"

mkdir -p /tmp/orig
echo "Running XnaToFna timemachine conversion..."
cd /tmp
"$MONO" "$XTF" --update-xna --skip-content \
  "$XTF" \
  "$LIB_DIR/FNA.dll" \
  "$LIB_DIR/FNA.NetStub.dll" \
  "$GAME_DIR/ZombieEstate.exe"

cp "$XTF" "$GAME_DIR/"
cp "$LIB_DIR/FNA.dll" "$LIB_DIR/FNA.NetStub.dll" "$GAME_DIR/"
echo "Conversion complete."
