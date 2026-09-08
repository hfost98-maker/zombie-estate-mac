# Parsec co-op on Mac host (Remote Player 2)

Parsec on **macOS cannot inject guest gamepads** into the host. This port adds **Remote P2** mode: the guest maps their Xbox controller to keyboard keys on their PC; Parsec forwards those keystrokes to your Mac; the game reads them as **Player 2**.

## Quick start

### Host (Mac)

1. Plug **your** Xbox controller (Player 1). Optionally plug a **second** controller only to join P2 at character select — you can also join P2 via the guest's **Enter** key once Remote P2 is enabled.
2. Launch with Remote P2:

```bash
ZOMBIE_ESTATE_REMOTE_P2=1 ZOMBIE_ESTATE_AI_P2=0 ./run-mac.sh
```

3. Start Parsec hosting and invite your friend.
4. In Parsec, click the guest's profile → grant **Keyboard** (and **Mouse** if needed) permission.
5. Go to **character select**. Guest presses **Start** on their controller (mapped to **Enter** on your Mac) to join as P2.

### Guest (PC)

1. Install [AntiMicroX](https://github.com/AntiMicroX/antimicrox) (or similar).
2. Load the mapping below (or map manually to the same keys).
3. Join Parsec **before** character select.
4. Enable the profile so your Xbox controller sends keys into Parsec.

## Key bindings (guest controller → Mac keyboard)

Map your Xbox pad to these keys on the **guest** machine:

| Game action | Key on Mac (guest sends) |
|-------------|--------------------------|
| Move | **W A S D** |
| Aim | **I J K L** |
| Fire (RT) | **Left Shift** |
| A | **E** |
| B | **Q** |
| Start (join / menus) | **Enter** |
| Reload / X | **R** |
| Change weapon | **F** |
| D-pad up/down/left/right | **1 2 3 4** |

**Do not** type on the guest keyboard while playing — those keys go to the host.

## Parsec settings

- **Host:** macOS hosting enabled; game window focused when guest plays.
- **Guest:** Keyboard permission required (gamepad permission alone does nothing on Mac hosts).
- **Host:** Use a controller for P1 so guest keys do not conflict with your keyboard.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Guest buttons do nothing | Grant **Keyboard** permission in Parsec; relaunch game with `ZOMBIE_ESTATE_REMOTE_P2=1` |
| P2 never joins | At character select, guest presses **Start** (mapped to **Enter**) |
| Wrong player moves | Host uses controller for P1 only; unplug extra controllers on Mac |
| AI plays P2 | Launch with `ZOMBIE_ESTATE_AI_P2=0` |
| Second Mac controller moves P2 | Leave the spare controller unplugged after join, or do not touch it — Remote P2 overrides pad 2 input in software |

## Why not native Parsec gamepads?

Parsec virtual USB gamepads are **Windows-host only**. Mac hosts can stream video and forward keyboard/mouse, not guest controllers. Remote P2 is the supported workaround for Mac-hosted sessions.
