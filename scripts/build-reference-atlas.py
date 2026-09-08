#!/usr/bin/env python3
"""Bake the carnival reference screenshot into MasterWallTexture (512×512, 16px cells)."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

try:
    from PIL import Image, ImageEnhance
except ImportError:
    print("Install Pillow: pip3 install pillow", file=sys.stderr)
    raise SystemExit(1)

ROOT = Path(__file__).resolve().parent.parent
REF_CANDIDATES = [
    ROOT / "tools/carnival-assets/reference/carnival-reference.jpg",
    Path.home() / ".cursor/projects/Users-hadenfoster-Projects-zombie-estate-mac/assets/image-a70fb186-14c3-4fa6-a26d-f6cb7c43b5fa.jpg",
]
REF_DIR = ROOT / "tools/carnival-assets/reference"
ASSETS = ROOT / "tools/map-editor/assets"
CONTENT = ROOT / "game/Content"
CELL = 16
GRID = 32


def find_reference() -> Path:
    for path in REF_CANDIDATES:
        if path.exists():
            return path
    raise SystemExit(
        "Missing reference image — copy screenshot to tools/carnival-assets/reference/carnival-reference.jpg"
    )


def load_reference(path: Path) -> Image.Image:
    img = Image.open(path).convert("RGBA")
    # Slight contrast boost so paths/tents read clearly in-game.
    img = ImageEnhance.Contrast(img).enhance(1.14)
    img = ImageEnhance.Color(img).enhance(1.18)
    img = ImageEnhance.Brightness(img).enhance(1.06)
    return img.resize((512, 512), Image.Resampling.LANCZOS)


def build_atlas(ref512: Image.Image, base: Image.Image) -> Image.Image:
    """Reference fills the atlas; preserve hedge row + carnival 3D/path tile slots."""
    atlas = base.copy().convert("RGBA")
    atlas.paste(ref512, (0, 0))
    hedge = base.crop((0, 0, CELL, CELL))
    for cx in range(GRID):
        atlas.paste(hedge, (cx * CELL, 0))

    # Re-stamp carnival 3D wall faces + stone path tiles (rows 28–31) over reference.
    sys.path.insert(0, str(ROOT / "scripts"))
    from carnival_pixel_art import WALL_SLOTS, paste_cell, paint_wall_atlas  # noqa: E402

    overlay = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    painted = paint_wall_atlas(overlay)
    for name, (cx, cy) in WALL_SLOTS.items():
        tile = painted.crop((cx * CELL, cy * CELL, (cx + 1) * CELL, (cy + 1) * CELL))
        paste_cell(atlas, cx, cy, tile)
    return atlas


def save_grid_preview(ref512: Image.Image, out: Path) -> None:
    preview = ref512.resize((GRID, GRID), Image.Resampling.NEAREST).resize(
        (GRID * 8, GRID * 8), Image.Resampling.NEAREST
    )
    preview.save(out)


def main() -> None:
    REF_DIR.mkdir(parents=True, exist_ok=True)
    src = find_reference()
    if src != REF_CANDIDATES[0]:
        REF_CANDIDATES[0].write_bytes(src.read_bytes())

    ref512 = load_reference(src)
    ref512.save(REF_DIR / "reference-512.png")
    save_grid_preview(ref512, REF_DIR / "reference-grid-preview.png")

    subprocess.run(
        [sys.executable, str(ROOT / "scripts/extract-xnb-texture.py")],
        check=False,
    )
    base_path = ASSETS / "MasterWallTexture.png"
    if not base_path.exists():
        raise SystemExit("Run extract-xnb-texture.py first")

    base = Image.open(base_path)
    atlas = build_atlas(ref512, base)
    atlas.save(ASSETS / "MasterWallTexture.reference.png")

    pack = ROOT / "scripts/pack-xnb-texture.py"
    xnb = CONTENT / "MasterWallTexture.xnb"
    if xnb.exists():
        out = ASSETS / "MasterWallTexture.reference.png"
        subprocess.run([sys.executable, str(pack), str(xnb), str(out)], check=True)
        out.replace(ASSETS / "MasterWallTexture.png")
        print(f"Packed {xnb}")

    # Also refresh clown sprites in MasterGrid
    subprocess.run([sys.executable, str(ROOT / "scripts/build-carnival-assets.py")], check=False)
    print(f"Reference atlas from {src}")
    print(f"Wrote {REF_DIR / 'reference-512.png'}")


if __name__ == "__main__":
    main()
