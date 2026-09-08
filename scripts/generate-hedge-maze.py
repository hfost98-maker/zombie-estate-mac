#!/usr/bin/env python3
"""Generate Hedge Maze map files matching the reference estate layout."""

from __future__ import annotations

import random
from collections import deque
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = ROOT / "game"
GRID = 32
TILE_LINES = 22
PATH_SIZE = 1024
SUB = 32

# Logical terrain cells used while composing the 32×32 layout.
GRASS = 0
HEDGE = 1
PATH = 2
MAZE = 3
CEMETERY = 4
CEM_WALL = 5
POND = 6
FENCE = 7
SHOP = 8
HOUSE = 9
FORT = 10
GARDEN = 11
SHED = 12
TREE = 13
ROCK = 14
MAUSOLEUM = 15
GATE = 16
BRIDGE = 17
DEAD_TREE = 18
GARDEN_FENCE = 19
BOOTH = 20

BLOCKED = {HEDGE, CEM_WALL, POND, FENCE, SHED, GARDEN_FENCE}

# Maze outer shell (inclusive). Interior walkable cells are 9..22.
MAZE_X0, MAZE_Y0 = 8, 8
MAZE_X1, MAZE_Y1 = 23, 23
MAZE_GATES = ((15, 8), (15, 23), (8, 15), (23, 15))

# Cemetery clearing inside the maze heart.
CEM_X0, CEM_Y0 = 13, 13
CEM_X1, CEM_Y1 = 18, 18

COMPOSE_PASSES = 3


def tile_types(tile: list[str]) -> list[str]:
    """Return the 18 type/texture lines (Manor lines 5–22)."""
    return tile[4:22]


def make_tile(walls: list[str], types: list[str]) -> list[str]:
    return walls + types


def apply_hedge_gate(tile: list[str]) -> None:
    """Iron gate opening on a green hedge segment (Manor lines 19–20)."""
    tile[18] = "1"
    tile[19] = "6"


def extract_manor_templates(
    manor_tiles: dict[tuple[int, int], list[str]],
) -> dict[str, list[str]]:
    """Copy proven 18-line tile type blocks from Manor.txt."""

    def types_at(x: int, y: int) -> list[str]:
        return tile_types(manor_tiles[(x, y)])

    return {
        "hedge": types_at(1, 0),
        "grass": types_at(10, 10),
        "path": types_at(7, 7),
        "cemetery_floor": types_at(12, 14),
        "pond": types_at(10, 13),
        "tree": types_at(17, 11),
        "rock": types_at(14, 9),
        "tombstone": types_at(15, 15),
        "mausoleum": types_at(15, 13),
        "perimeter_gate": types_at(15, 0),
        "bridge": types_at(10, 12),
        "dead_tree": types_at(6, 21),
        "picket_fence": types_at(24, 22),
        "wood_fence": types_at(13, 5),
        "garden_crop": types_at(30, 20),
        "garden_row": types_at(24, 21),
        "garden_till": types_at(28, 21),
        "shop_floor": types_at(25, 4),
        "shop_shelf": types_at(22, 7),
        "fort_floor": types_at(4, 24),
        "house_floor": types_at(4, 13),
    }


def collect_rich_grass_tiles(
    manor_tiles: dict[tuple[int, int], list[str]],
) -> list[list[str]]:
    """Manor open-lawn tiles with visible props (avoid flat single-ID grass)."""
    pool: list[list[str]] = []
    for y in range(GRID):
        for x in range(GRID):
            tile = manor_tiles[(x, y)]
            if tile[:4] != ["0", "0", "0", "0"]:
                continue
            types = tile_types(tile)
            if types[17] != "1":
                continue
            if sum(1 for v in types if v != "0") >= 3:
                pool.append(types[:])
    return pool or [tile_types(manor_tiles[(10, 10)])]


def load_manor_tiles() -> dict[tuple[int, int], list[str]]:
    lines = (GAME / "Manor.txt").read_text().splitlines()
    tiles: dict[tuple[int, int], list[str]] = {}
    for y in range(GRID):
        for x in range(GRID):
            base = (y * GRID + x) * TILE_LINES
            tiles[(x, y)] = lines[base : base + TILE_LINES]
    return tiles


def in_bounds(x: int, y: int) -> bool:
    return 0 <= x < GRID and 0 <= y < GRID


def fill_rect(world: list[list[int]], x0: int, y0: int, x1: int, y1: int, cell: int) -> None:
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if in_bounds(x, y):
                world[y][x] = cell


def fill_points(world: list[list[int]], points: set[tuple[int, int]], cell: int) -> None:
    for x, y in points:
        if in_bounds(x, y):
            world[y][x] = cell


def carve_paths(world: list[list[int]], segments: list[list[tuple[int, int]]]) -> None:
    protected = {
        HEDGE,
        CEMETERY,
        CEM_WALL,
        FENCE,
        POND,
        SHOP,
        HOUSE,
        FORT,
        GARDEN,
        SHED,
        MAUSOLEUM,
        GATE,
        GARDEN_FENCE,
        BOOTH,
    }
    for segment in segments:
        for x, y in segment:
            if not in_bounds(x, y):
                continue
            if world[y][x] in protected:
                continue
            world[y][x] = PATH


