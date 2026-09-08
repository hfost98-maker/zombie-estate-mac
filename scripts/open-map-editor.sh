#!/usr/bin/env bash
# Launch the visual map editor in a local browser.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

python3 scripts/build-map-template-library.py
python3 scripts/extract-xnb-texture.py 2>/dev/null || true

PORT="${MAP_EDITOR_PORT:-8765}"
echo "Map editor: http://127.0.0.1:${PORT}/tools/map-editor/index.html"
echo "Press Ctrl+C to stop."

if command -v open >/dev/null 2>&1; then
  (sleep 0.5 && open "http://127.0.0.1:${PORT}/tools/map-editor/index.html") &
fi

python3 -m http.server "$PORT"
