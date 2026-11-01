"""Create the character conversation cast locally; no game data is sent to a service.
Use the previously installed/pinned VieNeu authoring cache, outside the Unity project.
"""
import argparse, hashlib, json, os, shutil, subprocess, sys
from pathlib import Path
parser=argparse.ArgumentParser();parser.add_argument('--cache',required=True);args=parser.parse_args()
cache=Path(args.cache).resolve();sys.path.insert(0,str(cache/'voice-native-tools'))
os.environ['HF_HOME']=str(cache/'huggingface');os.environ['HF_HUB_OFFLINE']='1';os.environ['HF_HUB_DISABLE_PROGRESS_BARS']='1';os.environ['HF_HUB_DISABLE_SYMLINKS_WARNING']='1'
import numpy as np
import soundfile as sf
import imageio_ffmpeg
from vieneu import Vieneu
root=Path('Assets/Resources/BachDang938/Voices');stage=cache/'npc-conversations';stage.mkdir(parents=True,exist_ok=True)
catalog=json.loads((root/'NPCConversations.json').read_text(encoding='utf-8-sig'))
profiles=[p for group in catalog.values() for p in group]
manifest=json.loads((root/'manifest.json').read_text(encoding='utf-8-sig'))
entries={e['line']:e for e in manifest['entries']};model=None;takes=[]
for profile in profiles:
    for text in profile['lines']:
        signature=json.dumps([text,profile['voice'],profile['tempo'],'npc-v1'],ensure_ascii=False)
        key=hashlib.sha256(signature.encode('utf-8')).hexdigest()[:20]+'_'+profile['profile']
        output=stage/(key+'.mp3')
        if not output.exists():
            if model is None:model=Vieneu(backend='onnx',threads=4)
            audio=model.infer(text,voice=profile['voice'],temperature=.65,silence_p=.1);sr=48000
            if len(audio)<sr*.35 or not np.all(np.isfinite(audio)):raise RuntimeError('Invalid take '+key)
            active=np.flatnonzero(np.abs(audio)>.008)
            if not len(active):raise RuntimeError('Silent take '+key)
            audio=audio[max(0,active[0]-int(sr*.06)):min(len(audio),active[-1]+int(sr*.18))]
            rms=np.sqrt(np.mean(audio**2));audio*=min(.075/max(rms,1e-6),.9/max(np.max(np.abs(audio)),1e-6))
            wav=stage/(key+'.wav');sf.write(wav,audio,sr,subtype='PCM_16')
            subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-v','error','-y','-i',str(wav),'-af','atempo='+str(profile['tempo'])+',afade=t=in:d=0.012','-codec:a','libmp3lame','-b:a','96k',str(output)],check=True)
            wav.unlink()
        path='BachDang938/Voices/NPCast/'+key
        entry=entries.get(text)
        if entry is None:entry={'line':text,'male':'','female':'','variants':[]};entries[text]=entry;manifest['entries'].append(entry)
        variants=entry.setdefault('variants',[]);variants[:]=[v for v in variants if v['profile']!=profile['profile']];variants.append({'profile':profile['profile'],'path':path})
        gender='female' if profile.get('female',False) else 'male'
        if not entry.get(gender):entry[gender]=path
        takes.append({'profile':profile['profile'],'voice':profile['voice'],'clip':key,'bytes':output.stat().st_size})
        print('TAKE',len(takes),profile['profile'],output.stat().st_size,flush=True)
# Install only after the entire batch succeeds.
dest=root/'NPCast';dest.mkdir(exist_ok=True)
for take in takes:shutil.copy2(stage/(take['clip']+'.mp3'),dest/(take['clip']+'.mp3'))
known={p['id']:p for p in manifest['profiles']}
for p in profiles:known[p['profile']]={'id':p['profile'],'voice':'VieNeu/'+p['voice'],'tempo':p['tempo']}
manifest['profiles']=list(known.values())
(root/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(stage/'report.json').write_text(json.dumps(takes,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('INSTALLED',len(takes),'clips',flush=True)
