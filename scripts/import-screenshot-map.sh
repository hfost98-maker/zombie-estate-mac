#!/usr/bin/env bash
# Import a reference screenshot into game-ready map assets.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

IMAGE="${1:-}"
NAME="${2:-ScreenshotMap}"
CORNERS="${3:-}"

if [ -z "$IMAGE" ] || [ ! -f "$IMAGE" ]; then
  echo "Usage: $0 IMAGE [MapName] [corners.json]"
  echo ""
  echo "Align corners in the map editor first:"
  echo "  ./scripts/open-map-editor.sh  →  Screenshot Import tab"
  echo ""
  echo "Then run:"
  echo "  $0 ~/Desktop/reference.jpg ReferenceMap ~/Downloads/last-corners.json"
  exit 1
fi

ARGS=(python3 scripts/screenshot_to_assets.py "$IMAGE" --name "$NAME" --pack-atlas)
if [ -n "$CORNERS" ] && [ -f "$CORNERS" ]; then
  ARGS+=(--corners "$CORNERS")
fi

"${ARGS[@]}"

echo ""
echo "Next steps:"
echo "  1. Open map editor → Screenshot Import → load tools/screenshot-import/${NAME}.map.json"
echo "  2. Validate in 3D Preview, touch up in Map Builder"
echo "  3. Add to scripts/GameMods.cs if needed, then ./run-mac.sh"
