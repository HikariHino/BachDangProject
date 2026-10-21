using System;
using System.Collections.Generic;
using System.Linq;
using BeachBoatStory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BeachBoatVillageAnimals
{
    const string Folder="Assets/BeachBoatStory/Life";
    static readonly List<CombineInstance>[] parts={new List<CombineInstance>(),new List<CombineInstance>(),new List<CombineInstance>(),new List<CombineInstance>()};
    static Mesh sphere,cube;
    static void Part(int material,Vector3 position,Vector3 scale,bool round=true,float yaw=0,float pitch=0,float roll=0)
    {parts[material].Add(new CombineInstance{mesh=round?sphere:cube,transform=Matrix4x4.TRS(position,Quaternion.Euler(pitch,yaw,roll),scale)});}
    static Mesh Mesh(string name)
    {
        var merged=new List<Mesh>();
        for(int i=0;i<parts.Length;i++)
        {if(parts[i].Count==0)continue;var submesh=new Mesh();submesh.CombineMeshes(parts[i].ToArray(),true,true);parts[i].Clear();merged.Add(submesh);}
        var mesh=new Mesh{name=name};mesh.CombineMeshes(merged.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,false);
        foreach(var m in merged)UnityEngine.Object.DestroyImmediate(m);
        mesh.RecalculateBounds();var path=Folder+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing!=null){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static Material Mat(string name,Color color)
    {
        var path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    static Material[] Geometry(out Mesh body,out Mesh head,out Mesh leg)
    {
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/BeachBoatStory","Life");
        var scratch=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere=scratch.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(scratch);
        scratch=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=scratch.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(scratch);
        var materials=new[]{Mat("Chicken_Warm_Feathers",new Color(.40f,.29f,.17f)),Mat("Chicken_Comb",new Color(.53f,.075f,.045f)),Mat("Chicken_Beak_And_Feet",new Color(.65f,.43f,.14f)),Mat("Chicken_Eyes",new Color(.022f,.018f,.012f))};
        Part(0,new Vector3(0,2,0),new Vector3(2.5f,2.4f,3.7f));
        Part(0,new Vector3(-1.1f,2,-.2f),new Vector3(.5f,1.7f,2.3f));Part(0,new Vector3(1.1f,2,-.2f),new Vector3(.5f,1.7f,2.3f));
        for(int i=-1;i<=1;i++)Part(0,new Vector3(i*.4f,3,-1.65f),new Vector3(.48f,2.2f,.85f),true,0,-30,i*18);
        body=Mesh("Shared_Chicken_Body");
        Part(0,Vector3.zero,new Vector3(1.2f,1.35f,1.25f));
        for(int i=-1;i<=1;i++)Part(1,new Vector3(0,.72f,i*.23f),new Vector3(.26f,.42f,.36f));
        Part(1,new Vector3(0,-.5f,.5f),new Vector3(.3f,.55f,.28f));
        var beak=new Mesh();beak.vertices=new[]{new Vector3(-.5f,-.3f,0),new Vector3(.5f,-.3f,0),new Vector3(.5f,.3f,0),new Vector3(-.5f,.3f,0),Vector3.forward};
        beak.triangles=new[]{0,1,4,1,2,4,2,3,4,3,0,4,0,2,1,0,3,2};beak.RecalculateNormals();
        parts[2].Add(new CombineInstance{mesh=beak,transform=Matrix4x4.TRS(new Vector3(0,0,.5f),Quaternion.identity,new Vector3(.7f,.7f,1.1f))});
        Part(3,new Vector3(-.55f,.2f,.35f),Vector3.one*.18f);Part(3,new Vector3(.55f,.2f,.35f),Vector3.one*.18f);
        head=Mesh("Shared_Chicken_Head");UnityEngine.Object.DestroyImmediate(beak);
        Part(2,new Vector3(0,-.6f,0),new Vector3(.16f,1.2f,.16f),false);
        for(int i=-1;i<=1;i++)Part(2,new Vector3(i*.16f,-1.17f,.32f),new Vector3(.13f,.15f,.8f),false,i*18);
        leg=Mesh("Shared_Chicken_Leg");return materials;
    }
    static Transform[] Legs(Transform root,Mesh mesh,Material material)
    {
        var result=new Transform[2];
        for(int i=0;i<2;i++)
        {
            string name=i==0?"Chan_Trai":"Chan_Phai";var t=root.Find(name);
            if(t==null){t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;t.SetParent(root,false);}
            t.localPosition=new Vector3(i==0?-.55f:.55f,1.3f,0);t.GetComponent<MeshFilter>().sharedMesh=mesh;
            t.GetComponent<MeshRenderer>().sharedMaterial=material;t.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;result[i]=t;
        }
        return result;
    }
    public static int Build(Transform settlements,Terrain terrain,Func<Vector3,bool> free)
    {
        var materials=Geometry(out var body,out var head,out var leg);int count=0;
        foreach(Transform district in settlements)
        {
            if(district.name.Contains("Trai")||district.name.Contains("Tiep_Van"))continue;
            var yards=district.Cast<Transform>().Where(t=>t.name.StartsWith("Workyard_")).Take(3).ToArray();
            foreach(var yard in yards)
            {
                var animalYard=new GameObject("Dan_Ga_San_Nha").transform;animalYard.SetParent(district,false);animalYard.position=yard.position;
                for(int i=0;i<3;i++)
                {
                    Vector3 p=default;bool found=false;
                    for(int attempt=0;attempt<32;attempt++)
                    {
                        var candidate=yard.position+Quaternion.Euler(0,attempt*137.5f+i*81,0)*Vector3.forward*(25+attempt*.7f);
                        if(!free(candidate)||animalYard.Cast<Transform>().Any(a=>QuestRowboat.FlatDistance(a.position,candidate)<4.5f))continue;p=candidate;p.y=terrain.SampleHeight(p)+terrain.transform.position.y+.1f;found=true;break;
                    }
                    if(!found)continue;
                    var go=new GameObject("Ga_Kiem_An_"+count);go.transform.SetParent(animalYard,false);go.transform.position=p;go.transform.rotation=Quaternion.Euler(0,i*113,0);
                    go.AddComponent<MeshFilter>().sharedMesh=body;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=materials[0];r.shadowCastingMode=ShadowCastingMode.Off;
                    var h=new GameObject("Dau_Ga").transform;h.SetParent(go.transform,false);h.localPosition=new Vector3(0,3.3f,1.2f);h.gameObject.AddComponent<MeshFilter>().sharedMesh=head;
                    var hr=h.gameObject.AddComponent<MeshRenderer>();hr.sharedMaterials=materials;hr.shadowCastingMode=ShadowCastingMode.Off;
                    var legs=Legs(go.transform,leg,materials[2]);var animal=go.AddComponent<VillageChicken>();animal.head=h;animal.leftLeg=legs[0];animal.rightLeg=legs[1];animal.radius=8;animal.speed=2+i*.25f;
                    var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.015f,go.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();count++;
                }
            }
        }
        return count;
    }
    public static void PolishAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");var terrain=Terrain.activeTerrain;
        var materials=Geometry(out var body,out var head,out var leg);
        var animals=UnityEngine.Object.FindObjectsByType<VillageChicken>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        foreach(var flock in animals.GroupBy(a=>a.transform.parent))
        {
            var placed=new List<Vector3>();
            foreach(var animal in flock)
            {
                var p=animal.transform.position;
                if(placed.Any(v=>QuestRowboat.FlatDistance(p,v)<4.5f))
                    for(int n=0;n<16;n++)
                    {
                        var candidate=p+Quaternion.Euler(0,n*22.5f,0)*Vector3.forward*4.8f;
                        candidate.y=terrain.SampleHeight(candidate)+terrain.transform.position.y+.1f;
                        if(placed.Any(v=>QuestRowboat.FlatDistance(candidate,v)<4.5f)||Mathf.Abs(candidate.y-p.y)>.8f)continue;
                        animal.transform.position=candidate;break;
                    }
                placed.Add(animal.transform.position);
                animal.GetComponent<MeshFilter>().sharedMesh=body;animal.GetComponent<MeshRenderer>().sharedMaterials=new[]{materials[0]};
                animal.head.GetComponent<MeshFilter>().sharedMesh=head;animal.head.GetComponent<MeshRenderer>().sharedMaterials=materials;
                var legs=Legs(animal.transform,leg,materials[2]);animal.leftLeg=legs[0];animal.rightLeg=legs[1];animal.radius=8;
                var lod=animal.GetComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.015f,animal.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
                EditorUtility.SetDirty(animal);
            }
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorApplication.Exit(0);
    }
}
