#!/usr/bin/env python3
"""Generate Carnival map: reference grass + stone paths + 3D building stamps."""

from __future__ import annotations

import sys
from collections import deque
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = ROOT / "game"
sys.path.insert(0, str(ROOT / "scripts"))

from carnival_layout import (  # noqa: E402
    BIGTOP_DOORS,
    BIGTOP_X0,
    BIGTOP_X1,
    BIGTOP_Y0,
    BIGTOP_Y1,
    BLOCKED_CHARS,
    BOOTH_DOORS,
    CAROUSEL_CENTER,
    CAROUSEL_DOORS,
    CHAR_TO_NAME,
    MAIN_GATE,
    PLAYER_SPAWNS,
    SHOP_DOORS,
    SHOP_X0,
    SHOP_X1,
    SHOP_Y0,
    SHOP_Y1,
    SPAWN_CEMETERY,
    SPAWN_ENTRANCE,
    SPAWN_PARK,
    layout_grid,
)
from carnival_pixel_art import WALL_SLOTS as A  # noqa: E402

GRID = 32
TILE_LINES = 22
PATH_SIZE = 1024
SUB = 32

ZONE_CHARS = {
    "bigtop": frozenset("T"),
    "carousel": frozenset("CR"),
    "shop": frozenset("G"),
    "funhouse": frozenset("H"),
    "booth": frozenset("B"),
}

DOORS_BY_ZONE = {
    "bigtop": BIGTOP_DOORS,
    "carousel": CAROUSEL_DOORS,
    "shop": SHOP_DOORS,
    "booth": BOOTH_DOORS,
}

# Tall wall face UV (manor hedge) for exterior extrusion height.
HEDGE_WALL = (0, 31)
BUILDING_FLOOR = (2, 6)


def in_bounds(x: int, y: int) -> bool:
    return 0 <= x < GRID and 0 <= y < GRID


def is_blocked_char(ch: str) -> bool:
    return ch in BLOCKED_CHARS


def is_blocked(world: list[list[str]], x: int, y: int) -> bool:
    if not in_bounds(x, y):
        return True
    return is_blocked_char(world[y][x])


def tile_types(tile: list[str]) -> list[str]:
    return tile[4:22]


def make_tile(walls: list[str], types: list[str]) -> list[str]:
    return walls + types


def floor_types(fx: int, fy: int) -> list[str]:
    types = ["0"] * 18
    types[16] = str(fx)
    types[17] = str(fy)
    return types


def render_lawn(
    x: int,
    y: int,
    walls: list[str],
    templates: dict[str, list[str]],
) -> list[str]:
    """Bright carnival lawn from atlas slots + festive overlays."""
    n = x + y
    if n % 13 == 0:
        return make_tile(walls, templates["string_lights"])
    if n % 11 == 0:
        return make_tile(walls, templates["bunting"])
    if n % 7 == 0:
        balloon_keys = ["balloon_red", "balloon_blue", "balloon_yellow", "balloon_green", "balloon_purple"]
        bk = balloon_keys[n % len(balloon_keys)]
        return make_tile(walls, overlay_types(*A["confetti_lawn"], *A[bk]))
    if n % 5 == 1:
        return make_tile(walls, templates["fair_grass"])
    return make_tile(walls, templates["lawn"])


def overlay_types(fx: int, fy: int, ox: int, oy: int) -> list[str]:
    types = floor_types(fx, fy)
    types[8], types[9] = str(ox), str(oy)
    return types


def wall_types(fx: int, fy: int, wfx: int, wfy: int, sides: str) -> list[str]:
    types = floor_types(fx, fy)
    if "t" in sides:
        types[8], types[9] = str(wfx), str(wfy)
    if "b" in sides:
        types[12], types[13] = str(wfx), str(wfy)
    if "l" in sides:
        types[10], types[11] = str(wfx), str(wfy)
    if "r" in sides:
        types[14], types[15] = str(wfx), str(wfy)
    return types


def apply_gate(tile: list[str]) -> None:
    tile[18] = "1"
    tile[19] = "6"


def partial_wall_types(
    fx: int,
    fy: int,
    wfx: int,
    wfy: int,
    walls: list[str],
) -> list[str]:
    types = floor_types(fx, fy)
    if walls[1] == "1":
        types[8], types[9] = str(wfx), str(wfy)
    if walls[3] == "1":
        types[12], types[13] = str(wfx), str(wfy)
    if walls[2] == "1":
        types[10], types[11] = str(wfx), str(wfy)
    if walls[0] == "1":
        types[14], types[15] = str(wfx), str(wfy)
    return types


