"""Mixes the five NJ-Sort sounds (assets/sfx/sort|save|undo|info|error.wav) from real Mixkit recordings in sfx/ (git-ignored).

Mixkit Sound Effects Free License only (sfx/CREDITS.md). Same method as NJ-Flush's make_sfx.py: decode a trim with short
fades, place it on a cue, mix, scale to a low peak (Nader 2026-11-23: "10%, smooth, not loud, just sfx").
The sort sound is cut to Animator's cascade (src/Desktop/Animator.cs): icons start every Step ms (9 to 16 ms) and each takes Dur
(460 ms); the first land at ~0.34 s and the last at ~0.7 s, so the soft taps sit between those; the whoosh swells with the motion.
Needs ffmpeg on PATH (decode only). Stdlib otherwise.
"""
import array, subprocess, wave
from pathlib import Path

HERE = Path(__file__).parent
SR = 44100
SFX = {p.name.split("_")[0]: p for p in (HERE / "sfx").glob("mk-*.wav")}
OUT = HERE / "assets" / "sfx"


def load(key, start=0.0, end=None, pitch=1.0, rev=False, fade_in=0.004, fade_out=0.02):
    """Decode a trim to mono float samples; pitch resamples (speed and pitch together); short fades so no edge clicks."""
    args = ["ffmpeg", "-v", "error", "-ss", str(start), "-i", str(SFX[key])]
    if end is not None:
        args += ["-t", str(end - start)]
    if pitch != 1.0:
        args += ["-af", f"asetrate={SR * pitch},aresample={SR}"]
    raw = subprocess.run(args + ["-ac", "1", "-ar", str(SR), "-f", "s16le", "-"], capture_output=True, check=True).stdout
    x = [v / 32768 for v in array.array("h", raw)]
    if rev:
        x.reverse()
    fi, fo = int(fade_in * SR), int(fade_out * SR)
    for i in range(min(fi, len(x))):
        x[i] *= i / fi
    for i in range(min(fo, len(x))):
        x[-1 - i] *= (i / fo) ** 2
    return x


def db(g):
    return 10 ** (g / 20)


def mix(name, events, length_s, peak):
    """events: (at_ms, samples, gain_db). Writes assets/sfx/<name>.wav at `peak` of full scale."""
    out = [0.0] * int(length_s * SR)
    for at, x, g in events:
        s, k = int(at * SR / 1000), db(g)
        for i, v in enumerate(x):
            if 0 <= s + i < len(out):
                out[s + i] += v * k
    top = max(abs(v) for v in out) or 1
    data = array.array("h", (int(v / top * peak * 32767) for v in out))
    OUT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUT / f"{name}.wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data.tobytes())
    print(f"{name}.wav  {length_s:.2f}s  peak {peak:.2f}")


# sort: a steam swoosh swelling with the cascade, and light bubble-pop taps as the icons land (first ~340 ms, last ~700 ms)
whoosh = load("mk-1458", 0.12, 1.05, fade_in=0.15, fade_out=0.45)
pops = [load("mk-732", s, s + 0.07, pitch=p, fade_out=0.04) for s, p in ((0.0, 1.0), (0.09, 1.15), (0.17, 0.9), (0.26, 1.3))]
taps = [(340 + i * 38, pops[i % 4], -9 - (i % 3) * 2) for i in range(10)]
mix("sort", [(0, whoosh, -3)] + taps, 1.2, 0.10)

# save: a gentle confirm, a crystal chime with one soft tap under it
mix("save", [(0, pops[0], -8), (30, load("mk-3108", 0.0, 0.9, fade_out=0.5), 0)], 1.0, 0.10)

# undo: the swoosh played backwards (a reverse swish), a soft pop where it lands
mix("undo", [(0, load("mk-1458", 0.2, 0.75, rev=True, fade_in=0.2, fade_out=0.02), -2), (520, pops[2], -10)], 0.85, 0.10)

# info: a soft ding, the chime a third higher and short
mix("info", [(0, load("mk-3108", 0.0, 0.5, pitch=1.26, fade_out=0.3), 0)], 0.55, 0.08)

# error: a soft low two-note, the chime an octave and a fourth down, falling
mix("error", [(0, load("mk-3108", 0.0, 0.45, pitch=0.5, fade_out=0.25), 0), (170, load("mk-3108", 0.0, 0.5, pitch=0.42, fade_out=0.3), 0)], 0.8, 0.08)
