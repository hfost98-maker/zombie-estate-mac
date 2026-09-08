#!/usr/bin/env python3
"""Convert a reference screenshot into placeable map assets and a game-ready map pack.

Workflow:
  1. Align the map region (4 corners) — use tools/map-editor screenshot tab or --corners JSON
  2. Run this script on the aligned 512×512 PNG (or raw screenshot + corners)
  3. Unique 16×16 tiles are written into MasterWallTexture atlas slots (rows 24–31)
  4. Exports layout + spawns + path + map-editor JSON

Example:
  python3 scripts/screenshot_to_assets.py ~/Desktop/reference.jpg \\
    --name ReferenceMap --corners tools/map-editor/last-corners.json --pack-atlas
"""

from __future__ import annotations

import argparse
import json
import math
import subprocess
import sys
from collections import deque
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:
    print("Install Pillow: pip3 install pillow", file=sys.stderr)
    raise SystemExit(1)

ROOT = Path(__file__).resolve().parent.parent
GAME = ROOT / "game"
ASSETS = ROOT / "tools" / "map-editor" / "assets"
CONTENT = GAME / "Content"
OUT_DIR = ROOT / "tools" / "screenshot-import"

GRID = 32
TILE_LINES = 22
CELL = 16
WARP_SIZE = GRID * CELL  # 512
PATH_SIZE = 1024
SUB = 32

# Unused atlas band for imported screenshot tiles (512×512 atlas, 16px cells).
SLOT_ROWS = range(24, 32)
SLOT_COLS = range(0, 32)


def load_corners(path: Path | None, img: Image.Image) -> tuple[tuple[float, float], ...]:
    w, h = img.size
    if path and path.exists():
        data = json.loads(path.read_text())
        pts = data.get("corners") or data.get("points") or data
        if isinstance(pts, list) and len(pts) == 4:
            return tuple(tuple(p) for p in pts)
    # Default diamond for isometric ZE screenshots (north, east, south, west).
    return (
        (w * 0.50, h * 0.08),
        (w * 0.92, h * 0.50),
        (w * 0.50, h * 0.92),
        (w * 0.08, h * 0.50),
    )


def warp_to_orthographic(img: Image.Image, corners: tuple[tuple[float, float], ...]) -> Image.Image:
    """Perspective-warp quadrilateral (top, right, bottom, left) to 512×512 orthographic."""
    src = [c for pt in corners for c in pt]
    dst = [0, 0, WARP_SIZE, 0, WARP_SIZE, WARP_SIZE, 0, WARP_SIZE]
    return img.convert("RGBA").transform((WARP_SIZE, WARP_SIZE), Image.QUAD, src + dst, Image.BICUBIC)


def extract_cells(warped: Image.Image) -> list[list[Image.Image]]:
    cells: list[list[Image.Image]] = []
    for y in range(GRID):
        row: list[Image.Image] = []
        for x in range(GRID):
            patch = warped.crop((x * CELL, y * CELL, (x + 1) * CELL, (y + 1) * CELL))
            row.append(patch.convert("RGBA"))
        cells.append(row)
    return cells


def cell_signature(cell: Image.Image) -> tuple[int, ...]:
    small = cell.resize((8, 8), Image.NEAREST).convert("RGB")
    sig: list[int] = []
    for px in small.get_flattened_data():
        if isinstance(px, (tuple, list)):
            r, g, b = px[0], px[1], px[2]
        else:
            break
        sig.extend((r >> 3, g >> 3, b >> 3))
    if len(sig) < 192:
        flat = list(small.get_flattened_data())
        sig = []
        for i in range(0, min(len(flat) - 2, 192 * 3), 3):
            sig.extend((int(flat[i]) >> 3, int(flat[i + 1]) >> 3, int(flat[i + 2]) >> 3))
    return tuple(sig)


