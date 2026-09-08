#!/usr/bin/env python3
"""Hand-painted 16×16 carnival tiles for MasterWallTexture / MasterGrid atlases."""

from __future__ import annotations

import random
from typing import Callable

from PIL import Image, ImageDraw

CELL = 16

# MasterWallTexture atlas slots (512×512, 16px cells). Rows 28–31 are carnival-only.
WALL_SLOTS = {
    # Row 28 — base terrain & infrastructure
    "fair_grass": (16, 28),
    "gravel": (17, 28),
    "gravel_dark": (18, 28),
    "grass_worn": (19, 28),
    "pond_deep": (20, 28),
    "pond_edge": (21, 28),
    "lamp_post": (22, 28),
    "string_lights": (23, 28),
    "stone_fence": (24, 28),
    "clown_arch": (25, 28),
    "coaster_track": (26, 28),
    "coaster_wood": (27, 28),
    # Row 29 — surfaces & small structures
    "confetti_lawn": (16, 29),
    "pink_path": (17, 29),
    "blue_path": (18, 29),
    "gold_path": (19, 29),
    "stripe_h": (20, 29),
    "stripe_v": (21, 29),
    "fence_white": (22, 29),
    "tent_stripe": (23, 29),
    "carousel_deck": (24, 29),
    "carousel_roof": (25, 29),
    "ticket_floor": (26, 29),
    "funhouse_floor": (27, 29),
    # Row 30 — props & overlays
    "balloon_red": (16, 30),
    "balloon_blue": (17, 30),
    "balloon_yellow": (18, 30),
    "balloon_green": (19, 30),
    "balloon_purple": (20, 30),
    "striped_pole": (21, 30),
    "ferris_hub": (22, 30),
    "ferris_gondola": (23, 30),
    "popcorn": (24, 30),
    "bunting": (25, 30),
    "ring_toss": (26, 30),
    "carnival_sign": (27, 30),
    # Row 31 — building faces (wall extrusion in 3D)
    "bigtop_wall": (16, 31),
    "bigtop_peak": (17, 31),
    "shop_neon": (18, 31),
    "booth_awning": (19, 31),
    "carousel_horse": (20, 31),
    "ferris_tower": (21, 31),
    "tent_pole": (22, 31),
    "food_stall": (23, 31),
    "iron_gate": (24, 31),
    "arch_top": (25, 31),
    "coaster_rail": (26, 31),
    "bench": (27, 31),
}

GRID_SLOTS = {
    "clown_down": (24, 4),
    "clown_left": (25, 4),
    "clown_right": (26, 4),
    "clown_up": (27, 4),
    "clown_damage1": (24, 5),
    "clown_damage2": (25, 5),
}


def paste_cell(atlas: Image.Image, cx: int, cy: int, tile: Image.Image) -> None:
    atlas.paste(tile.convert("RGBA"), (cx * CELL, cy * CELL))


def _px(draw: ImageDraw.ImageDraw, x: int, y: int, color: tuple[int, int, int, int]) -> None:
    if 0 <= x < CELL and 0 <= y < CELL:
        draw.point((x, y), fill=color)


def tile_fair_grass(rng: random.Random | None = None) -> Image.Image:
    """Bright carnival lawn — vivid green base with sunlit speckles."""
    rng = rng or random.Random(7)
    base = Image.new("RGBA", (CELL, CELL), (52, 145, 58, 255))
    draw = ImageDraw.Draw(base)
    for _ in range(48):
        x, y = rng.randint(0, 15), rng.randint(0, 15)
        c = rng.choice([
            (68, 175, 72, 255), (42, 120, 48, 255), (88, 195, 82, 255),
            (58, 155, 62, 255), (78, 185, 70, 255),
        ])
        draw.point((x, y), fill=c)
    for _ in range(6):
        x, y = rng.randint(0, 15), rng.randint(0, 15)
        draw.point((x, y), fill=(38, 95, 42, 255))
    return base