def maze_internal_walls() -> set[tuple[int, int]]:
    """Hand-tuned hedge cells matching the reference photo's winding corridors."""
    w: set[tuple[int, int]] = set()

    for x in range(10, 22):
        if x not in (14, 15, 16):
            w.add((x, 10))
    for x in (11, 12, 13, 17, 18, 19, 20):
        w.add((x, 11))
    w |= {(21, 11), (21, 12), (13, 12), (20, 12)}

    for y in range(11, 18):
        w.add((10, y))
    for x in (11, 12, 13):
        w.add((x, 13))
    for x in (11, 12):
        w.add((x, 17))
    w |= {(12, 16), (13, 15)}

    for y in range(11, 22):
        if y != 15:
            w.add((21, y))
    for x in (17, 18, 19, 20):
        w.add((x, 13))
    w |= {(20, 16), (19, 17)}

    for y in (11, 12, 17, 18):
        w.add((14, y))
        w.add((18, y))

    for x in range(10, 22):
        if x not in (14, 15, 16):
            w.add((x, 18))
    for x in (11, 12, 13, 17, 18, 19, 20):
        w.add((x, 19))
    w |= {(10, 20), (10, 21), (13, 20), (21, 20), (12, 21)}
    for x in (16, 17, 18, 19):
        w.add((x, 21))

    cem = {
        (x, y)
        for x in range(CEM_X0 - 1, CEM_X1 + 2)
        for y in range(CEM_Y0 - 1, CEM_Y1 + 2)
    }
    return {p for p in w if p not in cem}


def build_maze_shell(world: list[list[int]]) -> None:
    maze_hedges: set[tuple[int, int]] = set()
    for x in range(MAZE_X0, MAZE_X1 + 1):
        maze_hedges.add((x, MAZE_Y0))
        maze_hedges.add((x, MAZE_Y1))
    for y in range(MAZE_Y0 + 1, MAZE_Y1):
        maze_hedges.add((MAZE_X0, y))
        maze_hedges.add((MAZE_X1, y))
    for gate in MAZE_GATES:
        maze_hedges.discard(gate)
        world[gate[1]][gate[0]] = GATE
    maze_hedges.update(maze_internal_walls())
    fill_points(world, maze_hedges, HEDGE)

    for y in range(MAZE_Y0 + 1, MAZE_Y1):
        for x in range(MAZE_X0 + 1, MAZE_X1):
            if world[y][x] == GRASS:
                world[y][x] = MAZE


def build_cemetery(world: list[list[int]]) -> None:
    fill_rect(world, CEM_X0, CEM_Y0, CEM_X1, CEM_Y1, CEMETERY)
    cem_wall = (
        {(12, y) for y in range(12, 20)}
        | {(19, y) for y in range(12, 20)}
        | {(x, 12) for x in range(12, 20)}
        | {(x, 19) for x in range(12, 20)}
    )
    cem_wall -= {(14, 19), (15, 19), (16, 19)}
    fill_points(world, cem_wall, CEM_WALL)
    fill_rect(world, 14, 12, 16, 12, MAUSOLEUM)


def build_quadrants(world: list[list[int]]) -> None:
    fill_rect(world, 21, 1, 30, 8, SHOP)

    pond_cells = {
        (3, 2), (4, 2), (5, 2), (6, 2),
        (2, 3), (3, 3), (4, 3), (5, 3), (6, 3), (7, 3), (8, 3),
        (2, 4), (3, 4), (4, 4), (5, 4), (6, 4), (7, 4), (8, 4),
        (2, 5), (3, 5), (4, 5), (5, 5), (6, 5), (7, 5),
        (2, 6), (3, 6), (7, 6), (8, 5), (8, 6),
    }
    fill_points(world, pond_cells, POND)
    for x, y in ((5, 6), (6, 6), (5, 7), (6, 7), (5, 8), (6, 8), (5, 9), (6, 9), (5, 10), (6, 10)):
        world[y][x] = BRIDGE

    fill_rect(world, 9, 1, 12, 3, BOOTH)

    fill_rect(world, 2, 11, 7, 18, HEDGE)
    fill_rect(world, 3, 12, 5, 14, HOUSE)
    fill_rect(world, 3, 15, 6, 15, HOUSE)
    world[15][7] = GRASS

    fill_rect(world, 2, 21, 9, 28, FORT)

    fill_rect(world, 21, 22, 28, 28, GARDEN)
    for x in range(21, 29):
        world[22][x] = GARDEN_FENCE
        world[28][x] = GARDEN_FENCE
    for y in range(23, 28):
        world[y][21] = GARDEN_FENCE
        world[y][29] = GARDEN_FENCE
    world[25][21] = GRASS
    fill_rect(world, 28, 23, 30, 26, SHED)


