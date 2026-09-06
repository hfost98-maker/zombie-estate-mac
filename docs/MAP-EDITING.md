# Map editing guide

Zombie Estate loads **one level at a time**, hardcoded in the executable as:

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

## Smaller legacy maps (112×128 cells — different size)

`TestMap.txt` and `OLDLEVELS/House.txt` use a **different grid size** and cannot be used as drop-in replacements without modifying the compiled game (level dimensions are fixed at 32×32 in the `.exe`).

## Adding a brand-new map name

The `.exe` only references `Manor.txt` / `PathMap_Manor.txt` / `ManorSpawns.txt`. To use a custom filename you would need to patch the executable strings (Mono.Cecil) — not supported by scripts here. **Workaround:** overwrite the `Manor.*` files with your custom map data.

## After editing

```bash
./run-mac.sh
```

Watch the terminal for `Building Level Initiated...` and `Loading Spawns Complete`. If zombies behave oddly, restore from backup or run `./scripts/install-map.sh manor`.
