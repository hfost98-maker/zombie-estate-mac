#!/usr/bin/env python3
"""Validate Carnival map + atlas alignment. Exit non-zero on failure when strict=True."""

from __future__ import annotations

import sys
from collections import deque
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = ROOT / "game"
ASSETS = ROOT / "tools/map-editor/assets"
REF = ROOT / "tools/carnival-assets/reference"

sys.path.insert(0, str(ROOT / "scripts"))

from carnival_layout import (  # noqa: E402
    BIGTOP_X0,
    BIGTOP_X1,
    BIGTOP_Y0,
    BIGTOP_Y1,
    BLOCKED_CHARS,
    CAROUSEL_CENTER,
    CHAR_TO_NAME,
    layout_grid,
)
from carnival_pixel_art import WALL_SLOTS  # noqa: E402

GRID = 32
TILE_LINES = 22
PATH_SIZE = 1024
CELL = 16

# Slots referenced by generate-carnival.py (keep in sync with build_templates + render_*).
USED_SLOTS = frozenset({
    "fair_grass", "confetti_lawn", "gravel", "gravel_dark", "gold_path", "pink_path",
    "pond_deep", "pond_edge", "stone_fence", "fence_white", "tent_stripe", "bigtop_wall",
    "bigtop_peak", "carousel_deck", "carousel_horse", "carousel_roof", "ferris_tower",
    "ferris_hub", "coaster_wood", "coaster_rail", "coaster_track", "ticket_floor",
    "funhouse_floor", "shop_neon", "booth_awning", "stripe_h", "clown_arch", "lamp_post",
    "bunting", "string_lights", "balloon_red", "balloon_blue", "balloon_yellow",
    "balloon_green", "balloon_purple",
})

STRUCTURE_SAMPLES = (
    (8, 1, "bigtop NW exterior", "T", {"walls_min": 1, "wall_uv_row": 31}),
    (11, 2, "bigtop interior", "T", {"walls_max": 0}),
    (14, 4, "bigtop south door", "T", {"walls_max": 0}),
    (15, 11, "carousel hub", "C", {}),
    (17, 10, "carousel ring exterior", "R", {"walls_min": 1}),
    (3, 7, "coaster under track", "K", {"top_wall": True, "floor_y": 31}),
    (25, 4, "mausoleum shop south", "G", {}),
    (18, 1, "ferris tower", "F", {"overlay_min": 1}),
    (8, 29, "entrance arch", "A", {"overlay_min": 1}),
    (11, 4, "path with lamp", "=", {"floor_slot": "gravel"}),
)

ZONE_EXPECTED = {
    ".": {"green", "confetti", "grass", "lawn"},
    "=": {"path", "gravel", "stone", "beige", "gold"},
    "~": {"pond", "water", "blue"},
    "T": {"tent", "stripe", "red", "white"},
    "C": {"carousel", "deck", "wood"},
    "R": {"carousel", "gravel", "path"},
    "K": {"coaster", "track", "wood", "red"},
    "F": {"ferris", "tower", "metal"},
    "G": {"shop", "ticket", "neon"},
}


class ValidationError(Exception):
    pass


def in_bounds(x: int, y: int) -> bool:
    return 0 <= x < GRID and 0 <= y < GRID


def is_blocked_char(ch: str) -> bool:
    return ch in BLOCKED_CHARS


def is_blocked(world: list[list[str]], x: int, y: int) -> bool:
    if not in_bounds(x, y):
        return True
    return is_blocked_char(world[y][x])


def tile_floor_uv(tile: list[str]) -> tuple[int, int]:
    return int(tile[20]), int(tile[21])


def tile_walls(tile: list[str]) -> tuple[bool, bool, bool, bool]:
    return tuple(v == "1" for v in tile[:4])


def count_nonzero_types(tile: list[str]) -> int:
    return sum(1 for v in tile[4:22] if v != "0")


