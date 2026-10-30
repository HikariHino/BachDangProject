"""Previously authorized Edge Vietnamese friendly-fire warning/order clips, not actor recordings."""
import asyncio,hashlib,json,subprocess,sys
from pathlib import Path
sys.path.insert(0,str(Path('.utmp/voice-tools').resolve()))
import edge_tts
root=Path('Assets/Resources/BachDang938');out=root/'Voices'/'Discipline';out.mkdir(exist_ok=True)
lines=[('CommanderWarning','Hạ cung xuống! Không được chĩa vũ khí vào chủ tướng. Ta chỉ nhắc ngươi lần này.','+22%','-8Hz'),('CommanderExecution','Quân đâu! Kẻ phản nghịch dám phạm thượng. Giết hắn!','+32%','-5Hz'),('AllyRetaliation','Có kẻ phản quân! Đừng để hắn chạy thoát!','+30%','+5Hz')]
async def build(name,line,rate,pitch,dest):
    raw=Path('.utmp')/('discipline_'+name+'_'+hashlib.sha1(line.encode()).hexdigest()[:8]+'.mp3')
    if not raw.exists() or raw.stat().st_size<100:
        for attempt in range(3):
            try:
                await edge_tts.Communicate(line,'vi-VN-NamMinhNeural',rate=rate,pitch=pitch).save(str(raw))
                if raw.stat().st_size>100:break
            except edge_tts.exceptions.NoAudioReceived:
                if attempt==2:raise
                await asyncio.sleep(1)
    subprocess.run(['ffmpeg','-v','error','-y','-i',str(raw),'-af','highpass=f=110,lowpass=f=6000,loudnorm=I=-18:TP=-2:LRA=7,afade=t=in:d=0.012','-ac','1','-ar','32000','-codec:a','libmp3lame','-b:a','96k',str(dest)],check=True)
async def main():
    for name,line,rate,pitch in lines:await build(name,line,rate,pitch,out/(name+'.mp3'))
asyncio.run(main())
p=root/'Voices'/'manifest.json';data=json.loads(p.read_text(encoding='utf-8-sig'))
for name,line,rate,pitch in lines:
    data['entries']=[e for e in data['entries'] if e.get('male')!='BachDang938/Voices/Discipline/'+name];path='BachDang938/Voices/Discipline/'+name;data['entries'].append(dict(line=line,male=path,female=path,variants=[]))
p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Prepared three discipline lines using approved Edge TTS.')