def tile_gravel(dark: bool = False) -> Image.Image:
    """Warm golden cobblestone paths."""
    base_c = (210, 185, 135, 255) if not dark else (175, 145, 95, 255)
    accent = (235, 210, 155, 255) if not dark else (140, 115, 75, 255)
    tile = Image.new("RGBA", (CELL, CELL), base_c)
    draw = ImageDraw.Draw(tile)
    rng = random.Random(11 if dark else 3)
    for y in range(0, 16, 4):
        for x in range(0, 16, 4):
            if rng.random() > 0.4:
                draw.rectangle([x, y, x + 3, y + 3], fill=accent)
    for _ in range(12):
        draw.point((rng.randint(0, 15), rng.randint(0, 15)), fill=(200, 182, 150, 255))
    return tile


def tile_colored_path(
    base: tuple[int, int, int],
    accent: tuple[int, int, int],
    speck: tuple[int, int, int],
    seed: int,
) -> Image.Image:
    """Tinted cobblestone — pink/blue/gold carnival walkways."""
    tile = Image.new("RGBA", (CELL, CELL), (*base, 255))
    draw = ImageDraw.Draw(tile)
    rng = random.Random(seed)
    for y in range(0, 16, 4):
        for x in range(0, 16, 4):
            c = accent if rng.random() > 0.35 else tuple(max(0, v - 18) for v in base)
            draw.rectangle([x, y, x + 3, y + 3], fill=(*c, 255))
    for _ in range(8):
        draw.point((rng.randint(0, 15), rng.randint(0, 15)), fill=(*speck, 255))
    return tile


def tile_grass_worn() -> Image.Image:
    g = tile_fair_grass(random.Random(99))
    draw = ImageDraw.Draw(g)
    for x in range(16):
        draw.point((x, 8 + (x % 3)), fill=(148, 128, 98, 255))
    return g


def tile_pond(deep: bool = True) -> Image.Image:
    if deep:
        tile = Image.new("RGBA", (CELL, CELL), (28, 58, 110, 255))
        draw = ImageDraw.Draw(tile)
        for x, y in ((3, 4), (10, 6), (6, 10), (12, 11)):
            draw.point((x, y), fill=(36, 72, 130, 255))
        draw.ellipse([5, 5, 10, 10], fill=(40, 80, 140, 180))
        return tile
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.rectangle([0, 8, 15, 15], fill=(28, 58, 110, 255))
    draw.ellipse([4, 2, 8, 6], fill=(60, 140, 70, 255))
    draw.line([(4, 6), (4, 8)], fill=(80, 60, 40, 255))
    return tile


def tile_lamp_post() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.rectangle([7, 6, 8, 15], fill=(30, 30, 35, 255))
    draw.rectangle([5, 3, 10, 6], fill=(20, 20, 25, 255))
    draw.ellipse([4, 0, 11, 7], fill=(255, 230, 120, 255))
    draw.point((6, 2), fill=(255, 255, 200, 255))
    return tile


def tile_string_lights() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.line([(0, 2), (15, 4)], fill=(80, 80, 90, 255))
    colors = [(255, 60, 60), (255, 220, 60), (60, 180, 255), (180, 60, 255), (60, 255, 120)]
    for i, col in enumerate(colors):
        x = 1 + i * 3
        draw.point((x, 2 + (i % 2)), fill=(*col, 255))
        draw.point((x, 3 + (i % 2)), fill=(255, 255, 220, 255))
    return tile


def tile_stone_fence() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.rectangle([0, 11, 15, 15], fill=(60, 58, 62, 255))
    for x in range(0, 16, 4):
        draw.rectangle([x, 5, x + 2, 13], fill=(90, 88, 94, 255))
    draw.line([(0, 7), (15, 7)], fill=(110, 108, 114, 255))
    return tile


