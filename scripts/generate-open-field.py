#!/usr/bin/env python3
"""Generate Open Field map — grass interior, perimeter fence, bottom-center gun shop."""

from __future__ import annotations

from collections import deque
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = ROOT / "game"
GRID = 32
TILE_LINES = 22
PATH_SIZE = 1024
SUB = 32

GRASS = 0
FENCE = 7
SHOP = 8
GATE = 16

BLOCKED = {FENCE}

# Manor gun shop source region (11×8) and destination at bottom-center (inside fence).
SHOP_SRC_X, SHOP_SRC_Y = 20, 3
SHOP_W, SHOP_H = 11, 8
SHOP_DX = (GRID - SHOP_W) // 2  # 10 → cells x=10..20
SHOP_DY = GRID - 1 - SHOP_H  # 23 → cells y=23..30 (row 31 = south fence)

# South gate on the fence row for access from outside; player spawns at top center.
SOUTH_GATE = (15, GRID - 1)


def tile_types(tile: list[str]) -> list[str]:
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
    def types_at(x: int, y: int) -> list[str]:
        return tile_types(manor_tiles[(x, y)])

    return {
        "grass": types_at(10, 10),
        "wood_fence": types_at(13, 5),
        "picket_fence": types_at(24, 22),
        "perimeter_gate": types_at(15, 0),
    }


def load_manor_tiles() -> dict[tuple[int, int], list[str]]:
    lines = (GAME / "Manor.txt").read_text().splitlines()
    tiles: dict[tuple[int, int], list[str]] = {}
    for y in range(GRID):
        for x in range(GRID):
            base = (y * GRID + x) * TILE_LINES
            tiles[(x, y)] = lines[base : base + TILE_LINES]
    return tiles


def load_manor_spawns() -> list[int]:
    return [int(v) for v in (GAME / "ManorSpawns.txt").read_text().splitlines()]


def in_bounds(x: int, y: int) -> bool:
    return 0 <= x < GRID and 0 <= y < GRID


def build_world() -> list[list[int]]:
    world = [[GRASS] * GRID for _ in range(GRID)]

    for x in range(GRID):
        world[0][x] = FENCE
        world[GRID - 1][x] = FENCE
    for y in range(GRID):
        world[y][0] = FENCE
        world[y][GRID - 1] = FENCE

    world[SOUTH_GATE[1]][SOUTH_GATE[0]] = GATE

    for dy in range(SHOP_H):
        for dx in range(SHOP_W):
            x, y = SHOP_DX + dx, SHOP_DY + dy
            if in_bounds(x, y):
                world[y][x] = SHOP

    return world


def is_blocked(world: list[list[int]], x: int, y: int) -> bool:
    if not in_bounds(x, y):
        return True
    cell = world[y][x]
    if cell in BLOCKED:
        return True
    if cell == SHOP:
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


def build_shop_tiles(
    manor_tiles: dict[tuple[int, int], list[str]],
) -> dict[tuple[int, int], list[str]]:
    """Copy Manor gun shop tiles verbatim (preserve store markers on lines 21-22)."""
    region = copy_region(manor_tiles, SHOP_SRC_X, SHOP_SRC_Y, SHOP_W, SHOP_H)
    building: dict[tuple[int, int], list[str]] = {}
    for (rx, ry), tile in region.items():
        building[(SHOP_DX + rx, SHOP_DY + ry)] = tile[:]
    return building


def render_cell(
    world: list[list[int]],
    x: int,
    y: int,
    templates: dict[str, list[str]],
    shop_tiles: dict[tuple[int, int], list[str]],
) -> list[str]:
    cell = world[y][x]
    walls = wall_flags(world, x, y)

    if (x, y) in shop_tiles:
        tile = shop_tiles[(x, y)][:]
        tile[:4] = walls
        return tile

    if cell == FENCE:
        return make_tile(walls, templates["wood_fence"])
    if cell == GATE:
        tile = make_tile(walls, templates["perimeter_gate"])
        apply_hedge_gate(tile)
        return tile

    return make_tile(walls, templates["grass"])


def build_level(
    world: list[list[int]],
    templates: dict[str, list[str]],
    shop_tiles: dict[tuple[int, int], list[str]],
) -> list[str]:
    level: list[str] = []
    for y in range(GRID):
        for x in range(GRID):
            level.extend(render_cell(world, x, y, templates, shop_tiles))
    return level


