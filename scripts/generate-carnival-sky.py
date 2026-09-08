#!/usr/bin/env python3
"""Generate starry night sky BarBG texture (128×32, matches game Content/BarBG.xnb)."""

from __future__ import annotations

import random
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:
    raise SystemExit("Install Pillow: pip3 install pillow")

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "game" / "CarnivalAssets"
WIDTH, HEIGHT = 128, 32


def main() -> None:
    rng = random.Random(2026)
    img = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 255))
    draw = ImageDraw.Draw(img)

    # Night gradient — deep indigo at top, purple horizon glow at bottom.
    for y in range(HEIGHT):
        t = y / (HEIGHT - 1)
        r = int(8 + 18 * t + 12 * (1 - t))
        g = int(6 + 8 * t + 6 * (1 - t))
        b = int(32 + 40 * t + 55 * (1 - t))
        draw.line([(0, y), (WIDTH, y)], fill=(r, g, b, 255))

    # Faint aurora band.
    import math
    for x in range(WIDTH):
        y = 7 + int(2 * math.sin(x * 0.11))
        draw.point((x, y), fill=(80, 40, 110, 60))

    # Stars — bright cores + soft halos.
    for _ in range(90):
        x = rng.randint(0, WIDTH - 1)
        y = rng.randint(0, HEIGHT - 8)
        bright = rng.random() > 0.82
        if bright:
            c = (255, 245, 210, 255)
            draw.point((x, y), fill=c)
            if x > 0:
                draw.point((x - 1, y), fill=(200, 190, 160, 120))
            if x < WIDTH - 1:
                draw.point((x + 1, y), fill=(200, 190, 160, 120))
        else:
            shade = rng.randint(160, 230)
            draw.point((x, y), fill=(shade, shade, shade + 20, 255))

    # A few colored carnival-tinted stars.
    for _ in range(12):
        x, y = rng.randint(0, WIDTH - 1), rng.randint(0, HEIGHT - 10)
        col = rng.choice([(255, 120, 140), (120, 180, 255), (255, 220, 100)])
        draw.point((x, y), fill=(*col, 255))

    # Horizon glow (fair lights below skyline).
    for x in range(WIDTH):
        for y in range(HEIGHT - 6, HEIGHT):
            t = (y - (HEIGHT - 6)) / 5.0
            draw.point(
                (x, y),
                fill=(int(40 + 30 * t), int(20 + 15 * t), int(50 + 40 * t), 255),
            )

    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / "BarBG_starry.png"
    img.save(path)
    print(f"Wrote {path} ({WIDTH}x{HEIGHT})")


if __name__ == "__main__":
    main()
