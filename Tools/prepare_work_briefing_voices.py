"""Bundle short preparation orders and two construction calls using the approved Edge Vietnamese voice."""
import asyncio,json,re,sys,subprocess
from pathlib import Path
sys.path.insert(0,str(Path('.utmp/voice-tools').resolve()))
import edge_tts
dest=Path('Assets/Resources/BachDang938/Voices/Preparation');dest.mkdir(parents=True,exist_ok=True)
staging=Path('.utmp/preparation-voices');staging.mkdir(parents=True,exist_ok=True)
source=Path('Assets/Scripts/BeachBoatStory/PreparationBriefing.cs').read_text(encoding='utf-8-sig')
lines=[('ngo_lure',re.search(r'Lure="([^"]+)"',source).group(1),'commander_brief'),('ngo_archery_natural',re.search(r'Archery="([^"]+)"',source).group(1),'commander_brief'),('crew_hold','Giữ chắc cọc! Một, hai, nện!','crew_work'),('crew_move','Cọc đã chắc! Chuyển sang hàng tiếp theo!','crew_work')]
async def main():
    for key,line,profile in lines:
        out=dest/(key+'.mp3')
        if not out.exists():
            raw=staging/(key+'.mp3')
            await edge_tts.Communicate(line,'vi-VN-NamMinhNeural',rate='+15%',pitch='-10Hz' if profile=='commander_brief' else '+0Hz').save(str(raw))
            subprocess.run(['ffmpeg','-v','error','-y','-i',str(raw),'-af','loudnorm=I=-20:TP=-3:LRA=7,afade=t=in:d=0.02','-codec:a','libmp3lame','-b:a','96k',str(out)],check=True)
        print(key,out.stat().st_size,flush=True)
asyncio.run(main())
file=Path('Assets/Resources/BachDang938/Voices/manifest.json');data=json.loads(file.read_text(encoding='utf-8-sig'))
for key,line,profile in lines:
    entry=next((e for e in data['entries'] if e['line']==line),None)
    if entry is None:entry={'line':line,'male':'','female':'','variants':[]};data['entries'].append(entry)
    entry['variants']=[{'profile':profile,'path':'BachDang938/Voices/Preparation/'+key}]
if not any(p['id']=='commander_brief' for p in data['profiles']):data['profiles'].append({'id':'commander_brief','voice':'Microsoft vi-VN-NamMinhNeural'})
if not any(p['id']=='crew_work' for p in data['profiles']):data['profiles'].append({'id':'crew_work','voice':'Microsoft vi-VN-NamMinhNeural'})
file.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
