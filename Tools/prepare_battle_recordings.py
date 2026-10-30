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
    ('Enemy_Advance_1','ok_KBwgS9eg',99.03,102.55),
    ('Enemy_Advance_2','ok_KBwgS9eg',230.28,233.35), # complete pursuit order, only when the bait engages
    ('Enemy_Cheer_Crowd','ok_KBwgS9eg',212.20,214.40),
    ('Enemy_Panic_1','ok_KBwgS9eg',308.35,310.10),
    ('Enemy_Panic_2','ok_KBwgS9eg',283.10,284.80),
    ('Enemy_Panic_3','ok_KBwgS9eg',289.90,291.50),
    ('NgoQuyen_Ambush','ok_KBwgS9eg',268.16,270.87),
    ('NgoQuyen_Attack','FEdW8VpWHA8',128.62,137.35),
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
        if name=='NgoQuyen_Attack':
            source=next(p for p in ROOT.glob(video+'.*') if p.suffix in ('.webm','.m4a','.mp4'))
            raw=subprocess.check_output([ffmpeg,'-v','error','-i',str(source),'-f','f32le','-ac','2','-ar',str(RATE),'-'])
            stereo=np.frombuffer(raw,'<f4').reshape(-1,2)[round(start*RATE):round(end*RATE)].astype(float)
            mid=np.pad(stereo.mean(axis=1),(512,1024));side=np.pad((stereo[:,0]-stereo[:,1])*.5,(512,1024));window=np.hanning(1024)
            clean=np.zeros(len(mid));weight=np.zeros(len(mid))
            for offset in range(0,len(mid)-1024+1,256):
                m=np.fft.rfft(mid[offset:offset+1024]*window);spectrum=np.fft.rfft(side[offset:offset+1024]*window)
                mask=np.clip(1-1.25*np.abs(spectrum)/np.maximum(1e-7,np.abs(m)),.12,1)**1.2
                clean[offset:offset+1024]+=np.fft.irfft(m*mask,n=1024)*window;weight[offset:offset+1024]+=window*window
            audio=(clean/np.maximum(1e-8,weight))[512:512+len(audio)]
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
