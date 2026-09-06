#!/bin/bash
# Extract the embedded FNA recovery payload from "Zombie Estate.exe" (233 MB launcher).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GAME_DIR="$ROOT/game"
LIB_DIR="$ROOT/lib"
SRC="${1:-$HOME/Desktop/Zombie Estate.exe}"

if [ ! -f "$SRC" ]; then
  echo "Missing recovery launcher: $SRC"
  echo "Place the ~233 MB Zombie Estate.exe recovery build on your Desktop, or pass the path:"
  echo "  $0 \"/path/to/Zombie Estate.exe\""
  exit 1
fi

export SRC="$SRC" GAME_DIR LIB_DIR

python3 << 'PY'
import struct, zipfile, io, os, shutil

src = os.path.expanduser(os.environ.get("SRC", ""))
game = os.environ.get("GAME_DIR", "")
lib = os.environ.get("LIB_DIR", "")

data = open(src, "rb").read()
size_mb = len(data) / 1e6
if size_mb < 100:
    raise SystemExit(f"Expected ~233 MB recovery launcher, got {size_mb:.1f} MB: {src}")

eocd = data.rfind(b"PK\x05\x06")
if eocd < 0:
    raise SystemExit("No embedded ZIP found in launcher")

cd_off = struct.unpack_from("<I", data, eocd + 16)[0]
start = None
for off in range(cd_off, max(0, cd_off - 0x2000000), -1):
    if data[off : off + 4] == b"PK\x03\x04":
        start = off
        break
for try_start in (start, 0x80c000, 0x810000):
    if try_start is None:
        continue
    try:
        z = zipfile.ZipFile(io.BytesIO(data[try_start : eocd + 22 + 65536]))
        z.namelist()
        start = try_start
        break
    except Exception:
        continue
else:
    raise SystemExit("Could not open embedded ZIP")

z = zipfile.ZipFile(io.BytesIO(data[start : eocd + 22 + 65536]))
names = z.namelist()
print(f"Embedded payload: {len(names)} files from {src} (zip at {hex(start)})")

extract_root = "/tmp/zombie-recovery-extract"
if os.path.exists(extract_root):
    shutil.rmtree(extract_root)
os.makedirs(extract_root)
z.extractall(extract_root)

for item in os.listdir(extract_root):
    sp = os.path.join(extract_root, item)
    dp = os.path.join(game, item)
    if os.path.isdir(sp):
        if os.path.exists(dp):
            shutil.rmtree(dp)
        shutil.copytree(sp, dp)
    else:
        shutil.copy2(sp, dp)

for name in ("FNA.dll", "FNA.NetStub.dll", "FNA.dll.config", "XnaToFna.dll"):
    sp = os.path.join(game, name)
    if os.path.isfile(sp):
        shutil.copy2(sp, os.path.join(lib, name))

content = os.path.join(game, "Content")
xnb = sum(
    1
    for r, d, f in os.walk(content)
    for x in f
    if x.endswith(".xnb")
)
print(f"Installed game payload -> {game}")
print(f"Content .xnb files: {xnb}")
print(f"MasterGrid.xnb: {os.path.exists(os.path.join(content, 'MasterGrid.xnb'))}")
PY

echo "Done. Run: $ROOT/run-mac.sh"
