using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using BeachBoatStory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

/// <summary>Rebuild surfaces and close-range asset presentation without changing gameplay transforms.</summary>
public static class BeachBoatCinematicArt
{
    const string Root="Assets/BachDangCinematic",ResourcesRoot="Assets/Resources/BachDang938/Visuals";
    static readonly Dictionary<string,Material> surfaces=new Dictionary<string,Material>();
    static readonly Dictionary<string,(Material material,Mesh original,Mesh middle,Mesh far)> cast=new Dictionary<string,(Material,Mesh,Mesh,Mesh)>();
    static readonly Dictionary<Material,Material> environment=new Dictionary<Material,Material>();
    static bool rebuildMeshes;
    static Texture2D Texture(string path,bool normal=false,bool linear=false)
    {
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null)throw new Exception("Missing texture "+path);
        var type=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        if(importer.textureType!=type||importer.sRGBTexture!=(!normal&&!linear)||importer.maxTextureSize!=2048||!importer.streamingMipmaps)
        {
            importer.textureType=type;importer.sRGBTexture=!normal&&!linear;importer.maxTextureSize=2048;
            importer.mipmapEnabled=true;importer.streamingMipmaps=true;importer.anisoLevel=8;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Material Lit(string path,string albedo,string normal,string mask)
    {
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetTexture("_BaseMap",Texture(albedo));m.SetColor("_BaseColor",Color.white);
        m.SetTexture("_BumpMap",Texture(normal,true));m.SetFloat("_BumpScale",.8f);m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap",Texture(mask,false,true));m.SetFloat("_Smoothness",1);m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetTexture("_OcclusionMap",Texture(mask,false,true));m.SetFloat("_OcclusionStrength",.7f);m.EnableKeyword("_OCCLUSIONMAP");
        m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    static Material Surface(string id)
    {
        if(surfaces.TryGetValue(id,out var m))return m;string folder=Root+"/Surfaces/"+id;
        m=Lit(folder+"/Cinematic.mat",folder+"/Albedo.jpg",folder+"/Normal.jpg",folder+"/MetallicSmoothnessAO.png");surfaces[id]=m;return m;
    }
    static Mesh SimplifyMesh(Mesh source,string path,float fraction)
    {
        var exists=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(exists!=null&&!rebuildMeshes)return exists;
        var simplifier=new UnityMeshSimplifier.MeshSimplifier();var options=UnityMeshSimplifier.SimplificationOptions.Default;
        options.PreserveBorderEdges=true;options.PreserveUVFoldoverEdges=true;options.EnableSmartLink=true;
        simplifier.SimplificationOptions=options;simplifier.Initialize(source);simplifier.SimplifyMesh(fraction);
        var mesh=simplifier.ToMesh();mesh.name=Path.GetFileNameWithoutExtension(path);mesh.bounds=source.bounds;
        if(exists==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,exists);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(exists);return exists;
    }
    static void ConfigureCast(JArray list)
    {
        foreach(var item in list)
        {
            string model=(string)item["model"],mask=(string)item["mask"];
            var importer=(ModelImporter)AssetImporter.GetAtPath(model);
            if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(model).GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh;
            string folder=Path.GetDirectoryName(mask).Replace('\\','/');
            var material=Lit(folder+"/Cinematic.mat",(string)item["albedo"],(string)item["normal"],mask);
            var middle=SimplifyMesh(source,folder+"/CrowdMiddle.asset",.35f);var far=SimplifyMesh(source,folder+"/CrowdFar.asset",.12f);
            cast[model]=(material,source,middle,far);
        }
    }
    static int BindCast(GameObject root)
    {
        int count=0;
        foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if(renderer.sharedMesh==null||!cast.TryGetValue(AssetDatabase.GetAssetPath(renderer.sharedMesh),out var entry))continue;
            renderer.sharedMaterials=Enumerable.Repeat(entry.material,renderer.sharedMaterials.Length).ToArray();
            var lod=renderer.GetComponent<CinematicCharacterLOD>()??renderer.gameObject.AddComponent<CinematicCharacterLOD>();
            lod.target=renderer;lod.closeMesh=entry.original;lod.middleMesh=entry.middle;lod.farMesh=entry.far;
            string name=entry.original.name+AssetDatabase.GetAssetPath(entry.original);lod.important=name.Contains("Ngo_Quyen")||name.Contains("Steppe_Ironclad");
            renderer.updateWhenOffscreen=false;EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(lod);count++;
            if(PrefabUtility.IsPartOfPrefabInstance(renderer)){PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(lod);}
        }
        return count;
    }
    static void BuildPipeline()
    {
        Directory.CreateDirectory(ResourcesRoot);AssetDatabase.Refresh();
        string rendererPath=ResourcesRoot+"/CinematicRenderer.asset",pipelinePath=ResourcesRoot+"/CinematicRP.asset";
        if(!File.Exists(rendererPath))AssetDatabase.CopyAsset("Assets/Settings/PC_Renderer.asset",rendererPath);
        if(!File.Exists(pipelinePath))AssetDatabase.CopyAsset("Assets/Settings/PC_RPAsset.asset",pipelinePath);
        var rp=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        var serialized=new SerializedObject(rp);serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        serialized.FindProperty("m_MainLightShadowmapResolution").intValue=4096;
        serialized.FindProperty("m_AdditionalLightsPerObjectLimit").intValue=8;
        serialized.FindProperty("m_ShadowCascadeCount").intValue=4;
        serialized.FindProperty("m_SoftShadowQuality").intValue=2;
        serialized.FindProperty("m_MSAA").intValue=2;serialized.ApplyModifiedPropertiesWithoutUndo();
        rp.renderScale=1;rp.shadowDistance=115;rp.supportsHDR=true;
        foreach(var feature in AssetDatabase.LoadAllAssetsAtPath(rendererPath).OfType<ScriptableRendererFeature>())
        {
            if(!feature.name.Contains("AmbientOcclusion"))continue;
            var data=new SerializedObject(feature);data.FindProperty("m_Settings.Intensity").floatValue=.55f;
            data.FindProperty("m_Settings.Radius").floatValue=1.3f;data.FindProperty("m_Settings.Falloff").floatValue=650;data.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(rp);
    }
    static void BuildTerrain()
    {
        var terrain=Terrain.activeTerrain;var old=terrain.terrainData.terrainLayers;var layers=new TerrainLayer[old.Length];
        string[] ids={"aerial_grass_rock","brown_mud_02","sandy_gravel_02","forest_floor","rock_boulder_dry","sandy_gravel_02"};float[] tiles={42,28,42,35,70,42};
        Directory.CreateDirectory(Root+"/Terrain");AssetDatabase.Refresh();
        for(int i=0;i<old.Length;i++)
        {
            string path=Root+"/Terrain/PhysicalGround_"+i+".terrainlayer";var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if(layer==null){layer=Object.Instantiate(old[i]);AssetDatabase.CreateAsset(layer,path);}
            var m=Surface(ids[Mathf.Min(i,ids.Length-1)]);layer.diffuseTexture=(Texture2D)m.GetTexture("_BaseMap");layer.normalMapTexture=(Texture2D)m.GetTexture("_BumpMap");layer.maskMapTexture=(Texture2D)m.GetTexture("_MetallicGlossMap");
            layer.tileSize=Vector2.one*tiles[Mathf.Min(i,tiles.Length-1)];layer.normalScale=.7f;layer.diffuseRemapMin=Vector4.zero;layer.diffuseRemapMax=Vector4.one;
            layer.maskMapRemapMin=Vector4.zero;layer.maskMapRemapMax=Vector4.one;layer.metallic=0;layer.smoothness=i==1?.32f:.12f;EditorUtility.SetDirty(layer);layers[i]=layer;
        }
        terrain.terrainData.terrainLayers=layers;EditorUtility.SetDirty(terrain.terrainData);
    }
    static void BuildAtmosphere()
    {
        var volume=Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).First(v=>v.sharedProfile!=null);
        string path=Root+"/CinematicAtmosphere.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if(profile==null){AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(volume.sharedProfile),path);profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);}
        profile.components.RemoveAll(c=>c==null);
        T Get<T>()where T:VolumeComponent
        {if(profile.TryGet<T>(out var v))return v;v=profile.Add<T>(true);AssetDatabase.AddObjectToAsset(v,profile);return v;}
        var tone=Get<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
        var color=Get<ColorAdjustments>();color.postExposure.Override(.3f);color.contrast.Override(9);color.saturation.Override(-10);
        var bloom=Get<Bloom>();bloom.intensity.Override(.12f);bloom.threshold.Override(1.3f);
        var white=Get<WhiteBalance>();white.temperature.Override(3);white.tint.Override(1);
        if(profile.TryGet<DepthOfField>(out var depth))depth.active=false;if(profile.TryGet<MotionBlur>(out var motion))motion.active=false;
        volume.sharedProfile=profile;foreach(var component in profile.components)EditorUtility.SetDirty(component);EditorUtility.SetDirty(profile);EditorUtility.SetDirty(volume);
        var tide=Object.FindAnyObjectByType<TideSystem>();var original=tide.GetComponent<Renderer>().sharedMaterial;
        string waterPath=Root+"/CinematicRiver.mat";var water=AssetDatabase.LoadAssetAtPath<Material>(waterPath);
        if(water==null){water=new Material(original);AssetDatabase.CreateAsset(water,waterPath);}
        water.SetColor("_WaterColor",new Color(.27f,.40f,.33f,1));water.SetColor("_DeepWaterColor",new Color(.10f,.23f,.21f,1));
        water.SetFloat("_NormalStrength",.42f);water.SetFloat("_NormalWorldScale",49);water.SetFloat("_FoamIntensity",1.2f);
        tide.GetComponent<Renderer>().sharedMaterial=water;EditorUtility.SetDirty(water);
    }
    static MeshFilter ScannedSource(string id)
    {return AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/"+id+".fbx").GetComponentsInChildren<MeshFilter>(true).OrderByDescending(m=>m.sharedMesh.vertexCount).First();}
    static Mesh UprightScan(string id)
    {
        string path=Root+"/Models/"+id+"_Upright.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
        var source=ScannedSource(id);mesh=Object.Instantiate(source.sharedMesh);mesh.name=id+"_Upright";var rotation=source.transform.rotation;
        mesh.vertices=mesh.vertices.Select(v=>rotation*v).ToArray();mesh.normals=mesh.normals.Select(n=>rotation*n).ToArray();
        mesh.tangents=mesh.tangents.Select(t=>{var v=rotation*new Vector3(t.x,t.y,t.z);return new Vector4(v.x,v.y,v.z,t.w);}).ToArray();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static void FitScan(MeshRenderer renderer,Mesh mesh,Bounds target)
    {
        renderer.GetComponent<MeshFilter>().sharedMesh=mesh;var b=mesh.bounds;
        float scale=Mathf.Min(target.size.x/b.size.x,Mathf.Min(target.size.y/b.size.y,target.size.z/b.size.z));var parentScale=renderer.transform.parent.lossyScale;
        renderer.transform.rotation=Quaternion.identity;renderer.transform.localScale=new Vector3(scale/Mathf.Abs(parentScale.x),scale/Mathf.Abs(parentScale.y),scale/Mathf.Abs(parentScale.z));
        renderer.transform.position=target.center-b.center*scale;
        renderer.transform.position+=Vector3.up*(target.min.y-renderer.bounds.min.y);
        EditorUtility.SetDirty(renderer.transform);EditorUtility.SetDirty(renderer.GetComponent<MeshFilter>());
    }
    static GameObject ScannedVisual(string id,Transform parent,Bounds target)
    {
        var go=new GameObject("Scanned_"+id,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,true);
        go.GetComponent<MeshRenderer>().sharedMaterial=Surface(id);FitScan(go.GetComponent<MeshRenderer>(),UprightScan(id),target);return go;
    }
    public static void RebuildVisualMeshes()
    {
        rebuildMeshes=true;try{ConfigureCast((JArray)JObject.Parse(File.ReadAllText(Root+"/sources.json"))["characters"]);}finally{rebuildMeshes=false;}
        foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.name.StartsWith("Scanned_")))
        {
            string id=renderer.name.Substring(8);Bounds target;
            if(id=="tree_stump_01"){var p=renderer.bounds.center;p.y=Terrain.activeTerrain.SampleHeight(p)+Terrain.activeTerrain.transform.position.y+6;target=new Bounds(p,new Vector3(22,12,22));}
            else
            {
                var parent=renderer.transform.parent;var lod=parent.GetComponent<LODGroup>();
                target=lod!=null?lod.GetLODs().SelectMany(l=>l.renderers).First(r=>r!=null).bounds:parent.GetComponent<MeshRenderer>().bounds;
            }
            FitScan(renderer,UprightScan(id),target);var box=renderer.GetComponent<BoxCollider>();if(box!=null){box.center=renderer.GetComponent<MeshFilter>().sharedMesh.bounds.center;box.size=renderer.GetComponent<MeshFilter>().sharedMesh.bounds.size;EditorUtility.SetDirty(box);}
        }
        AssetDatabase.SaveAssets();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/CinematicMeshRepair.json","{\"success\":true}");
    }
    public static void PersistAtmosphere()
    {BuildAtmosphere();AssetDatabase.SaveAssets();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
    public static void GroundScannedProps()
    {
        foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.name.StartsWith("Scanned_")))
        {
            var parent=renderer.transform.parent;var group=parent.GetComponent<LODGroup>();
            float floor=renderer.name.Contains("tree_stump")?Terrain.activeTerrain.SampleHeight(renderer.bounds.center)+Terrain.activeTerrain.transform.position.y:
                group!=null?group.GetLODs().SelectMany(l=>l.renderers).First(r=>r!=null).bounds.min.y:parent.GetComponent<MeshRenderer>().bounds.min.y;
            renderer.transform.position+=Vector3.up*(floor-renderer.bounds.min.y);EditorUtility.SetDirty(renderer.transform);
        }
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    public static void PreserveFactionAppearance()
    {
        var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/BeachBoatStory/WarFleet/DaiViet_Armour_Tint.mat");
        string path=Root+"/FriendlyArmour.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(original);AssetDatabase.CreateAsset(material,path);}
        var data=JObject.Parse(File.ReadAllText(Root+"/sources.json"));var soldier=data["characters"].First(c=>((string)c["model"]).Contains("OldSoldier"));
        material.SetTexture("_BumpMap",Texture((string)soldier["normal"],true));material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.8f);
        material.SetTexture("_MetallicGlossMap",Texture((string)soldier["mask"],false,true));material.EnableKeyword("_METALLICSPECGLOSSMAP");material.SetFloat("_Smoothness",1);
        string prefab="Assets/BeachBoatStory/WarFleet/DaiViet_Boarding_Soldier.prefab";var root=PrefabUtility.LoadPrefabContents(prefab);
        foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))renderer.sharedMaterial=material;
        PrefabUtility.SaveAsPrefabAsset(root,prefab);PrefabUtility.UnloadPrefabContents(root);EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
    }
    public static void CaptureBefore()=>CaptureViews("Before");
    public static void CaptureAfter()=>CaptureViews("After");
    static void CaptureViews(string suffix)
    {
        var cycle=Object.FindAnyObjectByType<DayNightCycle>();float hour=cycle.timeOfDay;
        var origin=GameObject.Find("CampLayoutOrigin").transform.position;
        var camera=new GameObject("Art_QA_Camera").AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.fieldOfView=58;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        var ngo=GameObject.Find("NgoQuyen_QuestGiver").transform;
        var views=new[]{(origin+new Vector3(0,23,-115),origin+new Vector3(0,14,100),5.75f),
            (ngo.position+ngo.forward*34+Vector3.up*14,ngo.position+Vector3.up*12,9f),
            (origin+new Vector3(-175,25,80),origin+new Vector3(-260,8,180),9f)};
        try
        {
            for(int i=0;i<views.Length;i++)
            {
                cycle.SetTimeOfDay(views[i].Item3);camera.transform.position=views[i].Item1;camera.transform.LookAt(views[i].Item2);
                TorchFlame.RefreshForCamera(camera);
                foreach(var t in Object.FindObjectsByType<TorchFlame>()){t.flame.Simulate(1.2f,true,true);if(t.embers!=null)t.embers.Simulate(1.2f,true,true);}
                var rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();
                File.WriteAllBytes("Logs/Cinematic_"+i+"_"+suffix+".png",pixels.EncodeToPNG());
                RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(pixels);Object.DestroyImmediate(rt);
            }
        }
        finally{cycle.SetTimeOfDay(hour);Object.DestroyImmediate(camera.gameObject);}
    }
    public static void UpgradePlainSurfaces()
    {
        var replacements=new Dictionary<Material,Material>();Directory.CreateDirectory(Root+"/Architecture");AssetDatabase.Refresh();
        foreach(var original in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct())
        {
            if(original.mainTexture!=null)continue;string name=original.name;
            bool wood=name=="Courtyard_Weathered_Wood"||name=="MinigameWood"||name=="hajotest"||name=="fedelzet";
            bool soil=name.Contains("Natural_Earth")||name.Contains("Natural_Mud")||name=="Village_Garden_Soil"||name=="Village_Packed_Earth";
            bool pebbles=name.Contains("Natural_Pebbles")||name=="River_Pebble";
            if(!wood&&!soil&&!pebbles)continue;
            string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original));string path=Root+"/Architecture/"+name+"_"+guid.Substring(0,8)+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Surface(wood?"weathered_brown_planks":pebbles?"rock_boulder_dry":"brown_mud_02"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",wood?(name=="hajotest"?new Color(.48f,.32f,.22f):new Color(.75f,.66f,.5f)):Color.white);
            material.SetTextureScale("_BaseMap",Vector2.one*(wood?3:2));material.SetFloat("_BumpScale",.6f);material.name=name+"_Cinematic";EditorUtility.SetDirty(material);replacements[original]=material;
        }
        bool Bind(GameObject root)
        {
            bool any=false;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<mats.Length;i++)if(mats[i]!=null&&replacements.TryGetValue(mats[i],out var m)){mats[i]=m;changed=true;}
                if(!changed)continue;any=true;renderer.sharedMaterials=mats;EditorUtility.SetDirty(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            return any;
        }
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();foreach(var root in scene.GetRootGameObjects())Bind(root);
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Resources","Assets/BeachBoatStory"}))
        {string path=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(path);if(Bind(root))PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    public static void Apply()
    {
        if(Application.isPlaying)throw new Exception("Apply visual asset rebuild in Edit Mode");
        if(GameObject.Find("Cinematic_Environment_938")!=null)throw new Exception("Visual asset rebuild already applied");
        surfaces.Clear();cast.Clear();environment.Clear();AssetDatabase.Refresh();
        var data=JObject.Parse(File.ReadAllText(Root+"/sources.json"));ConfigureCast((JArray)data["characters"]);
        foreach(var item in (JArray)data["environment"])
        {
            var original=AssetDatabase.LoadAssetAtPath<Material>((string)item["material"]);if(original==null)continue;
            string folder=Path.GetDirectoryName((string)item["mask"]).Replace('\\','/');environment[original]=Lit(folder+"/Cinematic.mat",(string)item["albedo"],(string)item["normal"],(string)item["mask"]);
        }
        int actors=0;var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();foreach(var root in scene.GetRootGameObjects())actors+=BindCast(root);
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Resources","Assets/BeachBoatStory"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);if(path.StartsWith(ResourcesRoot))continue;
            var root=PrefabUtility.LoadPrefabContents(path);int changed=BindCast(root);if(changed>0)PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
        }
        foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var materials=r.sharedMaterials;bool changed=false;
            for(int i=0;i<materials.Length;i++)if(materials[i]!=null&&environment.TryGetValue(materials[i],out var replacement)){materials[i]=replacement;changed=true;}
            if(changed){r.sharedMaterials=materials;EditorUtility.SetDirty(r);if(PrefabUtility.IsPartOfPrefabInstance(r))PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
        }
        BuildPipeline();BuildTerrain();BuildAtmosphere();UpgradePlainSurfaces();PreserveFactionAppearance();
        var art=new GameObject("Cinematic_Environment_938");int baskets=0,rocks=0;
        var campOrigin=GameObject.Find("CampLayoutOrigin").transform.position;
        foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).ToArray())
        {
            var mesh=renderer.GetComponent<MeshFilter>();if(mesh==null||mesh.sharedMesh==null)continue;string path=AssetDatabase.GetAssetPath(mesh.sharedMesh);
            if(path.EndsWith("/prop_barrel_01.fbx",StringComparison.OrdinalIgnoreCase)&&baskets<28)
            {ScannedVisual("wicker_basket_01",renderer.transform,renderer.bounds);renderer.enabled=false;EditorUtility.SetDirty(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);baskets++;}
            else if((path.EndsWith("Rock_A_02.fbx")||path.EndsWith("Rock_C_01.fbx"))&&rocks<32&&QuestRowboat.FlatDistance(renderer.transform.position,campOrigin)<1500)
            {
                var group=renderer.GetComponentInParent<LODGroup>();var anchor=group!=null?group.transform:renderer.transform;
                if(anchor.Find("Scanned_rock_moss_set_01")!=null)continue;
                var bounds=renderer.bounds;ScannedVisual("rock_moss_set_01",anchor,bounds);
                if(group!=null){group.enabled=false;EditorUtility.SetDirty(group);PrefabUtility.RecordPrefabInstancePropertyModifications(group);foreach(var old in group.GetLODs().SelectMany(l=>l.renderers))if(old!=null){old.enabled=false;EditorUtility.SetDirty(old);PrefabUtility.RecordPrefabInstancePropertyModifications(old);}}else {renderer.enabled=false;EditorUtility.SetDirty(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
                rocks++;
            }
        }
        for(int i=0;i<12;i++)
        {
            float angle=i*137.5f;var p=campOrigin+Quaternion.Euler(0,angle,0)*Vector3.forward*(440+(i%3)*110);
            p.y=Terrain.activeTerrain.SampleHeight(p)+Terrain.activeTerrain.transform.position.y;
            if(Physics.OverlapBox(p+Vector3.up*6,new Vector3(10,6,10),Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(c=>!(c is TerrainCollider)))continue;
            var go=ScannedVisual("tree_stump_01",art.transform,new Bounds(p+Vector3.up*6,new Vector3(22,12,22)));var box=go.AddComponent<BoxCollider>();box.center=go.GetComponent<MeshFilter>().sharedMesh.bounds.center;box.size=go.GetComponent<MeshFilter>().sharedMesh.bounds.size;
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/CinematicArtBuild.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{actors,characterTypes=cast.Count,environmentMaterials=environment.Count,baskets,rocks,scanProps=art.transform.childCount},Newtonsoft.Json.Formatting.Indented));
    }
}
