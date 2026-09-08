#!/bin/bash
# Run patched Zombie Estate on Mac using system Mono + FNA
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
GAME_DIR="$ROOT/game"
LIB_DIR="$ROOT/lib"
TOOLS_DIR="$ROOT/tools"
MONO="/Library/Frameworks/Mono.framework/Versions/Current/bin/mono"

if [ ! -x "$MONO" ]; then
  echo "Mono not found at $MONO"
  echo "Install Mono 6.x: https://www.mono-project.com/download/"
  exit 1
fi

# Native FNA libraries (SDL3, FNA3D, FAudio, theorafile)
export DYLD_LIBRARY_PATH="$GAME_DIR:$LIB_DIR${DYLD_LIBRARY_PATH:+:$DYLD_LIBRARY_PATH}"

if [ ! -f "$LIB_DIR/libSDL3.0.dylib" ]; then
  echo "fnalibs missing — run scripts/setup-fnlibs.sh first"
  exit 1
fi

# Windowed by default; set XBLIG_FULLSCREEN=1 for launch-time fullscreen (P2 windows work in both modes).
export XBLIG_FULLSCREEN="${XBLIG_FULLSCREEN:-0}"
export XNATOFNA_DISPLAY_FULLSCREEN="${XNATOFNA_DISPLAY_FULLSCREEN:-0}"
export XBLIG_DISPLAY_WIDTH="${XBLIG_DISPLAY_WIDTH:-1280}"
export XBLIG_DISPLAY_HEIGHT="${XBLIG_DISPLAY_HEIGHT:-720}"

# Unversioned dylib names expected by FNA DllImport
for pair in libSDL3.0.dylib:libSDL3.dylib libFAudio.0.dylib:libFAudio.dylib libFNA3D.0.dylib:libFNA3D.dylib libtheorafile.dylib:libtheorafile.dylib; do
  src="${pair%%:*}"
  dst="${pair##*:}"
  [ -e "$LIB_DIR/$src" ] || continue
  [ -e "$LIB_DIR/$dst" ] || ln -sf "$src" "$LIB_DIR/$dst"
  if [ ! -e "$GAME_DIR/$dst" ]; then
    ln -sf "../lib/$dst" "$GAME_DIR/$dst"
  fi
done

# Mono resolves managed assemblies from MONO_PATH and the exe directory.
export MONO_PATH="$GAME_DIR:$LIB_DIR${MONO_PATH:+:$MONO_PATH}"

