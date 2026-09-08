#!/bin/bash
# Install the unmodified original game launcher on the Desktop.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="$HOME/Desktop/Zombie Estate (Original).app"
TEMPLATE="$ROOT/Zombie Estate.app"

"$ROOT/scripts/setup-original-game.sh"

if [ ! -d "$TEMPLATE" ]; then
  echo "Missing template app: $TEMPLATE"
  exit 1
fi

mkdir -p "$DEST/Contents/MacOS" "$DEST/Contents/Resources"

cat > "$DEST/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleExecutable</key>
	<string>launch</string>
	<key>CFBundleIdentifier</key>
	<string>com.zombieestate.mac.original</string>
	<key>CFBundleName</key>
	<string>Zombie Estate (Original)</string>
	<key>CFBundlePackageType</key>
	<string>APPL</string>
	<key>CFBundleShortVersionString</key>
	<string>1.0</string>
	<key>LSMinimumSystemVersion</key>
	<string>11.0</string>
	<key>NSHighResolutionCapable</key>
	<true/>
</dict>
</plist>
PLIST

if [ -f "$TEMPLATE/Contents/Resources/AppIcon.icns" ]; then
  cp "$TEMPLATE/Contents/Resources/AppIcon.icns" "$DEST/Contents/Resources/AppIcon.icns"
fi

cat > "$DEST/Contents/MacOS/launch" <<'LAUNCH'
#!/bin/bash
GAME_ROOT="$HOME/Projects/zombie-estate-mac"
MONO="/Library/Frameworks/Mono.framework/Versions/Current/bin/mono"

alert() {
  osascript -e "display alert \"Zombie Estate (Original)\" message \"$1\" as critical" 2>/dev/null || echo "$1"
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
exec ./run-mac-original.sh
LAUNCH

chmod +x "$DEST/Contents/MacOS/launch"
echo "Installed $DEST"
echo "Uses unmodified FNA port binaries in $ROOT/game/original/"
