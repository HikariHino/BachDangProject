"""Extract three short human cries from HaelDB's CC0 Male Grunt/Yelling pack.
Download https://opengameart.org/sites/default/files/yelling%20sounds.zip
to Logs/yelling-sounds.zip first. No project content is uploaded.
"""
from pathlib import Path
import io
import wave
import zipfile
import numpy as np

folder = Path('Assets/Resources/BachDang938/WreckAudio')
folder.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile('Logs/yelling-sounds.zip') as archive:
    for index, name in enumerate(['1yell4', '2yell2', '3yell5'], 1):
        with wave.open(io.BytesIO(archive.read('yelling sounds/' + name + '.wav'))) as source:
            rate = source.getframerate()
            samples = np.frombuffer(source.readframes(source.getnframes()), '<i2').astype(float).reshape(-1, source.getnchannels()).mean(axis=1) / 32768
        active = np.flatnonzero(np.abs(samples) > .015)
        if len(active):
            samples = samples[max(0, active[0] - int(.025 * rate)):min(len(samples), active[-1] + int(.1 * rate))]
        samples -= samples.mean()
        samples *= .70 / max(.01, np.abs(samples).max())
        samples = np.interp(np.arange(int(len(samples) * 32000 / rate)) * rate / 32000, np.arange(len(samples)), samples)
        fade = min(640, len(samples) // 5)
        samples[:fade] *= np.linspace(0, 1, fade)
        samples[-fade:] *= np.linspace(1, 0, fade)
        path = folder / f'Shipwreck_Cry_{index}.wav'
        with wave.open(str(path), 'wb') as dest:
            dest.setparams((1, 2, 32000, 0, 'NONE', 'not compressed'))
            dest.writeframes((np.clip(samples, -1, 1) * 32767).astype('<i2').tobytes())
        print(path, round(len(samples) / 32000, 2))
