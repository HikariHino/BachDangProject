using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using Object=UnityEngine.Object;

/// <summary>Offline triangle clipping: no mesh cutting/cooking is done on a sinking frame.</summary>
public static class BeachBoatWreckBake
{
    const string Folder="Assets/Resources/BachDang938/Wrecks";
    struct V
    {
        public Vector3 p,n;public Vector2 uv;
        public static V Lerp(V a,V b,float t)=>new V{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t)};
    }
    sealed class Batch
    {
        public readonly List<Vector3> v=new List<Vector3>(),n=new List<Vector3>();public readonly List<Vector2> uv=new List<Vector2>();public readonly List<int> tris=new List<int>();
        public void Triangle(V a,V b,V c){foreach(var x in new[]{a,b,c}){tris.Add(v.Count);v.Add(x.p-Vector3.up*20);n.Add(x.n);uv.Add(x.uv);}}
        public Mesh Mesh(string name){var m=new Mesh{name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};m.SetVertices(v);m.SetNormals(n);m.SetUVs(0,uv);m.SetTriangles(tris,0);m.RecalculateBounds();return m;}
    }
    public static string Bake()
    {
        if(EditorApplication.isPlaying)throw new Exception("Bake outside Play mode");
        System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var ship=Object.FindObjectsByType<EnemyShip>(FindObjectsInactive.Include,FindObjectsSortMode.None)[0];var visual=ship.transform.Find("RedSailJunkVisual");
        var root=new GameObject("BrokenWarship");int parts=0;
        for(int side=-1;side<=1;side+=2)
        {
            var half=new GameObject(side<0?"Stern":"Bow");half.transform.SetParent(root.transform,false);
            var groups=new Dictionary<Material,Batch>();var collision=new Batch();
            foreach(var filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||filter.sharedMesh==null)continue;
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;var matrix=visual.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                bool solid=filter.name.Contains("Hull")||filter.name.Contains("Deck")||filter.name=="Roof"||filter.name=="Upturned_Bow"||filter.name=="Mast"||filter.name=="Carved_Pillar";
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var mat=renderer.sharedMaterials[Mathf.Min(sub,renderer.sharedMaterials.Length-1)];if(mat==null)continue;
                    if(!groups.TryGetValue(mat,out var batch)){batch=new Batch();groups.Add(mat,batch);}var indices=mesh.GetTriangles(sub);
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        var polygon=new List<V>();for(int j=0;j<3;j++){int k=indices[i+j];polygon.Add(new V{p=matrix.MultiplyPoint3x4(vertices[k]),n=matrix.inverse.transpose.MultiplyVector(normals.Length>k?normals[k]:Vector3.up).normalized,uv=uv.Length>k?uv[k]:Vector2.zero});}
                        var clipped=new List<V>();for(int j=0;j<polygon.Count;j++)
                        {
                            var a=polygon[j];var b=polygon[(j+1)%polygon.Count];bool ai=a.p.z*side>=0,bi=b.p.z*side>=0;
                            if(ai)clipped.Add(a);if(ai!=bi)clipped.Add(V.Lerp(a,b,-a.p.z/(b.p.z-a.p.z)));
                        }
                        for(int j=0;j<clipped.Count;j++)if(Mathf.Abs(clipped[j].p.z)<.001f){var edge=clipped[j];edge.p.z=3.2f*Mathf.Sin(edge.p.x*.31f+edge.p.y*.17f);clipped[j]=edge;}
                        for(int j=1;j+1<clipped.Count;j++){batch.Triangle(clipped[0],clipped[j],clipped[j+1]);if(solid)collision.Triangle(clipped[0],clipped[j],clipped[j+1]);}
                    }
                }
            }
            foreach(var group in groups)
            {
                if(group.Value.v.Count==0)continue;var go=new GameObject(group.Key.name);go.transform.SetParent(half.transform,false);var mesh=group.Value.Mesh(half.name+"_"+parts++);mesh=SaveMesh(mesh);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            var solidObject=new GameObject("Solid timber");solidObject.transform.SetParent(half.transform,false);var solidMesh=collision.Mesh(half.name+"_Collision");solidMesh=SaveMesh(solidMesh);var collider=solidObject.AddComponent<MeshCollider>();collider.sharedMesh=solidMesh;collider.convex=false;
        }
        PrefabUtility.SaveAsPrefabAsset(root,Folder+"/BrokenWarship.prefab");Object.DestroyImmediate(root);AssetDatabase.SaveAssets();return "Baked two real clipped halves, "+parts+" render batches";
    }
    public static string BakeParticleMaterial()
    {
        const string path="Assets/Resources/BachDang938/RuntimeParticle.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(material,path);}
        material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ColorMode",0);material.SetFloat("_Cull",0);material.SetFloat("_ZWrite",0);
        material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetColor("_BaseColor",Color.white);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();return material.shader.name;
    }
    static Mesh SaveMesh(Mesh mesh)
    {
        string path=Folder+"/"+mesh.name.Replace('/','_')+".asset";var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(previous!=null){EditorUtility.CopySerialized(mesh,previous);Object.DestroyImmediate(mesh);return previous;}else AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
}
