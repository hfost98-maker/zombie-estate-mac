#!/usr/bin/env python3
"""Export a map pack from editor JSON (grid of stamp IDs)."""

from __future__ import annotations

import argparse
import json
from collections import deque
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = ROOT / "game"
TEMPLATES = ROOT / "tools" / "map-editor" / "templates.json"

GRID = 32
TILE_LINES = 22
PATH_SIZE = 1024
SUB = 32


def load_library() -> dict:
    return json.loads(TEMPLATES.read_text())


def wall_flags(grid: list[list[str]], stamps: dict[str, dict], x: int, y: int) -> list[str]:
    def blocked(sx: int, sy: int) -> bool:
        if sx < 0 or sy < 0 or sx >= GRID or sy >= GRID:
            return True
        return stamps[grid[sy][sx]]["blocked"]

    if blocked(x, y):
        return ["0", "1", "0", "1"]
    right = 1 if blocked(x + 1, y) else 0
    top = 1 if blocked(x, y - 1) else 0
    left = 1 if blocked(x - 1, y) else 0
    bottom = 1 if blocked(x, y + 1) else 0
    return [str(right), str(top), str(left), str(bottom)]


def compose_tile(grid: list[list[str]], stamps: dict[str, dict], x: int, y: int) -> list[str]:
    stamp = stamps[grid[y][x]]
    return wall_flags(grid, stamps, x, y) + stamp["types"]


def build_level(grid: list[list[str]], stamps: dict[str, dict]) -> list[str]:
    lines: list[str] = []
    for y in range(GRID):
        for x in range(GRID):
            lines.extend(compose_tile(grid, stamps, x, y))
    return lines


def build_spawns(grid: list[list[str]], stamps: dict[str, dict]) -> list[int]:
    spawns = [-1] * (GRID * GRID)
    player_spots = [(14, 2), (15, 2), (16, 2), (15, 3), (14, 3), (16, 3)]
    for i, (x, y) in enumerate(player_spots):
        spawns[y * GRID + x] = i % 3
    zid = 3
    for y in range(1, GRID - 1):
        for x in range(1, GRID - 1):
            idx = y * GRID + x
            if spawns[idx] != -1:
                continue
            if stamps[grid[y][x]]["blocked"]:
                continue
            if (x + y) % 4 == 0:
                spawns[idx] = zid
                zid = 3 if zid >= 4 else zid + 1
    return spawns


def build_path_map(grid: list[list[str]], stamps: dict[str, dict]) -> list[str]:
    blocked = [[False] * PATH_SIZE for _ in range(PATH_SIZE)]
    path = [0] * (PATH_SIZE * PATH_SIZE)

    for ty in range(GRID):
        for tx in range(GRID):
            if not stamps[grid[ty][tx]]["blocked"]:
                continue
            for sy in range(SUB):
                for sx in range(SUB):
                    py = ty * SUB + sy
                    px = tx * SUB + sx
                    blocked[py][px] = True
                    path[py * PATH_SIZE + px] = 10

    center_px = 16 * SUB + 16
    center_py = 16 * SUB + 16
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

    return [str(v) for v in path]


def main() -> None:
    parser = argparse.ArgumentParser(description="Export map pack from editor JSON")
    parser.add_argument("json_file", type=Path, help="Editor export JSON with grid[][] stamp IDs")
    parser.add_argument("-o", "--output-dir", type=Path, default=GAME, help="Output directory")
    parser.add_argument("-n", "--name", default="", help="Map base name (default: from JSON)")
    args = parser.parse_args()

    payload = json.loads(args.json_file.read_text())
    grid = payload["grid"]
    name = args.name or payload.get("name") or "CustomMap"
    safe = "".join(ch for ch in name if ch.isalnum()) or "CustomMap"

    library = load_library()
    default = payload.get("defaultBlockId") or payload.get("defaultStamp", "grass")
    stamp_key = "blocks" if "blocks" in library else "stamps"
    stamps = {s["id"]: s for s in library[stamp_key]}

    for y in range(GRID):
        for x in range(GRID):
            if grid[y][x] not in stamps:
                grid[y][x] = default

    out = args.output_dir
    out.mkdir(parents=True, exist_ok=True)
    (out / f"{safe}.txt").write_text("\n".join(build_level(grid, stamps)) + "\n")
    (out / f"{safe}Spawns.txt").write_text("\n".join(str(v) for v in build_spawns(grid, stamps)) + "\n")
    (out / f"PathMap_{safe}.txt").write_text("\n".join(build_path_map(grid, stamps)) + "\n")
    print(f"Wrote {safe}.txt, {safe}Spawns.txt, PathMap_{safe}.txt to {out}")


if __name__ == "__main__":
    main()
