"""Original, deterministic work foley. Requires NumPy; no external recordings.

Run from the project root. PCM mono 32 kHz, short fades, no voice or music.
"""
from pathlib import Path
import json
import wave
import numpy as np

RATE = 32000
RNG = np.random.default_rng(9381028)
OUT = Path(__file__).resolve().parents[1] / "Assets/Resources/BachDang938/WorkAudio"


def noise(seconds, window):
    samples = int(seconds * RATE)
    white = RNG.normal(size=samples)
    return np.convolve(white, np.ones(window) / window, mode="same")


def grains(seconds, count, tone):
    x = np.zeros(int(seconds * RATE))
    for _ in range(count):
        start = int(RNG.uniform(0, len(x) - 500))
        length = int(RNG.uniform(90, 450))
        t = np.arange(length) / RATE
        pulse = np.exp(-t * 500) * (RNG.normal(size=length) * .25 + np.sin(t * tone * RNG.uniform(.8, 1.2) * 2 * np.pi))
        x[start:start + length] += pulse * RNG.uniform(.15, .6)
    return x


def make():
    OUT.mkdir(parents=True, exist_ok=True)
    stats = {}
    for name, duration in [("Saw", 1.1), ("Rope", 1.0), ("Cloth", 1.0), ("Scoop", .38), ("Pour", .65), ("Crease", .28), ("Knot", .2), ("SetDown", .3)]:
        t = np.arange(int(duration * RATE)) / RATE
        if name == "Saw":
            rasp = noise(duration, 8) - noise(duration, 65) * .3
            x = rasp * (.55 + .25 * np.sin(2 * np.pi * 71 * t) + .1 * np.sin(2 * np.pi * 137 * t))
        elif name == "Rope":
            x = noise(duration, 22) * (.6 + .3 * np.sin(2 * np.pi * 23 * t)) + grains(duration, 28, 340) * .1
        elif name == "Cloth":
            x = noise(duration, 15) * (.65 + .3 * np.sin(2 * np.pi * 9 * t)) + noise(duration, 80) * .3
        elif name in ("Scoop", "Pour"):
            x = grains(duration, 65 if name == "Scoop" else 150, 1800) * .8 + noise(duration, 5) * .15
        elif name == "Crease":
            x = noise(duration, 18) * np.exp(-t * 8) + grains(duration, 12, 460) * .1
        elif name == "Knot":
            x = (noise(duration, 25) + np.sin(t * 2 * np.pi * 170) * .08) * np.exp(-t * 20)
        else:
            x = (np.sin(t * 2 * np.pi * 95) * .4 + noise(duration, 18)) * np.exp(-t * 24)
        x -= x.mean()
        # Smooth endpoints, including the friction loops, to avoid seam clicks.
        fade = int(.015 * RATE)
        x[:fade] *= np.linspace(0, 1, fade)
        x[-fade:] *= np.linspace(1, 0, fade)
        x *= .62 / max(.001, np.max(np.abs(x)))
        pcm = (x * 32767).astype("<i2")
        path = OUT / f"Trade_{name}.wav"
        with wave.open(str(path), "wb") as f:
            f.setnchannels(1)
            f.setsampwidth(2)
            f.setframerate(RATE)
            f.writeframes(pcm.tobytes())
        stats[name] = {"seconds": duration, "peak": round(float(np.max(np.abs(x))), 3), "rms": round(float(np.sqrt(np.mean(x*x))), 4), "bytes": path.stat().st_size}
    print(json.dumps(stats, indent=2))


if __name__ == "__main__":
    make()
