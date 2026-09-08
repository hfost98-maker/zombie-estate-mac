#!/usr/bin/env bash
# Build reference atlas + carnival map, then launch with Carnival as default.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "Baking reference screenshot into atlas..."
python3 scripts/build-reference-atlas.py
CARNIVAL_REPAINT_WALL=1 python3 scripts/build-carnival-assets.py

echo "Carnival music..."
if [ -f "$ROOT/game/CarnivalMusic/carnival_track_1.wav" ] && [ -f "$ROOT/game/CarnivalMusic/carnival_track_2.wav" ]; then
  echo "  Using rotating playlist (track 1 + track 2)."
elif [ -f "$ROOT/game/CarnivalMusic/carnival_main.wav" ] || [ -f "$ROOT/game/CarnivalMusic/carnival_track_1.wav" ]; then
  echo "  Using custom carnival track (single)."
elif compgen -G "$ROOT/game/CarnivalMusic/source/"* >/dev/null 2>&1; then
  python3 scripts/import-carnival-music.py --slot 1
  python3 scripts/import-carnival-music.py --slot 2
else
  echo "  No custom tracks — generating procedural fallback."
  echo "  Tip: import Magnifying Glass + Gargoyle from Epidemic Sound:"
  echo '    python3 scripts/import-carnival-music.py --slot 1 ~/Downloads/"Magnifying Glass.mp3"'
  echo '    python3 scripts/import-carnival-music.py --slot 2 ~/Downloads/"Gargoyle.mp3"'
  python3 scripts/generate-carnival-music.py
fi
python3 scripts/generate-carnival-sky.py

echo "Generating Carnival map from reference layout..."
python3 scripts/generate-carnival.py

echo "Validating Carnival map + atlas..."
python3 scripts/validate-carnival.py

export ZOMBIE_ESTATE_DEFAULT_MAP="Carnival"
exec "$ROOT/run-mac.sh" "$@"
