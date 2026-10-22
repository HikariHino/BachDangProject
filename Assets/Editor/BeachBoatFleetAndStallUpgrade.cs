using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BeachBoatFleetAndStallUpgrade
{
    const string Folder="Assets/BeachBoatStory/WarFleet";
    static Material timber,dark,sail,gold;
    static Transform Group(string name,Transform parent,Vector3 p)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;return t;}
    static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material material)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
    static Material Material(string name,Color color,Material source=null)
    {
        string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat!=null)return mat;
        mat=source!=null?new Material(source):new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=name;mat.color=color;mat.enableInstancing=true;
        mat.SetFloat("_Smoothness",.16f);AssetDatabase.CreateAsset(mat,path);return mat;
    }
    static Transform MeshObject(string name,Transform parent,List<Vector3> vertices,List<int> triangles,Material material)
    {
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.uv=vertices.Select(v=>new Vector2(v.z/20,v.y/15+v.x/20)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
        string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}
        var t=Group(name,parent,Vector3.zero);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;return t;
    }
    static void Beam(string name,Transform parent,Vector3 a,Vector3 b,float thickness,Material material)
    {var go=Box(name,parent,(a+b)*.5f,new Vector3(thickness,thickness,(b-a).magnitude),material);go.transform.localRotation=Quaternion.LookRotation(b-a);}
    static void Sail(Transform parent,int index,float z,float top,float width,float height)
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int y=0;y<=6;y++)for(int x=0;x<=8;x++)
        {float u=x/8f,v=y/6f;vertices.Add(new Vector3((u-.5f)*width*Mathf.Lerp(1,.65f,v),top-height+height*v,z+Mathf.Sin(u*Mathf.PI)*Mathf.Sin(v*Mathf.PI)*12));}
        for(int y=0;y<6;y++)for(int x=0;x<8;x++){int a=y*9+x;triangles.AddRange(new[]{a,a+9,a+1,a+1,a+9,a+10});}
        MeshObject("Flagship_Curved_Indigo_Sail_"+index,parent,vertices,triangles,sail);
        for(int y=0;y<=6;y++)
        {float v=y/6f,w=width*Mathf.Lerp(1,.65f,v);Beam("Sail_Batten",parent,new Vector3(-w*.5f,top-height+height*v,z),new Vector3(w*.5f,top-height+height*v,z),.65f,gold);}
        Beam("Mast",parent,new Vector3(0,24,z),new Vector3(0,top+12,z),2.6f,dark);
        var banner=Box("Long_Command_Banner",parent,new Vector3(13,top+6,z),new Vector3(27,7,.4f),gold);banner.transform.localRotation=Quaternion.Euler(0,12,0);
    }
    static Transform Flagship(Transform parent)
    {
        var root=Group("RedSailJunkVisual",parent,Vector3.zero);
        var vertices=new List<Vector3>();var triangles=new List<int>();
        float[] zs={-158,-134,-92,0,92,134,158};float[] ws={2,26,43,47,40,22,2};
        for(int i=0;i<zs.Length;i++)
        {float sheer=Mathf.Abs(zs[i])/158;float top=27+sheer*12;vertices.AddRange(new[]{new Vector3(-ws[i],top,zs[i]),new Vector3(-ws[i]*.82f,2,zs[i]),new Vector3(0,-4,zs[i]),new Vector3(ws[i]*.82f,2,zs[i]),new Vector3(ws[i],top,zs[i])});}
        for(int i=0;i<zs.Length-1;i++)for(int j=0;j<4;j++){int a=i*5+j;triangles.AddRange(new[]{a,a+1,a+5,a+1,a+6,a+5});}
        MeshObject("Flagship_Timber_Hull",root,vertices,triangles,timber);
        var deckV=new List<Vector3>();var deckT=new List<int>();for(int i=0;i<zs.Length;i++){float y=27+Mathf.Abs(zs[i])/158*12;deckV.Add(new Vector3(-ws[i],y,zs[i]));deckV.Add(new Vector3(ws[i],y,zs[i]));}
        for(int i=0;i<zs.Length-1;i++){int a=i*2;deckT.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}MeshObject("Flagship_Plank_Deck",root,deckV,deckT,timber);
        for(int i=0;i<zs.Length-1;i++)foreach(int side in new[]{-1,1})
        {
            var a=new Vector3(side*ws[i],39+Mathf.Abs(zs[i])/158*12,zs[i]);var b=new Vector3(side*ws[i+1],39+Mathf.Abs(zs[i+1])/158*12,zs[i+1]);
            Beam("Gold_Gunwale",root,a,b,1.3f,gold);
            for(int k=0;k<4;k++){var p=Vector3.Lerp(a,b,k/4f);Beam("Deck_Railing_Post",root,p-Vector3.up*11,p,1.3f,dark);}
        }
        foreach(int tier in new[]{0,1})
        {
            float y=34+tier*20,z=-88-tier*8;var cabin=Group("Raised_Command_Deck_"+tier,root,new Vector3(0,y,z));
            Box("Timber_Deck",cabin,Vector3.zero,new Vector3(68-tier*12,3,63-tier*12),timber);
            foreach(float x in new[]{-27f+tier*5,27f-tier*5})foreach(float dz in new[]{-23f,23f})Box("Carved_Pillar",cabin,new Vector3(x,10,dz),new Vector3(2.3f,20,2.3f),dark);
            Box("Roof",cabin,new Vector3(0,20,0),new Vector3(77-tier*12,3,72-tier*12),sail);
            Box("Roof_Gold_Ridge",cabin,new Vector3(0,23,0),new Vector3(2,2,72-tier*12),gold);
        }
        Sail(root,0,-32,196,110,116);Sail(root,1,72,166,88,102);Sail(root,2,-115,133,67,66);
        Beam("Upturned_Bow",root,new Vector3(0,39,150),new Vector3(0,61,171),5,gold);
        for(int side=-1;side<=1;side+=2)for(int i=0;i<8;i++)
        {
            var shield=GameObject.CreatePrimitive(PrimitiveType.Cylinder);shield.name="Bronze_Rail_Shield";shield.transform.SetParent(root,false);shield.transform.localPosition=new Vector3(side*44,31,-72+i*20);shield.transform.localScale=new Vector3(8,.6f,8);shield.transform.localRotation=Quaternion.Euler(0,0,90);shield.GetComponent<Renderer>().sharedMaterial=gold;Object.DestroyImmediate(shield.GetComponent<Collider>());
        }
        PrefabUtility.SaveAsPrefabAsset(root.gameObject,Folder+"/NamHan_Indigo_Command_Junk.prefab");return root;
    }
    static Bounds LocalBounds(Transform root,Transform visual)
    {var rotation=root.rotation;root.rotation=Quaternion.identity;var b=BeachBoatWorldAudit.BoundsOf(visual);root.rotation=rotation;return b;}
    static float Deck(Transform visual,Transform ship,Vector3 point)
    {
        float best=-1000;
        foreach(var filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh=filter.sharedMesh;if(mesh==null)continue;
            var matrix=ship.worldToLocalMatrix*filter.transform.localToWorldMatrix;var vertices=mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3)
            {
                var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];var normal=Vector3.Cross(b-a,c-a);if(normal.sqrMagnitude<.001f||Mathf.Abs(normal.normalized.y)<.65f)continue;
                float den=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(den)<.0001f)continue;
                float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/den,v=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/den;
                float y=u*a.y+v*b.y+(1-u-v)*c.y;if(u>=0&&v>=0&&u+v<=1&&y>0&&y<90)best=Mathf.Max(best,y);
            }
        }
        if(best<-999)throw new Exception("No enlarged deck under archer "+point);return best+.3f;
    }
    public static void ApplyAndExit()
    {
        BeachBoatVillageWorkSetup.ScatterStalls();var scene=EditorSceneManager.GetActiveScene();
        if(GameObject.Find("WarFleet_Upgrade_V1")!=null)throw new Exception("Fleet upgrade already installed");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/BeachBoatStory","WarFleet");
        timber=AssetDatabase.LoadAssetAtPath<Material>("Assets/BeachBoatStory/Camp/Barracks_Dark_Beams.mat");
        dark=Material("Command_Dark_Timber",new Color(.32f,.25f,.18f),timber);sail=Material("Command_Indigo_Cloth",new Color(.095f,.18f,.28f));gold=Material("Command_Ochre_Bronze",new Color(.72f,.49f,.19f));
        var ships=Object.FindObjectsByType<EnemyShip>(FindObjectsInactive.Include,FindObjectsSortMode.None).OrderBy(s=>int.Parse(s.name.Split('_').Last())).ToArray();
        for(int i=0;i<ships.Length;i++)
        {
            var ship=ships[i];var visual=ship.transform.Find("RedSailJunkVisual");var old=LocalBounds(ship.transform,visual);float factor=228/Mathf.Max(old.size.x,old.size.z);
            var guards=ship.GetComponentsInChildren<EnemyDeckGuard>(true);
            if(i==0){Object.DestroyImmediate(visual.gameObject);visual=Flagship(ship.transform);}
            else {visual.localScale*=factor;var b=LocalBounds(ship.transform,visual);visual.position+=Vector3.up*(ship.transform.position.y-4-b.min.y);}
            foreach(var guard in guards){var p=guard.transform.localPosition;p.x*=factor;p.z*=factor;p.y=Deck(visual,ship.transform,p);guard.transform.localPosition=p;}
            ship.hullHalfLength=i==0?145:108;ship.stakeHitDistance=i==0?43:34;
            foreach(var c in ship.GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger&&c.GetComponentInParent<EnemyDeckGuard>()==null).ToArray())Object.DestroyImmediate(c);
            var collision=Group("Warship_Hull_Collision",ship.transform,new Vector3(0,11,0)).gameObject.AddComponent<BoxCollider>();collision.size=new Vector3(i==0?82:59,26,i==0?275:208);
        }
        foreach(var patrol in Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var visual=patrol.transform.Find("DaiVietPatrolVisual_Rebuilt");if(visual==null)throw new Exception("Missing patrol visual");
            var old=LocalBounds(patrol.transform,visual);float factor=190/Mathf.Max(old.size.x,old.size.z);visual.localScale*=factor;
            var b=LocalBounds(patrol.transform,visual);visual.position+=Vector3.up*(patrol.transform.position.y-3.2f-b.min.y);
            foreach(var collider in patrol.GetComponentsInChildren<BoxCollider>(true).Where(c=>!c.isTrigger&&!c.transform.IsChildOf(visual)))
            {collider.size=new Vector3(collider.size.x*factor,collider.size.y,collider.size.z*factor);}
        }
        InstallBoarding(ships);
        new GameObject("WarFleet_Upgrade_V1");EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
    static void InstallBoarding(EnemyShip[] ships)
    {
        var session=Object.FindAnyObjectByType<BeachBoatQuestSession>();
        var game=session.GetComponent<RiverBoardingCombat>();if(game==null)game=session.gameObject.AddComponent<RiverBoardingCombat>();
        game.swordPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/pf_sword_01.prefab");game.swordController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Resources/Sword_Anima.controller");
        var source=ships[0].GetComponentInChildren<EnemyDeckGuard>(true).visual;
        var go=Object.Instantiate(source.gameObject);go.name="Linh_Dai_Viet_Tiep_Chien";go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);go.transform.localScale=source.lossyScale;
        foreach(var c in go.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(c);
        foreach(var c in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
        game.enemyPrefab=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/NamHan_Boarding_Soldier.prefab");
        var blue=Material("DaiViet_Armour_Tint",new Color(.56f,.7f,.85f),go.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMaterial);
        foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))r.sharedMaterials=Enumerable.Repeat(blue,r.sharedMaterials.Length).ToArray();
        var animator=go.GetComponentInChildren<Animator>(true);animator.runtimeAnimatorController=game.swordController;animator.applyRootMotion=false;
        game.allyPrefab=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/DaiViet_Boarding_Soldier.prefab");Object.DestroyImmediate(go);
        game.friendlyBoats=Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        if(game.swordPrefab==null||game.swordController==null||game.allyPrefab==null)throw new Exception("Missing boarding combat art");
    }
    public static void PolishAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        var cloth=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Command_Indigo_Cloth.mat");cloth.SetFloat("_Cull",0);EditorUtility.SetDirty(cloth);
        for(int i=0;i<3;i++)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Flagship_Curved_Indigo_Sail_"+i+".asset");
            if(mesh.triangles.Length==576){var triangles=mesh.triangles;mesh.triangles=Enumerable.Range(0,48).SelectMany(n=>triangles.Skip(n*12).Take(6)).ToArray();mesh.RecalculateNormals();EditorUtility.SetDirty(mesh);}
        }
        InstallBoarding(Object.FindObjectsByType<EnemyShip>(FindObjectsInactive.Include,FindObjectsSortMode.None));
        foreach(var ship in Object.FindObjectsByType<EnemyShip>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        foreach(int side in new[]{-1,1})
        {
            string name=side<0?"Boarding_Port":"Boarding_Starboard";var old=ship.transform.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);
            var p=new Vector3(side*ship.stakeHitDistance*.5f,0,0);p.y=Deck(ship.transform.Find("RedSailJunkVisual"),ship.transform,p)+.8f;Group(name,ship.transform,p);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
    public static void AuditAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        object Describe(Transform t){var b=BeachBoatWorldAudit.BoundsOf(t);return new{name=t.name,p=t.position.ToString(),size=b.size.ToString(),center=b.center.ToString()};}
        var fields=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.name.Contains("Field")||r.name.Contains("Crop")||r.name.Contains("Rice_Rows")).ToArray();
        var ships=Object.FindObjectsByType<EnemyShip>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/FleetStallAudit.json",JsonConvert.SerializeObject(new{
            rowboat=Describe(Object.FindAnyObjectByType<QuestRowboat>().transform),
            patrols=Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(b=>Describe(b.transform)),
            ships=ships.Select(s=>new{ship=Describe(s.transform),visual=Describe(s.transform.Find("RedSailJunkVisual")),s.hullHalfLength,s.stakeHitDistance,route=s.routeWaypoints.Select(t=>t.position.ToString()),guards=s.GetComponentsInChildren<EnemyDeckGuard>(true).Select(g=>g.transform.localPosition.ToString())}),
            stalls=Object.FindObjectsByType<VillageWorkStall>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(s=>new{stall=Describe(s.transform),fields=fields.Where(f=>QuestRowboat.FlatDistance(f.bounds.center,s.transform.position)<300).Select(f=>new{f.name,bounds=f.bounds.ToString()})})},Formatting.Indented));EditorApplication.Exit(0);
    }
}