def zone_for_cell(ch: str) -> str | None:
    for name, chars in ZONE_CHARS.items():
        if ch in chars:
            return name
    return None


def is_exterior(world: list[list[str]], x: int, y: int, zone_chars: frozenset[str]) -> bool:
    for dx, dy in ((1, 0), (0, -1), (-1, 0), (0, 1)):
        nx, ny = x + dx, y + dy
        if not in_bounds(nx, ny) or world[ny][nx] not in zone_chars:
            return True
    return False


def walkable_structure_walls(
    world: list[list[str]],
    x: int,
    y: int,
    zone: str,
) -> list[str]:
    zone_chars = ZONE_CHARS[zone]
    doors = DOORS_BY_ZONE[zone]
    ch = world[y][x]
    if ch not in zone_chars:
        return wall_flags(world, x, y)

    walls = [0, 0, 0, 0]
    neighbors = ((1, 0, 0), (0, -1, 1), (-1, 0, 2), (0, 1, 3))
    for dx, dy, idx in neighbors:
        nx, ny = x + dx, y + dy
        if not in_bounds(nx, ny) or world[ny][nx] not in zone_chars:
            if (x, y) in doors and in_bounds(nx, ny) and world[ny][nx] in "=.bRfA":
                walls[idx] = 0
            else:
                walls[idx] = 1
    return [str(w) for w in walls]


def apply_door(tile: list[str], walls: list[str]) -> None:
    """Mark an open side as a gate texture when present."""
    if walls[3] == "0":
        apply_gate(tile)
    elif walls[2] == "0":
        tile[16], tile[17] = "1", "6"
    elif walls[0] == "0":
        tile[14], tile[15] = "1", "6"


def tall_structure_tile(
    walls: list[str],
    base_types: list[str],
    wall_uv: tuple[int, int],
    *,
    door: bool = False,
) -> list[str]:
    """Walkable cell with tall 3D walls on flagged sides."""
    types = partial_wall_types(
        int(base_types[16]),
        int(base_types[17]),
        wall_uv[0],
        wall_uv[1],
        walls,
    )
    for i in range(8, 10):
        if base_types[i] != "0":
            types[i] = base_types[i]
    if any(w == "1" for w in walls):
        types[16], types[17] = str(BUILDING_FLOOR[0]), str(BUILDING_FLOOR[1])
    else:
        types[16], types[17] = base_types[16], base_types[17]
    tile = make_tile(walls, types)
    if door:
        apply_door(tile, walls)
    return tile


def apply_perimeter_gate(tile: list[str]) -> None:
    apply_gate(tile)


def wall_flags(world: list[list[str]], x: int, y: int) -> list[str]:
    if is_blocked_char(world[y][x]):
        return ["0", "1", "0", "1"]
    return [
        str(int(is_blocked(world, x + 1, y))),
        str(int(is_blocked(world, x, y - 1))),
        str(int(is_blocked(world, x - 1, y))),
        str(int(is_blocked(world, x, y + 1))),
    ]


def load_manor_tiles() -> dict[tuple[int, int], list[str]]:
    lines = (GAME / "Manor.txt").read_text().splitlines()
    tiles: dict[tuple[int, int], list[str]] = {}
    for y in range(GRID):
        for x in range(GRID):
            base = (y * GRID + x) * TILE_LINES
            tiles[(x, y)] = lines[base : base + TILE_LINES]
    return tiles


def copy_region(
    manor_tiles: dict[tuple[int, int], list[str]],
    sx0: int, sy0: int, w: int, h: int,
) -> dict[tuple[int, int], list[str]]:
    region: dict[tuple[int, int], list[str]] = {}
    for dy in range(h):
        for dx in range(w):
            region[(dx, dy)] = manor_tiles[(sx0 + dx, sy0 + dy)][:]
    return region