def tile_clown_arch() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.arc([2, 4, 13, 15], 190, 350, fill=(200, 40, 50, 255), width=2)
    draw.ellipse([5, 6, 10, 11], fill=(255, 210, 160, 255))
    draw.point((7, 8), fill=(255, 40, 40, 255))
    draw.point((8, 8), fill=(255, 40, 40, 255))
    draw.arc([6, 4, 9, 7], 200, 340, fill=(255, 60, 120, 255))
    for i, c in enumerate([(255, 80, 80), (255, 220, 60), (80, 160, 255)]):
        draw.polygon([(3 + i * 4, 3), (5 + i * 4, 3), (4 + i * 4, 6)], fill=(*c, 255))
    return tile


def tile_coaster_track() -> Image.Image:
    """Overhead rail segment — bold red track at top of tile for tall wall face."""
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.rectangle([0, 0, 15, 4], fill=(255, 55, 45, 255))
    draw.rectangle([0, 4, 15, 5], fill=(180, 35, 30, 255))
    draw.line([(0, 8), (15, 5)], fill=(255, 80, 60, 255), width=2)
    for x in range(0, 16, 4):
        draw.rectangle([x, 10, x + 2, 15], fill=(130, 85, 45, 255))
    return tile


def tile_coaster_wood() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (120, 78, 42, 255))
    draw = ImageDraw.Draw(tile)
    for y in range(0, 16, 2):
        c = (150, 100, 55, 255) if (y // 2) % 2 else (95, 62, 34, 255)
        draw.line([(0, y), (15, y)], fill=c)
    return tile


def tile_confetti_lawn(rng: random.Random | None = None) -> Image.Image:
    rng = rng or random.Random(42)
    base = tile_fair_grass(rng)
    draw = ImageDraw.Draw(base)
    colors = [
        (255, 70, 110), (70, 190, 255), (255, 230, 50), (180, 80, 255),
        (70, 240, 130), (255, 140, 40), (255, 100, 180),
    ]
    for _ in range(18):
        draw.point((rng.randint(0, 15), rng.randint(0, 15)), fill=(*rng.choice(colors), 255))
    return base


def tile_stripes(horizontal: bool) -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (220, 35, 50, 255))
    draw = ImageDraw.Draw(tile)
    if horizontal:
        for y in range(0, 16, 4):
            draw.rectangle([0, y, 15, y + 1], fill=(255, 255, 255, 255))
    else:
        for x in range(0, 16, 4):
            draw.rectangle([x, 0, x + 1, 15], fill=(255, 255, 255, 255))
    return tile


def tile_tent_stripe() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    for x in range(0, 16, 4):
        c = (255, 45, 65, 255) if (x // 4) % 2 == 0 else (255, 255, 255, 255)
        draw.rectangle([x, 0, x + 3, 15], fill=c)
    draw.line([(8, 0), (8, 15)], fill=(255, 200, 50, 255), width=1)
    return tile


def tile_carousel_deck() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (150, 95, 48, 255))
    draw = ImageDraw.Draw(tile)
    for i in range(8):
        ang = i * 45
        import math
        x = 8 + int(5 * math.cos(math.radians(ang)))
        y = 8 + int(5 * math.sin(math.radians(ang)))
        draw.point((x, y), fill=(210, 170, 80, 255))
    draw.ellipse([6, 6, 9, 9], fill=(240, 200, 90, 255))
    for x in range(0, 16, 2):
        draw.line([(x, 0), (x, 15)], fill=(130, 80, 38, 255))
    return tile


def tile_carousel_roof() -> Image.Image:
    """Striped carousel canopy — reads clearly as round tent roof in 3D."""
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    stripes = [(255, 55, 75), (255, 255, 255), (55, 130, 255), (255, 220, 50)]
    for i, (w, col) in enumerate([(15, stripes[0]), (12, stripes[1]), (9, stripes[2]), (6, stripes[3])]):
        left = 8 - w // 2
        right = 8 + w // 2
        draw.polygon([(left, 15), (right, 15), (8, 1 + i)], fill=(*col, 255))
    draw.rectangle([7, 0, 8, 4], fill=(255, 210, 40, 255))
    draw.ellipse([6, 0, 9, 3], fill=(255, 240, 120, 255))
    return tile


