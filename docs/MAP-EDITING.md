# Map editing guide

Zombie Estate loads **one level at a time**. With the AI/map patch (`scripts/patch-ai-player2.cs`), you can pick the active map on **character select** using D-pad left/right (label shown at top of screen). The level is built when you press Start to begin the run—not when the game first launches.

Without the patch, only the hardcoded `Manor.*` filenames are used (see **Adding a brand-new map name** below).

| File | Purpose | Size |
|------|---------|------|
| `Manor.txt` | Tile layout (walls, floors, types) | **22,528 lines** (32×32 tiles × 22 lines/tile) |
| `ManorSpawns.txt` | Zombie spawn regions | **1,024 values** (32×32 grid) |
| `PathMap_Manor.txt` | Precomputed pathfinding | **1,048,576 values** (1024×1024) |

The game does **not** read `TestMap_NEW.txt` or `OLDLEVELS/` unless you copy/rename them over the `Manor.*` files.

## What you can change without touching the .exe

1. **Edit the current map** — modify `game/Manor.txt` (and matching spawn/path files)
2. **Swap in an included alternate map** — e.g. replace `Manor.txt` with `TestMap_NEW.txt` content
3. **Restore originals** — keep backups before experimenting

```bash
./scripts/install-map.sh testmap    # play TestMap_NEW layout
./scripts/install-map.sh manor      # restore default Manor
./scripts/install-map.sh backup     # save current maps to game/maps-backup/
```

## Map format: `Manor.txt`

- World grid: **32 columns × 32 rows** of tiles
- Each tile = **22 lines** (22528 ÷ 1024)
- First 4 lines per tile: wall flags (`0` = open, `1` = wall) for right, top, left, bottom edges
- Remaining lines: tile type / texture IDs (common values below)

| Value | Typical meaning |
|-------|-----------------|
| `0` | Empty / floor |
| `1`–`14` | Floor/wall/door variants |
| `31` | Solid / outer boundary |

**Important:** If you change walls or layout, zombies may walk through walls unless `PathMap_Manor.txt` matches. Safest approach when swapping whole maps: replace all three files together (layout + spawns + path).

## Spawn file: `ManorSpawns.txt`

- 1024 lines, one integer per cell (−1 = no spawn, 0–4 = spawn region id)

## Path file: `PathMap_Manor.txt`

- 1024×1024 precomputed paths — **do not edit by hand**
- When swapping maps, copy the matching `PathMap_*.txt` from the same map pack

## Included alternate maps (same 32×32 size as Manor)

| Map | Layout | Spawns | Path |
|-----|--------|--------|------|
| **Manor** (default) | `Manor.txt` | `ManorSpawns.txt` | `PathMap_Manor.txt` |
| **TestMap NEW** | `TestMap_NEW.txt` | `TestMapSpawns_NEW.txt` | `PathMap_New.txt` |
| **Hedge Maze** (generated) | `HedgeMaze.txt` | `HedgeMazeSpawns.txt` | `PathMap_HedgeMaze.txt` |

Regenerate Hedge Maze: `python3 scripts/generate-hedge-maze.py`

## Visual map builder dashboard

Use the browser-based editor to paint maps with Manor-derived stamps and export game-ready files:

```bash
./scripts/open-map-editor.sh
```

See `tools/map-editor/README.md` for workflow. Exported triplets can be added to `scripts/GameMods.cs` map rotation (same as other custom maps).

## Zombie Estate 2 maps (Steam — not bundled)

ZE2 uses a **different engine**; its assets are not drop-in compatible with ZE1. The Mac port registers optional ZE2-style slots that appear in the map rotation **only when all three files exist** under `game/Maps/`:

| Label | Layout | Spawns | Path |
|-------|--------|--------|------|
| Graveyard (ZE2) | `Maps/Graveyard.txt` | `Maps/GraveyardSpawns.txt` | `Maps/PathMap_Graveyard.txt` |
| Church (ZE2) | `Maps/Church.txt` | `Maps/ChurchSpawns.txt` | `Maps/PathMap_Church.txt` |
| Mall (ZE2) | `Maps/Mall.txt` | `Maps/MallSpawns.txt` | `Maps/PathMap_Mall.txt` |

Each pack must be **32×32 tiles** with the same `.txt` formats as Manor (22528-line layout file). There is no official converter from ZE2 Steam files today—you would need to recreate or port layouts manually, or use community tools if available. Place converted triplets in `game/Maps/` and restart the game.

See also `game/Maps/README.md`.

## Smaller legacy maps (112×128 cells — different size)

`TestMap.txt` and `OLDLEVELS/House.txt` use a **different grid size** and cannot be used as drop-in replacements without modifying the compiled game (level dimensions are fixed at 32×32 in the `.exe`). They are **not** in the in-game map rotation.

## Adding a brand-new map name

With the map-selection patch, add entries in `scripts/GameMods.cs` (`EnsureInitialized`) and re-run the patch (see README **Rebuilding tools**). Without the patch, the `.exe` only references `Manor.txt` / `PathMap_Manor.txt` / `ManorSpawns.txt`. **Workaround:** overwrite the `Manor.*` files with your custom map data (`./scripts/install-map.sh testmap`).

## After editing

```bash
./run-mac.sh
```

Watch the terminal for `Building Level Initiated...` and `Loading Spawns Complete`. If zombies behave oddly, restore from backup or run `./scripts/install-map.sh manor`.