def atlas_path() -> Path | None:
    for candidate in (
        ASSETS / "MasterWallTexture.carnival.png",
        ASSETS / "MasterWallTexture.reference.png",
        ASSETS / "MasterWallTexture.png",
    ):
        if candidate.exists():
            return candidate
    return None


def validate_atlas_slots(errors: list[str], warnings: list[str]) -> None:
    path = atlas_path()
    if path is None:
        warnings.append("Atlas PNG missing — skip slot pixel checks (run build-reference-atlas.py)")
        return

    try:
        from PIL import Image
    except ImportError:
        warnings.append("Pillow missing — skip atlas pixel checks")
        return

    img = Image.open(path).convert("RGBA")
    blank_slots: list[str] = []
    flat_slots: list[str] = []

    for name in sorted(USED_SLOTS):
        if name not in WALL_SLOTS:
            errors.append(f"USED_SLOTS references unknown WALL_SLOTS key: {name}")
            continue
        cx, cy = WALL_SLOTS[name]
        tile = img.crop((cx * CELL, cy * CELL, (cx + 1) * CELL, (cy + 1) * CELL))
        pixels = list(tile.getdata())
        opaque = [p for p in pixels if p[3] > 10]
        if len(opaque) < 8:
            blank_slots.append(f"{name}@({cx},{cy})")
            continue
        colors = {p[:3] for p in opaque}
        if len(colors) <= 2:
            flat_slots.append(name)

    if blank_slots:
        errors.append(f"Atlas slots blank or missing art: {', '.join(blank_slots[:8])}"
                       + (f" (+{len(blank_slots)-8} more)" if len(blank_slots) > 8 else ""))
    if flat_slots:
        warnings.append(f"Nearly flat atlas tiles (may look wrong in 3D): {', '.join(flat_slots[:6])}")

    unused = set(WALL_SLOTS) - USED_SLOTS
    if unused:
        warnings.append(f"{len(unused)} painted WALL_SLOTS unused by map generator")


def validate_tile_uvs(out_tiles: dict[tuple[int, int], list[str]], errors: list[str]) -> None:
    bad: list[str] = []
    for (x, y), tile in out_tiles.items():
        for i, val in enumerate(tile):
            if i < 4:
                continue
            if val == "0":
                continue
            n = int(val)
            if n < 0 or n > 31:
                bad.append(f"({x},{y}) line{i}={val}")
    if bad:
        errors.append(f"Out-of-range atlas UVs: {bad[:6]}" + (f" (+{len(bad)-6})" if len(bad) > 6 else ""))


def validate_blank_tiles(out_tiles: dict[tuple[int, int], list[str]], errors: list[str]) -> None:
    blanks = [
        (x, y)
        for (x, y), tile in out_tiles.items()
        if count_nonzero_types(tile) == 0 and tile[:4] == ["0", "0", "0", "0"]
    ]
    if blanks:
        errors.append(f"{len(blanks)} completely blank tiles (no walls, no UVs)")


def validate_walkability(world: list[list[str]], errors: list[str]) -> None:
    def bfs(start: tuple[int, int], goal: tuple[int, int]) -> bool:
        q: deque[tuple[int, int]] = deque([start])
        seen = {start}
        while q:
            pos = q.popleft()
            if pos == goal:
                return True
            x, y = pos
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if in_bounds(nx, ny) and not is_blocked(world, nx, ny) and (nx, ny) not in seen:
                    seen.add((nx, ny))
                    q.append((nx, ny))
        return False

    start = (15, 29)
    coaster = next(((x, y) for y in range(GRID) for x in range(GRID) if world[y][x] == "K"), None)
    checks = [
        ("big top interior", (11, 2)),
        ("carousel center", CAROUSEL_CENTER),
        ("shop interior", (25, 4)),
        ("funhouse", (2, 19)),
    ]
    if coaster:
        checks.append(("under coaster", coaster))

    failed = []
    for label, goal in checks:
        if not bfs(start, goal):
            failed.append(f"{label} {goal}")
    if failed:
        errors.append("Walkability blocked: " + "; ".join(failed))


