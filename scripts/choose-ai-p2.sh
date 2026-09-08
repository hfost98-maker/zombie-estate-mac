#!/bin/bash
# Resolve ZOMBIE_ESTATE_AI_P2 / ZOMBIE_ESTATE_KEYBOARD_P2 from env, saved prefs, or a launch dialog.
# Source this before run-mac.sh (desktop app sets ZOMBIE_ESTATE_GUI_LAUNCH=1).

choose_ai_p2() {
  local root="$1"
  local prefs="$root/game/zombie-estate.prefs"

  if [ -n "${ZOMBIE_ESTATE_KEYBOARD_P2:-}" ]; then
    export ZOMBIE_ESTATE_AI_P2=0
    export ZOMBIE_ESTATE_KEYBOARD_P2=1
    return 0
  fi

  if [ -n "${ZOMBIE_ESTATE_AI_P2:-}" ]; then
    if [ "${ZOMBIE_ESTATE_AI_P2}" = "1" ]; then
      export ZOMBIE_ESTATE_KEYBOARD_P2=0
    else
      export ZOMBIE_ESTATE_KEYBOARD_P2=1
    fi
    return 0
  fi

  if [ -f "$prefs" ] && [ "${ZOMBIE_ESTATE_ASK_AI:-0}" != "1" ]; then
    # shellcheck disable=SC1090
    . "$prefs"
    if [ "${KEYBOARD_P2:-0}" = "1" ]; then
      export ZOMBIE_ESTATE_KEYBOARD_P2=1
      export ZOMBIE_ESTATE_AI_P2=0
      return 0
    fi
    if [ -n "${AI_P2:-}" ]; then
      export ZOMBIE_ESTATE_AI_P2="$AI_P2"
      if [ "$AI_P2" = "0" ]; then
        export ZOMBIE_ESTATE_KEYBOARD_P2=1
      else
        export ZOMBIE_ESTATE_KEYBOARD_P2=0
      fi
      return 0
    fi
  fi

  if [ "${ZOMBIE_ESTATE_GUI_LAUNCH:-0}" != "1" ]; then
    export ZOMBIE_ESTATE_AI_P2="${ZOMBIE_ESTATE_AI_P2:-0}"
    export ZOMBIE_ESTATE_KEYBOARD_P2="${ZOMBIE_ESTATE_KEYBOARD_P2:-1}"
    return 0
  fi

  local choice
  choice="$(osascript <<'APPLESCRIPT' 2>/dev/null || true
set resp to display dialog "Player 2 for this session:" & return & return & "Co-op — P1 & P2 on controllers; keyboard + mouse join as P3 (or P2 if only one pad)." & return & "With AI Teammate — bot joins as P2." & return & "Solo — one human only." buttons {"Solo", "With AI Teammate", "Co-op"} default button "Co-op" with title "Zombie Estate" giving up after 120
if button returned of resp is "With AI Teammate" then
  return "ai"
end if
if button returned of resp is "Co-op" then
  return "keyboard"
end if
return "solo"
APPLESCRIPT
)"

  local ai_p2=0
  local keyboard_p2=0
  case "$choice" in
    ai) ai_p2=1 ;;
    keyboard) keyboard_p2=1 ;;
    solo) ai_p2=0; keyboard_p2=0 ;;
    *) keyboard_p2=1 ;;
  esac

  export ZOMBIE_ESTATE_AI_P2="$ai_p2"
  export ZOMBIE_ESTATE_KEYBOARD_P2="$keyboard_p2"
  mkdir -p "$(dirname "$prefs")"
  cat > "$prefs" <<EOF
# Zombie Estate launcher preferences (edit or delete to re-prompt)
AI_P2=$ai_p2
KEYBOARD_P2=$keyboard_p2
EOF
}