def build_path_network(world: list[list[int]]) -> None:
    def hline(y: int, x0: int, x1: int) -> list[tuple[int, int]]:
        return [(x, y) for x in range(min(x0, x1), max(x0, x1) + 1)]

    def vline(x: int, y0: int, y1: int) -> list[tuple[int, int]]:
        return [(x, y) for y in range(min(y0, y1), max(y0, y1) + 1)]

    carve_paths(
        world,
        [
            vline(15, 1, 30),
            hline(29, 14, 16),
            hline(28, 14, 16),
            hline(27, 14, 16),
            hline(9, 15, 30),
            vline(20, 1, 9),
            hline(11, 1, 14),
            hline(7, 6, 14),
            vline(6, 7, 11),
            hline(4, 6, 12),
            vline(12, 4, 11),
            hline(15, 1, 8),
            hline(15, 24, 30),
            vline(7, 11, 15),
            vline(24, 11, 15),
            vline(7, 14, 18),
            hline(15, 7, 8),
            hline(24, 7, 23),
            hline(25, 7, 23),
            vline(7, 15, 28),
            vline(24, 15, 28),
            hline(28, 7, 15),
            hline(28, 15, 24),
            vline(28, 22, 28),
            hline(20, 23, 28),
            vline(28, 20, 22),
        ],
    )
    world[14][7] = GATE


def finalize_maze(world: list[list[int]]) -> None:
    """Every non-path cell inside the maze shell becomes solid hedge."""
    for y in range(MAZE_Y0, MAZE_Y1 + 1):
        for x in range(MAZE_X0, MAZE_X1 + 1):
            cell = world[y][x]
            if cell in (PATH, GATE, CEMETERY, CEM_WALL, MAUSOLEUM):
                continue
            if cell == MAZE:
                world[y][x] = HEDGE


def scatter_props(world: list[list[int]]) -> None:
    """Fill open lawn with trees/rocks so no large empty grass patches remain."""
    pond_trees = {
        (1, 1), (1, 2), (9, 1), (9, 2), (10, 6), (1, 8), (10, 8), (3, 1), (7, 1),
        (0, 4), (10, 3), (10, 7), (1, 6), (8, 2),
    }
    shop_trees = {(20, 10), (19, 10), (29, 10), (20, 1), (30, 9), (21, 9)}
    fort_trees = {(1, 20), (10, 29), (1, 29), (10, 20), (1, 27), (10, 27)}
    garden_trees = {(20, 29), (30, 21), (30, 29), (20, 21), (30, 27), (20, 27)}
    maze_trees = {
        (9, 9), (22, 9), (9, 22), (22, 22), (11, 9), (20, 22),
        (9, 16), (22, 16), (16, 9), (16, 22),
    }
    path_trees = {
        (14, 1), (16, 1), (14, 30), (16, 30), (1, 15), (30, 15),
        (13, 7), (17, 7), (13, 27), (17, 27),
    }
    corner_rocks = {
        (1, 30), (30, 30), (1, 1), (30, 1), (11, 20), (20, 11),
        (5, 25), (25, 5), (18, 1), (18, 30), (1, 18), (30, 18),
    }
    fill_points(
        world,
        pond_trees | shop_trees | fort_trees | garden_trees | maze_trees | path_trees,
        TREE,
    )
    fill_points(world, corner_rocks, ROCK)

    random.seed(42)
    for x in range(1, 31):
        for y in range(1, 31):
            if world[y][x] not in (GRASS, PATH):
                continue
            h = (x * 17 + y * 31) % 97
            if h < 22 and world[y][x] == GRASS:
                world[y][x] = TREE
            elif h < 28 and world[y][x] == GRASS:
                world[y][x] = ROCK

    for tx, ty in ((14, 16), (17, 16), (15, 17)):
        if world[ty][tx] == CEMETERY:
            world[ty][tx] = DEAD_TREE


def fill_grass_deserts(world: list[list[int]]) -> int:
    """Add props to any 2×2+ grass-only block without nearby decoration."""
    fixed = 0

    def near_prop(x: int, y: int) -> bool:
        for dy in range(-2, 3):
            for dx in range(-2, 3):
                nx, ny = x + dx, y + dy
                if in_bounds(nx, ny) and world[ny][nx] in (TREE, ROCK, DEAD_TREE):
                    return True
        return False

    for y in range(1, 30):
        for x in range(1, 30):
            if world[y][x] != GRASS:
                continue
            block = True
            for dy in range(2):
                for dx in range(2):
                    nx, ny = x + dx, y + dy
                    if not in_bounds(nx, ny) or world[ny][nx] != GRASS:
                        block = False
                        break
                if not block:
                    break
            if not block or near_prop(x, y):
                continue
            world[y][x] = TREE if (x + y) % 2 == 0 else ROCK
            fixed += 1
    return fixed


def build_world() -> list[list[int]]:
    world = [[GRASS] * GRID for _ in range(GRID)]

    for x in range(GRID):
        world[0][x] = HEDGE
        world[GRID - 1][x] = HEDGE
    for y in range(GRID):
        world[y][0] = HEDGE
        world[y][GRID - 1] = HEDGE
    for gx, gy in ((15, 0), (15, GRID - 1), (0, 15), (GRID - 1, 15)):
        world[gy][gx] = GATE

    for x in range(GRID):
        if x != 15:
            world[GRID - 1][x] = FENCE

    build_maze_shell(world)
    build_cemetery(world)
    build_quadrants(world)
    build_path_network(world)
    finalize_maze(world)
    scatter_props(world)
    fill_grass_deserts(world)

    return world


