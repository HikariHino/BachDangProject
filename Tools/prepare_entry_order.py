"""Short exact trap-entry order, using the previously approved Edge Vietnamese TTS service."""
import asyncio,json,sys,subprocess,hashlib
from pathlib import Path
sys.path.insert(0,str(Path('.utmp/voice-tools').resolve()))
import edge_tts
out=Path('Assets/Resources/BachDang938/BattleVoices/Enemy_Trap_Order.mp3');raw=Path('.utmp/Enemy_Trap_Order.mp3')
async def main():
    await edge_tts.Communicate('Giết hết chúng nó!','vi-VN-NamMinhNeural',rate='+40%',pitch='+8Hz').save(str(raw))
if not raw.exists():asyncio.run(main())
subprocess.run(['ffmpeg','-v','error','-y','-i',str(raw),'-af','highpass=f=130,lowpass=f=5800,loudnorm=I=-17:TP=-2:LRA=7,afade=t=in:d=0.015','-ac','1','-ar','32000','-codec:a','libmp3lame','-b:a','96k',str(out)],check=True)
p=out.parent/'manifest.json';data=json.loads(p.read_text(encoding='utf-8-sig'));data['entries']=[e for e in data['entries'] if e['name']!='Enemy_Trap_Order'];data['entries'].append(dict(name='Enemy_Trap_Order',text='Giết hết chúng nó!',source='Microsoft Edge vi-VN-NamMinhNeural',kind='synthesized-short-order',rate='+40%',sha256=hashlib.sha256(out.read_bytes()).hexdigest(),bytes=out.stat().st_size));p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(out.stat().st_size)
