using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using VisualDesignCafe.Rendering.Nature;
using Object = UnityEngine.Object;

/// <summary>Moves the two civilian villages off the military island, without copying buildings.</summary>
public static class BachDangVillageRelocation
{
    const float F = 7f;
    const string Folder = "Assets/BachDangVillageRelocation";
    const string Marker = "12_Di_Doi_Dan_Cu";
    static readonly string[] Names = { "01_Lang_Cho_Ben_Song", "03_Xom_Vuon_Bo_Tay" };
    static readonly Vector3[] Old = { new Vector3(-690,0,110), new Vector3(-1090,0,255) };
    static readonly Vector3[] Destination = { new Vector3(-950,0,-730), new Vector3(-915,0,1095) };

    [MenuItem("Bach Dang/Reserve Island And Relocate Villages")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/beachBoat.unity" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open the current beachBoat in Edit Mode.");
        var settlement = scene.GetRootGameObjects().First(g=>g.name=="BachDang_Living_Settlements").transform;
        if (settlement.Find(Marker) != null) { Debug.Log("Island relocation is already complete."); return; }
        if (!Mathf.Approximately(BachDangWorldScale.ForScene(scene),F)) throw new InvalidOperationException("World scale must remain seven.");
        var terrain = Terrain.activeTerrain; var data = terrain.terrainData;
        if (AssetDatabase.GetAssetPath(data) != "Assets/BachDangScaleAndLife/BeachBoat_Player7Terrain.asset")
            throw new InvalidOperationException("Unexpected terrain data.");
        var nature = terrain.GetComponent<NatureRenderer>();
        if (nature != null && new SerializedObject(nature).FindProperty("_renderTreesWithNatureRenderer").boolValue)
            throw new InvalidOperationException("Keep Nature Renderer tree streaming disabled for the laptop.");
        if (AssetDatabase.IsValidFolder(Folder) && AssetDatabase.FindAssets("",new[]{Folder}).Length > 0)
            throw new InvalidOperationException("Relocation assets already exist without the scene marker.");
        var protectedTransforms = new[]{"02_Xom_Chai_Bo_Dong","04_Trai_Tiep_Van","05_Trai_Du_Bi","06_Trai_Bo_Dong","08_Ben_Ca_Dan_Sinh"}
            .SelectMany(n=>settlement.Find(n).GetComponentsInChildren<Transform>(true)).ToDictionary(t=>t,t=>t.localToWorldMatrix);
        var actors = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SkinnedMeshRenderer>(true)).ToDictionary(r=>r,r=>r.transform.localToWorldMatrix);
        int homesBefore = settlement.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("Nha_Dan_"));
        int treesBefore = data.treeInstanceCount;
        Directory.CreateDirectory(".utmp/Relocate");
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        if (!EditorSceneManager.SaveScene(scene,".utmp/Relocate/beachBoat-before-"+stamp+".unity",true)) throw new IOException("Scene backup failed.");
        AssetDatabase.SaveAssetIfDirty(data);
        File.Copy(AssetDatabase.GetAssetPath(data),".utmp/Relocate/terrain-before-"+stamp+".asset");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets","BachDangVillageRelocation");
        var assets = new List<string>(); var clearings = new List<Bounds>();
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Move civilians off military island");
        try
        {
            var marker = new GameObject(Marker); Undo.RegisterCreatedObjectUndo(marker,"Record village relocation");marker.transform.SetParent(settlement,false);
            for(int index=0;index<Names.Length;index++) MoveVillage(settlement,terrain,index,assets,clearings);
            Reconnect(settlement,terrain,marker.transform,assets,clearings);
            BachDangScaledTerrain.ClearNewDetails(terrain,clearings,F);
            if(data.treeInstanceCount>treesBefore || treesBefore-data.treeInstanceCount>1600) throw new InvalidOperationException("Unexpected vegetation change.");
            foreach(var pair in protectedTransforms)
                if(pair.Key==null || pair.Key.localToWorldMatrix!=pair.Value) throw new InvalidOperationException("An unmoved village, camp or landing changed.");
            foreach(var pair in actors)
                if(pair.Key==null || pair.Key.transform.localToWorldMatrix!=pair.Value) throw new InvalidOperationException("An existing actor moved.");
            int homesAfter=settlement.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("Nha_Dan_"));
            if(homesAfter!=homesBefore)throw new InvalidOperationException("House count changed during relocation.");
            long meshBytes=assets.Where(p=>p.EndsWith(".asset",StringComparison.Ordinal)).Select(AssetDatabase.LoadAssetAtPath<Mesh>).Where(m=>m!=null).Sum(Profiler.GetRuntimeMemorySizeLong);
            if(meshBytes>12L*1024*1024)throw new InvalidOperationException("Relocated surface geometry exceeds 12 MiB.");
            var camera=Camera.main;
            if(camera!=null)
            {
                var village=settlement.Find(Names[0]);Undo.RecordObject(camera.transform,"Frame relocated market village");
                camera.transform.position=village.Find("Tour_Camera").position;
                camera.transform.LookAt(village.Find("Tour_Target").position);EditorUtility.SetDirty(camera.transform);
            }
            var report=new {movedHomes=40,totalHomes=homesAfter,treesBefore,treesAfter=data.treeInstanceCount,actors=actors.Count,
                conformedMeshBytes=meshBytes,locations=Names.Select((n,i)=>new{name=n,x=Destination[i].x,z=Destination[i].z}).ToArray()};
            File.WriteAllText(".utmp/Relocate/result.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save relocated villages.");
            Undo.CollapseUndoOperations(undo);
            Debug.Log("Military island cleared of the two civilian villages: "+Newtonsoft.Json.JsonConvert.SerializeObject(report));
        }
        catch
        {
            Undo.RevertAllDownToGroup(undo);EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);terrain.Flush();
            if(nature!=null && nature.isActiveAndEnabled)nature.Restart();
            foreach(string asset in assets.AsEnumerable().Reverse())AssetDatabase.DeleteAsset(asset);
            throw;
        }
    }

    static float Height(Terrain terrain,Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;

    static void MoveVillage(Transform settlement,Terrain terrain,int index,List<string> assets,List<Bounds> clearings)
    {
        string name=Names[index];var village=settlement.Find(name);
        if((village.position-Old[index]*F).sqrMagnitude>.1f)throw new InvalidOperationException("Village has moved since site survey: "+name);
        var roots=new[]{village,settlement.Find("09_Sinh_Hoat/"+name+"_Sinh_Hoat"),settlement.Find("10_Lang_Tu_Nhien/"+name+"_Canh_Quan"),settlement.Find("11_San_Nha_Va_Cho/"+name)};
        if(roots.Any(r=>r==null))throw new InvalidOperationException("Incomplete village hierarchy.");
        var delta=(Destination[index]-Old[index])*F;
        // Test the entire destination envelope rather than just its center.
        for(float x=-150;x<=150;x+=10)for(float z=-150;z<=150;z+=10)
            if(Height(terrain,(Destination[index]+new Vector3(x,0,z))*F)<16.8f*F)
                throw new InvalidOperationException("Destination envelope reaches wet ground: "+name);
        var conforming=roots.SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true)).Where(f=>
            f.GetComponent<MeshRenderer>()!=null && (f.GetComponent<MeshRenderer>().sharedMaterial.shader.name=="BachDang/Village Path" || f.name.StartsWith("Co_Thap_Chan_Rao")))
            .Select(f=>new Surface(f)).ToArray();
        var units=new HashSet<Transform>();
        foreach(Transform child in village)
        {
            if(child.GetComponent<MeshFilter>()!=null)continue;
            units.Add(child);
            if(child.name.StartsWith("Nha_Dan_") || child.name=="Mai_Che_Hang_Cho")
                foreach(Transform accessory in child.Cast<Transform>().Skip(1))units.Add(accessory);
        }
        foreach(Transform area in roots[1])
        {
            if(area.GetComponent<MeshFilter>()!=null)continue;
            units.Add(area);
            foreach(Transform prop in area)
                if(prop.GetComponent<MeshFilter>()==null && prop.GetComponentsInChildren<MeshRenderer>().Length>0)units.Add(prop);
        }
        foreach(Transform prop in roots[2])if(prop.GetComponent<MeshFilter>()==null)units.Add(prop);
        foreach(Transform group in roots[3])
        {
            if(group.name.StartsWith("San_Nha_Dan_"))foreach(Transform prop in group)units.Add(prop);
            else units.Add(group); // Tables retain the exact height of their displayed goods.
        }
        var yTargets=units.ToDictionary(t=>t,t=>t.position.y+Height(terrain,t.position+delta)-Height(terrain,t.position));
        foreach(var root in roots){Undo.RegisterFullObjectHierarchyUndo(root.gameObject,"Move complete civilian village");root.position+=delta;EditorUtility.SetDirty(root);}
        foreach(var pair in yTargets.OrderBy(p=>Depth(p.Key)))
        {var point=pair.Key.position;point.y=pair.Value;pair.Key.position=point;EditorUtility.SetDirty(pair.Key);if(PrefabUtility.IsPartOfPrefabInstance(pair.Key))PrefabUtility.RecordPrefabInstancePropertyModifications(pair.Key);}
        foreach(var surface in conforming)
        {
            var source=surface.filter.sharedMesh;
            if(!source.isReadable)throw new InvalidOperationException("Relocated ground mesh is not readable.");
            var mesh=Object.Instantiate(source);mesh.name=source.name+"_Relocated";
            var vertices=source.vertices;
            for(int i=0;i<vertices.Length;i++)
            {
                var oldPoint=surface.matrix.MultiplyPoint3x4(vertices[i]);var point=oldPoint+delta;
                point.y=oldPoint.y+Height(terrain,point)-Height(terrain,oldPoint);
                vertices[i]=surface.filter.transform.InverseTransformPoint(point);
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Folder+"/"+assets.Count.ToString("000")+"_"+surface.filter.name+".asset";
            AssetDatabase.CreateAsset(mesh,path);assets.Add(path);
            Undo.RecordObject(surface.filter,"Conform relocated village ground");surface.filter.sharedMesh=mesh;EditorUtility.SetDirty(surface.filter);
        }
        // Keep trees around the villages; clear only the occupied plots and lanes, with a canopy margin.
        foreach(var root in roots.Take(3))foreach(Transform child in root)
        {
            if(child.name.StartsWith("Tour_") || child.name.StartsWith("Co_Thap_Chan_Rao"))continue;
            var renderers=child.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
            if(renderers.Length==0)continue;
            var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            bounds.Expand(new Vector3(7,0,7)*F);clearings.Add(bounds);
        }
        foreach(var house in village.Cast<Transform>().Where(t=>t.name.StartsWith("Nha_Dan_")))
        {
            var b=BachDangSettlementAssets.BoundsOf(house.GetChild(0).gameObject);
            float max=float.MinValue,min=float.MaxValue;
            foreach(int x in new[]{-1,0,1})foreach(int z in new[]{-1,0,1})
            {float h=Height(terrain,b.center+new Vector3(x*b.extents.x*.85f,0,z*b.extents.z*.85f));max=Mathf.Max(max,h);min=Mathf.Min(min,h);}
            if(min<16.8f*F || max-min>.75f*F || Mathf.Abs(b.min.y-max)>.45f*F)
                throw new InvalidOperationException("Relocated house failed terrain support check: "+name+"/"+house.name);
        }
    }

    static void Reconnect(Transform settlement,Terrain terrain,Transform root,List<string> assets,List<Bounds> clearings)
    {
        var links=settlement.Find("07_Duong_Lang_Va_Tiep_Van");
        foreach(string name in new[]{"Duong_Lang_Den_Xuong","Duong_Xom_Vuon_Tiep_Van"})
        {var renderer=links.Find(name).GetComponent<MeshRenderer>();Undo.RecordObject(renderer,"Retire former civilian connection");renderer.enabled=false;EditorUtility.SetDirty(renderer);}
        var painter=new BachDangVillageGround(terrain,F,Folder,assets);
        var market=settlement.Find(Names[0]);var camp=settlement.Find("05_Trai_Du_Bi");
        Vector3 W(float x,float z)=>new Vector3(x*F,Height(terrain,new Vector3(x*F,0,z*F)),z*F);
        var south=new[]{market.TransformPoint(new Vector3(0,0,96)),W(-990,-575),W(-1024,-525),camp.TransformPoint(new Vector3(0,0,-65))};
        var north=new[]{settlement.Find(Names[1]).TransformPoint(new Vector3(61,0,-50)),W(-810,1020),W(-765,1000)};
        foreach(var p in south.Concat(north))if(Height(terrain,p)<16.8f*F)throw new InvalidOperationException("New approach crosses wet ground.");
        painter.PathPointAllowed=(p,width)=>Height(terrain,p)>=16.8f*F;
        painter.CreatePath(root,"Duong_Lang_Cho_Bo_Nam",south,3.6f,painter.Earth,9382,clearings);
        painter.CreatePath(root,"Loi_Xom_Vuon_Bo_Bac",north,2.5f,painter.Earth,9383,clearings);
    }

    static int Depth(Transform t){int depth=0;while(t.parent!=null){depth++;t=t.parent;}return depth;}
    readonly struct Surface
    {
        public readonly MeshFilter filter;public readonly Matrix4x4 matrix;
        public Surface(MeshFilter filter){this.filter=filter;matrix=filter.transform.localToWorldMatrix;}
    }
}
