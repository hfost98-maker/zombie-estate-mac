#!/usr/bin/env python3
"""Build carnival pixel art atlases and pack into game XNB files."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Install Pillow: pip3 install pillow", file=sys.stderr)
    raise SystemExit(1)

ROOT = Path(__file__).resolve().parent.parent
GAME = ROOT / "game"
ASSETS = ROOT / "tools" / "map-editor" / "assets"
CONTENT = GAME / "Content"
EXTERNAL = ROOT / "tools" / "carnival-assets" / "external"

sys.path.insert(0, str(ROOT / "scripts"))
from carnival_pixel_art import (  # noqa: E402
    CELL,
    WALL_SLOTS,
    paint_grid_atlas,
    paint_wall_atlas,
    paste_cell,
)


def import_external_pngs(wall: Image.Image) -> int:
    """Optional: drop 16×16 PNGs named like fair_grass.png into tools/carnival-assets/external/."""
    if not EXTERNAL.is_dir():
        return 0
    count = 0
    for name, (cx, cy) in WALL_SLOTS.items():
        for ext in (".png", ".PNG"):
            path = EXTERNAL / f"{name}{ext}"
            if not path.exists():
                continue
            tile = Image.open(path).convert("RGBA")
            if tile.size != (CELL, CELL):
                tile = tile.resize((CELL, CELL), Image.NEAREST)
            paste_cell(wall, cx, cy, tile)
            count += 1
            break
    return count


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)
    EXTERNAL.mkdir(parents=True, exist_ok=True)

    import os
    ref_atlas = ROOT / "tools/carnival-assets/reference/reference-512.png"
    grid_only = ref_atlas.exists() and os.environ.get("CARNIVAL_REPAINT_WALL", "") != "1"

    subprocess.run(
        [sys.executable, str(ROOT / "scripts" / "extract-xnb-texture.py")],
        check=False,
    )

    wall_src = ASSETS / "MasterWallTexture.png"
    grid_src = ASSETS / "MasterGrid.png"
    if not wall_src.exists() or not grid_src.exists():
        raise SystemExit("Missing atlas PNGs — run extract-xnb-texture.py first")

    if grid_only:
        wall = Image.open(wall_src)
        imported = 0
        print("Reference atlas present — keeping wall texture, updating grid only")
    else:
        wall = paint_wall_atlas(Image.open(wall_src))
        imported = import_external_pngs(wall)
    grid = paint_grid_atlas(Image.open(grid_src))

    wall_out = ASSETS / "MasterWallTexture.carnival.png"
    grid_out = ASSETS / "MasterGrid.carnival.png"
    wall.save(wall_out)
    grid.save(grid_out)
    if grid_only:
        print("Painted MasterGrid clown sprites (wall atlas unchanged)")
    else:
        print(f"Painted {len(WALL_SLOTS)} carnival wall cells (+{imported} external imports)")
    print(f"Wrote {wall_out}")
    print(f"Wrote {grid_out}")

    pack = ROOT / "scripts" / "pack-xnb-texture.py"
    packs = [(grid_out, CONTENT / "MasterGrid.xnb")]
    if not grid_only:
        packs.insert(0, (wall_out, CONTENT / "MasterWallTexture.xnb"))
    for png, xnb in packs:
        if xnb.exists():
            subprocess.run([sys.executable, str(pack), str(xnb), str(png)], check=True)
        else:
            print(f"Skip pack (missing {xnb})")

    if not grid_only:
        wall_out.replace(ASSETS / "MasterWallTexture.png")
    grid_out.replace(ASSETS / "MasterGrid.png")
    print("Packed game/Content/" + ("MasterGrid.xnb" if grid_only else "MasterWallTexture.xnb + MasterGrid.xnb"))


if __name__ == "__main__":
    main()
