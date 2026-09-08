# Co-op: controllers + keyboard player

In co-op mode (AI teammate off):

| Slot | Input |
|------|--------|
| **Player 1** | Controller 1 only |
| **Player 2** | Controller 2 (when plugged in) |
| **Player 3** | Keyboard + mouse (when controller 2 is connected) |
| **Player 2 fallback** | Keyboard + mouse if no second controller |

Keyboard and mouse never control Player 1. When a second controller is connected, they control **Player 3 only** — not Player 2.

## Two controllers (2 players)

This is the simplest setup: plug in two Xbox controllers and launch via `./run-mac.sh`.

1. **Controller 1** — Player 1 (pick character with **A**).
2. **Controller 2** — Player 2 (**Start** on pad 2 to join at character select, then pick with **A**).

Both players get their own window (main + `Zombie Estate - Player 2`). Shop sync, ready-up, and wave start all treat each logged-in human independently — P1 closing shop does not end P2's shopping. Each player readies with **Back** on their own controller when done.

**Important:** Always launch via `./run-mac.sh` or the desktop app so co-op input mode is enabled (`ZOMBIE_ESTATE_KEYBOARD_P2=1`). The launcher sets `XBLIG_KEYBOARD_GAMEPAD_DISABLE=1`, and the input patch stops FNA from mirroring WASD/mouse onto Player 1's controller slot — keyboard never moves or aims P1 in co-op mode.

## Multi-window co-op

Co-op opens **one window per logged-in human player** after character select / game start (on by default via `./run-mac.sh`):

```bash
# Disable extra windows:
ZOMBIE_ESTATE_DUAL_SCREEN=0 ./run-mac.sh
```

| Window | Camera / input |
|--------|----------------|
| Main (`Zombie Estate`) | Follows **Player 1** — P1 HUD + P1 shop UI |
| `Zombie Estate - Player 2` | Follows **Player 2** (if human, not AI) — P2 HUD + P2 shop UI |
| `Zombie Estate - Player 3` | Follows **Player 3** (if human) — P3 HUD + P3 shop UI |
| `Zombie Estate - Player 4` | Follows **Player 4** (if human) — P4 HUD + P4 shop UI |

AI teammates do **not** get their own window. All windows use the same OpenGL render path (matching colors).

**Fullscreen:** You can toggle fullscreen on the main window (Alt+Enter / in-game option). The P2+ extra windows keep updating — the game recreates the shared render target when the main display size changes and presents P2 frames with `SDL_GL_SwapWindow` instead of the main swap chain.

**Input:** Each player's controller only controls that player. Keyboard/mouse only register when that player's window is focused (or the cursor is over it). P2 gamepad input never affects P1.

**Death / spectate:** If your player dies during a wave but teammates are still alive, your window follows another living player (mirrors their camera and HUD). By default it picks the first other logged-in human still alive. To always spectate a specific player, set `SPECTATE_PLAYER=1` through `4` in `game/zombie-estate.prefs` or `ZOMBIE_ESTATE_SPECTATE_PLAYER=2` in the environment (`auto` = default).

**Team wipe / continue:** When everyone is dead at the end of a round, the **Continue?** / **Game Over** screen appears on **every** player window, not just the main one. Each player can answer on their own controller.

**Shop:** When any human player opens a shop or inventory, the same shop UI appears on every logged-in human player's window (works for two controllers, controller + keyboard, etc.). Each player closes shop on their own when done (**B** on controller, **H** on keyboard); closing on one player's side does not end shopping for the others. Press **Back** when ready for the next wave — all players must ready before the wave starts.

## Launch

```bash
ZOMBIE_ESTATE_KEYBOARD_P2=1 ZOMBIE_ESTATE_AI_P2=0 ./run-mac.sh
```

Or in `game/zombie-estate.prefs`:

```
AI_P2=0
KEYBOARD_P2=1
```

## Join at character select

**Two controllers only (2 players):** see [Two controllers (2 players)](#two-controllers-2-players) above.

**Two controllers + keyboard friend (3 players):**

1. Controller 1 — Player 1 (pick character with **A**).
2. Controller 2 — Player 2 (**Start** on pad 2 to join).
3. Keyboard — Player 3 (**M** or **Enter** to join).

**One controller + keyboard friend (2 players):**

1. Controller — Player 1.
2. Keyboard — Player 2 (**M** or **Enter** to join).

## Controls (keyboard player)

Matches the original PC keyboard layout:

| Action | Key |
|--------|-----|
| Move (game + shop grid) | **W A S D** |
| Aim | **Mouse** |
| Fire | **Left click** |
| A (confirm / buy / select) | **G** |
| B (back / close) | **H** |
| Open inventory | **Q** |
| D-pad (equip slot, menus) | **Arrow keys** |
| Reload / sell hold | **R** (hold in inventory to sell) |
| Start / ready | **M** or **Enter** |
| Ready for next wave | **P** |

## Shop (keyboard player)

- Walk into the store, **G** to buy, **H** to close.
- **Q** opens inventory; arrow keys move the cursor; **G** selects a gun; **R** hold to sell.
