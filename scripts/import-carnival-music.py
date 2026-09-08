#!/usr/bin/env python3
"""Import licensed carnival music into game/CarnivalMusic/.

Slot 1 + slot 2 together enable alternating playback on the Carnival map:
  track 1 -> track 2 -> track 1 -> ...

Recommended Epidemic Sound pair:
  Slot 1: Magnifying Glass — Ch@ntarelle
  Slot 2: Gargoyle — Eden Avery

Examples:
  python3 scripts/import-carnival-music.py --slot 1 ~/Downloads/"Magnifying Glass.mp3" \\
      --title "Magnifying Glass" --artist "Ch@ntarelle"
  python3 scripts/import-carnival-music.py --slot 2 ~/Downloads/"Gargoyle.mp3" \\
      --title "Gargoyle" --artist "Eden Avery"
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / "game" / "CarnivalMusic"
SOURCE_DIR = OUT_DIR / "source"
META = OUT_DIR / "carnival_tracks.json"

SLOT_FILES = {
    1: OUT_DIR / "carnival_track_1.wav",
    2: OUT_DIR / "carnival_track_2.wav",
}

TARGET_RATE = 44100
TARGET_CHANNELS = 2


def find_ffmpeg() -> str | None:
    for name in ("ffmpeg", "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg"):
        path = shutil.which(name) if not name.startswith("/") else (name if Path(name).exists() else None)
        if path:
            return path
    return None


def wav_is_compatible(path: Path) -> bool:
    try:
        with wave.open(str(path), "rb") as wf:
            return (
                wf.getframerate() == TARGET_RATE
                and wf.getnchannels() in (1, 2)
                and wf.getsampwidth() == 2
            )
    except wave.Error:
        return False


def convert_with_ffmpeg(ffmpeg: str, src: Path, dst: Path) -> None:
    dst.parent.mkdir(parents=True, exist_ok=True)
    cmd = [
        ffmpeg,
        "-y",
        "-i",
        str(src),
        "-ar",
        str(TARGET_RATE),
        "-ac",
        str(TARGET_CHANNELS),
        "-sample_fmt",
        "s16",
        str(dst),
    ]
    subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)


def load_meta() -> dict:
    if META.exists():
        return json.loads(META.read_text())
    return {"tracks": {}}


def save_meta(data: dict) -> None:
    META.write_text(json.dumps(data, indent=2) + "\n")


def import_track(slot: int, src: Path, title: str | None, artist: str | None) -> None:
    if slot not in SLOT_FILES:
        raise SystemExit("--slot must be 1 or 2")

    if not src.is_file():
        raise SystemExit(f"Input not found: {src}")

    ffmpeg = find_ffmpeg()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    dst = SLOT_FILES[slot]

    if src.suffix.lower() == ".wav" and wav_is_compatible(src):
        shutil.copy2(src, dst)
        print(f"Copied compatible WAV -> {dst}")
    elif ffmpeg:
        convert_with_ffmpeg(ffmpeg, src, dst)
        print(f"Converted with ffmpeg -> {dst}")
    else:
        raise SystemExit(
            "Need ffmpeg to convert this file (brew install ffmpeg).\n"
            "Or provide a 44.1kHz 16-bit mono/stereo PCM .wav file."
        )

    meta = load_meta()
    meta["tracks"][str(slot)] = {
        "file": dst.name,
        "title": title or dst.stem,
        "artist": artist or "",
        "source": src.name,
    }
    save_meta(meta)

    size_mb = dst.stat().st_size / (1024 * 1024)
    with wave.open(str(dst), "rb") as wf:
        duration = wf.getnframes() / wf.getframerate()
    print(f"  Slot {slot}: {title or dst.stem} — {duration:.1f}s, {size_mb:.1f} MB")

    if SLOT_FILES[1].exists() and SLOT_FILES[2].exists():
        print("Both slots ready — Carnival will alternate track 1 and track 2.")
    else:
        missing = 2 if slot == 1 else 1
        print(f"Import slot {missing} to enable alternating playback.")


def main() -> None:
    parser = argparse.ArgumentParser(description="Import custom carnival level music")
    parser.add_argument(
        "input",
        nargs="?",
        help="Audio file (.mp3, .wav, .m4a, …). With --slot, required unless using source/ defaults.",
    )
    parser.add_argument(
        "--slot",
        type=int,
        choices=(1, 2),
        help="Playlist slot: 1 = first track, 2 = second track (alternates when both exist)",
    )
    parser.add_argument("--title", help="Track title")
    parser.add_argument("--artist", help="Artist name")
    args = parser.parse_args()

    slot = args.slot or 1

    if args.input:
        src = Path(args.input).expanduser().resolve()
    else:
        SOURCE_DIR.mkdir(parents=True, exist_ok=True)
        preferred = {
            1: ("magnifying", "chantarelle", "track1", "1"),
            2: ("gargoyle", "eden", "track2", "2"),
        }
        candidates = sorted(
            p
            for p in SOURCE_DIR.iterdir()
            if p.is_file() and p.suffix.lower() in {".mp3", ".wav", ".m4a", ".aac", ".flac", ".ogg"}
        )
        if not candidates:
            raise SystemExit(
                "No input file. Example:\n"
                '  python3 scripts/import-carnival-music.py --slot 1 ~/Downloads/"Magnifying Glass.mp3"\n'
                '  python3 scripts/import-carnival-music.py --slot 2 ~/Downloads/"Gargoyle.mp3"'
            )
        keys = preferred[slot]
        picked = None
        for p in candidates:
            name = p.stem.lower()
            if any(k in name for k in keys):
                picked = p
                break
        src = picked or candidates[min(slot - 1, len(candidates) - 1)]
        print(f"Using {src}")

    defaults = {
        1: ("Magnifying Glass", "Ch@ntarelle"),
        2: ("Gargoyle", "Eden Avery"),
    }
    title = args.title or defaults[slot][0]
    artist = args.artist or defaults[slot][1]
    import_track(slot, src, title, artist)


if __name__ == "__main__":
    main()
