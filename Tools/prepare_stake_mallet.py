"""Single real wooden-mallet strike. Source: leo153, Freesound 535629, CC0.
https://freesound.org/people/leo153/sounds/535629/
Cached preview is intentionally excluded from Git. Requires ffmpeg and NumPy.
"""
from pathlib import Path
import subprocess, urllib.request, wave, hashlib, json
import numpy as np
root=Path(__file__).resolve().parents[1]
source=root/'Logs/StakeSoundSources/wooden_mallet.mp3'
if not source.exists():
    source.parent.mkdir(parents=True,exist_ok=True)
    urllib.request.urlretrieve('https://cdn.freesound.org/previews/535/535629_2535988-hq.mp3',source)
rate=32000
raw=np.frombuffer(subprocess.check_output(['ffmpeg','-v','error','-i',str(source),'-ac','1','-ar',str(rate),'-f','f32le','-']),'<f4').astype(float)
x=raw[round(7.91*rate):round(8.33*rate)].copy()
hz=np.fft.rfftfreq(len(x),1/rate)
response=1/np.sqrt(1+(70/np.maximum(.01,hz))**4)/np.sqrt(1+(hz/5500)**8)
x=np.fft.irfft(np.fft.rfft(x)*response,n=len(x));x-=x.mean()
x*=min(.12/max(1e-6,np.sqrt(np.mean(x*x))),.75/max(1e-6,max(abs(x))))
x[:64]*=np.linspace(0,1,64);x[-1120:]*=np.linspace(1,0,1120)
out=root/'Assets/Resources/BachDang938/WorkAudio/Stake_Hammer.wav'
with wave.open(str(out),'wb') as w:
    w.setparams((1,2,rate,0,'NONE','PCM'));w.writeframes((x*32767).astype('<i2').tobytes())
record=dict(source='https://freesound.org/people/leo153/sounds/535629/',creator='leo153',license='CC0 1.0',start=7.91,end=8.33,rms=float(np.sqrt(np.mean(x*x))),peak=float(max(abs(x))),sha256=hashlib.sha256(out.read_bytes()).hexdigest())
(root/'Tools/stake_audio_manifest.json').write_text(json.dumps(record,indent=2)+'\n',encoding='utf-8')
print(record)
