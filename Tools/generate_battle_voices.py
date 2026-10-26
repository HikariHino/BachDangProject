"""Offline battle cues using the same locally cached VieNeu cast as dialogue.
No project text or files are uploaded. Model dependencies never enter Assets.
"""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

cache = Path(os.environ.get('BACHDANG_VOICE_CACHE', '.utmp/voice-authoring-cache')).resolve()
sys.path.insert(0, str(cache / 'voice-native-tools'))
os.environ['HF_HOME'] = str(cache / 'huggingface')
os.environ['HF_HUB_OFFLINE'] = '1'
os.environ['HF_HUB_DISABLE_PROGRESS_BARS'] = '1'
import numpy as np
import soundfile as sf
import imageio_ffmpeg
from vieneu import Vieneu

lines = [
    ('Enemy_Advance_1', 'Thái Sơn', 'Xông lên! Chiếm lấy bờ sông!', 1.22),
    ('Enemy_Advance_2', 'Đức Trí', 'Tiến lên! Quân ta sắp thắng rồi!', 1.23),
    ('Enemy_Panic_1', 'Thái Sơn', 'Trúng mai phục rồi! Rút lui! Rút lui!', 1.25),
    ('Enemy_Panic_2', 'Đức Trí', 'Thuyền mắc cọc rồi! Cứu ta với!', 1.22),
    ('Enemy_Panic_3', 'Hải Đăng', 'Bỏ thuyền! Mau chạy! Chạy đi!', 1.24),
    ('NgoQuyen_Ambush', 'Minh Đức', 'Thời cơ đã đến! Bắn! Toàn quân, đánh!', 1.14),
    ('NgoQuyen_Attack', 'Minh Đức', 'Đánh! Quyết không để giặc thoát!', 1.14),
]
stage = cache / 'battle-cues'
dest = Path('Assets/Resources/BachDang938/BattleVoices')
stage.mkdir(parents=True, exist_ok=True)
dest.mkdir(parents=True, exist_ok=True)
tts = None
entries = []
def raw_take(name, voice, text, tempo):
    global tts
    key = hashlib.sha256(json.dumps([name, voice, text, tempo], ensure_ascii=False).encode()).hexdigest()[:20]
    raw = stage / (key + '.wav')
    if not raw.exists():
        if tts is None:
            tts = Vieneu(backend='onnx', threads=3)
        audio = tts.infer(text, voice=voice, temperature=.75, silence_p=.06)
        if len(audio) < 12000 or not np.all(np.isfinite(audio)):
            raise ValueError('Invalid generated cue ' + name)
        active = np.flatnonzero(np.abs(audio) > .008)
        if not len(active):
            raise ValueError('Silent generated cue ' + name)
        audio = audio[max(0, active[0]-2400):min(len(audio), active[-1]+4800)]
        rms = np.sqrt(np.mean(audio**2))
        audio *= min(.09/max(rms, 1e-6), .88/max(np.max(np.abs(audio)), 1e-6))
        sf.write(raw, audio, 48000, subtype='PCM_16')
    return raw

def master(raw, output, tempo):
    shaped = stage / 'master-working.wav'
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), '-v', 'error', '-y', '-i', str(raw),
                    '-af', 'atempo='+str(tempo)+',highpass=f=85,equalizer=f=1800:t=q:w=0.8:g=2,acompressor=threshold=0.12:ratio=2.5:attack=5:release=90:makeup=2',
                    '-ar', '32000', '-ac', '1', str(shaped)], check=True)
    audio, rate = sf.read(shaped)
    audio *= min(.16/max(float(np.sqrt(np.mean(audio**2))),1e-6), .85/max(float(np.max(np.abs(audio))),1e-6))
    sf.write(shaped, audio, rate, subtype='PCM_16')
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), '-v', 'error', '-y', '-i', str(shaped),
                    '-codec:a', 'libmp3lame', '-b:a', '96k', str(output)], check=True)

for name, voice, text, tempo in lines:
    raw = raw_take(name, voice, text, tempo)
    output = dest / (name + '.mp3')
    master(raw, output, tempo)
    entries.append(dict(name=name, voice='VieNeu/'+voice, text=text, tempo=tempo, mastering='battle-presence-v2', bytes=output.stat().st_size))
    print('CUE', name, output.stat().st_size, flush=True)

# Three different performances form the approaching army's shout, instead
# of adding gain to a single quiet narrator. These raw takes stay outside Assets.
parts=[]
for i, voice in enumerate(['Quốc Tuấn', 'Hải Đăng', 'Đức Trí']):
    raw=raw_take('Crowd_'+str(i), voice, 'Xông lên! Xông lên!', 1.15)
    audio, rate=sf.read(raw)
    audio=np.interp(np.arange(int(len(audio)/(1+(i-1)*.018)))*(1+(i-1)*.018),np.arange(len(audio)),audio)
    parts.append((int(rate*i*.13),audio))
mix=np.zeros(max(offset+len(audio) for offset,audio in parts))
for offset,audio in parts:mix[offset:offset+len(audio)]+=audio/3
raw=stage/'crowd-three-voices.wav';sf.write(raw,mix,48000,subtype='PCM_16')
output=dest/'Enemy_Cheer_Crowd.mp3';master(raw,output,1.15)
entries.append(dict(name='Enemy_Cheer_Crowd',voice='VieNeu/Quốc Tuấn + Hải Đăng + Đức Trí',text='Xông lên! Xông lên!',tempo=1.15,mastering='battle-presence-v2',bytes=output.stat().st_size))
(dest / 'manifest.json').write_text(json.dumps(dict(entries=entries), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print('DONE', len(entries), 'offline battle cues', flush=True)
