#!/usr/bin/env python3
"""Replace Texture2D RGBA payload inside an XNB file from a PNG (same dimensions)."""

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


def find_texture_payload(data: bytes) -> tuple[int, int, int]:
    for pos in range(10, min(len(data) - 16, 256)):
        width = struct.unpack_from("<i", data, pos)[0]
        height = struct.unpack_from("<i", data, pos + 4)[0]
        if width <= 0 or height <= 0 or width > 4096 or height > 4096:
            continue
        if width != height and max(width, height) > 2048:
            continue
        expected = width * height * 4
        if pos + 16 + expected == len(data):
            return pos, width, height
    raise ValueError("Could not locate Texture2D payload")


def pack_png_into_xnb(xnb_path: Path, png_path: Path, backup: bool = True) -> None:
    data = bytearray(xnb_path.read_bytes())
    pos, width, height = find_texture_payload(bytes(data))
    img = Image.open(png_path).convert("RGBA")
    if img.size != (width, height):
        raise ValueError(f"{png_path.name} is {img.size}, expected {(width, height)}")

    payload = img.tobytes()
    data[pos + 16 : pos + 16 + len(payload)] = payload

    if backup:
        bak = xnb_path.with_suffix(xnb_path.suffix + ".pre-carnival")
        if not bak.exists():
            bak.write_bytes(xnb_path.read_bytes())

    xnb_path.write_bytes(data)
    print(f"Packed {png_path.name} -> {xnb_path.name} ({width}x{height})")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("xnb")
    parser.add_argument("png")
    parser.add_argument("--no-backup", action="store_true")
    args = parser.parse_args()
    pack_png_into_xnb(Path(args.xnb), Path(args.png), backup=not args.no_backup)


if __name__ == "__main__":
    main()
