import sys,os,json,hashlib,re,subprocess,shutil
from pathlib import Path
cache=Path(os.environ.get('BACHDANG_VOICE_CACHE', '.utmp/voice-authoring-cache')).resolve()
sys.path.insert(0,str(cache/'voice-native-tools'))
os.environ['HF_HOME']=str(cache/'huggingface');os.environ['HF_HUB_DISABLE_SYMLINKS_WARNING']='1';os.environ['HF_HUB_DISABLE_PROGRESS_BARS']='1'
import numpy as np
import soundfile as sf
import imageio_ffmpeg
from vieneu import Vieneu
sys.dont_write_bytecode=True
from generate_dialogue_cast import assignments
root=Path('Assets/Resources/BachDang938/Voices');stage=cache/'native-cast';stage.mkdir(parents=True,exist_ok=True)
voices=json.loads(Path('Tools/dialogue_native_cast.json').read_text(encoding='utf-8-sig'))
manifest=json.loads((root/'manifest.json').read_text(encoding='utf-8-sig'))
entries=assignments([e for e in manifest['entries'] if not any(v.get('profile')=='narrator' for v in e.get('variants',[]))])
intro=Path('Assets/Scripts/BeachBoatStory/OpeningEvent.cs').read_text(encoding='utf-8-sig').split('readonly string[] storyLines = {')[1].split('};')[0]
for line in re.findall(r'"([^"\n]+)"',intro): entries.append({'line':line,'cast':['narrator'],'male':'','female':''})
tts=None;report=[]
for entry in entries:
    entry['variants']=[]
    for profile in entry.pop('cast'):
        voice=voices[profile]['voice'];tempo=voices[profile]['tempo']
        key=hashlib.sha256(json.dumps([entry['line'],voice,tempo,'native-v1'],ensure_ascii=False).encode()).hexdigest()[:20]+'_'+profile
        output=stage/(key+'.mp3');entry['variants'].append({'profile':profile,'path':'BachDang938/Voices/NativeCast/'+key})
        if not output.exists():
            if tts is None: tts=Vieneu(backend='onnx',threads=4)
            audio=tts.infer(entry['line'],voice=voice,temperature=.75,silence_p=.1)
            sr=48000
            if len(audio)<sr*.35 or not np.all(np.isfinite(audio)): raise RuntimeError('Invalid take '+key)
            active=np.flatnonzero(np.abs(audio)>.008)
            if len(active)==0:raise RuntimeError('Silent take '+key)
            audio=audio[max(0,active[0]-int(sr*.06)):min(len(audio),active[-1]+int(sr*.16))]
            rms=np.sqrt(np.mean(audio**2));audio*=min(.075/max(rms,1e-6),.90/max(np.max(np.abs(audio)),1e-6))
            wav=stage/(key+'.wav');sf.write(wav,audio,sr,subtype='PCM_16')
            subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-v','error','-y','-i',str(wav),'-af','atempo='+str(tempo),'-codec:a','libmp3lame','-b:a','96k',str(output)],check=True)
            wav.unlink()
        report.append({'profile':profile,'voice':voice,'clip':key,'bytes':output.stat().st_size})
        print('TAKE',len(report),profile,voice,output.stat().st_size,flush=True)
# Only install the complete batch. No network or model dependencies enter the game.
dest=root/'NativeCast';dest.mkdir(exist_ok=True)
for take in report:shutil.copy2(stage/(take['clip']+'.mp3'),dest/(take['clip']+'.mp3'))
manifest['entries']=entries;manifest['profiles']=[{'id':k,**v,'voice':'VieNeu/'+v['voice']} for k,v in voices.items()]
(root/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(stage/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('INSTALLED',len(report),flush=True)