def build_spawns(world: list[list[int]]) -> list[int]:
    spawns = [-1] * (GRID * GRID)
    manor_spawns = load_manor_spawns()

    # Player spawn band near top-center, inside north fence.
    player_spots = [
        (14, 2),
        (15, 2),
        (16, 2),
        (15, 3),
        (14, 3),
        (16, 3),
    ]
    for i, (x, y) in enumerate(player_spots):
        spawns[y * GRID + x] = i % 3

    # Zombie spawn regions on open grass inside the fence.
    zid = 3
    for y in range(1, GRID - 1):
        for x in range(1, GRID - 1):
            if world[y][x] != GRASS:
                continue
            idx = y * GRID + x
            if spawns[idx] != -1:
                continue
            if (x + y) % 4 == 0:
                spawns[idx] = zid
                zid = 3 if zid >= 4 else zid + 1

    # Store spawn band — mirror Manor shop layout onto our shop footprint.
    for y in range(SHOP_DY, SHOP_DY + SHOP_H):
        for x in range(SHOP_DX, SHOP_DX + SHOP_W):
            if world[y][x] != SHOP:
                continue
            mx = x - SHOP_DX + SHOP_SRC_X
            my = y - SHOP_DY + SHOP_SRC_Y
            manor_val = manor_spawns[my * GRID + mx]
            if manor_val >= 0:
                spawns[y * GRID + x] = manor_val

    return spawns


def build_path_map(world: list[list[int]]) -> list[str]:
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
            if (
                0 <= nx < PATH_SIZE
                and 0 <= ny < PATH_SIZE
                and not blocked[ny][nx]
                and dist[ny][nx] == -1
            ):
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
    chars = {GRASS: ".", FENCE: "f", SHOP: "G", GATE: ">"}
    for y in range(GRID):
        print("".join(chars.get(world[y][x], "?") for x in range(GRID)))


def main() -> None:
    manor_tiles = load_manor_tiles()
    templates = extract_manor_templates(manor_tiles)
    assert templates["grass"][17] == "1", "Manor grass template line 22 must be 1"

    world = build_world()
    shop_tiles = build_shop_tiles(manor_tiles)
    level_lines = build_level(world, templates, shop_tiles)
    spawn_lines = [str(v) for v in build_spawns(world)]
    path_lines = build_path_map(world)

    assert len(level_lines) == GRID * GRID * TILE_LINES
    assert len(spawn_lines) == GRID * GRID
    assert len(path_lines) == PATH_SIZE * PATH_SIZE

    store_markers = sum(
        1
        for y in range(SHOP_DY, SHOP_DY + SHOP_H)
        for x in range(SHOP_DX, SHOP_DX + SHOP_W)
        if level_lines[(y * GRID + x) * TILE_LINES + 20 : (y * GRID + x) * TILE_LINES + 22]
        == ["3", "6"]
    )

    interior_grass = sum(
        1 for y in range(1, GRID - 1) for x in range(1, GRID - 1) if world[y][x] == GRASS
    )
    fence_cells = sum(1 for y in range(GRID) for x in range(GRID) if world[y][x] == FENCE)

    (GAME / "OpenField.txt").write_text("\n".join(level_lines) + "\n")
    (GAME / "OpenFieldSpawns.txt").write_text("\n".join(spawn_lines) + "\n")
    (GAME / "PathMap_OpenField.txt").write_text("\n".join(path_lines) + "\n")

    print("Wrote OpenField.txt, OpenFieldSpawns.txt, PathMap_OpenField.txt")
    print(f"Fence tile: Manor wood_fence from (13,5), line22={templates['wood_fence'][17]}")
    print(f"South gate: {SOUTH_GATE} (perimeter_gate from Manor 15,0)")
    print(f"Interior grass cells (1..30): {interior_grass} (30×30 minus shop = 900 - 88 = 812 expected)")
    print(f"Perimeter fence cells: {fence_cells}")
    print(
        f"Shop footprint: x={SHOP_DX}..{SHOP_DX + SHOP_W - 1}, "
        f"y={SHOP_DY}..{SHOP_DY + SHOP_H - 1}"
    )
    print(f"Store tile markers (lines 21-22 = 3,6): {store_markers}")
    print("Player spawns: (14,2), (15,2), (16,2), (15,3), (14,3), (16,3)")
    print_layout(world)


if __name__ == "__main__":
    main()