def validate_structure_tiles(
    world: list[list[str]],
    out_tiles: dict[tuple[int, int], list[str]],
    errors: list[str],
    warnings: list[str],
) -> None:
    for x, y, label, expect_ch, rules in STRUCTURE_SAMPLES:
        ch = world[y][x]
        if ch != expect_ch:
            warnings.append(f"Sample {label} ({x},{y}): layout char {ch!r} != expected {expect_ch!r}")
        tile = out_tiles[(x, y)]
        r, t, l, b = tile_walls(tile)
        wall_count = sum((r, t, l, b))
        fx, fy = tile_floor_uv(tile)

        if rules.get("walls_min") is not None and wall_count < rules["walls_min"]:
            errors.append(f"{label}: expected >= {rules['walls_min']} walls, got {tile[:4]}")
        if rules.get("walls_max") is not None and wall_count > rules["walls_max"]:
            errors.append(f"{label}: expected interior (no walls), got {tile[:4]}")
        if rules.get("top_wall") and not t:
            errors.append(f"{label}: coaster missing top wall flag (needs overhead track)")
        if rules.get("floor_y") is not None and fy != rules["floor_y"]:
            warnings.append(f"{label}: floor Y={fy} (expected {rules['floor_y']} for tall extrusion)")
        if rules.get("overlay_min") and count_nonzero_types(tile) < rules["overlay_min"]:
            errors.append(f"{label}: missing overlay/prop art (nonzero types too low)")
        if rules.get("wall_uv_row") == 31:
            uvs = []
            if t:
                uvs.append(int(tile[13]))
            if l:
                uvs.append(int(tile[15]))
            if r:
                uvs.append(int(tile[17]))
            if b:
                uvs.append(int(tile[19]))
            if uvs and max(uvs) < 28:
                warnings.append(f"{label}: wall UV row max {max(uvs)} (expected row 31 building face)")
        if rules.get("floor_slot") == "gravel":
            slot = WALL_SLOTS["gravel"]
            if (fx, fy) != slot:
                errors.append(f"{label}: path floor UV ({fx},{fy}) != gravel slot {slot}")


def validate_zone_footprints(world: list[list[str]], errors: list[str]) -> None:
    checks = [
        ("big top", "T", BIGTOP_X0, BIGTOP_Y0, BIGTOP_X1, BIGTOP_Y1, 20),
        ("coaster", "K", 2, 7, 6, 13, 8),
        ("carousel hub", "C", 14, 11, 16, 12, 2),
        ("shop", "G", 24, 3, 27, 4, 8),
        ("entrance arch", "A", 7, 29, 9, 30, 4),
        ("game booths", "B", 2, 18, 4, 24, 18),
    ]
    for name, ch, x0, y0, x1, y1, min_cells in checks:
        count = sum(1 for y in range(y0, y1 + 1) for x in range(x0, x1 + 1) if world[y][x] == ch)
        if count < min_cells:
            errors.append(f"Zone footprint {name}: {count} cells (need >= {min_cells})")


def validate_reference_colors(world: list[list[str]], warnings: list[str]) -> None:
    ref = REF / "reference-512.png"
    if not ref.exists():
        warnings.append("reference-512.png missing — skip color alignment check")
        return
    try:
        from PIL import Image
    except ImportError:
        return

    img = Image.open(ref).convert("RGB").resize((GRID, GRID), Image.Resampling.NEAREST)
    mismatches = 0
    samples = 0
    for y in range(GRID):
        for x in range(GRID):
            ch = world[y][x]
            if ch not in "TCRKFG=.=":
                continue
            r, g, b = img.getpixel((x, y))
            brightness = r + g + b
            samples += 1
            if ch == "." and brightness < 120:
                mismatches += 1
            elif ch == "=" and brightness < 100:
                mismatches += 1
            elif ch in "TCRK" and brightness < 80:
                mismatches += 1
    if samples and mismatches > samples * 0.25:
        warnings.append(
            f"Reference alignment: {mismatches}/{samples} structure/lawn cells look darker than "
            "reference (atlas may need brighter slots or repainted rows 28–31)"
        )


