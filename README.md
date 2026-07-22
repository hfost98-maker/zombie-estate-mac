# Zombie Estate (Mac port)

Experimental port of **Zombie Estate** (2010 Xbox Live Indie Game) to macOS using [Mono](https://www.mono-project.com/) and [FNA](https://github.com/FNA-XNA/FNA).

The original `ZombieEstate.exe` is an **Xbox 360 XNA 3.1** build. This repo converts it with a custom **XnaToFna timemachine** toolchain plus Xbox-specific shims.

## Requirements

- **Mono 6.12+** — `/Library/Frameworks/Mono.framework/Versions/Current/bin/mono`
- **fnalibs** (native `.dylib` files) — downloaded automatically by `scripts/setup-fnlibs.sh` from [fnalibs-dailies](https://github.com/FNA-XNA/fnalibs-dailies/actions) (the old `fna.flibitijibibo.com` URL is dead)
- **XnaToFna timemachine** — prebuilt as `tools/XnaToFna.exe` (or rebuild from [0x0ade/XnaToFna](https://github.com/0x0ade/XnaToFna/tree/timemachine))

## Quick start

```bash
git clone https://github.com/hfost98-maker/zombie-estate-mac.git
cd zombie-estate-mac
chmod +x run-mac.sh scripts/*.sh
./scripts/setup-fnlibs.sh    # once: download native libs into lib/
./run-mac.sh                 # converts (first run) and launches
```

First launch runs `scripts/convert-xbox.sh` if the exe still matches the Xbox backup.

## Repository layout

| Path | Purpose |
|------|---------|
| `game/` | Game executable, maps, spawn data, icons |
| `lib/` | FNA, FNA.NetStub, Mono.Cecil, fnalibs `.dylib` (not in git) |
| `tools/` | `patch-xna.exe`, `XnaToFna.exe` |
| `scripts/` | `convert-xbox.sh`, `setup-fnlibs.sh`, `patch-xna.cs` |
| `run-mac.sh` | Launcher |

## Conversion pipeline

1. Restore `game/ZombieEstate.exe` from `ZombieEstate.exe.xbox.backup`
2. `patch-xna.exe --mscorlib-only` — fixes Xbox 3.5 corlib refs so Cecil can read the assembly
3. `XnaToFna.exe --update-xna` — timemachine relink (SpriteBlendMode, EffectPool, Exiting event, etc.)
4. Run with fnalibs on `DYLD_LIBRARY_PATH`

## Current status

- **XnaToFna timemachine conversion succeeds** for `ZombieEstate.exe`
- **Game reaches LoadContent** — SDL3/Metal init works on Apple Silicon
- **Blocker:** compiled content (`.xnb` files under `game/Content/`) is not present in the Internet Archive dump; the game fails loading `MasterGrid.xnb`. Map/spawn `.txt` files are included but textures/audio content may need to be sourced separately.

## Rebuilding tools

```bash
# patch-xna
mcs -r:lib/Mono.Cecil.dll -out:tools/patch-xna.exe scripts/patch-xna.cs

# XnaToFna timemachine (requires msbuild + submodules)
git clone --recursive --branch timemachine https://github.com/0x0ade/XnaToFna.git
# Fix System.Drawing usings in src/Helper/ProxyForms/*.cs if build fails
msbuild XnaToFna.sln /p:Configuration=Release
cp bin/Release/XnaToFna.exe tools/
cp lib-projs/FNA/bin/Release/FNA.dll lib/
```

## Legal

Game assets are from the Xbox Live Indie Games release. Do not redistribute outside personal backup/porting use.
