#!/usr/bin/env python3
"""Build visual block library from all 32×32 map layout files."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = ROOT / "game"
OUT = ROOT / "tools" / "map-editor" / "templates.json"

GRID = 32
TILE_LINES = 22

LEVEL_MAPS = [
    ("Manor", "Manor.txt"),
    ("Hedge Maze", "HedgeMaze.txt"),
    ("Open Field", "OpenField.txt"),
    ("TestMap NEW", "TestMap_NEW.txt"),
]

LINE22_LEGEND = {
    "0": {"name": "Overlay only", "color": "#6b7280"},
    "1": {"name": "Grass / lawn", "color": "#4a7c3f"},
    "2": {"name": "Stone path", "color": "#8b8680"},
    "3": {"name": "Dirt / cemetery", "color": "#6b5b4f"},
    "4": {"name": "Wood / fence", "color": "#8b6914"},
    "5": {"name": "Building wall", "color": "#5c4a3a"},
    "6": {"name": "Shop / building", "color": "#7a5c45"},
    "7": {"name": "Interior floor", "color": "#6a5040"},
    "8": {"name": "Shop interior", "color": "#9a8060"},
    "9": {"name": "Special", "color": "#a855f7"},
    "11": {"name": "Building variant", "color": "#6d5847"},
    "12": {"name": "Building variant", "color": "#655040"},
    "13": {"name": "Prop / tomb floor", "color": "#5a6a5a"},
    "14": {"name": "Garden", "color": "#3d7a4a"},
    "31": {"name": "Solid hedge / boundary", "color": "#2f5233"},
}

ID_COLORS = {
    "0": "#1a1a1a",
    "1": "#5a9e4b",
    "2": "#9a9590",
    "3": "#7a6555",
    "4": "#a07828",
    "5": "#6a5040",
    "6": "#8a6048",
    "7": "#705040",
    "8": "#b09068",
    "9": "#a855f7",
    "10": "#4a4038",
    "11": "#6d5847",
    "12": "#655040",
    "13": "#4a5a4a",
    "14": "#3d7a4a",
    "31": "#2f5233",
}


def tile_types(tile: list[str]) -> list[str]:
    return tile[4:22]


def load_map_tiles(path: Path) -> dict[tuple[int, int], list[str]] | None:
    if not path.exists():
        return None
    lines = path.read_text().splitlines()
    expected = GRID * GRID * TILE_LINES
    if len(lines) != expected:
        print(f"Skipping {path.name}: expected {expected} lines, got {len(lines)}")
        return None
    tiles: dict[tuple[int, int], list[str]] = {}
    for y in range(GRID):
        for x in range(GRID):
            base = (y * GRID + x) * TILE_LINES
            tiles[(x, y)] = lines[base : base + TILE_LINES]
    return tiles


def infer_blocked(types: list[str], blocked_signatures: set[tuple[str, ...]]) -> bool:
    key = tuple(types)
    if key in blocked_signatures:
        return True
    line22 = types[17]
    if line22 in ("31", "5"):
        return True
    if line22 == "6" and not (types[16] == "3" and line22 == "6"):
        return True
    return False


def category_for(types: list[str]) -> str:
    line22 = types[17]
    if line22 == "31":
        return "boundaries"
    if line22 in ("4",):
        return "boundaries"
    if line22 in ("3", "13") or (line22 == "0" and any(v != "0" for v in types)):
        return "cemetery"
    if line22 in ("6", "8", "5", "7", "11", "12"):
        return "buildings"
    if line22 == "2":
        return "paths"
    if sum(1 for v in types if v != "0") >= 4 and line22 == "1":
        return "props"
    return "terrain"


def auto_name(types: list[str], primary_source: dict) -> str:
    line22 = types[17]
    legend = LINE22_LEGEND.get(line22, {"name": f"Type {line22}"})
    overlay = sum(1 for v in types if v != "0")
    src = f"{primary_source['map']} ({primary_source['x']},{primary_source['y']})"
    extras: list[str] = []
    if types[16] == "3" and line22 == "6":
        extras.append("store")
    if types[15] == "1" and types[16] == "6":
        extras.append("gate")
    if overlay > 1:
        extras.append(f"{overlay} layers")
    suffix = f" — {', '.join(extras)}" if extras else ""
    return f"{legend['name']}{suffix} · {src}"


def build_blocked_signatures(manor: dict[tuple[int, int], list[str]]) -> set[tuple[str, ...]]:
    blocked_coords = [
        (1, 0),   # hedge
        (10, 13), # pond
        (17, 11), # tree
        (14, 9),  # rock
        (15, 13), # mausoleum
        (13, 5),  # wood fence
        (24, 22), # picket fence
        (0, 5),   # house wall
    ]
    sigs: set[tuple[str, ...]] = set()
    for xy in blocked_coords:
        if xy in manor:
            sigs.add(tuple(tile_types(manor[xy])))
    return sigs


def block_id(types: list[str]) -> str:
    digest = hashlib.sha1("|".join(types).encode()).hexdigest()[:10]
    return f"b_{digest}"


def make_block(
    types: list[str],
    sources: list[dict],
    blocked_signatures: set[tuple[str, ...]],
) -> dict:
    line22 = types[17]
    legend = LINE22_LEGEND.get(line22, {"name": f"Type {line22}", "color": "#444444"})
    overlay_count = sum(1 for v in types if v != "0")
    primary = sources[0]
    return {
        "id": block_id(types),
        "name": auto_name(types, primary),
        "category": category_for(types),
        "line22": int(line22),
        "line22Name": legend["name"],
        "color": legend["color"],
        "blocked": infer_blocked(types, blocked_signatures),
        "types": types,
        "overlayCount": overlay_count,
        "sources": sources,
        "primaryMap": primary["map"],
        "isStore": types[16] == "3" and line22 == "6",
        "isGate": types[15] == "1" and types[16] == "6",
        "idColors": {k: ID_COLORS.get(k, legend["color"]) for k in set(types) if k != "0"},
    }


def main() -> None:
    manor = load_map_tiles(GAME / "Manor.txt")
    if manor is None:
        raise SystemExit("Manor.txt is required to build the block library")

    blocked_signatures = build_blocked_signatures(manor)
    blocks_by_key: dict[tuple[str, ...], dict] = {}

    maps_loaded: list[str] = []
    for map_name, filename in LEVEL_MAPS:
        tiles = load_map_tiles(GAME / filename)
        if tiles is None:
            continue
        maps_loaded.append(map_name)
        for y in range(GRID):
            for x in range(GRID):
                types = tile_types(tiles[(x, y)])
                key = tuple(types)
                source = {"map": map_name, "x": x, "y": y}
                if key not in blocks_by_key:
                    blocks_by_key[key] = make_block(types, [source], blocked_signatures)
                else:
                    existing = blocks_by_key[key]["sources"]
                    if len(existing) < 3:
                        existing.append(source)

    blocks = sorted(
        blocks_by_key.values(),
        key=lambda b: (b["primaryMap"], b["line22"], -b["overlayCount"], b["name"]),
    )

    grass_key = tuple(tile_types(manor[(10, 10)]))
    default_block_id = blocks_by_key[grass_key]["id"]

    payload = {
        "version": 2,
        "gridSize": GRID,
        "tileLines": TILE_LINES,
        "sourceMaps": maps_loaded,
        "defaultBlockId": default_block_id,
        "line22Legend": LINE22_LEGEND,
        "idColors": ID_COLORS,
        "mapFilters": ["All"] + maps_loaded,
        "categories": [
            {"id": "terrain", "name": "Terrain"},
            {"id": "paths", "name": "Paths"},
            {"id": "boundaries", "name": "Fences & hedges"},
            {"id": "props", "name": "Props"},
            {"id": "cemetery", "name": "Cemetery"},
            {"id": "buildings", "name": "Buildings"},
        ],
        "blocks": blocks,
    }

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(payload, indent=2))
    print(f"Wrote {len(blocks)} unique visual blocks from {', '.join(maps_loaded)} to {OUT}")


if __name__ == "__main__":
    main()
