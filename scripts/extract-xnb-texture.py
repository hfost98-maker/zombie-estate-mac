#!/usr/bin/env python3
"""Extract Texture2D .xnb files to PNG for the map editor."""

from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Install Pillow: pip3 install pillow", file=sys.stderr)
    raise SystemExit(1)

ROOT = Path(__file__).resolve().parent.parent
DEFAULT_OUT = ROOT / "tools" / "map-editor" / "assets"


def read7bit_int(data: bytes, pos: int) -> tuple[int, int]:
    value = 0
    shift = 0
    while True:
        if pos >= len(data):
            raise ValueError("Truncated 7-bit int")
        b = data[pos]
        pos += 1
        value |= (b & 0x7F) << shift
        if not (b & 0x80):
            return value, pos
        shift += 7
        if shift > 35:
            raise ValueError("Invalid 7-bit int")


def find_texture_payload(data: bytes) -> tuple[int, int, int, bytes]:
    """Locate width/height/payload for XNB Texture2D (XNA 3.1 style)."""
    for pos in range(10, min(len(data) - 16, 256)):
        width = struct.unpack_from("<i", data, pos)[0]
        height = struct.unpack_from("<i", data, pos + 4)[0]
        if width <= 0 or height <= 0 or width > 4096 or height > 4096:
            continue
        if width != height and max(width, height) > 2048:
            continue
        expected = width * height * 4
        end = pos + 16 + expected
        if end == len(data):
            return pos, width, height, data[pos + 16 : end]
    raise ValueError("Could not locate Texture2D payload")


def parse_texture2d(path: Path) -> tuple[int, int, bytes]:
    data = path.read_bytes()
    if data[:3] != b"XNB":
        raise ValueError(f"{path.name}: not an XNB file")
    _, width, height, tex_data = find_texture_payload(data)
    return width, height, tex_data


def save_rgba(path: Path, width: int, height: int, tex_data: bytes) -> None:
    img = Image.frombytes("RGBA", (width, height), tex_data)
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, optimize=True)
    print(f"Wrote {path} ({width}x{height})")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "inputs",
        nargs="*",
        default=[
            str(ROOT / "game" / "Content" / "MasterGrid.xnb"),
            str(ROOT / "game" / "Content" / "MasterWallTexture.xnb"),
            str(ROOT / "game" / "Content" / "CementTest2.xnb"),
        ],
    )
    parser.add_argument("-o", "--out-dir", default=str(DEFAULT_OUT))
    args = parser.parse_args()
    out_dir = Path(args.out_dir)

    for raw in args.inputs:
        src = Path(raw)
        if not src.exists():
            print(f"Skip missing {src}")
            continue
        w, h, tex = parse_texture2d(src)
        dst = out_dir / (src.stem + ".png")
        save_rgba(dst, w, h, tex)
        meta = out_dir / (src.stem + ".json")
        cell = 16 if w >= 512 else max(1, w // 32)
        meta.write_text(
            '{"width":%d,"height":%d,"cellSize":%d,"atlasSize":%d}\n' % (w, h, cell, max(w, h))
        )


if __name__ == "__main__":
    main()
