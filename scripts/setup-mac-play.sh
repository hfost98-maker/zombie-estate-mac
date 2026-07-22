#!/bin/bash
# Convert Xbox 360 Zombie Estate -> macOS playable (FNA)
# Needs: mono (https://www.mono-project.com/download/), ffmpeg (brew install ffmpeg)
set -euo pipefail
cd "$(dirname "$0")"

command -v mono >/dev/null || { echo "Install Mono first: https://www.mono-project.com/download/"; exit 1; }
command -v ffmpeg >/dev/null || { echo "Install ffmpeg first (e.g. brew install ffmpeg)"; exit 1; }

PORT_DIR="$(pwd)"
TOOLS="$PORT_DIR/xnatofna"

if [ ! -f "$TOOLS/XnaToFna.exe" ]; then
  echo "Downloading XnaToFna..."
  curl -L -o /tmp/xnatofna.zip "https://github.com/0x0ade/XnaToFna/archive/refs/heads/timemachine.zip"
  unzip -o /tmp/xnatofna.zip -d /tmp
  mkdir -p "$TOOLS"
  echo "Build timemachine branch with Visual Studio/MonoDevelop, then copy bin output to xnatofna/"
  echo "Or use the v18.05.1 release binaries already in xnatofna/ if present."
fi

if [ ! -f "$PORT_DIR/FNA.dll" ]; then
  cp "$TOOLS/FNA.dll" "$PORT_DIR/" 2>/dev/null || true
fi

if [ ! -f "$PORT_DIR/libSDL2-2.0.0.dylib" ]; then
  echo "Downloading FNA native libraries..."
  curl -L -o /tmp/fnalibs.tar.bz2 "https://fna.flibitijibibo.com/archive/fnalibs.tar.bz2"
  tar -xjf /tmp/fnalibs.tar.bz2 -C /tmp
  cp /tmp/osx-arm64/*.dylib "$PORT_DIR/" 2>/dev/null || cp /tmp/osx/*.dylib "$PORT_DIR/" 2>/dev/null || true
fi

cp "$TOOLS/MonoMod.exe" "$PORT_DIR/MonoMod.dll" 2>/dev/null || true
cp "$TOOLS"/*.dll "$PORT_DIR/" 2>/dev/null || true
cp "$TOOLS/XnaToFna.exe" "$PORT_DIR/" 2>/dev/null || true

echo "Converting Xbox 360 XNA -> FNA (one-time)..."
mono "$PORT_DIR/XnaToFna.exe"

echo ""
echo "Launch with:"
echo "  cd \"$PORT_DIR\" && mono ZombieEstate.exe"
