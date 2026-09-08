# Carnival level music

The Carnival map supports **two alternating licensed tracks** or a procedural fallback.

## Recommended Epidemic Sound playlist

| Slot | Track | Artist | Link |
|------|-------|--------|------|
| 1 | Magnifying Glass | Ch@ntarelle | [Epidemic Sound](https://www.epidemicsound.com/music/tracks/6f310f3b-e924-427f-a8e4-99754a496f10/) |
| 2 | Gargoyle | Eden Avery | [Epidemic Sound](https://www.epidemicsound.com/music/tracks/425f9295-1857-499b-a613-3b00709fb907/) |

When **both** slots are imported, the game plays track 1, then track 2, then back to track 1, and so on.

## Import

Download both tracks from your Epidemic Sound account, then:

```bash
python3 scripts/import-carnival-music.py --slot 1 ~/Downloads/"Magnifying Glass.mp3"
python3 scripts/import-carnival-music.py --slot 2 ~/Downloads/"Gargoyle.mp3"
```

Or drop files into `game/CarnivalMusic/source/` (name them with `magnifying` / `gargoyle` if you can) and run:

```bash
python3 scripts/import-carnival-music.py --slot 1
python3 scripts/import-carnival-music.py --slot 2
```

## Play

```bash
./scripts/play-carnival.sh
```

Enable **Music: On** in the options menu. On Carnival, the console logs which track is playing as they alternate.

## Files

| File | Purpose |
|------|---------|
| `carnival_track_1.wav` | Playlist slot 1 |
| `carnival_track_2.wav` | Playlist slot 2 |
| `carnival_tracks.json` | Title/artist metadata |
| `carnival_main.wav` | Legacy single-track slot (used as slot 1 fallback) |
| `source/` | Drop Epidemic Sound downloads here |
| `carnival_*.wav` | Procedural fallback (if no custom tracks) |

## Requirements

- **ffmpeg** for MP3/M4A: `brew install ffmpeg`
- 44.1 kHz 16-bit PCM WAV can be copied without ffmpeg.

## Licensing

Only use music you are licensed to use. Epidemic Sound downloads follow your subscription terms.
