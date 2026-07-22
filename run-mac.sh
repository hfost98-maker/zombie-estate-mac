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

# Native FNA libraries (SDL3, FNA3D, FAudio, theorafile)
export DYLD_LIBRARY_PATH="$GAME_DIR:$LIB_DIR${DYLD_LIBRARY_PATH:+:$DYLD_LIBRARY_PATH}"

if [ ! -f "$LIB_DIR/libSDL3.0.dylib" ]; then
  echo "fnalibs missing — run scripts/setup-fnlibs.sh first"
  exit 1
fi

# Unversioned dylib names expected by FNA DllImport
for pair in libSDL3.0.dylib:libSDL3.dylib libFAudio.0.dylib:libFAudio.dylib libFNA3D.0.dylib:libFNA3D.dylib; do
  src="${pair%%:*}"
  dst="${pair##*:}"
  [ -e "$LIB_DIR/$src" ] || continue
  [ -e "$LIB_DIR/$dst" ] || ln -sf "$src" "$LIB_DIR/$dst"
  [ -e "$GAME_DIR/$dst" ] || ln -sf "../lib/$dst" "$GAME_DIR/$dst"
done

# Mono resolves managed assemblies from MONO_PATH and the exe directory.
export MONO_PATH="$LIB_DIR${MONO_PATH:+:$MONO_PATH}"

# Symlink port libs into game/ for patch-xna and runtime resolution.
for dll in "$LIB_DIR"/*.dll "$LIB_DIR"/*.dylib "$LIB_DIR"/FNA.dll.config; do
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

# If still the raw Xbox build, run one-time timemachine conversion.
if [ -f "ZombieEstate.exe.xbox.backup" ] && cmp -s "ZombieEstate.exe" "ZombieEstate.exe.xbox.backup" 2>/dev/null; then
  echo "Unconverted Xbox build detected — running scripts/convert-xbox.sh ..."
  "$ROOT/scripts/convert-xbox.sh"
fi

# Legacy: simple Cecil relink if timemachine conversion was not done.
if [ -f "$TOOLS_DIR/patch-xna.exe" ] && [ ! -f "XnaToFna.exe" ]; then
  if cmp -s "ZombieEstate.exe" "ZombieEstate.exe.xbox.backup" 2>/dev/null; then
    echo "Patching Xbox build -> FNA (basic)..."
    "$MONO" "$TOOLS_DIR/patch-xna.exe" "$GAME_DIR/ZombieEstate.exe"
  fi
fi

exec "$MONO" "$GAME_DIR/ZombieEstate.exe" "$@"
