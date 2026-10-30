"""Previously authorized Edge Vietnamese clips for the post-battle quest, not actor recordings."""
import asyncio,hashlib,json,subprocess,sys
from pathlib import Path
sys.path.insert(0,str(Path('.utmp/voice-tools').resolve()))
import edge_tts
root=Path('Assets/Resources/BachDang938');out=root/'Voices'/'Epilogue';out.mkdir(exist_ok=True)
lines=[('PrisonersOrder','Giặc đã tan. Hãy trở lại những thuyền đắm, tìm binh lính còn sống, nhận đầu hàng và đưa họ về làm tù binh. Nhớ đề phòng tướng giặc còn ẩn náu.','+18%','-8Hz'),('Prisoner','Xin đừng giết! Chủ tướng vẫn còn trên soái hạm. Chúng tôi xin hạ vũ khí.','+23%','+9Hz'),('BossChallenge','Ta vẫn còn đây. Hãy lên boong soái hạm đối mặt với ta!','+30%','+8Hz'),('BossDefeated','Tướng giặc đã bị hạ! Thu quân, đưa tù binh về doanh trại!','+28%','+7Hz'),('FinalReport','Tướng giặc đã bị hạ, tù binh đã quy hàng. Tướng sĩ đã tận lực vì non sông. Toàn quân mừng đại thắng Bạch Đằng!','+20%','-8Hz')]
async def build(name,line,rate,pitch,dest):
    raw=Path('.utmp')/('epilogue_'+name+'.mp3')
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
    await build('Victory','Thắng rồi! Đại thắng Bạch Đằng!','+45%','+12Hz',root/'BattleVoices'/'DaiViet_Victory.mp3')
asyncio.run(main())
p=root/'Voices'/'manifest.json';data=json.loads(p.read_text(encoding='utf-8-sig'))
for name,line,rate,pitch in lines:
    data['entries']=[e for e in data['entries'] if e['line']!=line];path='BachDang938/Voices/Epilogue/'+name;data['entries'].append(dict(line=line,male=path,female=path,variants=[]))
p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Prepared five epilogue lines and one short victory cheer using Edge TTS.')