def print_sample_tiles(
    world: list[list[str]],
    out_tiles: dict[tuple[int, int], list[str]],
) -> None:
    print("\nStructure tile audit (walls | floor | top-wall UV | overlays):")
    for x, y, label, _, _ in STRUCTURE_SAMPLES:
        tile = out_tiles[(x, y)]
        ch = world[y][x]
        fx, fy = tile_floor_uv(tile)
        top = (tile[12], tile[13]) if tile[1] == "1" else ("-", "-")
        nz = count_nonzero_types(tile)
        print(
            f"  ({x:2d},{y:2d}) {label:22s} ch={ch} walls={tile[:4]} "
            f"floor=({fx},{fy}) top={top} nz={nz}"
        )


def print_zone_summary(world: list[list[str]]) -> None:
    counts: dict[str, int] = {}
    for y in range(GRID):
        for x in range(GRID):
            name = CHAR_TO_NAME.get(world[y][x], world[y][x])
            counts[name] = counts.get(name, 0) + 1
    print("\nZones:", dict(sorted(counts.items(), key=lambda kv: -kv[1])))


def run_validation(
    world: list[list[str]],
    out_tiles: dict[tuple[int, int], list[str]],
    level_lines: list[str] | None = None,
    *,
    strict: bool = True,
) -> None:
    errors: list[str] = []
    warnings: list[str] = []

    if level_lines is not None:
        if len(level_lines) != GRID * GRID * TILE_LINES:
            errors.append(f"Carnival.txt line count {len(level_lines)} != {GRID * GRID * TILE_LINES}")

    validate_atlas_slots(errors, warnings)
    validate_tile_uvs(out_tiles, errors)
    validate_blank_tiles(out_tiles, errors)
    validate_walkability(world, errors)
    validate_structure_tiles(world, out_tiles, errors, warnings)
    validate_zone_footprints(world, errors)
    validate_reference_colors(world, warnings)

    music_dir = ROOT / "game" / "CarnivalMusic"
    sky = ROOT / "game" / "CarnivalAssets" / "BarBG_starry.png"
    main_track = music_dir / "carnival_track_1.wav"
    alt_track = music_dir / "carnival_track_2.wav"
    legacy_track = music_dir / "carnival_main.wav"
    if not (
        (main_track.exists() and alt_track.exists())
        or legacy_track.exists()
        or (music_dir / "carnival_bass.wav").exists()
    ):
        warnings.append(
            "Carnival music not set — import both tracks (scripts/import-carnival-music.py "
            "--slot 1 and --slot 2) or run scripts/generate-carnival-music.py"
        )
    if not sky.exists():
        warnings.append("Starry sky not built — run scripts/generate-carnival-sky.py")

    print_sample_tiles(world, out_tiles)
    print_zone_summary(world)

    if warnings:
        print(f"\nValidation warnings ({len(warnings)}):")
        for w in warnings:
            print(f"  ! {w}")

    if errors:
        print(f"\nValidation FAILED ({len(errors)}):")
        for e in errors:
            print(f"  x {e}")
        if strict:
            raise SystemExit(1)
    else:
        print(f"\nValidation OK ({len(warnings)} warning(s))")


def main() -> None:
    import importlib.util

    spec = importlib.util.spec_from_file_location("gen", ROOT / "scripts/generate-carnival.py")
    gen = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(gen)

    world = layout_grid()
    manor = gen.load_manor_tiles()
    templates = gen.build_templates(gen.extract_manor_templates(manor))
    out_tiles = gen.compose_tiles(world, manor, templates)
    run_validation(world, out_tiles, strict=True)


if __name__ == "__main__":
    main()
