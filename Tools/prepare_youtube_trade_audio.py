"""Short recorded work foley. Source recordings remain in ignored Logs/TradeAudioSources.
Attribution/creator permissions are in Docs/TradeAudioSources.md. Requires ffmpeg/NumPy.
"""
from pathlib import Path
import hashlib
import json
import subprocess
import wave
import numpy as np

RATE = 32000
ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / 'Logs/TradeAudioSources'
OUT = ROOT / 'Assets/Resources/BachDang938/WorkAudio'
CUTS = [
    ('Saw', 'UJ72WfPN4ek', 14.05, 15.25, .026, 3500),
    ('Rope', '3XnP7nX_Z_s', 12.00, 13.10, .022, 3200),
    ('Cloth', '5JhQIIPQaE0', 1.35, 2.35, .020, 3800),
    ('Scoop', 's2-CF2g5_q0', 1.15, 1.57, .025, 4500),
    ('Pour', 's2-CF2g5_q0', 5.05, 5.82, .027, 4500),
    ('Crease', '5JhQIIPQaE0', 2.45, 2.80, .020, 3300),
    ('Knot', '3XnP7nX_Z_s', 14.15, 14.55, .024, 3200),
    ('SetDown', '5JhQIIPQaE0', 2.05, 2.35, .022, 3200),
]


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    sources, records = {}, []
    for name, video, start, end, rms, low in CUTS:
        if video not in sources:
            path = next(p for p in CACHE.glob(video + '.*') if p.suffix in ('.webm', '.m4a'))
            raw = subprocess.check_output(['ffmpeg', '-v', 'error', '-i', str(path), '-ac', '1', '-ar', str(RATE), '-f', 'f32le', '-'])
            sources[video] = np.frombuffer(raw, '<f4').astype(float)
        audio = sources[video][round(start*RATE):round(end*RATE)].copy()
        hz = np.fft.rfftfreq(len(audio), 1/RATE)
        response = 1 / np.sqrt(1 + (90 / np.maximum(.01, hz))**4)
        response *= 1 / np.sqrt(1 + (hz/low)**8)
        audio = np.fft.irfft(np.fft.rfft(audio)*response, n=len(audio)); audio -= audio.mean()
        gain = min(12, rms / max(1e-6, np.sqrt(np.mean(audio**2))), .22 / max(1e-6, np.max(np.abs(audio))))
        audio *= gain
        fade = min(640, len(audio)//8)
        audio[:fade] *= np.linspace(0, 1, fade); audio[-fade:] *= np.linspace(1, 0, fade)
        output = OUT / ('Trade_' + name + '.wav')
        with wave.open(str(output), 'wb') as w:
            w.setparams((1, 2, RATE, 0, 'NONE', 'PCM')); w.writeframes((audio*32767).astype('<i2').tobytes())
        records.append(dict(name=name, source='https://www.youtube.com/watch?v='+video, start=start, end=end,
            rms=round(float(np.sqrt(np.mean(audio**2))), 5), peak=round(float(np.max(np.abs(audio))), 5), sha256=hashlib.sha256(output.read_bytes()).hexdigest()))
    (ROOT/'Tools/trade_audio_manifest.json').write_text(json.dumps(records, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(records, indent=2))


if __name__ == '__main__':
    main()
