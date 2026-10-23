"""Author role-specific Vietnamese speech using the user's approved Edge TTS service.

Generate outside Assets, then import only after all selected recordings pass validation.
The game uses bundled clips and has no TTS service dependency. Existing fallback clips
and their GUIDs remain intact. Run from the Unity project root.
"""
import argparse
import asyncio
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys

sys.path.insert(0, str(Path('.utmp/voice-tools').resolve()))

ROOT = Path('Assets/Resources/BachDang938/Voices')
STAGING = Path('.utmp/dialogue-cast')
LITERAL = r'"((?:\\.|[^"\\])*)"'


def unquote(value):
    return json.loads('"' + value + '"')


def assignments(entries):
    """Cast every authored line, retaining role and gender rather than guessing from pitch."""
    roles = ['villager', 'farmer', 'fisher', 'merchant', 'porter', 'smith', 'guard']
    source = Path('Assets/Scripts/BeachBoatStory/NPCAmbientDialogue.cs').read_text(encoding='utf-8-sig')
    block = source.split('static readonly string[][] Lines=')[1].split('void Start()')[0]
    ambient = {}
    for role, group in zip(roles, re.findall(r'new\[\]\{([^}]+)\}', block)):
        for match in re.finditer(LITERAL, group):
            ambient[unquote(match.group(1))] = role
    commander = set()
    for source in Path('Assets/Scripts/BeachBoatStory').glob('*.cs'):
        for match in re.finditer(r'\.Show\(\s*' + LITERAL + r'\s*,\s*' + LITERAL, source.read_text(encoding='utf-8-sig')):
            if unquote(match.group(1)) == 'Ngô Quyền':
                commander.add(unquote(match.group(2)))
    for entry in entries:
        line = entry['line']
        if line in commander:
            entry['cast'] = ['commander']
        elif ambient.get(line) == 'guard' or any(word in line for word in ('Doanh trại đã sẵn sàng', 'Quân Nam Hán đang dòm', 'Ngươi đã thuộc hiệu lệnh', 'luyện hiệu lệnh trống', 'Một bó cọc vừa', 'Đã vớt lại đủ cọc', 'Hãy về doanh trại báo Chủ tướng')):
            entry['cast'] = ['soldier', 'veteran']
        else:
            role = ambient.get(line, 'villager')
            entry['cast'] = [role + '_m', role + '_f']
            if role == 'villager':
                entry['cast'] += ['villager_alt_m', 'villager_alt_f']
    if len(commander) != 5 or len(ambient) != 21:
        raise RuntimeError('Dialogue layout changed; review casting before generating.')
    return entries


async def main():
    import edge_tts
    parser = argparse.ArgumentParser()
    parser.add_argument('--only', default='', help='Generate one profile only.')
    parser.add_argument('--exclude', default='', help='Skip one profile while auditioning.')
    parser.add_argument('--install', action='store_true', help='Validate and copy the complete cast into Assets.')
    args = parser.parse_args()
    profiles = json.loads(Path('Tools/dialogue_cast.json').read_text(encoding='utf-8-sig'))
    manifest = json.loads((ROOT / 'manifest.json').read_text(encoding='utf-8-sig'))
    entries = assignments(manifest['entries'])
    STAGING.mkdir(parents=True, exist_ok=True)
    jobs = []
    for entry in entries:
        entry['variants'] = []
        for profile in entry.pop('cast'):
            settings = profiles[profile]
            signature = json.dumps({'line': entry['line'], 'profile': profile, **settings}, sort_keys=True, ensure_ascii=False)
            key = hashlib.sha256(signature.encode('utf-8')).hexdigest()[:20] + '_' + profile
            entry['variants'].append({'profile': profile, 'path': 'BachDang938/Voices/Cast/' + key})
            jobs.append((entry['line'], profile, key, settings))
    semaphore = asyncio.Semaphore(3)

    async def generate(job):
        text, profile, key, settings = job
        path = STAGING / (key + '.mp3')
        if path.exists() and path.stat().st_size > 1500:
            return
        async with semaphore:
            for attempt in range(4):
                temporary = path.with_suffix('.part.mp3')
                try:
                    await edge_tts.Communicate(text, settings['voice'], rate=settings['rate'], pitch=settings['pitch'], connect_timeout=15, receive_timeout=30).save(str(temporary))
                    if temporary.stat().st_size < 1500:
                        raise RuntimeError('Speech response was empty: ' + key)
                    temporary.replace(path)
                    print('CAST', profile, key, path.stat().st_size, flush=True)
                    return
                except Exception:
                    if attempt == 3:
                        raise
                    await asyncio.sleep(2 + attempt * 2)

    selected = [job for job in jobs if (not args.only or job[1] == args.only) and job[1] != args.exclude]
    await asyncio.gather(*(generate(job) for job in selected))
    if args.install:
        missing = [key for _, _, key, _ in jobs if not (STAGING / (key + '.mp3')).exists()]
        if missing:
            raise RuntimeError('Incomplete cast; not installing: ' + ', '.join(missing))
        cast_folder = ROOT / 'Cast'
        cast_folder.mkdir(exist_ok=True)
        for _, _, key, _ in jobs:
            shutil.copy2(STAGING / (key + '.mp3'), cast_folder / (key + '.mp3'))
        manifest['profiles'] = [{'id': key, **settings} for key, settings in profiles.items()]
        manifest['entries'] = entries
        (ROOT / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (STAGING / 'casting-report.json').write_text(json.dumps({'lines': len(entries), 'recordings': len(jobs), 'profiles': len(profiles), 'installed': args.install}, indent=2), encoding='utf-8')
    print('DONE', len(selected), 'selected recordings;', len(jobs), 'total cast clips', flush=True)


if __name__ == '__main__':
    asyncio.run(main())
