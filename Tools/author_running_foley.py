"""Build soft soil/cloth running steps from the project's existing Walk2 recording."""
import os,sys,subprocess,wave
from pathlib import Path
cache=Path(os.environ.get('BACHDANG_VOICE_CACHE','.utmp/voice-authoring-cache')).resolve()
sys.path.insert(0,str(cache/'voice-native-tools'))
import numpy as np
import imageio_ffmpeg
from scipy.signal import butter,sosfilt,resample_poly,find_peaks
ffmpeg=imageio_ffmpeg.get_ffmpeg_exe();sr=22050
raw=subprocess.check_output([ffmpeg,'-v','error','-i','Assets/Audio/Action/Walk2.mp3','-ac','1','-ar',str(sr),'-f','f32le','pipe:1'])
x=np.frombuffer(raw,dtype='<f4').copy()
x=sosfilt(butter(3,[90,1250],btype='bandpass',fs=sr,output='sos'),x)
energy=np.convolve(x*x,np.ones(220)/220,mode='same')
peaks,_=find_peaks(energy,distance=int(sr*.25),prominence=max(energy)*.05)
if len(peaks)<4:peaks=np.argsort(energy)[-4:]
peaks=sorted(peaks,key=lambda i:energy[i],reverse=True)[:4]
rng=np.random.default_rng(938);report=[]
for i,at in enumerate(peaks):
    start=max(0,at-int(sr*.055));clip=x[start:start+int(sr*.28)]
    clip=resample_poly(clip,4,5);t=np.arange(len(clip))/sr
    cloth=sosfilt(butter(2,[180,950],btype='bandpass',fs=sr,output='sos'),rng.normal(0,1,len(clip)))
    clip+=cloth*np.exp(-((t-.1)/.045)**2)*.018
    fade=np.minimum(1,t/.012)*np.minimum(1,(t[-1]-t)/.07);clip*=fade
    clip*=min(.22/max(abs(clip)),.050/max(np.sqrt(np.mean(clip*clip)),1e-6))
    out=Path('Assets/Resources/BachDang938/RunSteps')/f'Boot_Run_{i+1}.wav'
    with wave.open(str(out),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(sr);w.writeframes((clip*32767).astype('<i2').tobytes())
    report.append({'clip':str(out),'peak':float(max(abs(clip))),'rms':float(np.sqrt(np.mean(clip*clip))),'seconds':len(clip)/sr})
print(report)
