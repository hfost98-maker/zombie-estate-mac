# Carnival asset slots

Drop optional **16×16 PNG** files here to override hand-painted tiles.
Name each file after its slot key (e.g. `fair_grass.png`, `gravel.png`).

Run:

```bash
python3 scripts/build-carnival-assets.py
python3 scripts/generate-carnival.py
```

## Format

- **Atlas:** `MasterWallTexture.xnb` — 512×512 RGBA, 16×16 cells (32×32 grid)
- **Sprites:** `MasterGrid.xnb` — same size; clown zombies at cells (24–27, row 4)
- **Map tiles:** reference atlas coords in lines 21–22 (floor) and 13–20 (walls)

## Free external tilesets (manual download)

These are **not** XNB format — export 16×16 PNGs and drop them here:

| Source | License | URL |
|--------|---------|-----|
| Puny World 16×16 | CC0 | https://merchant-shade.itch.io/16x16-puny-world |
| Minifantasy Medieval Carnival | Commercial (~$5) | https://krishna-palacio.itch.io/minifantasy-medieval-carnival |
| Colored 16×16 Fantasy (Jerom/Eiyeron) | CC-BY-SA 3.0 | https://opengameart.org/content/colored-16x16-fantasy-tileset |

### Bulk import from a spritesheet

1. Place a 16×16 grid PNG in this folder (e.g. `oga_colored_16x16.png`).
2. Edit `sheet_map.json` — map slot names to `[col, row]` in the sheet.
3. Run:

```bash
python3 scripts/import-external-tileset.py tools/carnival-assets/external/oga_colored_16x16.png --cols 10
python3 scripts/build-carnival-assets.py
```

A bundled OGA sheet is included for grass/path/fence overrides (CC-BY-SA — credit Jerom & Eiyeron if shipped).

The build script only imports PNGs you place in this folder; it never downloads automatically.

## Painted slots (rows 28–31)

See `scripts/carnival_pixel_art.py` → `WALL_SLOTS` for the full registry.
