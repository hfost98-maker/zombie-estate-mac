# Zombie Estate (Mac port)

Experimental port of **Zombie Estate** (2010 Xbox Live Indie Game) to macOS using [Mono](https://www.mono-project.com/) and [FNA](https://github.com/FNA-XNA/FNA).

The original `ZombieEstate.exe` is an **Xbox 360 XNA 3.1** build. This repo relinks it to FNA with a custom Cecil patcher. Full Xbox API shimming (via XnaToFna `timemachine`) is still needed before the game runs cleanly.

## Requirements

- **Mono 6.12+** — install the macOS `.pkg` from [mono-project.com](https://www.mono-project.com/download/). System path:
  `/Library/Frameworks/Mono.framework/Versions/Current/bin/mono`
- **FNA native libraries** (SDL2, OpenAL, etc.) — not included. Download [fnalibs](https://fna.flibitijibibo.com/archive/fnalibs.tar.bz2) and place the macOS `.dylib` files in `lib/` (or `game/`).
- **ffmpeg** (optional, for media) — install via Homebrew; excluded from git (~80 MB).

## Quick start

```bash
git clone <your-repo-url>
cd zombie-estate-mac
chmod +x run-mac.sh
./run-mac.sh
```

On first run, if `ZombieEstate.exe` still matches the Xbox backup, `tools/patch-xna.exe` relinks XNA references to FNA automatically.

## Repository layout

| Path | Purpose |
|------|---------|
| `game/` | Game executable, maps, spawn data, icons |
| `lib/` | FNA, FNA.NetStub, Mono.Cecil |
| `tools/` | `patch-xna.exe` (compiled from `scripts/patch-xna.cs`) |
| `scripts/` | Source scripts (`patch-xna.cs`, setup helpers) |
| `run-mac.sh` | Launcher (uses system Mono) |

## Rebuilding the patcher

```bash
mcs -r:lib/Mono.Cecil.dll -out:tools/patch-xna.exe scripts/patch-xna.cs
```

## Current status

- Assembly relinking (Xbox XNA → FNA) works.
- Runtime currently fails with missing Xbox-specific APIs, e.g. `Microsoft.Xna.Framework.Game.add_Exiting`.
- Next step: complete an **XnaToFna timemachine** conversion for Xbox 360 builds, or extend the patcher/shims.

## Legal

Game assets are from the Xbox Live Indie Games release. Do not redistribute outside personal backup/porting use.