def is_blocked(world: list[list[int]], x: int, y: int) -> bool:
    if not in_bounds(x, y):
        return True
    cell = world[y][x]
    if cell in BLOCKED:
        return True
    if cell in (SHOP, HOUSE, FORT, GARDEN, MAUSOLEUM, GARDEN_FENCE, BOOTH):
        return True
    return False


def wall_flags(world: list[list[int]], x: int, y: int) -> list[str]:
    if is_blocked(world, x, y):
        return ["0", "1", "0", "1"]
    right = 1 if is_blocked(world, x + 1, y) else 0
    top = 1 if is_blocked(world, x, y - 1) else 0
    left = 1 if is_blocked(world, x - 1, y) else 0
    bottom = 1 if is_blocked(world, x, y + 1) else 0
    return [str(right), str(top), str(left), str(bottom)]


def copy_region(
    manor_tiles: dict[tuple[int, int], list[str]],
    sx0: int,
    sy0: int,
    w: int,
    h: int,
) -> dict[tuple[int, int], list[str]]:
    region: dict[tuple[int, int], list[str]] = {}
    for dy in range(h):
        for dx in range(w):
            region[(dx, dy)] = manor_tiles[(sx0 + dx, sy0 + dy)][:]
    return region


def type_nonzero_count(tile: list[str]) -> int:
    return sum(1 for v in tile_types(tile) if v != "0")


def audit_blank_tiles(out_tiles: dict[tuple[int, int], list[str]]) -> list[tuple[int, int]]:
    """Flag tiles whose type lines 5–22 are all zero."""
    blanks: list[tuple[int, int]] = []
    for y in range(GRID):
        for x in range(GRID):
            if all(v == "0" for v in tile_types(out_tiles[(x, y)])):
                blanks.append((x, y))
    return blanks


def audit_sparse_tiles(
    out_tiles: dict[tuple[int, int], list[str]],
    *,
    max_nonzero: int = 2,
) -> list[tuple[int, int]]:
    sparse: list[tuple[int, int]] = []
    for y in range(GRID):
        for x in range(GRID):
            tile = out_tiles[(x, y)]
            if type_nonzero_count(tile) <= max_nonzero and tile[21] in ("0", "1"):
                sparse.append((x, y))
    return sparse


def audit_grass_deserts(world: list[list[int]]) -> list[tuple[int, int]]:
    """Flag 2×2 grass blocks with no props nearby."""
    deserts: list[tuple[int, int]] = []

    def near_prop(x: int, y: int) -> bool:
        for dy in range(-2, 3):
            for dx in range(-2, 3):
                nx, ny = x + dx, y + dy
                if in_bounds(nx, ny) and world[ny][nx] in (TREE, ROCK, DEAD_TREE):
                    return True
        return False

    for y in range(1, 30):
        for x in range(1, 30):
            if world[y][x] != GRASS:
                continue
            block = True
            for dy in range(2):
                for dx in range(2):
                    nx, ny = x + dx, y + dy
                    if not in_bounds(nx, ny) or world[ny][nx] != GRASS:
                        block = False
                        break
                if not block:
                    break
            if block and not near_prop(x, y):
                deserts.append((x, y))
    return deserts


def repair_blank_tiles(
    out_tiles: dict[tuple[int, int], list[str]],
    world: list[list[int]],
    manor_tiles: dict[tuple[int, int], list[str]],
    templates: dict[str, list[str]],
    rich_grass: list[list[str]],
    building_tiles: dict[tuple[int, int], list[str]],
) -> int:
    fixed = 0
    for x, y in audit_blank_tiles(out_tiles):
        walls = wall_flags(world, x, y)
        if (x, y) in building_tiles:
            out_tiles[(x, y)] = building_tiles[(x, y)][:]
        elif world[y][x] == HEDGE:
            out_tiles[(x, y)] = make_tile(walls, templates["hedge"])
        elif world[y][x] == PATH:
            out_tiles[(x, y)] = make_tile(walls, templates["path"])
        elif world[y][x] in (TREE, ROCK, DEAD_TREE):
            key = {TREE: "tree", ROCK: "rock", DEAD_TREE: "dead_tree"}[world[y][x]]
            out_tiles[(x, y)] = make_tile(walls, templates[key])
        else:
            pick = rich_grass[(x * 17 + y * 13) % len(rich_grass)]
            out_tiles[(x, y)] = make_tile(walls, pick)
        fixed += 1
    return fixed


