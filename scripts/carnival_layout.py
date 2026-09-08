#!/usr/bin/env python3
"""32×32 carnival layout traced from the reference screenshot.

Legend (one char per cell):
  # perimeter fence (blocked)
  + main entrance gate (walkable)
  A arch / bunting row (walkable decor)
  . lawn (walkable)
  = gravel path (walkable)
  ~ pond water (blocked)
  b pond bridge (walkable)
  T big-top tent (walkable — 3D walls with door openings)
  C carousel hub (walkable — 3D deck with door openings)
  R carousel ring (walkable gravel)
  F ferris wheel hub (blocked — 3D tower)
  f ferris apron (walkable)
  K coaster canopy (walkable under — overhead 3D track)
  G shop building in cemetery mausoleum (walkable — 3D with door)
  B game booth row (walkable — open front booths)
  s small cottage (blocked — 3D shed)
  O cemetery iron fence (blocked)
  o cemetery lawn (walkable)
  m tombstone (blocked)
  D dead tree (blocked)
  g cemetery gate (walkable)
"""

from __future__ import annotations

# y=0 is north (top of reference image), y=31 is south entrance row.
LAYOUT_ROWS: tuple[str, ...] = (
    "################################",  # 0
    "#.~~....TTTTTTTT.FFf........OOO#",  # 1 pond | big top | ferris | cemetery
    "#.~~....TTTTTTTT.FFf........Ooo#",  # 2
    "#.~~b...TTTTTTTT.FFf....GGGGooo#",  # 3 bridge | mausoleum shop
    "#.~~..========TTT.......GGGGOoo#",  # 4 big-top south | shop row 2
    "#.~~..========..........omomo.O#",  # 5
    "#......========.........omomo.O#",  # 6
    "#..K...========.........omomo.O#",  # 7 coaster
    "#..KK..====.............omomo.O#",  # 8
    "#..KKKK===..............omomo.O#",  # 9
    "#..KKKK===....RRRR......omomo.O#",  # 10 carousel ring
    "#..K..K===...RRCCRR.....omomo.O#",  # 11
    "#..K..K===...RRCCRR.....omomo.O#",  # 12 carousel hub
    "#..K..K===...RRRRR......omomo.O#",  # 13
    "#.......===.............omomo.O#",  # 14
    "#.......===.............omomo.O#",  # 15
    "#...=...===.............oDooo.O#",  # 16 west path spur
    "#...=...===.............omomo.O#",  # 17
    "#.BBB...===.............omomo.O#",  # 18 game booths
    "#.BBB...===.............omomo.O#",  # 19
    "#.BBB...===.............omomo.O#",  # 20
    "#.BBB...===.........====goooo.O#",  # 21 path to cemetery gate
    "#.BBB...===.........========..O#",  # 22
    "#.BBB...===.................o.O#",  # 23
    "#.BBB...===.................o.O#",  # 24
    "#.......===.............ss..o.O#",  # 25 cottage
    "#.......===.............Bs..o.O#",  # 26 small tent booth
    "#.......===.................o.O#",  # 27
    "#.......===.................o.O#",  # 28
    "#.......AAA.................o.O#",  # 29 clown arch
    "#.......AAA.................o.O#",  # 30
    "###############+################",  # 31 south gate
)

assert all(len(row) == 32 for row in LAYOUT_ROWS)
assert len(LAYOUT_ROWS) == 32

CHAR_TO_NAME: dict[str, str] = {
    "#": "fence",
    "+": "gate",
    "A": "arch",
    ".": "lawn",
    "=": "path",
    "~": "pond",
    "b": "bridge",
    "T": "bigtop",
    "C": "carousel",
    "R": "carousel_ring",
    "F": "ferris",
    "f": "ferris_apron",
    "K": "coaster",
    "G": "shop",
    "B": "booth",
    "s": "cottage",
    "O": "cem_fence",
    "o": "cemetery",
    "m": "tombstone",
    "D": "dead_tree",
    "g": "cem_gate",
}

# Cells that block movement / pathfinding (walkable structures are NOT listed here).
BLOCKED_CHARS = frozenset({"#", "~", "F", "D", "O", "m", "s"})

# Walkable structure zones — door cells have open wall toward outside.
BIGTOP_X0, BIGTOP_Y0, BIGTOP_X1, BIGTOP_Y1 = 8, 1, 15, 4
BIGTOP_DOORS = frozenset({(13, 4), (14, 4), (8, 2), (8, 3)})  # south + west

CAROUSEL_CX, CAROUSEL_CY = 15, 12
CAROUSEL_DOORS = frozenset({(15, 12), (14, 12), (16, 11), (14, 11)})  # south + east

# Gun shop inside cemetery mausoleum (top of graveyard).
SHOP_X0, SHOP_Y0, SHOP_X1, SHOP_Y1 = 24, 3, 27, 4
SHOP_DOORS = frozenset({(25, 4), (26, 4), (24, 4), (27, 3)})  # south + west + east

BOOTH_DOORS = frozenset({(2, 21), (2, 22), (2, 23), (2, 24), (6, 21)})

# Zombie spawn region ids by area.
SPAWN_ENTRANCE = 0
SPAWN_PARK = 3
SPAWN_CEMETERY = 4

PLAYER_SPAWNS = ((14, 29), (15, 29), (16, 29), (15, 28))
CAROUSEL_CENTER = (CAROUSEL_CX, CAROUSEL_CY)
MAIN_GATE = (15, 31)
CEM_GATE = (22, 21)


def in_zone(ch: str, zone: str) -> bool:
    if zone == "bigtop":
        return ch == "T"
    if zone == "carousel":
        return ch in "CR"
    if zone == "shop":
        return ch == "G"
    if zone == "booth":
        return ch == "B"
    return False


def cell_at(x: int, y: int) -> str:
    return LAYOUT_ROWS[y][x]


def char_to_id(ch: str) -> int:
    return ord(ch)


def layout_grid() -> list[list[str]]:
    return [list(row) for row in LAYOUT_ROWS]
