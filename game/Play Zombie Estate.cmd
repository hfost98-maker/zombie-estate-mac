@echo off
setlocal
cd /d "%~dp0"

rem Verified native-display fullscreen configuration.
set "FNA3D_FORCE_DRIVER=D3D11"
set "XNATOFNA_DISPLAY_FULLSCREEN=1"
set "XBLIG_FULLSCREEN=1"
set "XBLIG_BYPASS_APPLYWINDOWCHANGES_HOOK=0"

rem The game's own keyboard/mouse input and native SDL controllers remain
rem active. This disables only the legacy keyboard-as-gamepad fallback, which
rem can stall during a fullscreen mode transition on some Windows systems.
set "XBLIG_KEYBOARD_GAMEPAD_DISABLE=1"

set "XNATOFNA_DISPLAY_SIZE="
set "XNATOFNA_DISPLAY_WIDTH="
set "XNATOFNA_DISPLAY_HEIGHT="
set "XBLIG_DISPLAY_SIZE="
set "XBLIG_DISPLAY_WIDTH="
set "XBLIG_DISPLAY_HEIGHT="

"%~dp0ZombieEstate.exe"
endlocal