# Symlink port libs into game/ only when missing (never replace recovery payload files).
for dll in "$LIB_DIR"/*.dll "$LIB_DIR"/*.dylib "$LIB_DIR"/FNA.dll.config; do
  [ -e "$dll" ] || continue
  base="$(basename "$dll")"
  if [ ! -e "$GAME_DIR/$base" ]; then
    ln -sf "../lib/$base" "$GAME_DIR/$base"
  fi
done

cd "$GAME_DIR"

if [ ! -f "ZombieEstate.exe" ]; then
  echo "ZombieEstate.exe missing in game/"
  exit 1
fi

# FNA recovery build (embedded payload) is pre-converted; skip Xbox timemachine pass.
if [ -f "MANIFEST.json" ] && [ -f "XnaToFna.dll" ]; then
  : # recovery payload from scripts/extract-recovery-payload.sh
elif [ -f "ZombieEstate.exe.xbox.backup" ] && cmp -s "ZombieEstate.exe" "ZombieEstate.exe.xbox.backup" 2>/dev/null; then
  echo "Unconverted Xbox build detected — running scripts/convert-xbox.sh ..."
  "$ROOT/scripts/convert-xbox.sh"
elif [ -f "$TOOLS_DIR/patch-xna.exe" ] && [ ! -f "XnaToFna.dll" ]; then
  if cmp -s "ZombieEstate.exe" "ZombieEstate.exe.xbox.backup" 2>/dev/null; then
    echo "Patching Xbox build -> FNA (basic)..."
    "$MONO" "$TOOLS_DIR/patch-xna.exe" "$GAME_DIR/ZombieEstate.exe"
  fi
fi

# Ignore mouse/keyboard when the game window is not focused (one-time IL patch).
if [ -f "$TOOLS_DIR/patch-input-focus.exe" ] && [ ! -f "$GAME_DIR/.input-focus-patched" ]; then
  echo "Applying input focus patch..."
  MONO_PATH="$MONO_PATH" "$MONO" "$TOOLS_DIR/patch-input-focus.exe" "$ROOT"
  touch "$GAME_DIR/.input-focus-patched"
fi

# AI teammate for couch co-op player 2 (one-time IL patch).
# shellcheck disable=SC1091
source "$ROOT/scripts/choose-ai-p2.sh"
choose_ai_p2 "$ROOT"
# Remote Parsec P2 reads guest keyboard as Player 2; do not run AI teammate at the same time.
if [ "${ZOMBIE_ESTATE_KEYBOARD_P2:-0}" = "1" ] || [ "${ZOMBIE_ESTATE_REMOTE_P2:-0}" = "1" ]; then
  export ZOMBIE_ESTATE_KEYBOARD_P2=1
  export ZOMBIE_ESTATE_AI_P2=0
elif [ "${ZOMBIE_ESTATE_AI_P2:-0}" != "1" ]; then
  export ZOMBIE_ESTATE_KEYBOARD_P2=1
  export ZOMBIE_ESTATE_AI_P2=0
fi
# FNA merges keyboard+mouse into Player 1's gamepad unless disabled (Windows .cmd sets this too).
# Without it, WASD/mouse leak to P1 even when AiTeammate routes keyboard to P2/P3.
export XBLIG_KEYBOARD_GAMEPAD_DISABLE="${XBLIG_KEYBOARD_GAMEPAD_DISABLE:-1}"
# Extra windows: one per logged-in human player (P2–P4), not AI.
# On by default for co-op; set ZOMBIE_ESTATE_DUAL_SCREEN=0 to disable.
export ZOMBIE_ESTATE_DUAL_SCREEN="${ZOMBIE_ESTATE_DUAL_SCREEN:-1}"
# Optional: ZOMBIE_ESTATE_DEFAULT_MAP="Hedge Maze" — or set DEFAULT_MAP in game/zombie-estate.prefs
if [ -z "${ZOMBIE_ESTATE_DEFAULT_MAP:-}" ] && [ -f "$GAME_DIR/zombie-estate.prefs" ]; then
  _map_line="$(grep -E '^DEFAULT_MAP=' "$GAME_DIR/zombie-estate.prefs" 2>/dev/null | tail -1 || true)"
  if [ -n "$_map_line" ]; then
    ZOMBIE_ESTATE_DEFAULT_MAP="${_map_line#DEFAULT_MAP=}"
    ZOMBIE_ESTATE_DEFAULT_MAP="${ZOMBIE_ESTATE_DEFAULT_MAP#\"}"
    ZOMBIE_ESTATE_DEFAULT_MAP="${ZOMBIE_ESTATE_DEFAULT_MAP%\"}"
    export ZOMBIE_ESTATE_DEFAULT_MAP
  fi
  unset _map_line
fi
# Optional: ZOMBIE_ESTATE_SPECTATE_PLAYER=1|2|3|4|auto — which player a dead window follows (default auto).
# Patch always applied (map select + optional AI); runtime AI is off unless ZOMBIE_ESTATE_AI_P2=1.
if [ -f "$TOOLS_DIR/patch-ai-player2.exe" ]; then
  if [ -f "$GAME_DIR/.ai-p2-patched" ] && { [ "$ROOT/scripts/AiTeammate.cs" -nt "$GAME_DIR/.ai-p2-patched" ] || [ "$ROOT/scripts/GameMods.cs" -nt "$GAME_DIR/.ai-p2-patched" ] || [ "$ROOT/scripts/DualScreen.cs" -nt "$GAME_DIR/.ai-p2-patched" ] || [ "$ROOT/scripts/patch-ai-player2.cs" -nt "$GAME_DIR/.ai-p2-patched" ] || [ "$TOOLS_DIR/patch-ai-player2.exe" -nt "$GAME_DIR/.ai-p2-patched" ]; }; then
    echo "Updating AI patch (restoring backup first)..."
    cp "$GAME_DIR/ZombieEstate.exe.pre-ai-p2-patch" "$GAME_DIR/ZombieEstate.exe"
    if [ -f "$GAME_DIR/FNA.dll.pre-coop-input-patch" ]; then
      cp "$GAME_DIR/FNA.dll.pre-coop-input-patch" "$GAME_DIR/FNA.dll"
    fi
    if [ -f "$LIB_DIR/FNA.dll.pre-coop-input-patch" ]; then
      cp "$LIB_DIR/FNA.dll.pre-coop-input-patch" "$LIB_DIR/FNA.dll"
    fi
    if [ -f "$GAME_DIR/FNA.NetStub.dll.pre-focus-patch" ]; then
      cp "$GAME_DIR/FNA.NetStub.dll.pre-focus-patch" "$GAME_DIR/FNA.NetStub.dll"
    fi
    if [ -f "$LIB_DIR/FNA.NetStub.dll.pre-focus-patch" ]; then
      cp "$LIB_DIR/FNA.NetStub.dll.pre-focus-patch" "$LIB_DIR/FNA.NetStub.dll"
    fi
    rm -f "$GAME_DIR/.ai-p2-patched"
  fi
  if [ ! -f "$GAME_DIR/.ai-p2-patched" ]; then
    echo "Applying AI player 2 patch..."
    if ! MONO_PATH="$MONO_PATH" "$MONO" "$TOOLS_DIR/patch-ai-player2.exe" "$ROOT"; then
      echo "Patch failed — restoring game/FNA backups..."
      [ -f "$GAME_DIR/ZombieEstate.exe.pre-ai-p2-patch" ] && cp "$GAME_DIR/ZombieEstate.exe.pre-ai-p2-patch" "$GAME_DIR/ZombieEstate.exe"
      [ -f "$GAME_DIR/FNA.dll.pre-coop-input-patch" ] && cp "$GAME_DIR/FNA.dll.pre-coop-input-patch" "$GAME_DIR/FNA.dll"
      [ -f "$LIB_DIR/FNA.dll.pre-coop-input-patch" ] && cp "$LIB_DIR/FNA.dll.pre-coop-input-patch" "$LIB_DIR/FNA.dll"
      exit 1
    fi
    touch "$GAME_DIR/.ai-p2-patched"
  fi
fi

exec "$MONO" "$GAME_DIR/ZombieEstate.exe" "$@"