def extract_manor_templates(manor_tiles: dict[tuple[int, int], list[str]]) -> dict[str, list[str]]:
    def types_at(x: int, y: int) -> list[str]:
        return tile_types(manor_tiles[(x, y)])

    return {
        "path": types_at(7, 7),
        "cemetery_floor": types_at(12, 14),
        "pond": types_at(10, 13),
        "tombstone": types_at(15, 15),
        "mausoleum": types_at(15, 13),
        "bridge": types_at(10, 12),
        "dead_tree": types_at(6, 21),
        "wood_fence": types_at(13, 5),
        "shop_floor": types_at(25, 4),
        "fort_floor": types_at(4, 24),
        "house_floor": types_at(4, 13),
    }


def build_templates(manor_templates: dict[str, list[str]]) -> dict[str, list[str]]:
    g, gr = A["fair_grass"], A["gravel"]
    gd = A["gravel_dark"]
    conf = A["confetti_lawn"]
    balloons = [
        A["balloon_red"], A["balloon_blue"], A["balloon_yellow"],
        A["balloon_green"], A["balloon_purple"],
    ]
    return {
        "gravel": floor_types(*gr),
        "gravel_dark": floor_types(*gd),
        "lawn": floor_types(*conf),
        "fair_grass": floor_types(*g),
        "confetti": floor_types(*conf),
        "path": floor_types(*gr),
        "pink_path": floor_types(*A["pink_path"]),
        "pond": floor_types(*A["pond_deep"]),
        "pond_edge": floor_types(*A["pond_edge"]),
        "bridge": manor_templates["bridge"],
        "cemetery_floor": manor_templates["cemetery_floor"],
        "tombstone": manor_templates["tombstone"],
        "mausoleum": manor_templates["mausoleum"],
        "dead_tree": manor_templates["dead_tree"],
        "fence": wall_types(*conf, *A["stone_fence"], "tbl"),
        "fence_white": wall_types(*conf, *A["fence_white"], "tbl"),
        "tent_wall": wall_types(*A["tent_stripe"], *A["bigtop_wall"], "tblr"),
        "tent_peak": overlay_types(*conf, *A["bigtop_peak"]),
        "carousel_deck": floor_types(*A["carousel_deck"]),
        "carousel_horse": overlay_types(*A["carousel_deck"], *A["carousel_horse"]),
        "carousel_roof": overlay_types(*A["carousel_deck"], *A["carousel_roof"]),
        "ferris_tower": overlay_types(*conf, *A["ferris_tower"]),
        "ferris_hub": overlay_types(*conf, *A["ferris_hub"]),
        "ferris_apron": floor_types(*A["gold_path"]),
        "coaster_wood": wall_types(*A["coaster_wood"], *A["coaster_rail"], "tblr"),
        "shop_floor": floor_types(*A["ticket_floor"]),
        "shop_wall": wall_types(*A["ticket_floor"], *A["shop_neon"], "tblr"),
        "house_floor": floor_types(*A["funhouse_floor"]),
        "house_wall": wall_types(*A["funhouse_floor"], *A["stripe_h"], "tblr"),
        "fort_floor": manor_templates["fort_floor"],
        "booth": wall_types(*A["tent_stripe"], *A["booth_awning"], "tblr"),
        "arch": overlay_types(*gr, *A["clown_arch"]),
        "lamp": overlay_types(*gr, *A["lamp_post"]),
        "bunting": overlay_types(*conf, *A["bunting"]),
        "string_lights": overlay_types(*conf, *A["string_lights"]),
        "balloons": [floor_types(*b) for b in balloons],
    }


def build_building_tiles(
    world: list[list[str]],
    manor_tiles: dict[tuple[int, int], list[str]],
) -> dict[tuple[int, int], list[str]]:
    building: dict[tuple[int, int], list[str]] = {}

    regions: list[tuple[str, int, int, int, int, int, int]] = [
        ("s", 4, 13, 3, 3, 24, 25),
    ]
    for ch, sx, sy, w, h, dx, dy in regions:
        region = copy_region(manor_tiles, sx, sy, w, h)
        for (rx, ry), tile in region.items():
            x, y = dx + rx, dy + ry
            if in_bounds(x, y) and world[y][x] == ch:
                building[(x, y)] = tile[:]

    cemetery = copy_region(manor_tiles, 11, 11, 9, 9)
    for y in range(GRID):
        for x in range(GRID):
            ch = world[y][x]
            if ch not in "oOm":
                continue
            rx = min(max(x - 22, 0), 8)
            ry = min(max(y - 8, 0), 8)
            building[(x, y)] = cemetery[(rx, ry)][:]

    return building


