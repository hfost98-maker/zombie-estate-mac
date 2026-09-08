#!/bin/bash
# Install/update the Desktop Zombie Estate.app launcher from this repo.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="$HOME/Desktop/Zombie Estate.app"
TEMPLATE="$ROOT/Zombie Estate.app"

if [ ! -d "$TEMPLATE" ]; then
  echo "Missing template app: $TEMPLATE"
  exit 1
fi

mkdir -p "$DEST/Contents/MacOS" "$DEST/Contents/Resources"

cp "$TEMPLATE/Contents/Info.plist" "$DEST/Contents/Info.plist"
if [ -f "$TEMPLATE/Contents/Resources/AppIcon.icns" ]; then
  cp "$TEMPLATE/Contents/Resources/AppIcon.icns" "$DEST/Contents/Resources/AppIcon.icns"
fi

cat > "$DEST/Contents/MacOS/launch" <<'LAUNCH'
#!/bin/bash
GAME_ROOT="$HOME/Projects/zombie-estate-mac"
MONO="/Library/Frameworks/Mono.framework/Versions/Current/bin/mono"

alert() {
  osascript -e "display alert \"Zombie Estate\" message \"$1\" as critical" 2>/dev/null || echo "$1"
}

if [ ! -d "$GAME_ROOT" ]; then
  alert "Game folder not found at $GAME_ROOT"
  exit 1
fi

if [ ! -x "$MONO" ]; then
  alert "Mono is not installed. Install from https://www.mono-project.com/download/"
  exit 1
fi

if [ ! -f "$GAME_ROOT/lib/libSDL3.0.dylib" ]; then
  alert "Native libraries missing. Open Terminal and run:\ncd $GAME_ROOT && ./scripts/setup-fnlibs.sh"
  exit 1
fi

cd "$GAME_ROOT" || exit 1
export ZOMBIE_ESTATE_GUI_LAUNCH=1
# Hold Option while launching to pick AI on/off again (ignores saved pref).
if /usr/bin/python3 - <<'PY' 2>/dev/null
import Quartz
flags = Quartz.CGEventSourceFlagsState(Quartz.kCGEventSourceStateCombinedSessionState)
raise SystemExit(0 if (flags & Quartz.kCGEventFlagMaskAlternate) else 1)
PY
then
  export ZOMBIE_ESTATE_ASK_AI=1
fi
# shellcheck disable=SC1091
source "$GAME_ROOT/scripts/choose-ai-p2.sh"
choose_ai_p2 "$GAME_ROOT"
exec ./run-mac.sh
LAUNCH

chmod +x "$DEST/Contents/MacOS/launch"
echo "Installed $DEST"
echo "AI preference file: $ROOT/game/zombie-estate.prefs"
echo "Hold Option while launching to change AI on/off."
