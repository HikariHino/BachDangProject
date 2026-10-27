"""Offline asset authoring. Powered by Poly Haven (https://polyhaven.com).
Original assets: CC0 https://polyhaven.com/license; no game-time API dependency.
"""
from pathlib import Path
import json,hashlib,urllib.request,concurrent.futures
from PIL import Image
import numpy as np

OUT=Path('Assets/BachDangCinematic');CACHE=Path('.utmp/GraphicsSources')
ASSETS=['weathered_brown_planks','brown_mud_02','forest_floor','rock_boulder_dry',
        'sandy_gravel_02','aerial_grass_rock','tree_stump_01','rock_moss_set_01','wicker_basket_01']
HEADERS={'User-Agent':'BachDang938-AssetPreparation/1.0 (offline asset authoring; Powered by Poly Haven)'}
def request(url):return urllib.request.urlopen(urllib.request.Request(url,headers=HEADERS),timeout=90)
def metadata(name):
    p=CACHE/(name+'.json')
    if not p.exists():p.write_bytes(request('https://api.polyhaven.com/files/'+name).read())
    return json.loads(p.read_text(encoding='utf8'))
def download(entry,path):
    path.parent.mkdir(parents=True,exist_ok=True)
    if not path.exists() or hashlib.md5(path.read_bytes()).hexdigest()!=entry['md5']:
        data=request(entry['url']).read()
        if hashlib.md5(data).hexdigest()!=entry['md5']:raise ValueError('Digest mismatch: '+entry['url'])
        path.write_bytes(data)
    return dict(path=path.as_posix(),url=entry['url'],md5=entry['md5'],bytes=path.stat().st_size)
def main():
    OUT.mkdir(exist_ok=True);CACHE.mkdir(parents=True,exist_ok=True);jobs=[];records=[]
    for name in ASSETS:
        d=metadata(name);folder=OUT/'Surfaces'/name
        for kind,label in [('Diffuse','Albedo'),('nor_gl','Normal'),('Rough','Roughness'),('AO','AO')]:
            if kind not in d:continue
            variants=d[kind]['2k'];entry=variants.get('jpg') or variants.get('png')
            jobs.append((entry,folder/(label+Path(entry['url']).suffix)))
        if 'fbx' in d:
            entry=d['fbx']['1k']['fbx'];jobs.append((entry,OUT/'Models'/(name+'.fbx')))
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        for record in pool.map(lambda args:download(*args),jobs):records.append(record);print(record['path'],flush=True)
    for name in ASSETS:
        folder=OUT/'Surfaces'/name
        rough=np.asarray(Image.open(next(folder.glob('Roughness.*'))).convert('L'),dtype=np.uint8)
        ao_path=next(folder.glob('AO.*'),None);ao=np.asarray(Image.open(ao_path).convert('L')) if ao_path else np.full_like(rough,255)
        packed=np.zeros((*rough.shape,4),dtype=np.uint8);packed[:,:,1]=ao;packed[:,:,3]=255-rough
        Image.fromarray(packed).save(folder/'MetallicSmoothnessAO.png',optimize=True)
    characters=[]
    for model in list(Path('Assets/Model_NPC').rglob('*_Character_output.fbx'))+list(Path('Assets/Main_Character').rglob('*_Character_output.fbx')):
        folder=model.parent;normal=next(folder.glob('*_normal.png'),None);rough=next(folder.glob('*_roughness.png'),None)
        if normal is None or rough is None:continue
        base=Path(str(normal).replace('_normal.png','.png'));metal=Path(str(normal).replace('_normal.png','_metallic.png'))
        if not base.exists():continue
        stem=model.stem.replace('_Character_output','');dest=OUT/'Characters'/stem;dest.mkdir(parents=True,exist_ok=True)
        r=np.asarray(Image.open(rough).convert('L'));m=np.asarray(Image.open(metal).convert('L')) if metal.exists() else np.zeros_like(r)
        packed=np.zeros((*r.shape,4),dtype=np.uint8);packed[:,:,0]=m;packed[:,:,1]=255
        # Prevent waxy skin and preserve a broader, weathered highlight on armour.
        packed[:,:,3]=np.minimum(255-r,185)
        Image.fromarray(packed).save(dest/'Surface.png',optimize=True)
        characters.append(dict(model=model.as_posix(),albedo=base.as_posix(),normal=normal.as_posix(),mask=(dest/'Surface.png').as_posix()))
    # Existing project textures keep their original licensing. Export their mapping
    # once from the editor; keep it in the manifest for reproducible offline builds.
    previous=json.loads((OUT/'sources.json').read_text(encoding='utf8')) if (OUT/'sources.json').exists() else {}
    environment=previous.get('environment',[])
    for item in environment:
        rough=np.asarray(Image.open(item['roughness']).convert('L'))
        packed=np.zeros((*rough.shape,4),dtype=np.uint8);packed[:,:,1]=255;packed[:,:,3]=np.minimum(255-rough,130)
        mask=Path(item['mask']);mask.parent.mkdir(parents=True,exist_ok=True)
        Image.fromarray(packed).save(mask,optimize=True)
    (OUT/'sources.json').write_text(json.dumps(dict(externalAssetLicense='CC0-1.0',poweredBy='https://polyhaven.com',files=records,characters=characters,environment=environment),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print('Prepared',len(records),'source files and',len(characters),'character surface sets',flush=True)
if __name__=='__main__':main()
