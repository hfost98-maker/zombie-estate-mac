# Map Editor

Three-tab editor for building custom Zombie Estate maps.

Requires `game/Content/MasterGrid.xnb` (from the recovery launcher). The open script extracts PNG atlases automatically when present.

## Launch

```bash
./scripts/open-map-editor.sh
```

Open [http://127.0.0.1:8765/tools/map-editor/index.html](http://127.0.0.1:8765/tools/map-editor/index.html)

## Tabs

### Tile Assets
Browse **323 unique visual blocks** from Manor, Hedge Maze, Open Field, and TestMap NEW. Click a tile to select it as your paint brush.

### Map Builder
- **Load** a full existing map (Manor, Hedge Maze, Open Field, TestMap NEW, or blank grass)
- **Birds-eye 32×32 view** of the entire layout — buildings, roads, fences, all visible
- **Paint / Fill / Pick / Erase** with your selected brush from the Assets tab
- **Export map pack** — downloads layout, spawns, and path files

### 3D Preview (game-accurate)
- Renders with **MasterWallTexture** — the single atlas the game uses when drawing the map (`GameWorld.DRAW` → `Global.TESTWALL`)
- **Floor** art: tile lines **21–22** (`floorTexCoord` in the game)
- **Wall** faces: tile lines **13–20** (per-edge wall texture coords)
- **Not** MasterGrid (UI, player icons, tombstones) and **not** `CementTest2` / `TESTFLOOR` (loaded at startup but unused in the compiled game)
- Same UV math as the game: `coord × 16 / 512`
- **Validate:** load Manor, open this tab, compare to in-game Manor

Extract textures manually if needed:

```bash
python3 scripts/extract-xnb-texture.py
```

## Workflow

1. **Tile Assets** tab → pick the brush you want (hedge, path, shop tile, etc.)
2. **Map Builder** tab → load Manor (or another map) as a starting point
3. Edit the birds-eye layout — move roads, copy building regions, replace tiles
4. **3D Preview** tab → confirm the layout matches what you expect in-game (try Manor first)
5. Enter a map name and click **Export map pack**
5. Copy files into `game/` and register in `scripts/GameMods.cs`, then repatch

Regenerate block library after editing source maps:

```bash
python3 scripts/build-map-template-library.py
```
