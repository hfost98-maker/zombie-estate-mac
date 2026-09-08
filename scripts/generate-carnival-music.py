#!/usr/bin/env python3
"""Generate looping spooky carnival music stems (3 parts, like the game's MusicParts)."""

from __future__ import annotations

import math
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "game" / "CarnivalMusic"
SAMPLE_RATE = 44100
DURATION = 24.0


def clamp(x: float) -> float:
    return max(-1.0, min(1.0, x))


def sine(freq: float, t: float, amp: float = 1.0) -> float:
    return amp * math.sin(2.0 * math.pi * freq * t)


def write_wav(path: Path, samples: list[float]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "w") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(SAMPLE_RATE)
        frames = bytearray()
        for s in samples:
            v = int(clamp(s) * 32767)
            frames.extend(struct.pack("<h", v))
        wf.writeframes(frames)


def generate_bass() -> list[float]:
    """Low E-minor drone with slow wobble — creepy carnival undercurrent."""
    n = int(SAMPLE_RATE * DURATION)
    out: list[float] = []
    for i in range(n):
        t = i / SAMPLE_RATE
        wobble = 0.5 + 0.5 * math.sin(2.0 * math.pi * 0.08 * t)
        s = sine(41.2, t, 0.35) + sine(82.4, t, 0.12)
        s += sine(123.5, t, 0.05) * wobble
        s *= 0.85 + 0.15 * math.sin(2.0 * math.pi * 0.03 * t)
        out.append(s * 0.55)
    return out


def generate_calliope() -> list[float]:
    """Minor-key calliope melody — sparse, slightly detuned."""
    n = int(SAMPLE_RATE * DURATION)
    # E minor pentatonic hits (Hz)
    melody = [
        (329.6, 0.0, 0.55),
        (392.0, 0.65, 0.45),
        (440.0, 1.35, 0.5),
        (329.6, 2.1, 0.4),
        (493.9, 2.85, 0.55),
        (392.0, 3.6, 0.35),
        (329.6, 4.4, 0.5),
        (587.3, 5.15, 0.45),
        (440.0, 6.0, 0.4),
        (392.0, 6.8, 0.35),
        (329.6, 7.55, 0.5),
        (659.3, 8.35, 0.4),
        (493.9, 9.1, 0.45),
        (392.0, 9.9, 0.35),
        (329.6, 10.65, 0.5),
        (440.0, 11.45, 0.4),
        (587.3, 12.2, 0.45),
        (329.6, 13.0, 0.5),
        (392.0, 13.75, 0.35),
        (493.9, 14.55, 0.45),
        (329.6, 15.3, 0.5),
        (440.0, 16.1, 0.4),
        (392.0, 16.85, 0.35),
        (659.3, 17.65, 0.4),
        (329.6, 18.4, 0.5),
        (493.9, 19.2, 0.45),
        (392.0, 20.0, 0.35),
        (329.6, 20.75, 0.5),
        (587.3, 21.55, 0.4),
        (440.0, 22.3, 0.45),
    ]
    out = [0.0] * n
    for freq, start, dur in melody:
        for phase in (0.0, 0.004):
            f = freq * (1.0 + phase)
            end = start + dur
            i0 = int(start * SAMPLE_RATE)
            i1 = min(n, int(end * SAMPLE_RATE))
            for i in range(i0, i1):
                t = i / SAMPLE_RATE - start
                env = math.exp(-2.8 * t) * (1.0 - math.exp(-20.0 * t))
                out[i] += sine(f, t, 0.22 * env)
                out[i] += sine(f * 2.01, t, 0.08 * env)
    return [clamp(s * 0.7) for s in out]


def generate_chimes() -> list[float]:
    """High glockenspiel / wind-chime accents."""
    n = int(SAMPLE_RATE * DURATION)
    hits = [
        (880.0, 0.4),
        (987.8, 1.2),
        (1174.7, 2.0),
        (1318.5, 2.85),
        (987.8, 3.7),
        (880.0, 4.55),
        (1174.7, 5.4),
        (1568.0, 6.25),
        (987.8, 7.1),
        (880.0, 8.0),
        (1318.5, 8.85),
        (1174.7, 9.7),
        (987.8, 10.55),
        (1568.0, 11.4),
        (880.0, 12.25),
        (1174.7, 13.1),
        (987.8, 13.95),
        (1318.5, 14.8),
        (880.0, 15.65),
        (1568.0, 16.5),
        (1174.7, 17.35),
        (987.8, 18.2),
        (1318.5, 19.05),
        (880.0, 19.9),
        (1174.7, 20.75),
        (1568.0, 21.6),
        (987.8, 22.45),
    ]
    out = [0.0] * n
    for freq, start in hits:
        i0 = int(start * SAMPLE_RATE)
        i1 = min(n, i0 + int(1.8 * SAMPLE_RATE))
        for i in range(i0, i1):
            t = (i - i0) / SAMPLE_RATE
            env = math.exp(-4.5 * t)
            out[i] += sine(freq, t, 0.18 * env)
            out[i] += sine(freq * 2.756, t, 0.06 * env)
    return [clamp(s * 0.65) for s in out]


def main() -> None:
    stems = [
        ("carnival_bass.wav", generate_bass()),
        ("carnival_calliope.wav", generate_calliope()),
        ("carnival_chimes.wav", generate_chimes()),
    ]
    for name, samples in stems:
        path = OUT / name
        write_wav(path, samples)
        print(f"Wrote {path} ({len(samples) / SAMPLE_RATE:.1f}s)")


if __name__ == "__main__":
    main()
