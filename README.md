# Zombie Estate (Mac port)

Experimental port of **Zombie Estate** (2010 Xbox Live Indie Game) to macOS using [Mono](https://www.mono-project.com/) and [FNA](https://github.com/FNA-XNA/FNA).

The original `ZombieEstate.exe` is an **Xbox 360 XNA 3.1** build. This repo converts it with a custom **XnaToFna timemachine** toolchain plus Xbox-specific shims.

## Requirements

- **Mono 6.12+** — `/Library/Frameworks/Mono.framework/Versions/Current/bin/mono`
- **fnalibs** (native `.dylib` files) — downloaded automatically by `scripts/setup-fnlibs.sh` from [fnalibs-dailies](https://github.com/FNA-XNA/fnalibs-dailies/actions) (the old `fna.flibitijibibo.com` URL is dead)
- **XnaToFna timemachine** — prebuilt as `tools/XnaToFna.exe` (or rebuild from [0x0ade/XnaToFna](https://github.com/0x0ade/XnaToFna/tree/timemachine))

## Quick start (recovery build — recommended)

If you have the **233 MB `Zombie Estate.exe` recovery launcher** (embeds the full FNA Windows port + all `Content/*.xnb` assets):

```bash
git clone https://github.com/hfost98-maker/zombie-estate-mac.git
cd zombie-estate-mac
chmod +x run-mac.sh scripts/*.sh
./scripts/setup-fnlibs.sh              # once: native SDL3/FAudio/FNA3D dylibs
./scripts/extract-recovery-payload.sh  # extracts embedded payload into game/
./run-mac.sh
```

The recovery launcher is a self-contained Windows FNA build. On Mac it runs under Mono with fnalibs; no timemachine conversion needed.

### Desktop app (recommended)

```bash
chmod +x scripts/install-desktop-app.sh
./scripts/install-desktop-app.sh
```

This installs **Zombie Estate.app** on your Desktop. Double-click to play. On first launch you choose **With AI Teammate** or **Solo**; the choice is saved to `game/zombie-estate.prefs`. Hold **Option** while launching to pick again.

The app runs the game from `~/Projects/zombie-estate-mac` (same folder as this repo).

## Quick start (Xbox backup only)

If you only have the raw **196 KB Xbox 360** `ZombieEstate.exe` from Internet Archive:

```bash
./scripts/setup-fnlibs.sh
./run-mac.sh    # first run converts via scripts/convert-xbox.sh
```

You still need `game/Content/` (see below).

## Repository layout

| Path | Purpose |
|------|---------|
| `game/` | Game executable, maps, spawn data, icons |
| `lib/` | FNA, FNA.NetStub, Mono.Cecil, fnalibs `.dylib` (not in git) |
| `tools/` | `patch-xna.exe`, `XnaToFna.exe` |
| `scripts/` | `convert-xbox.sh`, `setup-fnlibs.sh`, `extract-recovery-payload.sh`, `fetch-content.sh` |
| `run-mac.sh` | Launcher |

## Conversion pipeline

1. Restore `game/ZombieEstate.exe` from `ZombieEstate.exe.xbox.backup`
2. `patch-xna.exe --mscorlib-only` — fixes Xbox 3.5 corlib refs so Cecil can read the assembly
3. `XnaToFna.exe --update-xna` — timemachine relink (SpriteBlendMode, EffectPool, Exiting event, etc.)
4. Run with fnalibs on `DYLD_LIBRARY_PATH`

## Current status

- **Recovery build runs on Mac** — SDL3/Metal init, LoadContent, level build verified on Apple Silicon (M1)
- **AI player 2 (optional)** — the desktop app asks on launch (saved in `game/zombie-estate.prefs`). Hold **Option** while opening the app to choose again. With AI on: P1 gets an **independent camera** (AI no longer pulls the view sideways). Press **A on the P1 controller during a wave** to toggle AI follow. The AI shoots nearby zombies, shops between waves, and auto-readies for the next wave.
- **110 `.xnb` content files** installed via `scripts/extract-recovery-payload.sh` from the 233 MB recovery launcher
- Harmless warning on load: `MOJOSHADER_compileEffect Error: Not an Effects Framework binary` (Xbox-only effect bypassed by recovery CPU renderer)
- **Legacy Xbox-only path:** timemachine conversion still works if you only have the raw Xbox exe, but requires separate `Content/` assets

### Missing content assets

Searched `~/Desktop`, `~/Projects`, `~/Downloads`, `zombie-estate.zip`, Whisky bottles, and VMware VMs — **zero `.xnb` files** on this machine. The [Internet Archive item](https://archive.org/details/zombie-estate) (`zombie-estate_files.xml`) lists only 19 files (exe, maps, icons, metadata); no `Content/` folder was ever uploaded.

The game expects at least these compiled assets (from `ZombieEstate.exe` strings):

| Path | Type |
|------|------|
| `Content/MasterGrid.xnb` | Tile/grid texture (first load failure) |
| `Content/MasterWall.xnb`, `MasterFloor.xnb`, `MasterWallTexture.xnb` | Level textures |
| `Content/Font.xnb`, `BigFont.xnb`, `HugeFont.xnb` | Fonts |
| `Content/Texture*.xnb`, `TextureNumber.xnb` | UI/game textures |
| `Content/Sounds/*.xnb` | ~25 sound effects |
| `Content/MusicParts/*.xnb` | Music stems |

**Where to get them:**

1. **Internet Archive full XBLIG package (recommended)** — the loose IA dump is incomplete; the full ~41 MB package is in the private [XBOX_360_XBLIG_4](https://archive.org/details/XBOX_360_XBLIG_4) collection:
   - Create a free [archive.org account](https://archive.org/account/signup) and log in
   - Download [Zombie Estate.rar](https://archive.org/download/XBOX_360_XBLIG_4/Zombie%20Estate.rar) (~41 MB) to `~/Downloads/`
   - Run `./scripts/fetch-content.sh` (or `./scripts/extract-content.sh ~/Downloads/Zombie\ Estate.rar`)
   - This unrar → STFS extract → copies `game/Content/` automatically
2. **Xbox 360 you own** — copy from a licensed install under the title's package `Content/` folder (Title ID `1481966848`).
3. **Your encrypted VMware VM** — if you recover the password, the Windows bottle may have a full XBLIG install with `Content/`.
4. **Internet Archive loose dump is incomplete** — [archive.org/details/zombie-estate](https://archive.org/details/zombie-estate) only preserved exe/maps/icons, not the full package.
5. **Zombie Estate 2 (Steam)** — different game/engine; assets are not interchangeable.

Once you have a `Content/` folder, copy it to `game/Content/` and run `./run-mac.sh` again.

## Rebuilding tools

```bash
# patch-xna
mcs -r:lib/Mono.Cecil.dll -out:tools/patch-xna.exe scripts/patch-xna.cs

# patch-input-focus
mcs -r:lib/Mono.Cecil.dll -out:tools/patch-input-focus.exe scripts/patch-input-focus.cs

# patch-ai-player2
mcs -r:lib/Mono.Cecil.dll -out:tools/patch-ai-player2.exe scripts/patch-ai-player2.cs

# XnaToFna timemachine (requires msbuild + submodules)
git clone --recursive --branch timemachine https://github.com/0x0ade/XnaToFna.git
# Fix System.Drawing usings in src/Helper/ProxyForms/*.cs if build fails
msbuild XnaToFna.sln /p:Configuration=Release
cp bin/Release/XnaToFna.exe tools/
cp lib-projs/FNA/bin/Release/FNA.dll lib/
```

## Legal

Game assets are from the Xbox Live Indie Games release. Do not redistribute outside personal backup/porting use.
