"""Cut the explicitly reusable YouTube recordings listed in Docs/ActionAudioSources.md.
Requires ffmpeg and numpy. Source WebM/M4A files stay in ignored Logs/ActionAudioSources.
No project files are sent to YouTube. Output is 32 kHz mono PCM with short fades.
"""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
import wave
import numpy as np

RATE = 32000
CACHE = Path('Logs/ActionAudioSources')
OUT = Path('Assets/Resources/BachDang938/ActionAudio')
OUT.mkdir(parents=True, exist_ok=True)
ffmpeg = shutil.which('ffmpeg')
if not ffmpeg:
    raise RuntimeError('ffmpeg must be on PATH')
sources = {}
records = []

def source(video):
    if video not in sources:
        path = next(p for p in CACHE.glob(video + '.*') if p.suffix in ('.webm', '.m4a'))
        result = subprocess.run([ffmpeg, '-v', 'error', '-i', str(path), '-f', 'f32le', '-ar', str(RATE), '-ac', '1', '-'], check=True, capture_output=True)
        sources[video] = np.frombuffer(result.stdout, '<f4').astype(float)
    return sources[video]

def cut(name, video, start, end, rms=.12, speed=1, high=65, low=6500):
    x = source(video)[round(start * RATE):round(end * RATE)].copy()
    # Remove rumble and high hiss without replacing the recorded texture.
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    response = 1 / np.sqrt(1 + (high / np.maximum(f, .001)) ** 4)
    response *= 1 / np.sqrt(1 + (f / low) ** 8)
    x = np.fft.irfft(np.fft.rfft(x) * response, n=len(x))
    if speed != 1:
        x = np.interp(np.arange(0, len(x), speed), np.arange(len(x)), x)
    x -= x.mean()
    # RMS matching with a strict peak ceiling: no hard clipping or loudness jumps.
    gain = min(rms / max(1e-5, np.sqrt(np.mean(x*x))), .72 / max(1e-5, np.max(abs(x))))
    x *= gain
    fade_in = min(round(.004 * RATE), len(x) // 8)
    fade_out = min(round(.035 * RATE), len(x) // 5)
    x[:fade_in] *= np.linspace(0, 1, fade_in)
    x[-fade_out:] *= np.linspace(1, 0, fade_out)
    path = OUT / (name + '.wav')
    with wave.open(str(path), 'wb') as w:
        w.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        w.writeframes((np.clip(x, -.999, .999) * 32767).astype('<i2').tobytes())
    records.append(dict(name=name, video=video, start=start, end=end, speed=speed,
                        seconds=round(len(x)/RATE, 4), peak=round(float(abs(x).max()), 5),
                        rms=round(float(np.sqrt(np.mean(x*x))), 5), sha256=hashlib.sha256(path.read_bytes()).hexdigest()))

# Distinct individual steps, never a walking loop that continues after stopping.
for i, peak in enumerate([1.12, 1.82, 3.09, 3.71, 4.33, 5.57], 1):
    cut(f'Walk_Grass_{i:02}', 'nbcNpkmUC0Q', peak-.105, peak+.29, rms=.10, high=80)
for i, peak in enumerate([19.72, 20.44, 20.97, 21.59, 23.8, 24.42], 1):
    cut(f'Run_Grass_{i:02}', 'nbcNpkmUC0Q', peak-.08, peak+.235, rms=.13, speed=1.07, high=80)
for i, peak in enumerate([22.43, 22.92, 23.38, 23.87, 24.35, 24.85], 1):
    cut(f'Deck_Wood_{i:02}', 'Ae6DnC84jaY', peak-.085, peak+.27, rms=.10, high=85)
for i, peak in enumerate([8.76, 11.49, 14.16, 16.90], 1):
    cut(f'Oar_Stroke_{i:02}', 'h-zpC8JTLbM', peak-.16, peak+.66, rms=.12, high=55, low=7500)
for i, peak in enumerate([1.54, 5.39, 9.70], 1):
    cut(f'Bow_Release_{i:02}', 'K9HLsnmZoic', peak-.14, peak+.44, rms=.15, high=75, low=8000)
cut('Arrow_Flesh_01', 'K9HLsnmZoic', 6.32, 6.80, rms=.16, high=65, low=7000)
for i, peak in enumerate([.54, 2.04, 3.29, 5.01, 7.03], 1):
    cut(f'Sword_Swing_{i:02}', '70IZTBE03mU', peak-.20, peak+.34, rms=.14, high=100, low=8000)

manifest = dict(sample_rate=RATE, channels=1, clips=records)
Path('Tools/action_audio_manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
print(json.dumps(dict(clips=len(records), bytes=sum(p.stat().st_size for p in OUT.glob('*.wav')),
                      max_peak=max(r['peak'] for r in records), min_rms=min(r['rms'] for r in records))))
