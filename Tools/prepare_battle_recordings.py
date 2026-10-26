"""Short actor/crowd excerpts from Bach Dang battle films, requested by the player.
Full sources and captions remain in ignored Logs/BattleVoiceSources. This does
not grant a license to the source films; attribution/timestamps are recorded.
"""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
import numpy as np

ROOT=Path('Logs/BattleVoiceSources')
OUT=Path('Assets/Resources/BachDang938/BattleVoices')
RATE=32000
CLIPS=[
    ('Enemy_Advance_1','ok_KBwgS9eg',230.28,233.35),
    ('Enemy_Advance_2','ok_KBwgS9eg',231.32,233.35), # legacy resource; no longer in the playlist
    ('Enemy_Cheer_Crowd','ok_KBwgS9eg',212.20,214.40),
    ('Enemy_Panic_1','ok_KBwgS9eg',308.35,310.10),
    ('Enemy_Panic_2','ok_KBwgS9eg',283.10,284.80),
    ('Enemy_Panic_3','ok_KBwgS9eg',289.90,291.50),
    ('NgoQuyen_Ambush','ok_KBwgS9eg',268.16,270.87),
    ('NgoQuyen_Attack','FEdW8VpWHA8',128.62,137.80),
]
def main():
    ffmpeg=shutil.which('ffmpeg')
    if not ffmpeg:raise RuntimeError('ffmpeg is required')
    OUT.mkdir(parents=True,exist_ok=True)
    sources={};entries=[]
    for name,video,start,end in CLIPS:
        if video not in sources:
            source=next(p for p in ROOT.glob(video+'.*') if p.suffix in ('.webm','.m4a','.mp4'))
            result=subprocess.run([ffmpeg,'-v','error','-i',str(source),'-f','f32le','-ac','1','-ar',str(RATE),'-'],check=True,capture_output=True)
            sources[video]=np.frombuffer(result.stdout,'<f4').astype(float)
        audio=sources[video][round(start*RATE):round(end*RATE)].copy()
        # Remove film rumble/hiss while retaining the recorded attack and voice.
        hz=np.fft.rfftfreq(len(audio),1/RATE)
        response=(1/np.sqrt(1+(110/np.maximum(hz,.01))**4))*(1/np.sqrt(1+(hz/6500)**8))
        audio=np.fft.irfft(np.fft.rfft(audio)*response,n=len(audio));audio-=audio.mean()
        audio*=min(.16/max(1e-5,float(np.sqrt(np.mean(audio*audio)))),.78/max(1e-5,float(np.max(np.abs(audio)))))
        fade_in=min(480,len(audio)//10);fade_out=min(2560,len(audio)//10)
        audio[:fade_in]*=np.linspace(0,1,fade_in);audio[-fade_out:]*=np.linspace(1,0,fade_out)
        output=OUT/(name+'.mp3')
        subprocess.run([ffmpeg,'-v','error','-y','-f','f32le','-ar',str(RATE),'-ac','1','-i','-',
                        '-codec:a','libmp3lame','-b:a','96k',str(output)],input=audio.astype('<f4').tobytes(),check=True)
        entries.append(dict(name=name,source='https://www.youtube.com/watch?v='+video,start=start,end=end,tempo=1.0,kind='recorded-film-excerpt',sha256=hashlib.sha256(output.read_bytes()).hexdigest(),bytes=output.stat().st_size))
        print(name,output.stat().st_size,flush=True)
    (OUT/'manifest.json').write_text(json.dumps(dict(entries=entries),indent=2)+'\n',encoding='utf-8')
if __name__=='__main__':main()
