# Zombie Estate - Windows recovery

Run `Play Zombie Estate.cmd` for the verified native-display fullscreen mode.
Run `Play Zombie Estate Windowed.cmd` for a window.

The folder is self-contained. Keep the executable, DLLs, `Content`,
`OLDLEVELS`, map files, and `gamecontrollerdb.txt` together.

## Input

- Controllers: native SDL gamepad support with the included community mapping
  database. The database includes Xbox 360/One/Series, PlayStation 3/4/5, and
  Nintendo Switch mappings.
- Keyboard: the original keyboard handling remains available, including WASD
  movement and the game's normal menu/pause keys.
- Mouse: the recovery adds stable viewport-relative aiming; left click follows
  the original fire path.

The controller database broadens recognition, but an individual adapter, very
old controller, or vendor driver can still require its normal Windows, Steam
Input, or DS4Windows compatibility layer.

## Display behavior

The fullscreen launcher uses the current desktop's native display mode instead
of forcing a possibly unsupported monitor mode. The verified machine used a
1280x720 logical desktop at 150% DPI on a 1920x1080 physical display. The game
reported a matching 1280x720 fullscreen backbuffer and occupied the complete
1920x1080 physical output without visible window chrome.

Windowed rendering was also verified at 1024x768, 1280x720, and 1600x900.

## Notes

An Xbox-only effect emits one harmless compatibility warning while loading.
The recovered game bypasses that effect with its verified desktop CPU
instancing renderer; it is not a runtime crash or a missing visual.
