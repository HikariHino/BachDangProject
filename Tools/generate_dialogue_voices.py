"""Build offline Vietnamese dialogue assets with edge-tts; never runs inside the game.

Install edge-tts separately, then run from the project root. The staging directory is
outside Assets so it is safe to generate while Unity is running a playtest.
"""
import ast
import asyncio
import hashlib
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path('.utmp/voice-tools').resolve()))
import edge_tts

STAGING = Path('.utmp/dialogue-audio')
STAGING.mkdir(parents=True, exist_ok=True)

def lines():
    result = set()
    literal = r'"((?:\\.|[^"\\])*)"'
    for source in Path('Assets/Scripts/BeachBoatStory').glob('*.cs'):
        text = source.read_text(encoding='utf-8-sig')
        for match in re.finditer(r'\.Show\(\s*'+literal+r'\s*,\s*'+literal, text):
            result.add(json.loads('"'+match.group(2)+'"'))
        if source.name == 'NPCAmbientDialogue.cs':
            block = text.split('static readonly string[][] Lines=')[1].split('void Start()')[0]
            for match in re.finditer(literal, block):
                result.add(json.loads('"'+match.group(1)+'"'))
    scene = Path('Assets/Scenes/beachBoat.unity').read_text(encoding='utf-8')
    for value in re.findall(r'^  dialogueText: (.+)$', scene, re.M):
        result.add(ast.literal_eval(value) if value.startswith('"') else value)
    result.add('Ngươi đã hoàn thành bài luyện hiệu lệnh trống.')
    result.add('Vật tư đã được tập kết đầy đủ.')
    return sorted(t for t in result if t.strip())

async def main():
    semaphore = asyncio.Semaphore(4)
    entries = []
    jobs = []
    for text in lines():
        key = hashlib.sha256(text.encode('utf-8')).hexdigest()[:20]
        entry = {'line': text, 'male': 'BachDang938/Voices/'+key+'_m', 'female': 'BachDang938/Voices/'+key+'_f'}
        entries.append(entry)
        for suffix, voice in [('m','vi-VN-NamMinhNeural'), ('f','vi-VN-HoaiMyNeural')]:
            jobs.append((text, key+'_'+suffix, voice))

    async def generate(job):
        text, key, voice = job
        path = STAGING/(key+'.mp3')
        if path.exists() and path.stat().st_size > 1000:
            return
        async with semaphore:
            for attempt in range(4):
                try:
                    await edge_tts.Communicate(text, voice, connect_timeout=12, receive_timeout=25).save(str(path))
                    if path.stat().st_size < 1000:
                        raise ValueError('Empty audio: '+key)
                    print('VOICE', key, path.stat().st_size, flush=True)
                    return
                except Exception as error:
                    if attempt == 3:
                        raise RuntimeError(key+': '+str(error)) from error
                    await asyncio.sleep(min(8,1+attempt))

    results=await asyncio.gather(*(generate(job) for job in jobs), return_exceptions=True)
    failures=[str(r) for r in results if isinstance(r,Exception)]
    if failures:
        print('RETRY_NEEDED',json.dumps(failures),flush=True)
        raise SystemExit(1)
    (STAGING/'manifest.json').write_text(json.dumps({'entries':entries}, ensure_ascii=False, indent=2), encoding='utf-8')
    print('DONE',len(entries),'dialogue lines,',len(jobs),'Vietnamese voice clips',flush=True)

asyncio.run(main())
