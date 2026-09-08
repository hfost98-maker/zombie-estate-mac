#!/bin/bash
# Run the unmodified FNA port (no AI, co-op, or map-select patches).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
GAME_DIR="$ROOT/game/original"
LIB_DIR="$ROOT/lib"
MONO="/Library/Frameworks/Mono.framework/Versions/Current/bin/mono"

if [ ! -x "$MONO" ]; then
  echo "Mono not found at $MONO"
  echo "Install Mono 6.x: https://www.mono-project.com/download/"
  exit 1
fi

if [ ! -f "$GAME_DIR/ZombieEstate.exe" ]; then
  echo "Original game not set up — run scripts/setup-original-game.sh first"
  exit 1
fi

export DYLD_LIBRARY_PATH="$GAME_DIR:$LIB_DIR${DYLD_LIBRARY_PATH:+:$DYLD_LIBRARY_PATH}"

if [ ! -f "$LIB_DIR/libSDL3.0.dylib" ]; then
  echo "fnalibs missing — run scripts/setup-fnlibs.sh first"
  exit 1
fi

export XBLIG_FULLSCREEN="${XBLIG_FULLSCREEN:-0}"
export XNATOFNA_DISPLAY_FULLSCREEN="${XNATOFNA_DISPLAY_FULLSCREEN:-0}"
export XBLIG_DISPLAY_WIDTH="${XBLIG_DISPLAY_WIDTH:-1280}"
export XBLIG_DISPLAY_HEIGHT="${XBLIG_DISPLAY_HEIGHT:-720}"

for pair in libSDL3.0.dylib:libSDL3.dylib libFAudio.0.dylib:libFAudio.dylib libFNA3D.0.dylib:libFNA3D.dylib libtheorafile.dylib:libtheorafile.dylib; do
  src="${pair%%:*}"
  dst="${pair##*:}"
  [ -e "$LIB_DIR/$src" ] || continue
  [ -e "$LIB_DIR/$dst" ] || ln -sf "$src" "$LIB_DIR/$dst"
  if [ ! -e "$GAME_DIR/$dst" ]; then
    ln -sf "../../lib/$dst" "$GAME_DIR/$dst"
  fi
done

export MONO_PATH="$GAME_DIR:$LIB_DIR${MONO_PATH:+:$MONO_PATH}"

for dll in "$LIB_DIR"/*.dll "$LIB_DIR"/*.dylib "$LIB_DIR"/FNA.dll.config; do
  [ -e "$dll" ] || continue
  base="$(basename "$dll")"
  case "$base" in
    FNA.dll|FNA.NetStub.dll) continue ;;
  esac
  if [ ! -e "$GAME_DIR/$base" ]; then
    ln -sf "../../lib/$base" "$GAME_DIR/$base"
  fi
done

cd "$GAME_DIR"
exec "$MONO" "$GAME_DIR/ZombieEstate.exe" "$@"
