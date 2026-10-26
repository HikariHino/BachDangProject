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
    ('NgoQuyen_Ambush', 'Minh Đức', 'Thời cơ đã đến! Quân sĩ hai bờ, bắn tên!', 1.18),
    ('NgoQuyen_Attack', 'Minh Đức', 'Toàn quân phản công! Không để giặc thoát!', 1.18),
]
stage = cache / 'battle-cues'
dest = Path('Assets/Resources/BachDang938/BattleVoices')
stage.mkdir(parents=True, exist_ok=True)
dest.mkdir(parents=True, exist_ok=True)
tts = None
entries = []
for name, voice, text, tempo in lines:
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
    output = dest / (name + '.mp3')
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), '-v', 'error', '-y', '-i', str(raw),
                    '-af', 'atempo=' + str(tempo), '-ar', '32000', '-ac', '1',
                    '-codec:a', 'libmp3lame', '-b:a', '96k', str(output)], check=True)
    entries.append(dict(name=name, voice='VieNeu/'+voice, text=text, tempo=tempo, bytes=output.stat().st_size))
    print('CUE', name, output.stat().st_size, flush=True)
(dest / 'manifest.json').write_text(json.dumps(dict(entries=entries), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print('DONE', len(entries), 'offline battle cues', flush=True)
