# Optional ZE2-style map packs

Place **32×32** ZE1-format map triplets here to unlock extra entries on character select (D-pad left/right).

Each map needs all three files:

- `YourMap.txt` — tile layout (22528 lines)
- `YourMapSpawns.txt` — spawn grid (1024 lines)
- `PathMap_YourMap.txt` — pathfinding data

Built-in placeholder names (see `scripts/GameMods.cs`):

- `Graveyard.txt`, `GraveyardSpawns.txt`, `PathMap_Graveyard.txt`
- `Church.txt`, `ChurchSpawns.txt`, `PathMap_Church.txt`
- `Mall.txt`, `MallSpawns.txt`, `PathMap_Mall.txt`

**Zombie Estate 2 (Steam)** uses a different engine; you cannot copy its level files directly. To play ZE2 layouts in this port you must convert or recreate them in ZE1 `.txt` format.

See `docs/MAP-EDITING.md` for format details.