def render_coaster(
    world: list[list[str]],
    x: int,
    y: int,
    templates: dict[str, list[str]],
) -> list[str]:
    """Walkable ground with overhead coaster track and support posts."""
    walls = ["0", "1", "0", "0"]
    if x % 2 == 0:
        walls[2] = "1"
    if (x + y) % 3 == 1:
        walls[0] = "1"
    types = partial_wall_types(
        A["gravel"][0],
        A["gravel"][1],
        HEDGE_WALL[0],
        HEDGE_WALL[1],
        walls,
    )
    types[8], types[9] = str(A["coaster_rail"][0]), str(A["coaster_rail"][1])
    types[10], types[11] = str(A["coaster_track"][0]), str(A["coaster_track"][1])
    types[16], types[17] = "0", "31"
    return make_tile(walls, types)


def render_bigtop(
    world: list[list[str]],
    x: int,
    y: int,
    templates: dict[str, list[str]],
) -> list[str]:
    walls = walkable_structure_walls(world, x, y, "bigtop")
    door = (x, y) in BIGTOP_DOORS
    if y == BIGTOP_Y0:
        base = templates["tent_peak"]
    elif is_exterior(world, x, y, ZONE_CHARS["bigtop"]):
        base = templates["tent_wall"]
    else:
        base = templates["confetti"]
    return tall_structure_tile(walls, base, A["bigtop_wall"], door=door)


def render_carousel(
    world: list[list[str]],
    x: int,
    y: int,
    templates: dict[str, list[str]],
) -> list[str]:
    walls = walkable_structure_walls(world, x, y, "carousel")
    door = (x, y) in CAROUSEL_DOORS
    ch = world[y][x]
    if ch == "R":
        base = templates["gravel_dark"]
    elif (x + y) % 2 == 0:
        base = templates["carousel_horse"]
    else:
        base = templates["carousel_deck"]
    if is_exterior(world, x, y, ZONE_CHARS["carousel"]):
        return tall_structure_tile(walls, base, A["carousel_roof"], door=door)
    return make_tile(walls, base)


def render_walkable_building(
    world: list[list[str]],
    x: int,
    y: int,
    zone: str,
    tile: list[str],
) -> list[str]:
    walls = walkable_structure_walls(world, x, y, zone)
    out = tile[:]
    out[:4] = walls
    if (x, y) in DOORS_BY_ZONE[zone]:
        apply_door(out, walls)
    return out


def render_cell(
    world: list[list[str]],
    x: int,
    y: int,
    templates: dict[str, list[str]],
    building: dict[tuple[int, int], list[str]],
) -> list[str]:
    ch = world[y][x]
    zone = zone_for_cell(ch)

    if (x, y) in building:
        tile = building[(x, y)][:]
        if zone in DOORS_BY_ZONE:
            return render_walkable_building(world, x, y, zone, tile)
        tile[:4] = wall_flags(world, x, y)
        return tile

    walls = wall_flags(world, x, y)

    if ch == "#":
        return make_tile(walls, templates["fence_white"])
    if ch == "+":
        tile = make_tile(walls, templates["fence"])
        apply_perimeter_gate(tile)
        return tile
    if ch == "g":
        tile = make_tile(walls, templates["fence"])
        apply_gate(tile)
        return tile
    if ch == "O":
        return make_tile(walls, templates["fence"])

    # Stone paths — gravel atlas; tinted near attractions for carnival color.
    if ch == "=":
        t = templates["gravel"][:]
        zone = x + y
        if 10 <= x <= 18 and 10 <= y <= 15:
            t = templates["pink_path"][:]
        elif 6 <= x <= 10 and 7 <= y <= 13:
            t = templates["path"][:]
        elif 22 <= x <= 27 and 3 <= y <= 5:
            t = templates["path"][:]
        if zone % 4 == 0:
            t = overlay_types(*A["gravel"], *A["lamp_post"])
        elif zone % 6 == 0:
            t = overlay_types(*A["gravel"], *A["string_lights"])
        return make_tile(walls, t)
    if ch == "f":
        return make_tile(walls, templates["ferris_apron"])

    if ch == "T":
        return render_bigtop(world, x, y, templates)
    if ch in "CR":
        return render_carousel(world, x, y, templates)
    if ch == "F":
        if y <= BIGTOP_Y0 + 1:
            return make_tile(walls, templates["ferris_tower"])
        return make_tile(walls, templates["ferris_hub"])
    if ch == "K":
        return render_coaster(world, x, y, templates)
    if ch == "G":
        walls = walkable_structure_walls(world, x, y, "shop")
        door = (x, y) in SHOP_DOORS
        if is_exterior(world, x, y, ZONE_CHARS["shop"]):
            base = templates["mausoleum"]
            return tall_structure_tile(walls, base, A["shop_neon"], door=door)
        return tall_structure_tile(walls, templates["shop_wall"], A["shop_neon"], door=door)
    if ch == "B":
        walls = walkable_structure_walls(world, x, y, "booth")
        door = (x, y) in BOOTH_DOORS
        return tall_structure_tile(walls, templates["booth"], A["booth_awning"], door=door)
    if ch == "s" and (x, y) not in building:
        return make_tile(walls, templates["house_floor"])

    if ch == "~":
        edge = x >= 6 or y >= 5
        key = "pond_edge" if edge else "pond"
        return make_tile(walls, templates[key])
    if ch == "b":
        return make_tile(walls, templates["bridge"])
    if ch == "o":
        return make_tile(walls, templates["cemetery_floor"])
    if ch == "m":
        return make_tile(walls, templates["tombstone"])
    if ch == "M":
        return make_tile(walls, templates["mausoleum"])
    if ch == "D":
        return make_tile(walls, templates["dead_tree"])
    if ch == "A":
        return make_tile(walls, templates["arch"])

    # Lawn — carnival atlas slots (not reference map coordinates).
    return render_lawn(x, y, walls, templates)


