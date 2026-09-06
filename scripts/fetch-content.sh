#!/bin/bash
# Download Zombie Estate.rar from Internet Archive and extract Content/.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="/tmp/zombie-estate-download"
RAR="$DEST/Zombie Estate.rar"
URL="https://archive.org/download/XBOX_360_XBLIG_4/Zombie%20Estate.rar"
EXPECTED_MD5="0aaef33c5800ae90b3605711b4e0866e"

mkdir -p "$DEST"

download_ok() {
  [ -f "$1" ] || return 1
  local size
  size=$(stat -f%z "$1" 2>/dev/null || echo 0)
  [ "$size" -ge 40000000 ] || return 1
  if command -v md5 >/dev/null 2>&1; then
    local md5
    md5=$(md5 -q "$1")
    [ "$md5" = "$EXPECTED_MD5" ] && return 0
  fi
  # Size check is enough if md5 unavailable
  return 0
}

if download_ok "$RAR"; then
  echo "Using existing $RAR"
elif download_ok "$HOME/Downloads/Zombie Estate.rar"; then
  RAR="$HOME/Downloads/Zombie Estate.rar"
  echo "Using $RAR"
else
  echo "Internet Archive requires a free login for XBOX_360_XBLIG_4 (private collection)."
  echo ""
  echo "1. Sign up / log in: https://archive.org/account/login"
  echo "2. Download (~41 MB): $URL"
  echo "   (Save as: ~/Downloads/Zombie Estate.rar)"
  echo "3. Re-run: $0"
  echo ""
  echo "Or configure the IA CLI once, then re-run:"
  echo "  python3 -m internetarchive configure"
  echo "  python3 -m internetarchive download XBOX_360_XBLIG_4 --glob='Zombie Estate.rar' --destdir='$DEST'"
  echo ""
  open "https://archive.org/download/XBOX_360_XBLIG_4/Zombie%20Estate.rar" 2>/dev/null || true
  exit 1
fi

# Ensure unrar exists (download rarlab ARM binary if missing)
UNRAR="/tmp/rarlab/rar/unrar"
if [ ! -x "$UNRAR" ]; then
  echo "Fetching unrar..."
  mkdir -p /tmp/rarlab
  curl -L -o /tmp/rarlab/rarmacos-arm.tar.gz "https://www.rarlab.com/rar/rarmacos-arm-701.tar.gz"
  tar xzf /tmp/rarlab/rarmacos-arm.tar.gz -C /tmp/rarlab
fi

"$ROOT/scripts/extract-content.sh" "$RAR"
echo ""
echo "Done. Launch with: cd '$ROOT' && ./run-mac.sh"
