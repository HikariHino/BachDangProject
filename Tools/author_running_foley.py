"""Author eight original soil-running impacts; no reuse of Walk1/Walk2 or old run audio."""
import os,sys,wave,json
from pathlib import Path
cache=Path(os.environ.get('BACHDANG_VOICE_CACHE','.utmp/voice-authoring-cache')).resolve()
sys.path.insert(0,str(cache/'voice-native-tools'))
import numpy as np
from scipy.signal import butter,sosfilt
rate=24000
folder=Path('Assets/Resources/BachDang938/RunSteps');folder.mkdir(parents=True,exist_ok=True)
report=[]
for index in range(8):
 rng=np.random.default_rng(938240+index);duration=.19+rng.uniform(-.01,.018);t=np.arange(round(rate*duration))/rate
 def noise(low,high):
  return sosfilt(butter(2,[low,high],btype='bandpass',fs=rate,output='sos'),rng.normal(0,1,len(t)))
 # A short damped heel contact, a delayed toe/soil crunch and a brief boot scuff.
 # Each layer has a separate irregular excitation; none is a sustained tone or loop.
 heel=noise(60,390)*(1-np.exp(-t/.003))*np.exp(-t/rng.uniform(.018,.026))
 toe_at=rng.uniform(.026,.042);u=np.maximum(0,t-toe_at)
 toe=noise(170,1600)*(t>=toe_at)*(1-np.exp(-u/.002))*np.exp(-u/.025)
 scuff=noise(500,2700)*np.exp(-((t-rng.uniform(.08,.105))/.030)**2)
 grains=np.zeros(len(t))
 for _ in range(18):
  when=rng.uniform(.018,.10);u=np.maximum(0,t-when)
  grains+=noise(450,2200)*(t>=when)*np.exp(-u/.0025)*rng.uniform(.12,.28)
 x=heel*.9+toe*.72+scuff*.12+grains*.22
 x=sosfilt(butter(2,2700,fs=rate,output='sos'),x)
 x*=np.minimum(1,t/.004)*np.minimum(1,(t[-1]-t)/.025)
 x-=np.mean(x);x*=.085/max(np.sqrt(np.mean(x*x)),1e-9)
 if max(abs(x))>.68:x*=.68/max(abs(x))
 x=np.tanh(x*1.05)/1.05
 out=folder/f'Boot_Run_{index+1}.wav'
 with wave.open(str(out),'wb') as file:file.setnchannels(1);file.setsampwidth(2);file.setframerate(rate);file.writeframes(np.round(x*32767).astype('<i2').tobytes())
 report.append({'clip':out.name,'seconds':len(x)/rate,'peak':float(max(abs(x))),'rms':float(np.sqrt(np.mean(x*x)))})
Path('Logs/RunningFoleyOriginal.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))
