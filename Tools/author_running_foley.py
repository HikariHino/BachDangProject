"""Import recorded CC0 Fantozzi sand/grass contacts; see Docs/RunningFootsteps.md."""
import hashlib, json, subprocess, wave
from pathlib import Path
import numpy as np

folder = Path('Assets/Resources/BachDang938/RunSteps')
source = Path('Logs/RunningFoleySource/Fantozzi-footsteps/flac')
report, preview = [], []
rate = 24000
for index, side in enumerate(('L1', 'R1', 'L2', 'R2', 'L3', 'R3'), 1):
    original = source / f'Fantozzi-Sand{side}.flac'
    raw = subprocess.check_output(['ffmpeg', '-v', 'error', '-i', str(original), '-ac', '1', '-ar', str(rate),
                                   '-af', 'highpass=f=65,lowpass=f=3800', '-f', 'f32le', '-'])
    samples = np.frombuffer(raw, dtype='<f4').copy()
    active = np.flatnonzero(abs(samples) > .012)
    assert active.size, original
    samples = samples[max(0, active[0]-120):min(len(samples), active[-1]+480)]
    samples -= samples.mean()
    samples *= min(.10/max(np.sqrt(np.mean(samples*samples)), 1e-9), .67/max(abs(samples)))
    fade = min(96, len(samples)//4)
    samples[:fade] *= np.linspace(0, 1, fade)
    samples[-fade:] *= np.linspace(1, 0, fade)
    output = folder / f'Boot_Run_{index}.wav'
    with wave.open(str(output), 'wb') as audio:
        audio.setnchannels(1); audio.setsampwidth(2); audio.setframerate(rate)
        audio.writeframes(np.round(samples*32767).astype('<i2').tobytes())
    report.append(dict(clip=output.name,source=original.name,sourceSHA256=hashlib.sha256(original.read_bytes()).hexdigest(),
                       seconds=len(samples)/rate,peak=float(max(abs(samples))),rms=float(np.sqrt(np.mean(samples*samples)))))
    preview.extend(samples*.48)
    preview.extend(np.zeros(max(0, round(rate*.31)-len(samples))))
with wave.open('Logs/Running_Recorded_Preview.wav', 'wb') as audio:
    audio.setnchannels(1); audio.setsampwidth(2); audio.setframerate(rate)
    audio.writeframes(np.round(np.array(preview)*32767).astype('<i2').tobytes())
Path('Logs/RunningFoleyRecorded.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))