def tile_balloon(color: tuple[int, int, int]) -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.ellipse([3, 0, 12, 11], fill=(*color, 255))
    draw.ellipse([5, 2, 8, 6], fill=tuple(min(255, c + 80) for c in color) + (200,))
    draw.ellipse([4, 9, 10, 12], fill=tuple(max(0, c - 30) for c in color) + (255,))
    draw.line([(7, 11), (6, 15)], fill=(120, 85, 50, 255))
    draw.line([(7, 11), (8, 15)], fill=(90, 60, 35, 255))
    return tile


def tile_ferris_hub() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.line([(8, 0), (8, 15)], fill=(160, 165, 180, 255), width=2)
    draw.line([(0, 8), (15, 8)], fill=(160, 165, 180, 255), width=2)
    draw.ellipse([5, 5, 10, 10], fill=(255, 210, 60, 255))
    draw.ellipse([6, 6, 9, 9], fill=(255, 240, 180, 255))
    return tile


def tile_ferris_gondola() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.line([(8, 0), (8, 4)], fill=(130, 135, 150, 255))
    draw.rectangle([4, 4, 11, 9], fill=(255, 90, 110, 255))
    draw.rectangle([4, 9, 11, 10], fill=(60, 60, 70, 255))
    draw.point((6, 6), fill=(255, 200, 80, 255))
    return tile


def tile_bunting() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.line([(0, 2), (15, 3)], fill=(180, 180, 190, 255))
    cols = [(255, 70, 70), (255, 220, 50), (70, 160, 255), (200, 80, 255)]
    for i, col in enumerate(cols):
        x = 1 + i * 4
        draw.polygon([(x, 3), (x + 2, 3), (x + 1, 7 + (i % 2))], fill=(*col, 255))
    return tile


def tile_popcorn() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.rectangle([4, 8, 11, 14], fill=(240, 50, 60, 255))
    draw.rectangle([5, 9, 10, 13], fill=(255, 245, 220, 255))
    for px, py in [(5, 5), (7, 4), (9, 5), (8, 6), (10, 7)]:
        draw.point((px, py), fill=(255, 252, 230, 255))
    return tile


def tile_ring_toss() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.rectangle([7, 9, 8, 15], fill=(130, 85, 45, 255))
    draw.ellipse([2, 2, 6, 6], outline=(255, 70, 70, 255))
    draw.ellipse([9, 2, 13, 6], outline=(70, 140, 255, 255))
    draw.point((4, 4), fill=(255, 220, 60, 255))
    return tile


def tile_bigtop_wall() -> Image.Image:
    return tile_tent_stripe()


def tile_bigtop_peak() -> Image.Image:
    """Big-top peak — tall striped cone for north row of tent."""
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.polygon([(0, 15), (15, 15), (8, 0)], fill=(255, 50, 70, 255))
    draw.polygon([(2, 15), (13, 15), (8, 3)], fill=(255, 255, 255, 255))
    draw.polygon([(5, 15), (11, 15), (8, 6)], fill=(255, 50, 70, 255))
    draw.line([(8, 0), (8, 15)], fill=(255, 210, 40, 255))
    draw.point((8, 1), fill=(255, 255, 200, 255))
    return tile


def tile_shop_neon() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (90, 65, 45, 255))
    draw = ImageDraw.Draw(tile)
    draw.rectangle([1, 4, 14, 10], fill=(30, 25, 20, 255))
    # "SHOP" hint in neon colors
    for i, c in enumerate([(255, 80, 80), (255, 220, 80), (80, 200, 255), (255, 120, 200)]):
        draw.rectangle([2 + i * 3, 5, 3 + i * 3, 9], fill=(*c, 255))
    draw.rectangle([0, 10, 15, 12], fill=(200, 40, 50, 255))
    draw.rectangle([0, 12, 15, 14], fill=(255, 255, 255, 255))
    return tile


