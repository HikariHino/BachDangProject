"""Original, deterministic pentatonic lobby score and timber/bronze interface foley.
No samples, network sources, or third-party compositions are used.
"""
from pathlib import Path
import json
import wave
import numpy as np

RATE = 32000
rng = np.random.default_rng(938)
folder = Path('Assets/Resources/BachDang938/LobbyAudio')
folder.mkdir(parents=True, exist_ok=True)

def save(name, audio, peak=.78):
    audio = np.asarray(audio, dtype=float)
    audio -= audio.mean()
    audio *= min(1., peak / max(abs(audio).max(), 1e-8))
    fade = min(200, len(audio)//10)
    audio[:fade] *= np.linspace(0, 1, fade)
    audio[-fade:] *= np.linspace(1, 0, fade)
    with wave.open(str(folder / (name + '.wav')), 'wb') as f:
        f.setnchannels(1); f.setsampwidth(2); f.setframerate(RATE)
        f.writeframes(np.round(audio * 32767).astype('<i2').tobytes())

def drum(duration=.7, frequency=85):
    t = np.arange(round(duration*RATE))/RATE
    phase = 2*np.pi*(frequency*t + 35*.023*(1-np.exp(-t/.023)))
    return .6*np.sin(phase)*np.exp(-t*8)*(1-np.exp(-t*700)) + rng.normal(0,.06,len(t))*np.exp(-t*65)

def pluck(frequency, duration=1.9):
    # Damped resonant string; a short noisy pick gives a wooden attack.
    t = np.arange(round(duration*RATE))/RATE
    wave = sum((.8**h/h)*np.sin(2*np.pi*frequency*h*t+rng.uniform(-.06,.06))*np.exp(-t*(1.5+.7*h)) for h in range(1,8))
    return wave*(1-np.exp(-t*800)) + rng.normal(0,.025,len(t))*np.exp(-t*100)

def flute(frequency, duration):
    t = np.arange(round(duration*RATE))/RATE
    phase = 2*np.pi*frequency*t + .16*np.sin(2*np.pi*4.8*t)*(1-np.exp(-t*5))
    envelope = np.minimum(t/.12,1)*np.minimum((duration-t)/.22,1)
    return envelope*(.48*np.sin(phase)+.10*np.sin(2*phase)+.028*np.sin(3*phase)+rng.normal(0,.006,len(t)))

beat = .75
length = round(32*beat*RATE)
music = np.zeros(length)
def add(signal, seconds, gain):
    indices = (np.arange(len(signal))+round(seconds*RATE)) % length
    np.add.at(music, indices, signal*gain)

# D minor pentatonic; alternating bamboo melody and spare plucked answers.
notes = [293.665,349.228,391.995,440.,523.251,587.33]
melody = [(0,0,2),(2,2,1),(3,3,1),(4,4,2),(6,3,1),(7,2,1),
          (8,1,2),(10,0,2),(12,2,1),(13,3,1),(14,2,2),
          (16,3,2),(18,4,1),(19,5,1),(20,4,2),(22,2,2),
          (24,1,2),(26,2,1),(27,1,1),(28,0,3)]
for at, note, hold in melody:
    add(flute(notes[note], hold*beat*.9), at*beat, .16)
for at in range(0,32,2):
    add(pluck(notes[[0,2,3,2,1,0,2,1][(at//2)%8]]/2), at*beat, .25)
for at in range(0,32,4):
    add(drum(.8,68 if at%8==0 else 92), at*beat, .12)
# A quiet, wrapped reflection prevents a dry electronic timbre and respects the loop.
music += np.roll(music,round(.19*RATE))*.16 + np.roll(music,round(.33*RATE))*.07
save('BachDang_Lobby_Pentatonic', music, .65)
click = drum(.16,230)*.55 + pluck(1174.66,.16)*.09
save('Menu_Timber_Bronze_Click', click, .67)
entry = drum(1.2,74)*.4
for f in (293.665,391.995,587.33):
    signal = pluck(f,1.2)
    entry += signal*.14
save('Lobby_Entry_Seal', entry, .7)
t = np.arange(round(.24*RATE))/RATE
arrow = np.sin(2*np.pi*(105*t+22*.012*(1-np.exp(-t/.012))))*np.exp(-t*19)*(1-np.exp(-t*1800))
arrow += rng.normal(0,.18,len(t))*np.exp(-t*45)*(1-np.exp(-t*1500))
save('Arrow_Flesh_Impact', arrow, .78)
print(json.dumps({'rate':RATE,'lobby_seconds':len(music)/RATE,'assets':[f.name for f in folder.glob('*.wav')]}))