def repair_sparse_tiles(
    out_tiles: dict[tuple[int, int], list[str]],
    world: list[list[int]],
    templates: dict[str, list[str]],
    rich_grass: list[list[str]],
    building_tiles: dict[tuple[int, int], list[str]],
) -> int:
    fixed = 0
    for x, y in audit_sparse_tiles(out_tiles):
        if (x, y) in building_tiles:
            continue
        cell = world[y][x]
        walls = wall_flags(world, x, y)
        if cell == HEDGE:
            out_tiles[(x, y)] = make_tile(walls, templates["hedge"])
            fixed += 1
        elif cell in (GRASS, MAZE):
            pick = rich_grass[(x * 23 + y * 11) % len(rich_grass)]
            out_tiles[(x, y)] = make_tile(walls, pick)
            fixed += 1
        elif cell == PATH:
            out_tiles[(x, y)] = make_tile(walls, templates["path"])
            fixed += 1
        elif cell in (TREE, ROCK, DEAD_TREE):
            key = {TREE: "tree", ROCK: "rock", DEAD_TREE: "dead_tree"}[cell]
            out_tiles[(x, y)] = make_tile(walls, templates[key])
            fixed += 1
    return fixed


def build_building_tiles(
    world: list[list[int]],
    manor_tiles: dict[tuple[int, int], list[str]],
) -> dict[tuple[int, int], list[str]]:
    """Map each building cell to its Manor source tile (never strip art lines)."""
    building: dict[tuple[int, int], list[str]] = {}
    regions = [
        (SHOP, 20, 3, 11, 8, 21, 1),
        (HOUSE, 13, 5, 4, 3, 3, 12),
        (HOUSE, 13, 8, 4, 2, 3, 15),
        (FORT, 2, 20, 8, 8, 2, 21),
        (GARDEN, 23, 22, 8, 7, 21, 22),
        (SHED, 27, 23, 3, 4, 28, 23),
        (BOOTH, 2, 2, 4, 3, 9, 1),
    ]
    for cell_type, sx, sy, w, h, dx, dy in regions:
        region = copy_region(manor_tiles, sx, sy, w, h)
        for (rx, ry), tile in region.items():
            x, y = dx + rx, dy + ry
            if in_bounds(x, y) and world[y][x] == cell_type:
                building[(x, y)] = tile[:]

    cemetery_region = copy_region(manor_tiles, 11, 11, 9, 9)
    for (rx, ry), tile in cemetery_region.items():
        x, y = 11 + rx, 11 + ry
        if not in_bounds(x, y):
            continue
        if world[y][x] in (CEMETERY, CEM_WALL, MAUSOLEUM):
            building[(x, y)] = tile[:]

    bridge_region = copy_region(manor_tiles, 10, 12, 2, 10)
    for (rx, ry), tile in bridge_region.items():
        x, y = 5 + rx, 6 + ry
        if in_bounds(x, y) and world[y][x] == BRIDGE:
            building[(x, y)] = tile[:]

    return building


def render_cell(
    world: list[list[int]],
    x: int,
    y: int,
    templates: dict[str, list[str]],
    rich_grass: list[list[str]],
    building_tiles: dict[tuple[int, int], list[str]],
    manor_tiles: dict[tuple[int, int], list[str]],
) -> list[str]:
    cell = world[y][x]
    walls = wall_flags(world, x, y)

    if (x, y) in building_tiles:
        tile = building_tiles[(x, y)][:]
        tile[:4] = walls
        return tile

    if x == 0 or y == 0 or x == GRID - 1 or y == GRID - 1:
        if cell == FENCE:
            return make_tile(walls, templates["wood_fence"])
        if cell == GATE:
            tile = make_tile(walls, templates["hedge"])
            apply_hedge_gate(tile)
            return tile
        return make_tile(walls, templates["hedge"])

    if cell == HEDGE:
        return make_tile(walls, templates["hedge"])
    if cell == GATE:
        tile = make_tile(walls, templates["hedge"])
        apply_hedge_gate(tile)
        return tile
    if cell == PATH:
        return make_tile(walls, templates["path"])
    if cell == POND:
        return make_tile(walls, templates["pond"])
    if cell == BRIDGE:
        return make_tile(walls, templates["bridge"])
    if cell == CEMETERY:
        return make_tile(walls, templates["cemetery_floor"])
    if cell == CEM_WALL:
        tile = manor_tiles[(11 + (x - 12), 11 + (y - 12))][:]
        tile[:4] = walls
        return tile
    if cell == MAUSOLEUM:
        return make_tile(walls, templates["mausoleum"])
    if cell == TREE:
        return make_tile(walls, templates["tree"])
    if cell == ROCK:
        return make_tile(walls, templates["rock"])
    if cell == DEAD_TREE:
        return make_tile(walls, templates["dead_tree"])
    if cell == FENCE:
        return make_tile(walls, templates["wood_fence"])
    if cell == GARDEN_FENCE:
        return make_tile(walls, templates["picket_fence"])

    pick = rich_grass[(x * 19 + y * 7) % len(rich_grass)]
    return make_tile(walls, pick)


