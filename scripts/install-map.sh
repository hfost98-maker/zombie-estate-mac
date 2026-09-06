#!/bin/bash
# Install a map pack into the active Manor.* slots the game loads.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GAME="$ROOT/game"
BACKUP="$GAME/maps-backup"

usage() {
  cat <<EOF
Usage: $0 <manor|testmap|backup|restore>

  manor    Install default Manor map (default gameplay)
  testmap  Install TestMap_NEW layout + spawns + path
  backup   Save current Manor.* files to maps-backup/
  restore  Restore Manor.* from maps-backup/

See docs/MAP-EDITING.md for editing map .txt files.
EOF
}

backup_maps() {
  mkdir -p "$BACKUP"
  for f in Manor.txt ManorSpawns.txt PathMap_Manor.txt; do
    [ -f "$GAME/$f" ] && cp "$GAME/$f" "$BACKUP/$f"
  done
  echo "Backed up to $BACKUP/"
}

install_manor() {
  backup_maps
  # Manor files are already the default names; ensure TestMap isn't partially mixed
  if [ -f "$GAME/Manor.txt" ] && [ -f "$GAME/ManorSpawns.txt" ] && [ -f "$GAME/PathMap_Manor.txt" ]; then
    echo "Manor map already active (Manor.txt present)."
    echo "To restore from payload originals, re-run: scripts/extract-recovery-payload.sh"
  fi
  echo "Active map: Manor"
}

install_testmap() {
  backup_maps
  for pair in "TestMap_NEW.txt:Manor.txt" "TestMapSpawns_NEW.txt:ManorSpawns.txt" "PathMap_New.txt:PathMap_Manor.txt"; do
    src="${pair%%:*}"
    dst="${pair##*:}"
    if [ ! -f "$GAME/$src" ]; then
      echo "Missing $GAME/$src"
      exit 1
    fi
    cp "$GAME/$src" "$GAME/$dst"
    echo "Installed $src -> $dst"
  done
  echo "Active map: TestMap_NEW (via Manor.* slots)"
}

restore_maps() {
  if [ ! -d "$BACKUP" ]; then
    echo "No backup at $BACKUP — run: $0 backup"
    exit 1
  fi
  for f in Manor.txt ManorSpawns.txt PathMap_Manor.txt; do
    [ -f "$BACKUP/$f" ] && cp "$BACKUP/$f" "$GAME/$f" && echo "Restored $f"
  done
}

cmd="${1:-}"
case "$cmd" in
  manor) install_manor ;;
  testmap) install_testmap ;;
  backup) backup_maps ;;
  restore) restore_maps ;;
  *) usage; exit 1 ;;
esac
