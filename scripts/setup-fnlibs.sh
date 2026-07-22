#!/bin/bash
# Download fnalibs for macOS from fnalibs-dailies (official archive URL is 404).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LIB="$ROOT/lib"
GH="${GH:-/tmp/gh-install/gh_2.63.2_macOS_arm64/bin/gh}"

mkdir -p "$LIB"

if ls "$LIB"/libSDL3*.dylib >/dev/null 2>&1; then
  echo "fnalibs already present in lib/"
  exit 0
fi

if [ ! -x "$GH" ]; then
  echo "gh CLI not found — install fnalibs manually into lib/ from:"
  echo "  https://github.com/FNA-XNA/fnalibs-dailies/actions"
  exit 1
fi

RUN_ID="$("$GH" run list --repo FNA-XNA/fnalibs-dailies --limit 1 --json databaseId --jq '.[0].databaseId')"
TMP="$(mktemp -d)"
"$GH" run download "$RUN_ID" --repo FNA-XNA/fnalibs-dailies --name fnalibs-apple --dir "$TMP"
cp "$TMP"/osx/*.dylib "$LIB/"
xattr -c "$LIB"/*.dylib 2>/dev/null || true

# FNA DllImport names (SDL3, FAudio, FNA3D) vs versioned dylib filenames
ln -sf libSDL3.0.dylib "$LIB/libSDL3.dylib"
ln -sf libFAudio.0.dylib "$LIB/libFAudio.dylib"
ln -sf libFNA3D.0.dylib "$LIB/libFNA3D.dylib"

echo "Installed fnalibs to $LIB"
