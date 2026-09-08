#!/usr/bin/env python3
"""Slice a 16×16 tile spritesheet into tools/carnival-assets/external/ PNGs.

Usage:
  python3 scripts/import-external-tileset.py SHEET.png [--cols N]

Mapping file (optional): tools/carnival-assets/external/sheet_map.json
  { "fair_grass": [0, 0], "gravel": [1, 4], ... }  # col, row in sheet
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Install Pillow: pip3 install pillow", file=sys.stderr)
    raise SystemExit(1)

ROOT = Path(__file__).resolve().parent.parent
EXTERNAL = ROOT / "tools" / "carnival-assets" / "external"
MAP_FILE = EXTERNAL / "sheet_map.json"
CELL = 16


def slice_tile(sheet: Image.Image, col: int, row: int) -> Image.Image:
    x, y = col * CELL, row * CELL
    return sheet.crop((x, y, x + CELL, y + CELL)).convert("RGBA")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("sheet", type=Path, help="16×16 grid spritesheet PNG")
    ap.add_argument("--cols", type=int, default=0, help="Columns in sheet (auto-detect if 0)")
    args = ap.parse_args()

    if not args.sheet.exists():
        raise SystemExit(f"Missing {args.sheet}")

    sheet = Image.open(args.sheet).convert("RGBA")
    cols = args.cols or sheet.width // CELL
    rows = sheet.height // CELL
    EXTERNAL.mkdir(parents=True, exist_ok=True)

    if MAP_FILE.exists():
        mapping: dict[str, list[int]] = json.loads(MAP_FILE.read_text())
    else:
        mapping = {}

    if not mapping:
        print(f"No {MAP_FILE.name} — writing preview grid only.")
        preview = Image.new("RGBA", (cols * CELL, rows * CELL), (0, 0, 0, 0))
        for row in range(rows):
            for col in range(cols):
                preview.paste(slice_tile(sheet, col, row), (col * CELL, row * CELL))
        out = EXTERNAL / "sheet_preview.png"
        preview.save(out)
        print(f"Sheet is {cols}×{rows} tiles. Wrote {out}")
        print(f"Create {MAP_FILE.name} with slot → [col, row] entries, then re-run.")
        return

    count = 0
    for name, pos in mapping.items():
        if len(pos) != 2:
            continue
        col, row = pos
        if col >= cols or row >= rows:
            print(f"Skip {name}: ({col},{row}) out of range")
            continue
        tile = slice_tile(sheet, col, row)
        path = EXTERNAL / f"{name}.png"
        tile.save(path)
        print(f"Wrote {path.name} from sheet ({col},{row})")
        count += 1
    print(f"Exported {count} tiles to {EXTERNAL}")


if __name__ == "__main__":
    main()
