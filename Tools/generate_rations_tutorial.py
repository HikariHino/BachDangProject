"""Render an original, offline minigame demonstration with a cursor and soft UI cues."""
from pathlib import Path
import math
import subprocess
import wave
import shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFont

W,H,FPS,SECONDS=960,420,24,15
OUT=Path('Assets/Resources/BachDang938/Tutorials/RationsHowTo.webm')
FONT=Path('C:/Windows/Fonts/arial.ttf')
EVENTS=[(2.1,0),(2.7,0),(3.3,0),(3.9,0),(4.9,1),(5.5,1),(6.6,2),(7.5,2),(8.8,3),(10.7,4)]
YELLOW=(133,102,29);GREEN=(26,107,75);RED=(143,49,33);GOLD=(255,198,88)
def font(n):return ImageFont.truetype(str(FONT),n)
def centered(d,xy,text,n=25,fill='white'):
    d.text(xy,text,font=font(n),fill=fill,anchor='mm')
def frame(t):
    im=Image.new('RGB',(W,H),(16,23,24));d=ImageDraw.Draw(im)
    centered(d,(480,31),'GÓI LƯƠNG CHO 2 NGƯỜI',29,GOLD)
    centered(d,(480,67),'Mỗi lần bấm: thêm 1 nguyên liệu',22)
    counts=[0,0,0];cursor=(130,370);last=-10
    for at,action in EVENTS:
        target=((180,210),(480,210),(780,210),(245,335),(660,335))[action]
        if t<at:
            f=max(0,min(1,(t-at+.35)/.35));cursor=(cursor[0]+(target[0]-cursor[0])*f,cursor[1]+(target[1]-cursor[1])*f);break
        cursor=target;last=at
        if action<3:counts[action]+=1
        elif action==3:counts[2]-=1
    for i,(name,recipe,need) in enumerate([('GẠO','Mỗi người: 2 gạo',4),('CÁ KHÔ','Mỗi người: 1 cá',2),('MUỐI','Cả nhóm: 1 muối',1)]):
        x=30+i*300;have=counts[i];color=GREEN if have==need else RED if have>need else YELLOW
        d.rounded_rectangle((x,102,x+280,294),radius=12,fill=color)
        if i==0:
            d.polygon([(x+85,173),(x+195,173),(x+178,199),(x+103,199)],fill=(167,106,44))
            for row in range(3):
                for col in range(5-row):
                    q=x+87+row*9+col*20;r=177-row*12;d.polygon([(q,r),(q+19,r),(q+13,r-11)],fill=(241,211,129))
        elif i==1:
            d.polygon([(x+105,169),(x+65,144),(x+65,193)],fill=(172,110,58));d.ellipse((x+96,142,x+208,195),fill=(172,110,58));d.ellipse((x+185,156,x+191,162),fill=(25,25,20))
        else:d.polygon([(x+74,197),(x+140,144),(x+211,197)],fill=(240,238,220));d.polygon([(x+140,144),(x+135,197),(x+74,197)],fill=(255,255,249))
        centered(d,(x+140,124),name,22,GOLD)
        centered(d,(x+140,226),recipe,22)
        centered(d,(x+140,266),'ĐỦ' if have==need else 'DƯ' if have>need else 'THIẾU',26)
    d.rounded_rectangle((70,312,420,356),radius=6,fill=(49,60,57));centered(d,(245,334),'HOÀN TÁC [Z]',24)
    d.rounded_rectangle((450,312,870,356),radius=6,fill=(49,60,57));centered(d,(660,334),'KIỂM TRA VÀ GÓI [4]',24)
    caption='Vàng: thiếu • Xanh: đủ • Đỏ: dư'
    if 1.8<=t<4.7:caption='Thêm gạo đến khi thẻ chuyển xanh'
    elif 4.7<=t<6.3:caption='Tiếp tục với phần cá khô'
    elif 6.3<=t<7.4:caption='Muối chỉ tính CHO CẢ NHÓM'
    elif 7.4<=t<8.6:caption='Đỏ là dư — dùng Z để hoàn tác'
    elif 8.6<=t<10.4:caption='Cả ba thẻ xanh: có thể gói lương'
    elif t>=10.4:caption='Bấm KIỂM TRA VÀ GÓI để giao khẩu phần'
    centered(d,(480,391),caption,25,GOLD)
    x,y=cursor
    if t-last<.22:d.ellipse((x-19,y-19,x+19,y+19),outline=GOLD,width=4)
    d.polygon([(x,y),(x+5,y+27),(x+12,y+19),(x+22,y+19)],fill='white',outline=(5,10,9))
    if t>=11.4:
        d.rounded_rectangle((175,145,785,274),radius=16,fill=(22,91,65),outline=(112,230,170),width=3)
        centered(d,(480,188),'ĐÚNG KHẨU PHẦN!',36,GOLD)
        centered(d,(480,237),'Đến lượt bạn',28)
    return im
def main():
    OUT.parent.mkdir(parents=True,exist_ok=True);cache=Path('Logs/RationsTutorial');cache.mkdir(parents=True,exist_ok=True)
    rate=24000;samples=np.zeros(rate*SECONDS,dtype=np.float64)
    for at,action in EVENTS:
        n=int(rate*.10);s=np.arange(n)/rate;tone=np.sin(2*math.pi*(700 if action<4 else 950)*s)*np.exp(-s*55)*.12
        k=round(at*rate);samples[k:k+n]+=tone
    for at,hz in [(11.4,660),(11.55,880)]:
        n=int(rate*.22);s=np.arange(n)/rate;k=round(at*rate);samples[k:k+n]+=np.sin(2*math.pi*hz*s)*np.exp(-s*18)*.10
    audio=cache/'cues.wav'
    with wave.open(str(audio),'wb') as wav:wav.setparams((1,2,rate,0,'NONE','not compressed'));wav.writeframes((samples*32767).astype('<i2').tobytes())
    process=subprocess.Popen(['ffmpeg','-v','error','-y','-f','rawvideo','-pix_fmt','rgb24','-s',f'{W}x{H}','-r',str(FPS),'-i','-','-i',str(audio),'-c:v','libvpx','-b:v','400k','-crf','10','-deadline','good','-cpu-used','4','-auto-alt-ref','0','-pix_fmt','yuv420p','-c:a','libvorbis','-q:a','3','-shortest',str(OUT)],stdin=subprocess.PIPE)
    try:
        for n in range(FPS*SECONDS):process.stdin.write(frame(n/FPS).tobytes())
    finally:process.stdin.close()
    if process.wait()!=0:raise RuntimeError('Video encoding failed')
    # Predecoded frames also let the in-game guide play without platform codecs.
    for sheet in range(2):
        atlas=Image.new('RGB',(3840,2800),(16,23,24))
        for tile in range(60):
            atlas.paste(frame((sheet*60+tile)/8).resize((640,280),Image.Resampling.LANCZOS),((tile%6)*640,(tile//6)*280))
        atlas.save(OUT.parent/f'RationsFrames{sheet}.png',optimize=True)
    shutil.copyfile(audio,OUT.parent/'RationsHowToCues.wav')
    frame(7.9).save(cache/'preview.png');print(str(OUT),OUT.stat().st_size,flush=True)
if __name__=='__main__':main()