def compose_tiles(
    world: list[list[str]],
    manor_tiles: dict[tuple[int, int], list[str]],
    templates: dict[str, list[str]],
) -> dict[tuple[int, int], list[str]]:
    building = build_building_tiles(world, manor_tiles)
    return {
        (x, y): render_cell(world, x, y, templates, building)
        for y in range(GRID)
        for x in range(GRID)
    }


def build_spawns(world: list[list[str]]) -> list[int]:
    spawns = [-1] * (GRID * GRID)
    for i, (x, y) in enumerate(PLAYER_SPAWNS):
        spawns[y * GRID + x] = i % 3

    zpark = SPAWN_PARK
    for y in range(1, GRID - 1):
        for x in range(1, GRID - 1):
            idx = y * GRID + x
            if spawns[idx] != -1 or is_blocked_char(world[y][x]):
                continue
            ch = world[y][x]
            if ch in "=.RfA" and (x + y) % 3 == 0:
                spawns[idx] = zpark
                zpark = SPAWN_PARK if zpark >= 4 else zpark + 1

    for y in range(GRID):
        for x in range(GRID):
            if world[y][x] in "omD" and spawns[y * GRID + x] == -1:
                spawns[y * GRID + x] = SPAWN_CEMETERY if (x + y) % 2 == 0 else SPAWN_PARK

    for y in range(28, 31):
        for x in range(12, 19):
            if not is_blocked_char(world[y][x]):
                spawns[y * GRID + x] = SPAWN_ENTRANCE

    return spawns