def tile_carousel_horse() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.rectangle([6, 10, 9, 15], fill=(160, 100, 50, 255))
    draw.polygon([(5, 10), (10, 10), (9, 4), (6, 4)], fill=(250, 250, 245, 255))
    draw.point((7, 5), fill=(30, 30, 30, 255))
    draw.line([(5, 7), (3, 4)], fill=(250, 250, 245, 255))
    draw.line([(10, 7), (12, 4)], fill=(250, 250, 245, 255))
    return tile


def tile_ferris_tower() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.polygon([(8, 0), (4, 15), (12, 15)], fill=(170, 175, 195, 255))
    draw.line([(5, 13), (11, 3)], fill=(255, 230, 60, 255), width=2)
    draw.line([(11, 13), (5, 3)], fill=(255, 230, 60, 255), width=2)
    draw.ellipse([6, 0, 10, 4], fill=(255, 100, 120, 255))
    return tile


def tile_white_fence() -> Image.Image:
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.rectangle([0, 10, 15, 15], fill=(34, 72, 38, 255))
    for x in range(1, 16, 4):
        draw.rectangle([x, 4, x + 1, 13], fill=(245, 245, 250, 255))
    draw.line([(0, 6), (15, 6)], fill=(245, 245, 250, 255))
    return tile


def draw_clown_sprite(facing: str) -> Image.Image:
    """16×16 clown character for MasterGrid (drawn from scratch)."""
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    suit = (220, 45, 65, 255)
    suit2 = (45, 85, 210, 255)
    skin = (255, 210, 175, 255)
    hair = (255, 55, 130, 255)
    hair2 = (55, 180, 255, 255)
    dot = (255, 255, 255, 255)

    if facing == "down":
        draw.ellipse([3, 0, 12, 4], fill=hair)
        draw.ellipse([4, 1, 11, 8], fill=skin)
        draw.ellipse([6, 4, 9, 7], fill=(255, 50, 50, 255))
        draw.point((6, 5), fill=(30, 30, 30, 255))
        draw.point((9, 5), fill=(30, 30, 30, 255))
        draw.arc([6, 6, 9, 8], 10, 170, fill=(180, 40, 40, 255))
        draw.rectangle([5, 8, 10, 13], fill=suit)
        for px, py in ((6, 9), (8, 10), (7, 12)):
            draw.point((px, py), fill=dot)
        draw.rectangle([4, 13, 6, 15], fill=suit2)
        draw.rectangle([9, 13, 11, 15], fill=suit2)
        draw.point((5, 14), fill=dot)
        draw.point((10, 14), fill=dot)
    elif facing == "left":
        draw.ellipse([3, 2, 10, 9], fill=hair2)
        draw.ellipse([4, 3, 9, 8], fill=skin)
        draw.point((5, 5), fill=(30, 30, 30, 255))
        draw.point((6, 6), fill=(255, 40, 40, 255))
        draw.rectangle([3, 9, 9, 14], fill=suit)
        draw.rectangle([2, 13, 5, 15], fill=suit2)
        draw.rectangle([6, 13, 8, 15], fill=suit2)
    elif facing == "right":
        draw.ellipse([5, 2, 12, 9], fill=hair)
        draw.ellipse([6, 3, 11, 8], fill=skin)
        draw.point((10, 5), fill=(30, 30, 30, 255))
        draw.point((9, 6), fill=(255, 40, 40, 255))
        draw.rectangle([6, 9, 12, 14], fill=suit2)
        draw.rectangle([7, 13, 10, 15], fill=suit)
        draw.rectangle([10, 13, 13, 15], fill=suit)
    else:  # up
        draw.ellipse([4, 2, 11, 9], fill=hair2)
        draw.rectangle([5, 8, 10, 14], fill=suit)
        draw.rectangle([4, 13, 6, 15], fill=suit2)
        draw.rectangle([9, 13, 11, 15], fill=suit)

    return tile


def draw_clown_damage(variant: int) -> Image.Image:
    base = draw_clown_sprite("down")
    draw = ImageDraw.Draw(base)
    if variant == 1:
        draw.line([(4, 4), (7, 7)], fill=(180, 40, 40, 255))
    else:
        draw.point([(5, 3), (9, 4), (7, 8)], fill=(120, 20, 20, 255))
    return base


