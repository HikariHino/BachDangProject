"""Short Vietnamese ally field calls. Dialogue text only is sent to Edge TTS."""
import asyncio,sys,subprocess,uuid
from pathlib import Path
sys.path.insert(0,str(Path('.utmp/voice-tools').resolve()))
import edge_tts
lines={
'route_intro':'Hai luồng phía trước! Vòng qua cờ xanh tránh tên, hoặc đi thẳng để nhử địch nhanh hơn!',
'safe_route':'Bám luồng vòng! Giữ khoảng cách, địch đang áp sát!',
'fast_route':'Đi thẳng! Coi chừng tên phía trước, bẻ lái sớm!',
'arrows':'Tên tới! Bẻ lái!',
'route_done':'Qua luồng rồi! Dẫn chúng vào trận địa!',
'raft_ready':'Thả bè thử dòng chảy! Xem cọc đã giữ chắc chưa!',
'raft_hit':'Chắc rồi! Bè đã mắc cọc! Anh em dựng tiếp trận địa!',
'drum_gather':'Đã rõ! Toàn đội tập hợp!',
'drum_advance':'Tiến lên! Giữ đội hình!',
'drum_fire':'Hàng trước giữ trận! Cung thủ, bắn!'}
dest=Path('Assets/Resources/BachDang938/Voices/FieldCalls');dest.mkdir(parents=True,exist_ok=True)
stage=Path('.utmp/field-calls');stage.mkdir(parents=True,exist_ok=True)
async def main():
 for key,line in lines.items():
  out=dest/(key+'.mp3')
  if not out.exists():
   raw=stage/(key+'.mp3')
   for attempt in range(4):
    try:
     await asyncio.wait_for(edge_tts.Communicate(line,'vi-VN-NamMinhNeural').save(str(raw)),25)
     break
    except (edge_tts.exceptions.NoAudioReceived,asyncio.TimeoutError):
     if attempt==3: raise
     await asyncio.sleep(1)
   subprocess.run(['ffmpeg','-v','error','-y','-i',str(raw),'-af','atempo=1.16,loudnorm=I=-20:TP=-3:LRA=7,afade=t=in:d=0.015','-codec:a','libmp3lame','-b:a','96k',str(out)],check=True)
  print(key,out.stat().st_size,flush=True)
asyncio.run(main())