def compose_tiles(
    world: list[list[int]],
    manor_tiles: dict[tuple[int, int], list[str]],
    templates: dict[str, list[str]],
    rich_grass: list[list[str]],
) -> tuple[dict[tuple[int, int], list[str]], dict[str, int]]:
    building_tiles = build_building_tiles(world, manor_tiles)
    out_tiles: dict[tuple[int, int], list[str]] = {}

    for y in range(GRID):
        for x in range(GRID):
            out_tiles[(x, y)] = render_cell(
                world, x, y, templates, rich_grass, building_tiles, manor_tiles
            )

    tombstone_spots = [
        (13, 14), (14, 14), (17, 14), (18, 14),
        (13, 17), (14, 17), (17, 17), (18, 17),
        (15, 15), (16, 15), (13, 15), (18, 15),
    ]
    for tx, ty in tombstone_spots:
        if world[ty][tx] == CEMETERY:
            out_tiles[(tx, ty)] = make_tile(
                wall_flags(world, tx, ty),
                templates["tombstone"],
            )

    garden_rows = ["garden_crop", "garden_row", "garden_till", "garden_row"]
    for y in range(23, 28):
        for x in range(22, 29):
            if world[y][x] != GARDEN:
                continue
            row = garden_rows[(y - 23) % len(garden_rows)]
            out_tiles[(x, y)] = make_tile(wall_flags(world, x, y), templates[row])

    for y in range(1, 9):
        for x in range(21, 31):
            if world[y][x] != SHOP:
                continue
            if out_tiles[(x, y)][20:22] == ["3", "6"]:
                continue
            if type_nonzero_count(out_tiles[(x, y)]) > 2:
                continue
            key = "shop_shelf" if x <= 23 else "shop_floor"
            out_tiles[(x, y)] = make_tile(wall_flags(world, x, y), templates[key])

    for y in range(21, 29):
        for x in range(2, 10):
            if world[y][x] != FORT:
                continue
            if type_nonzero_count(out_tiles[(x, y)]) <= 2:
                out_tiles[(x, y)] = make_tile(
                    wall_flags(world, x, y), templates["fort_floor"]
                )

    for y in range(12, 16):
        for x in range(3, 7):
            if world[y][x] != HOUSE:
                continue
            if type_nonzero_count(out_tiles[(x, y)]) <= 2:
                out_tiles[(x, y)] = make_tile(
                    wall_flags(world, x, y), templates["house_floor"]
                )

    stats = {"blank_fixed": 0, "sparse_fixed": 0, "desert_fixed": 0}
    for pass_num in range(COMPOSE_PASSES):
        stats["blank_fixed"] += repair_blank_tiles(
            out_tiles, world, manor_tiles, templates, rich_grass, building_tiles
        )
        stats["sparse_fixed"] += repair_sparse_tiles(
            out_tiles, world, templates, rich_grass, building_tiles
        )
        deserts = audit_grass_deserts(world)
        if deserts:
            for x, y in deserts:
                world[y][x] = TREE if (x + y) % 2 == 0 else ROCK
                out_tiles[(x, y)] = make_tile(
                    wall_flags(world, x, y),
                    templates["tree" if world[y][x] == TREE else "rock"],
                )
            stats["desert_fixed"] += len(deserts)

    return out_tiles, stats


def build_level(
    world: list[list[int]],
    manor_tiles: dict[tuple[int, int], list[str]],
    templates: dict[str, list[str]],
    rich_grass: list[list[str]],
) -> tuple[list[str], dict[str, int]]:
    out_tiles, stats = compose_tiles(world, manor_tiles, templates, rich_grass)
    level: list[str] = []
    for y in range(GRID):
        for x in range(GRID):
            level.extend(out_tiles[(x, y)])
    return level, stats


def load_manor_spawns() -> list[int]:
    return [int(v) for v in (GAME / "ManorSpawns.txt").read_text().splitlines()]


def build_spawns(world: list[list[int]]) -> list[int]:
    spawns = [-1] * (GRID * GRID)
    manor_spawns = load_manor_spawns()

    player_spots = [
        (14, 28),
        (15, 28),
        (16, 28),
        (15, 29),
        (14, 29),
        (16, 29),
    ]
    for i, (x, y) in enumerate(player_spots):
        spawns[y * GRID + x] = i % 3

    zid = 3
    for y in range(1, GRID - 1):
        for x in range(1, GRID - 1):
            cell = world[y][x]
            idx = y * GRID + x
            if spawns[idx] != -1:
                continue
            if cell in (PATH, GRASS):
                if (x + y) % 4 == 0:
                    spawns[idx] = zid
                    zid = 3 if zid >= 4 else zid + 1
            elif cell == CEMETERY:
                if (x + y) % 2 == 0:
                    spawns[idx] = 4

    for y in range(1, 9):
        for x in range(21, 31):
            idx = y * GRID + x
            mx, my = x - 21 + 20, y - 1 + 3
            manor_val = manor_spawns[my * GRID + mx]
            if manor_val >= 0:
                spawns[idx] = manor_val

    return spawns


