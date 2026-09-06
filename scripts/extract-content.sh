#!/bin/bash
# Extract Content/*.xnb from a full XBLIG Zombie Estate package.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORKDIR="${TMPDIR:-/tmp}/zombie-estate-extract-$$"
RAR="${1:-${ZOMBIE_ESTATE_RAR:-}}"

UNRAR="${UNRAR:-/tmp/rarlab/rar/unrar}"
STFS_EXTRACT="${STFS_EXTRACT:-/tmp/360tools/tools/extract_stfs.py}"
GAME_CONTENT="$ROOT/game/Content"

if [ -z "$RAR" ]; then
  for candidate in \
    "$HOME/Downloads/Zombie Estate.rar" \
    "/tmp/zombie-estate-download/Zombie Estate.rar"; do
    if [ -f "$candidate" ]; then
      RAR="$candidate"
      break
    fi
  done
fi

if [ -z "$RAR" ] || [ ! -f "$RAR" ]; then
  echo "Missing RAR. Download from (requires free archive.org login):"
  echo "  https://archive.org/download/XBOX_360_XBLIG_4/Zombie%20Estate.rar"
  echo ""
  echo "Then run:"
  echo "  $0 \"\$HOME/Downloads/Zombie Estate.rar\""
  exit 1
fi

if [ ! -x "$UNRAR" ]; then
  echo "unrar not found at $UNRAR"
  echo "Install from https://www.rarlab.com/rar_add.htm or set UNRAR=..."
  exit 1
fi

mkdir -p "$WORKDIR"
echo "Extracting RAR: $RAR"
"$UNRAR" x -o+ "$RAR" "$WORKDIR/"

PKG="$(find "$WORKDIR" -maxdepth 3 \( -iname '*.live' -o -iname '*.con' -o -iname '*.pirs' \) | head -1)"
if [ -z "$PKG" ]; then
  # Some repacks ship loose Content/ or STFS without extension.
  if [ -d "$WORKDIR/Content" ]; then
    echo "Found loose Content/ in archive"
    rm -rf "$GAME_CONTENT"
    cp -R "$WORKDIR/Content" "$GAME_CONTENT"
  else
    echo "No STFS package (.live/.con/.pirs) or Content/ found in:"
    find "$WORKDIR" -type f | head -20
    exit 1
  fi
else
  echo "Extracting STFS: $PKG"
  STFS_OUT="$WORKDIR/stfs"
  mkdir -p "$STFS_OUT"
  python3 "$STFS_EXTRACT" "$PKG" "$STFS_OUT"

  CONTENT_SRC="$(find "$STFS_OUT" -type d -name Content | head -1)"
  if [ -z "$CONTENT_SRC" ]; then
    echo "No Content/ directory inside STFS package"
    find "$STFS_OUT" | head -30
    exit 1
  fi

  echo "Installing $CONTENT_SRC -> $GAME_CONTENT"
  rm -rf "$GAME_CONTENT"
  cp -R "$CONTENT_SRC" "$GAME_CONTENT"
fi

XNB_COUNT="$(find "$GAME_CONTENT" -name '*.xnb' | wc -l | tr -d ' ')"
echo "Installed $XNB_COUNT .xnb files under game/Content/"
find "$GAME_CONTENT" -name '*.xnb' | head -10
rm -rf "$WORKDIR"