def build_path_map(world: list[list[str]]) -> list[str]:
    path = [0] * (PATH_SIZE * PATH_SIZE)
    blocked = [[False] * PATH_SIZE for _ in range(PATH_SIZE)]
    for ty in range(GRID):
        for tx in range(GRID):
            if is_blocked(world, tx, ty):
                for sy in range(SUB):
                    for sx in range(SUB):
                        py = ty * SUB + sy
                        px = tx * SUB + sx
                        blocked[py][px] = True
                        path[py * PATH_SIZE + px] = 10

    cx, cy = CAROUSEL_CENTER
    center_px = cx * SUB + SUB // 2
    center_py = cy * SUB + SUB // 2
    dist = [[-1] * PATH_SIZE for _ in range(PATH_SIZE)]
    q: deque[tuple[int, int]] = deque([(center_px, center_py)])
    dist[center_py][center_px] = 0
    while q:
        px, py = q.popleft()
        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            nx, ny = px + dx, py + dy
            if 0 <= nx < PATH_SIZE and 0 <= ny < PATH_SIZE and not blocked[ny][nx] and dist[ny][nx] == -1:
                dist[ny][nx] = dist[py][px] + 1
                q.append((nx, ny))

    for py in range(PATH_SIZE):
        for px in range(PATH_SIZE):
            if blocked[py][px]:
                continue
            best_val = 0
            best_d = dist[py][px]
            for dx, dy, val in ((0, -1, 1), (1, 0, 2), (0, 1, 3), (-1, 0, 4)):
                nx, ny = px + dx, py + dy
                if 0 <= nx < PATH_SIZE and 0 <= ny < PATH_SIZE and dist[ny][nx] >= 0 and dist[ny][nx] < best_d:
                    best_d = dist[ny][nx]
                    best_val = val
            if best_val:
                path[py * PATH_SIZE + px] = best_val
            elif dist[py][px] < 0:
                path[py * PATH_SIZE + px] = 10
            else:
                path[py * PATH_SIZE + px] = 2

    zero_walkable = sum(
        1
        for py in range(PATH_SIZE)
        for px in range(PATH_SIZE)
        if not blocked[py][px] and path[py * PATH_SIZE + px] == 0
    )
    if zero_walkable:
        raise SystemExit(f"Path audit failed: {zero_walkable} walkable cells still have path value 0")

    return [str(v) for v in path]


def stamp_primary_store(
    out_tiles: dict[tuple[int, int], list[str]],
    world: list[list[str]],
    templates: dict[str, list[str]],
) -> int:
    """Place gun-shop spawn markers (tile lines 21-22 = 3,6) on mausoleum shop cells."""
    for y in range(GRID):
        for x in range(GRID):
            if world[y][x] != "G" and out_tiles[(x, y)][20:22] == ["3", "6"]:
                out_tiles[(x, y)][20] = out_tiles[(x, y)][16]
                out_tiles[(x, y)][21] = out_tiles[(x, y)][17]

    count = 0
    for y in range(GRID):
        for x in range(GRID):
            if world[y][x] != "G":
                continue
            tile = out_tiles[(x, y)][:]
            walls = tile[:4]
            out_tiles[(x, y)] = make_tile(walls, templates["shop_floor"][:])
            out_tiles[(x, y)][20] = "3"
            out_tiles[(x, y)][21] = "6"
            count += 1
    return count


def print_layout(world: list[list[str]]) -> None:
    for row in world:
        print("".join(row))


def main() -> None:
    world = layout_grid()
    manor_tiles = load_manor_tiles()
    templates = build_templates(extract_manor_templates(manor_tiles))
    out_tiles = compose_tiles(world, manor_tiles, templates)
    store_cells = stamp_primary_store(out_tiles, world, templates)

    level: list[str] = []
    for y in range(GRID):
        for x in range(GRID):
            level.extend(out_tiles[(x, y)])

    spawn_lines = [str(v) for v in build_spawns(world)]
    path_lines = build_path_map(world)

    assert len(level) == GRID * GRID * TILE_LINES
    assert len(spawn_lines) == GRID * GRID
    assert len(path_lines) == PATH_SIZE * PATH_SIZE

    (GAME / "Carnival.txt").write_text("\n".join(level) + "\n")
    (GAME / "CarnivalSpawns.txt").write_text("\n".join(spawn_lines) + "\n")
    (GAME / "PathMap_Carnival.txt").write_text("\n".join(path_lines) + "\n")

    blocked = sum(1 for y in range(GRID) for x in range(GRID) if is_blocked_char(world[y][x]))
    print("Generated Carnival map (reference layout + 3D structures):")
    print(f"  {GAME / 'Carnival.txt'}")
    print(f"  Blocked (3D solids): {blocked}  Walkable: {GRID * GRID - blocked}")
    print(f"  Gun shop markers in mausoleum: {store_cells}")

    import importlib.util

    spec = importlib.util.spec_from_file_location(
        "validate_carnival", ROOT / "scripts/validate-carnival.py"
    )
    validate = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(validate)
    validate.run_validation(world, out_tiles, level, strict=True)
    print()
    print_layout(world)
    counts: dict[str, int] = {}
    for y in range(GRID):
        for x in range(GRID):
            name = CHAR_TO_NAME.get(world[y][x], world[y][x])
            counts[name] = counts.get(name, 0) + 1
    print("\nZones:", dict(sorted(counts.items(), key=lambda kv: -kv[1])))


if __name__ == "__main__":
    main()