def build_path_map(world: list[list[int]]) -> list[str]:
    base_path = (GAME / "PathMap_Manor.txt").read_text().splitlines()
    path = [int(v) for v in base_path]
    if len(path) != PATH_SIZE * PATH_SIZE:
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

    center_py = 16 * SUB + 16
    center_px = 16 * SUB + 16
    dist = [[-1] * PATH_SIZE for _ in range(PATH_SIZE)]
    q: deque[tuple[int, int]] = deque([(center_px, center_py)])
    dist[center_py][center_px] = 0

    while q:
        x, y = q.popleft()
        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < PATH_SIZE and 0 <= ny < PATH_SIZE and not blocked[ny][nx] and dist[ny][nx] == -1:
                dist[ny][nx] = dist[y][x] + 1
                q.append((nx, ny))

    for y in range(PATH_SIZE):
        for x in range(PATH_SIZE):
            if blocked[y][x]:
                continue
            best_val = 0
            best_d = dist[y][x]
            for dx, dy, val in ((0, -1, 1), (1, 0, 2), (0, 1, 3), (-1, 0, 4)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < PATH_SIZE and 0 <= ny < PATH_SIZE and dist[ny][nx] >= 0:
                    if dist[ny][nx] < best_d:
                        best_d = dist[ny][nx]
                        best_val = val
            if best_val:
                path[y * PATH_SIZE + x] = best_val
                continue

            # Path value 0 makes zombies step east off the map (Tile.GetNextTile).
            # Unreachable or plateau cells must not stay at 0.
            if dist[y][x] < 0:
                path[y * PATH_SIZE + x] = 10
                continue

            for dx, dy, val in ((0, -1, 1), (1, 0, 2), (0, 1, 3), (-1, 0, 4)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < PATH_SIZE and 0 <= ny < PATH_SIZE and dist[ny][nx] >= 0:
                    path[y * PATH_SIZE + x] = val
                    break
            else:
                path[y * PATH_SIZE + x] = 10

    zero_walkable = sum(
        1
        for y in range(PATH_SIZE)
        for x in range(PATH_SIZE)
        if not blocked[y][x] and path[y * PATH_SIZE + x] == 0
    )
    if zero_walkable:
        raise SystemExit(f"Path audit failed: {zero_walkable} walkable cells still have path value 0")

    return [str(v) for v in path]


def print_layout(world: list[list[int]]) -> None:
    chars = {
        GRASS: ".",
        HEDGE: "#",
        PATH: "=",
        MAZE: "+",
        CEMETERY: "o",
        CEM_WALL: "O",
        POND: "~",
        FENCE: "-",
        SHOP: "G",
        HOUSE: "H",
        FORT: "F",
        GARDEN: "g",
        SHED: "s",
        TREE: "T",
        ROCK: "r",
        MAUSOLEUM: "M",
        GATE: ">",
        BRIDGE: "b",
        DEAD_TREE: "D",
        GARDEN_FENCE: "f",
        BOOTH: "W",
    }
    for y in range(GRID):
        print("".join(chars.get(world[y][x], "?") for x in range(GRID)))


def zone_footprints(world: list[list[int]]) -> None:
    print("\nZone footprints (logical cells):")
    counts = {
        "Shop": sum(1 for y in range(GRID) for x in range(GRID) if world[y][x] == SHOP),
        "House": sum(1 for y in range(GRID) for x in range(GRID) if world[y][x] == HOUSE),
        "Fort": sum(1 for y in range(GRID) for x in range(GRID) if world[y][x] == FORT),
        "Garden": sum(1 for y in range(GRID) for x in range(GRID) if world[y][x] == GARDEN),
        "Pond": sum(1 for y in range(GRID) for x in range(GRID) if world[y][x] == POND),
        "Cemetery": sum(1 for y in range(GRID) for x in range(GRID) if world[y][x] == CEMETERY),
        "Maze hedge (interior+shell)": sum(
            1 for y in range(MAZE_Y0, MAZE_Y1 + 1) for x in range(MAZE_X0, MAZE_X1 + 1) if world[y][x] == HEDGE
        ),
        "Paths": sum(1 for y in range(GRID) for x in range(GRID) if world[y][x] == PATH),
        "Trees": sum(1 for y in range(GRID) for x in range(GRID) if world[y][x] == TREE),
        "Rocks": sum(1 for y in range(GRID) for x in range(GRID) if world[y][x] == ROCK),
    }
    for label, n in counts.items():
        print(f"  {label:28s} {n:4d} cells")


def print_tile_audit(
    level_lines: list[str],
    manor_tiles: dict[tuple[int, int], list[str]],
    templates: dict[str, list[str]],
) -> None:
    def line22_counts_from_tiles(tiles: dict[tuple[int, int], list[str]]) -> dict[str, int]:
        counts: dict[str, int] = {}
        for y in range(GRID):
            for x in range(GRID):
                v = tiles[(x, y)][21]
                counts[v] = counts.get(v, 0) + 1
        return counts

    hm_tiles: dict[tuple[int, int], list[str]] = {}
    for y in range(GRID):
        for x in range(GRID):
            base = (y * GRID + x) * TILE_LINES
            hm_tiles[(x, y)] = level_lines[base : base + TILE_LINES]

    hm = line22_counts_from_tiles(hm_tiles)
    manor = line22_counts_from_tiles(manor_tiles)

    print("\nTile template sources (Manor line 22 / key type IDs):")
    for name, types in templates.items():
        line22 = types[17]
        extras = {i + 5: v for i, v in enumerate(types) if v != "0"}
        print(f"  {name:16s} line22={line22}  types={extras}")

    print("\nFull-map line-22 distribution:")
    print(f"  HedgeMaze: {dict(sorted(hm.items(), key=lambda kv: -kv[1]))}")
    print(f"  Manor:     {dict(sorted(manor.items(), key=lambda kv: -kv[1]))}")


def print_sample_tiles(level_lines: list[str]) -> None:
    coords = [
        (15, 15, "cemetery center"),
        (26, 5, "shop"),
        (4, 4, "pond"),
        (5, 15, "house"),
        (7, 24, "fort"),
        (25, 25, "garden"),
        (10, 10, "maze hedge"),
        (15, 16, "cemetery path"),
    ]
    print("\nSample tile audits:")
    for x, y, label in coords:
        base = (y * GRID + x) * TILE_LINES
        t = level_lines[base : base + TILE_LINES]
        types = t[4:22]
        nz = sum(1 for v in types if v != "0")
        print(f"  ({x:2d},{y:2d}) {label:16s} walls={t[:4]} nonzero={nz:2d}/18 line22={t[21]}")


def print_alignment_report(world: list[list[int]], store_count: int, audit: dict[str, int]) -> None:
    checks = [
        ("Perimeter hedge N/E/W + fence S", True),
        ("Cardinal perimeter gates", True),
        ("Central maze shell 8-23 with 4 gates", True),
        ("Dense maze hedges (no MAZE grass filler)", not any(
            world[y][x] == MAZE for y in range(MAZE_Y0, MAZE_Y1 + 1) for x in range(MAZE_X0, MAZE_X1 + 1)
        )),
        ("Cemetery 6×6 with mausoleum north + south opening", True),
        ("Gun Shop top-right (21-30, 1-8)", sum(1 for y in range(1, 9) for x in range(21, 31) if world[y][x] == SHOP) >= 60),
        (f"Gun Shop store markers (need 6)", store_count == 6),
        ("Pond + N-S bridge top-left", any(world[y][x] == POND for y in range(2, 8) for x in range(2, 9))),
        ("Ruined L-house + hedge enclosure", any(world[y][x] == HOUSE for y in range(12, 16) for x in range(3, 7))),
        ("Bottom-left fort/shack", any(world[y][x] == FORT for y in range(21, 29) for x in range(2, 10))),
        ("Bottom-right garden + shed", any(world[y][x] == GARDEN for y in range(22, 29) for x in range(21, 29))),
        ("Main N-S stone spine col 15", sum(1 for y in range(1, 31) if world[y][15] == PATH) >= 12),
        ("No blank tiles after compose", audit["final_blanks"] == 0),
        ("No 2×2 grass deserts", audit["final_deserts"] == 0),
    ]
    passed = sum(1 for _, ok in checks if ok)
    print(f"\nAlignment: {passed}/{len(checks)} structural checks passed")
    print(f"Compose repair stats: {audit}")
    print("\nStructural checklist:")
    for label, ok in checks:
        mark = "OK" if ok else "MISS"
        print(f"  [{mark}] {label}")


def main() -> None:
    manor_tiles = load_manor_tiles()
    templates = extract_manor_templates(manor_tiles)
    rich_grass = collect_rich_grass_tiles(manor_tiles)
    world = build_world()

    level_lines, compose_stats = build_level(world, manor_tiles, templates, rich_grass)
    spawn_lines = [str(v) for v in build_spawns(world)]
    path_lines = build_path_map(world)

    assert len(level_lines) == GRID * GRID * TILE_LINES
    assert len(spawn_lines) == GRID * GRID
    assert len(path_lines) == PATH_SIZE * PATH_SIZE

    out_tiles: dict[tuple[int, int], list[str]] = {}
    for y in range(GRID):
        for x in range(GRID):
            base = (y * GRID + x) * TILE_LINES
            out_tiles[(x, y)] = level_lines[base : base + TILE_LINES]

    final_blanks = audit_blank_tiles(out_tiles)
    final_sparse = audit_sparse_tiles(out_tiles)
    final_deserts = audit_grass_deserts(world)
    if final_blanks or final_deserts:
        raise SystemExit(
            f"Audit failed: blanks={len(final_blanks)} sparse={len(final_sparse)} deserts={len(final_deserts)}"
        )

    (GAME / "HedgeMaze.txt").write_text("\n".join(level_lines) + "\n")
    (GAME / "HedgeMazeSpawns.txt").write_text("\n".join(spawn_lines) + "\n")
    (GAME / "PathMap_HedgeMaze.txt").write_text("\n".join(path_lines) + "\n")

    store_count = sum(
        1
        for y in range(1, 9)
        for x in range(21, 31)
        if level_lines[(y * GRID + x) * TILE_LINES + 20 : (y * GRID + x) * TILE_LINES + 22]
        == ["3", "6"]
    )

    audit_report = {
        **compose_stats,
        "final_blanks": len(final_blanks),
        "final_sparse": len(final_sparse),
        "final_deserts": len(final_deserts),
    }

    print("Wrote HedgeMaze.txt, HedgeMazeSpawns.txt, PathMap_HedgeMaze.txt")
    print_layout(world)
    zone_footprints(world)
    print_tile_audit(level_lines, manor_tiles, templates)
    print_sample_tiles(level_lines)
    print_alignment_report(world, store_count, audit_report)


if __name__ == "__main__":
    main()
