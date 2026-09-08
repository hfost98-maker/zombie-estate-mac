#!/bin/bash
# Frozen copy of the FNA port from the download (before AI/co-op/map-select patches).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GAME_DIR="$ROOT/game"
ORIG_DIR="$GAME_DIR/original"

if [ ! -f "$GAME_DIR/ZombieEstate.exe.pre-ai-p2-patch" ]; then
  echo "Missing backup: $GAME_DIR/ZombieEstate.exe.pre-ai-p2-patch"
  exit 1
fi

mkdir -p "$ORIG_DIR"

cp -f "$GAME_DIR/ZombieEstate.exe.pre-ai-p2-patch" "$ORIG_DIR/ZombieEstate.exe"
cp -f "$GAME_DIR/FNA.dll.pre-coop-input-patch" "$ORIG_DIR/FNA.dll"
cp -f "$GAME_DIR/FNA.NetStub.dll.pre-focus-patch" "$ORIG_DIR/FNA.NetStub.dll"
[ -f "$GAME_DIR/ZombieEstate.exe.config" ] && cp -f "$GAME_DIR/ZombieEstate.exe.config" "$ORIG_DIR/ZombieEstate.exe.config"

for item in "$GAME_DIR"/*; do
  base="$(basename "$item")"
  case "$base" in
    original|ZombieEstate.exe|FNA.dll|FNA.NetStub.dll|ZombieEstate.exe.config|\
    .ai-p2-patched|.input-focus-patched|zombie-estate.prefs|\
    ZombieEstate.exe.pre-*|ZombieEstate.exe.xbox.backup|\
    FNA.dll.pre-*|FNA.NetStub.dll.pre-*|\
    HedgeMaze*|OpenField*|PathMap_HedgeMaze.txt|PathMap_OpenField.txt|Maps)
      continue
      ;;
  esac
  if [ ! -e "$ORIG_DIR/$base" ]; then
    ln -sf "../$base" "$ORIG_DIR/$base"
  fi
done

echo "Original game files ready in $ORIG_DIR"