def rgb_mean(cell: Image.Image) -> tuple[float, float, float]:
    data = cell.resize((8, 8), Image.NEAREST).convert("RGB").get_flattened_data()
    rs = gs = bs = 0.0
    n = 0
    for px in data:
        if isinstance(px, (tuple, list)):
            r, g, b = px[0], px[1], px[2]
        else:
            # flat r,g,b,... stream
            continue
        rs += r
        gs += g
        bs += b
        n += 1
    if n == 0:
        flat = list(cell.resize((8, 8), Image.NEAREST).convert("RGB").get_flattened_data())
        n = max(len(flat) // 3, 1)
        for i in range(0, len(flat) - 2, 3):
            rs += flat[i]
            gs += flat[i + 1]
            bs += flat[i + 2]
    else:
        n = max(n, 1)
    return (rs / n, gs / n, bs / n)


def sig_distance(a: tuple[int, ...], b: tuple[int, ...]) -> float:
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(a, b)))


def cluster_cells(cells: list[list[Image.Image]], max_palette: int, merge_threshold: float) -> tuple[list[list[int]], list[Image.Image]]:
    flat = [cells[y][x] for y in range(GRID) for x in range(GRID)]
    sigs = [cell_signature(c) for c in flat]
    labels = list(range(len(flat)))

    def find(label: int) -> int:
        while labels[label] != label:
            labels[label] = labels[labels[label]]
            label = labels[label]
        return label

    def union(a: int, b: int) -> None:
        ra, rb = find(a), find(b)
        if ra != rb:
            labels[rb] = ra

    def cluster_ids() -> dict[int, list[int]]:
        groups: dict[int, list[int]] = {}
        for i in range(len(flat)):
            groups.setdefault(find(i), []).append(i)
        return groups

    threshold = merge_threshold
    for _ in range(8):
        for y in range(GRID):
            for x in range(GRID):
                i = y * GRID + x
                for nx, ny in ((x + 1, y), (x, y + 1)):
                    if nx >= GRID or ny >= GRID:
                        continue
                    j = ny * GRID + nx
                    if sig_distance(sigs[i], sigs[j]) <= threshold:
                        union(i, j)
        if len(cluster_ids()) <= max_palette:
            break
        threshold *= 1.35

    groups = cluster_ids()
    if len(groups) > max_palette:
        # Final pass: bucket by coarse RGB and merge buckets.
        bucket_for: dict[int, int] = {}
        bucket_members: dict[int, list[int]] = {}
        for root, members in groups.items():
            r, g, b = rgb_mean(flat[members[0]])
            bucket = (int(r) >> 4, int(g) >> 4, int(b) >> 4)
            if bucket not in bucket_members:
                bucket_members[bucket] = []
            bucket_members[bucket].extend(members)
        buckets = list(bucket_members.values())
        while len(buckets) > max_palette:
            # merge two closest buckets by mean RGB
            means = [rgb_mean(flat[b[0]]) for b in buckets]
            best = (1e9, 0, 1)
            for i in range(len(buckets)):
                for j in range(i + 1, len(buckets)):
                    d = sum((means[i][k] - means[j][k]) ** 2 for k in range(3))
                    if d < best[0]:
                        best = (d, i, j)
            _, i, j = best
            buckets[i].extend(buckets.pop(j))
        for members in buckets:
            root = members[0]
            for m in members[1:]:
                union(root, m)

    groups = cluster_ids()
    remap = {old: idx for idx, old in enumerate(groups.keys())}
    grid_labels = [[0] * GRID for _ in range(GRID)]
    cluster_proto: list[Image.Image] = []

    for old, members in groups.items():
        pick = sorted(members, key=lambda m: sum(rgb_mean(flat[m])))[len(members) // 2]
        cluster_proto.append(flat[pick])

    for y in range(GRID):
        for x in range(GRID):
            i = y * GRID + x
            grid_labels[y][x] = remap[find(i)]

    return grid_labels, cluster_proto


def iter_slots() -> list[tuple[int, int]]:
    slots: list[tuple[int, int]] = []
    for row in SLOT_ROWS:
        for col in SLOT_COLS:
            slots.append((col, row))
    return slots


def classify_blocked(proto: Image.Image) -> bool:
    r, g, b = rgb_mean(proto)
    lum = 0.299 * r + 0.587 * g + 0.114 * b
    sat = max(r, g, b) - min(r, g, b)
    # Dark masses — buildings, fences, water shadow.
    if lum < 42:
        return True
    # Strong red/white carnival structures.
    if r > 140 and sat > 60 and g < 120:
        return True
    # Deep blue water.
    if b > r + 25 and b > g + 15 and lum < 120:
        return True
    # Hedge green boundary (very dark green).
    if g > r and g > b and lum < 70:
        return True
    return False


def floor_line22(r: float, g: float, b: float, blocked: bool) -> int:
    if blocked:
        return 31 if g > r else 5
    lum = 0.299 * r + 0.587 * g + 0.114 * b
    if b > r + 20 and b > g:
        return 0
    if abs(r - g) < 25 and abs(g - b) < 25 and 90 < lum < 180:
        return 2  # path/gravel
    if g > r and g > b:
        return 1  # grass
    if r > 100 and g < 90:
        return 3  # dirt/cemetery
    return 1


def make_types(fx: int, fy: int, proto: Image.Image, blocked: bool) -> list[str]:
    r, g, b = rgb_mean(proto)
    types = ["0"] * 18
    types[16] = str(fx)
    types[17] = str(fy)
    if blocked:
        # Wall faces sample same atlas cell on all sides for extrusion.
        for i in (8, 9, 10, 11, 12, 13, 14, 15):
            types[i] = str(fx if i % 2 == 0 else fy)
    return types


def wall_flags(blocked_grid: list[list[bool]], x: int, y: int) -> list[str]:
    def blocked_at(sx: int, sy: int) -> bool:
        if sx < 0 or sy < 0 or sx >= GRID or sy >= GRID:
            return True
        return blocked_grid[sy][sx]

    if blocked_at(x, y):
        return ["0", "1", "0", "1"]
    return [
        "1" if blocked_at(x + 1, y) else "0",
        "1" if blocked_at(x, y - 1) else "0",
        "1" if blocked_at(x - 1, y) else "0",
        "1" if blocked_at(x, y + 1) else "0",
    ]


def build_level(
    labels: list[list[int]],
    slot_for: dict[int, tuple[int, int]],
    protos: list[Image.Image],
    blocked_grid: list[list[bool]],
) -> list[str]:
    lines: list[str] = []
    for y in range(GRID):
        for x in range(GRID):
            cid = labels[y][x]
            fx, fy = slot_for[cid]
            proto = protos[cid]
            types = make_types(fx, fy, proto, blocked_grid[y][x])
            lines.extend(wall_flags(blocked_grid, x, y) + types)
    return lines


def build_spawns(blocked_grid: list[list[bool]]) -> list[int]:
    spawns = [-1] * (GRID * GRID)
    for i, (x, y) in enumerate([(14, 29), (15, 29), (16, 29), (15, 28), (14, 28), (16, 28)]):
        if not blocked_grid[y][x]:
            spawns[y * GRID + x] = i % 3
    zid = 3
    for y in range(1, GRID - 1):
        for x in range(1, GRID - 1):
            idx = y * GRID + x
            if spawns[idx] != -1 or blocked_grid[y][x]:
                continue
            if (x + y) % 4 == 0:
                spawns[idx] = zid
                zid = 3 if zid >= 4 else zid + 1
    return spawns


def build_path_map(blocked_grid: list[list[bool]]) -> list[str]:
    blocked = [[False] * PATH_SIZE for _ in range(PATH_SIZE)]
    path = [0] * (PATH_SIZE * PATH_SIZE)
    for ty in range(GRID):
        for tx in range(GRID):
            if not blocked_grid[ty][tx]:
                continue
            for sy in range(SUB):
                for sx in range(SUB):
                    py = ty * SUB + sy
                    px = tx * SUB + sx
                    blocked[py][px] = True
                    path[py * PATH_SIZE + px] = 10

    cx = cy = 16 * SUB + 16
    dist = [[-1] * PATH_SIZE for _ in range(PATH_SIZE)]
    q: deque[tuple[int, int]] = deque([(cx, cy)])
    dist[cy][cx] = 0
    while q:
        x, y = q.popleft()
        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < PATH_SIZE and 0 <= ny < PATH_SIZE and not blocked[ny][nx] and dist[ny][nx] == -1:
                dist[ny][nx] = dist[y][x] + 1
                q.append((nx, ny))

    out: list[str] = []
    for y in range(PATH_SIZE):
        for x in range(PATH_SIZE):
            if blocked[y][x]:
                out.append("10")
                continue
            best_val = 0
            best_d = dist[y][x]
            for dx, dy, val in ((0, -1, 1), (1, 0, 2), (0, 1, 3), (-1, 0, 4)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < PATH_SIZE and 0 <= ny < PATH_SIZE and dist[ny][nx] >= 0 and dist[ny][nx] < best_d:
                    best_d = dist[ny][nx]
                    best_val = val
            if best_val:
                out.append(str(best_val))
            elif dist[y][x] < 0:
                out.append("10")
            else:
                out.append("2")
    return out


def paste_into_atlas(atlas: Image.Image, slot_for: dict[int, tuple[int, int]], protos: list[Image.Image]) -> None:
    for cid, (fx, fy) in slot_for.items():
        tile = protos[cid].resize((CELL, CELL), Image.NEAREST)
        atlas.paste(tile, (fx * CELL, fy * CELL))


def build_editor_blocks(
    name: str,
    slot_for: dict[int, tuple[int, int]],
    protos: list[Image.Image],
    blocked_flags: list[bool],
) -> dict:
    blocks = []
    for cid, (fx, fy) in slot_for.items():
        proto = protos[cid]
        r, g, b = rgb_mean(proto)
        l22 = floor_line22(r, g, b, blocked_flags[cid])
        blocks.append(
            {
                "id": f"ss_{cid:03d}",
                "name": f"Screenshot {cid} ({fx},{fy})",
                "category": "boundaries" if blocked_flags[cid] else "terrain",
                "line22": l22,
                "line22Name": "imported",
                "color": f"#{int(r):02x}{int(g):02x}{int(b):02x}",
                "blocked": blocked_flags[cid],
                "types": make_types(fx, fy, proto, blocked_flags[cid]),
                "overlayCount": 1,
                "sources": [{"map": name, "x": -1, "y": -1}],
                "primaryMap": name,
            }
        )
    return {
        "version": 1,
        "source": name,
        "defaultBlockId": blocks[0]["id"] if blocks else "",
        "blocks": blocks,
    }


def pack_atlas_png(png_path: Path) -> None:
    pack = ROOT / "scripts" / "pack-xnb-texture.py"
    xnb = CONTENT / "MasterWallTexture.xnb"
    if pack.exists() and xnb.exists():
        subprocess.run([sys.executable, str(pack), str(xnb), str(png_path)], check=True)


def save_preview(warped: Image.Image, labels: list[list[int]], protos: list[Image.Image], slot_for: dict[int, tuple[int, int]], path: Path) -> None:
    preview = warped.copy()
    draw = ImageDraw.Draw(preview)
    for y in range(GRID):
        for x in range(GRID):
            fx, fy = slot_for[labels[y][x]]
            draw.rectangle((x * CELL, y * CELL, x * CELL + CELL - 1, y * CELL + CELL - 1), outline=(255, 255, 0, 180))
            draw.text((x * CELL + 1, y * CELL + 1), f"{fx},{fy}", fill=(255, 255, 0))
    preview.save(path)


def main() -> None:
    ap = argparse.ArgumentParser(description="Screenshot → atlas tiles + map pack")
    ap.add_argument("image", type=Path, help="Screenshot or pre-aligned 512×512 PNG")
    ap.add_argument("-n", "--name", default="ScreenshotMap", help="Map base name")
    ap.add_argument("-o", "--output-dir", type=Path, default=GAME, help="Write map triplet here")
    ap.add_argument("--corners", type=Path, help="JSON with 4 corner points [[x,y],...]")
    ap.add_argument("--max-palette", type=int, default=48, help="Max unique tile clusters")
    ap.add_argument("--merge-threshold", type=float, default=18.0, help="Lower = more unique tiles")
    ap.add_argument("--aligned", action="store_true", help="Image is already 512×512 orthographic")
    ap.add_argument("--pack-atlas", action="store_true", help="Pack MasterWallTexture.xnb from updated PNG")
    ap.add_argument("--no-atlas", action="store_true", help="Skip writing atlas PNG (layout only)")
    args = ap.parse_args()

    if not args.image.exists():
        raise SystemExit(f"Missing {args.image}")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    safe = "".join(ch for ch in args.name if ch.isalnum()) or "ScreenshotMap"

    img = Image.open(args.image)
    if args.aligned or (img.size == (WARP_SIZE, WARP_SIZE)):
        warped = img.convert("RGBA").resize((WARP_SIZE, WARP_SIZE), Image.LANCZOS)
    else:
        corners = load_corners(args.corners, img)
        warped = warp_to_orthographic(img, corners)
        warped.save(OUT_DIR / f"{safe}-aligned.png")

    cells = extract_cells(warped)
    labels, protos = cluster_cells(cells, args.max_palette, args.merge_threshold)

    slots = iter_slots()
    if len(protos) > len(slots):
        raise SystemExit(f"Too many unique tiles ({len(protos)}); raise --merge-threshold or --max-palette")

    slot_for = {cid: slots[cid] for cid in range(len(protos))}
    blocked_flags = [classify_blocked(p) for p in protos]
    blocked_grid = [[blocked_flags[labels[y][x]] for x in range(GRID)] for y in range(GRID)]

    level = build_level(labels, slot_for, protos, blocked_grid)
    spawns = build_spawns(blocked_grid)
    path = build_path_map(blocked_grid)

    args.output_dir.mkdir(parents=True, exist_ok=True)
    (args.output_dir / f"{safe}.txt").write_text("\n".join(level) + "\n")
    (args.output_dir / f"{safe}Spawns.txt").write_text("\n".join(str(v) for v in spawns) + "\n")
    (args.output_dir / f"PathMap_{safe}.txt").write_text("\n".join(path) + "\n")

    editor_blocks = build_editor_blocks(safe, slot_for, protos, blocked_flags)
    blocks_path = OUT_DIR / f"{safe}-blocks.json"
    blocks_path.write_text(json.dumps(editor_blocks, indent=2))

    # Map-editor project JSON (grid of block IDs).
    id_for = {cid: f"ss_{cid:03d}" for cid in slot_for}
    grid = [[id_for[labels[y][x]] for x in range(GRID)] for y in range(GRID)]
    project = {
        "version": 2,
        "name": safe,
        "defaultBlockId": editor_blocks["defaultBlockId"],
        "grid": grid,
        "screenshotBlocks": str(blocks_path.relative_to(ROOT)),
    }
    (OUT_DIR / f"{safe}.map.json").write_text(json.dumps(project, indent=2))

    save_preview(warped, labels, protos, slot_for, OUT_DIR / f"{safe}-preview.png")

    if not args.no_atlas:
        subprocess.run([sys.executable, str(ROOT / "scripts" / "extract-xnb-texture.py")], check=False)
        atlas_path = ASSETS / "MasterWallTexture.png"
        if atlas_path.exists():
            atlas = Image.open(atlas_path).convert("RGBA")
            paste_into_atlas(atlas, slot_for, protos)
            out_png = OUT_DIR / f"MasterWallTexture.{safe}.png"
            atlas.save(out_png)
            if args.pack_atlas:
                pack_atlas_png(out_png)
                # Also refresh map-editor asset
                atlas.save(ASSETS / "MasterWallTexture.png")
            print(f"Atlas patch: {out_png}")
        else:
            print("Skip atlas — run extract-xnb-texture.py first")

    print(f"Wrote {safe}.txt, {safe}Spawns.txt, PathMap_{safe}.txt")
    print(f"Editor blocks: {blocks_path}")
    print(f"Preview: {OUT_DIR / f'{safe}-preview.png'}")
    print(f"Unique tiles: {len(protos)} | Blocked clusters: {sum(blocked_flags)}")
    print("Load in map editor: import project JSON or game/ map file; use 3D Preview to validate.")


if __name__ == "__main__":
    main()
