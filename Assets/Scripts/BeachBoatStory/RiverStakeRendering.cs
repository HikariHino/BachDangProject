using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BeachBoatStory
{
    /// <summary>Chunk only the static visuals. Every authored stake keeps its real solid collider.</summary>
    public sealed class RiverStakeRendering : MonoBehaviour
    {
        sealed class Group
        {
            public Material material;
            public int layer;
            public Vector3 center;
            public readonly List<CombineInstance> pieces=new List<CombineInstance>();
        }
        RiverStakeZone zone;
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<GameObject> chunks=new List<GameObject>();
        readonly HashSet<MeshRenderer> originals=new HashSet<MeshRenderer>();
        public bool Ready {get;private set;}
        public int OriginalCount=>originals.Count;
        public int ChunkCount=>chunks.Count;
        IEnumerator Start()
        {
            zone=GetComponent<RiverStakeZone>();
            var groups=new Dictionary<(int,int,Material,int),Group>();
            int count=0;
            foreach(var stake in zone.fieldStakes)
            {
                if(stake==null)continue;
                foreach(var filter in stake.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer=filter.GetComponent<MeshRenderer>();var source=filter.sharedMesh;
                    if(renderer==null||!renderer.enabled||source==null)continue;
                    int x=Mathf.FloorToInt(filter.transform.position.x/128),z=Mathf.FloorToInt(filter.transform.position.z/128);
                    var materials=renderer.sharedMaterials;bool included=false;
                    for(int sub=0;sub<source.subMeshCount&&sub<materials.Length;sub++)
                    {
                        if(materials[sub]==null)continue;
                        var key=(x,z,materials[sub],renderer.gameObject.layer);
                        if(!groups.TryGetValue(key,out var group))groups[key]=group=new Group{material=materials[sub],layer=renderer.gameObject.layer,center=new Vector3(x*128,0,z*128)};
                        group.pieces.Add(new CombineInstance{mesh=source,subMeshIndex=sub,transform=Matrix4x4.Translate(-group.center)*filter.transform.localToWorldMatrix});included=true;
                    }
                    if(included)originals.Add(renderer);
                }
                if(++count%96==0)yield return null;
            }
            int prepared=0;
            foreach(var group in groups.Values)
            {
                var go=new GameObject("Static_stake_chunk",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,true);go.transform.position=group.center;go.layer=group.layer;
                var mesh=new Mesh{name="River stakes 128-unit chunk",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.pieces.ToArray(),true,true);meshes.Add(mesh);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=group.material;renderer.shadowCastingMode=ShadowCastingMode.On;
                go.SetActive(false);chunks.Add(go);if(++prepared%8==0)yield return null;
            }
            foreach(var renderer in originals)if(renderer!=null)renderer.enabled=false;
            Ready=true;SetVisible(zone.IsFieldRevealed);
        }
        public void SetVisible(bool visible){if(!Ready)return;foreach(var chunk in chunks)chunk.SetActive(visible);}
        void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}