def paint_wall_atlas(base: Image.Image) -> Image.Image:
    """Paint all carnival tiles onto a copy of MasterWallTexture."""
    img = base.copy().convert("RGBA")
    painters: dict[str, Callable[[], Image.Image]] = {
        "fair_grass": tile_fair_grass,
        "gravel": lambda: tile_gravel(False),
        "gravel_dark": lambda: tile_gravel(True),
        "grass_worn": tile_grass_worn,
        "pond_deep": lambda: tile_pond(True),
        "pond_edge": lambda: tile_pond(False),
        "lamp_post": tile_lamp_post,
        "string_lights": tile_string_lights,
        "stone_fence": tile_stone_fence,
        "clown_arch": tile_clown_arch,
        "coaster_track": tile_coaster_track,
        "coaster_wood": tile_coaster_wood,
        "confetti_lawn": tile_confetti_lawn,
        "pink_path": lambda: tile_colored_path((210, 120, 145), (235, 155, 175), (255, 200, 220), 17),
        "blue_path": lambda: tile_colored_path((95, 145, 210), (130, 175, 235), (180, 210, 255), 18),
        "gold_path": lambda: tile_colored_path((205, 170, 75), (235, 200, 95), (255, 235, 150), 19),
        "stripe_h": lambda: tile_stripes(True),
        "stripe_v": lambda: tile_stripes(False),
        "fence_white": tile_white_fence,
        "tent_stripe": tile_tent_stripe,
        "carousel_deck": tile_carousel_deck,
        "carousel_roof": tile_carousel_roof,
        "ticket_floor": lambda: tile_gravel(False),
        "funhouse_floor": lambda: tile_gravel(True),
        "balloon_red": lambda: tile_balloon((255, 55, 75)),
        "balloon_blue": lambda: tile_balloon((55, 145, 255)),
        "balloon_yellow": lambda: tile_balloon((255, 215, 45)),
        "balloon_green": lambda: tile_balloon((70, 205, 85)),
        "balloon_purple": lambda: tile_balloon((165, 85, 255)),
        "striped_pole": lambda: tile_stripes(False),
        "ferris_hub": tile_ferris_hub,
        "ferris_gondola": tile_ferris_gondola,
        "popcorn": tile_popcorn,
        "bunting": tile_bunting,
        "ring_toss": tile_ring_toss,
        "carnival_sign": tile_shop_neon,
        "bigtop_wall": tile_bigtop_wall,
        "bigtop_peak": tile_bigtop_peak,
        "shop_neon": tile_shop_neon,
        "booth_awning": tile_tent_stripe,
        "carousel_horse": tile_carousel_horse,
        "ferris_tower": tile_ferris_tower,
        "tent_pole": lambda: tile_stripes(False),
        "food_stall": tile_popcorn,
        "iron_gate": tile_stone_fence,
        "arch_top": tile_clown_arch,
        "coaster_rail": tile_coaster_track,
        "bench": tile_gravel,
    }
    for name, fn in painters.items():
        cx, cy = WALL_SLOTS[name]
        paste_cell(img, cx, cy, fn())
    return img


def paint_grid_atlas(base: Image.Image) -> Image.Image:
    img = base.copy().convert("RGBA")
    paste_cell(img, *GRID_SLOTS["clown_down"], draw_clown_sprite("down"))
    paste_cell(img, *GRID_SLOTS["clown_left"], draw_clown_sprite("left"))
    paste_cell(img, *GRID_SLOTS["clown_right"], draw_clown_sprite("right"))
    paste_cell(img, *GRID_SLOTS["clown_up"], draw_clown_sprite("up"))
    paste_cell(img, *GRID_SLOTS["clown_damage1"], draw_clown_damage(1))
    paste_cell(img, *GRID_SLOTS["clown_damage2"], draw_clown_damage(2))
    return img
