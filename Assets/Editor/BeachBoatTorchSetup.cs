using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Attach one lightweight fire prefab to each authored torch mesh in beachBoat.</summary>
public static class BeachBoatTorchSetup
{
    const string Folder="Assets/BeachBoatStory/Camp/", PrefabPath=Folder+"Torch_Flame.prefab";
    static Material Material(string name,Texture texture)
    {
        string path=Folder+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(material,path);}
        material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);
        material.SetFloat("_Surface",1);material.SetFloat("_Blend",2);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.One);
        material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;EditorUtility.SetDirty(material);return material;
    }
    static ParticleSystem Particles(string name,Transform parent,Material material,bool sparks)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);
        go.transform.localPosition=Vector3.up*(sparks?.25f:.48f);
        var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.loop=true;main.prewarm=true;main.duration=2;main.playOnAwake=true;
        main.maxParticles=sparks?8:4;main.startLifetime=new ParticleSystem.MinMaxCurve(sparks?.5f:.65f,sparks?1.2f:1.05f);
        main.startSpeed=0;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
        main.cullingMode=ParticleSystemCullingMode.Pause;
        main.startSize=new ParticleSystem.MinMaxCurve(sparks?.018f:.85f,sparks?.04f:1.1f);
        main.startColor=sparks?new Color(1,.6f,.13f):Color.white;
        var emission=ps.emission;emission.rateOverTime=sparks?2:3;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=sparks?.09f:.025f;
        var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;
        velocity.y=new ParticleSystem.MinMaxCurve(sparks?.9f:.12f,sparks?1.5f:.24f);
        velocity.x=new ParticleSystem.MinMaxCurve(-.05f,.05f);velocity.z=new ParticleSystem.MinMaxCurve(-.05f,.05f);
        var color=ps.colorOverLifetime;color.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.8f,.7f),new GradientAlphaKey(0,1)});color.color=gradient;
        if(!sparks)
        {
            var sheet=ps.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=8;sheet.numTilesY=4;
            sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));sheet.cycleCount=1;
            sheet.startFrame=new ParticleSystem.MinMaxCurve(0,.2f);
        }
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.VerticalBillboard;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;
        renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        return ps;
    }
    static GameObject BuildPrefab()
    {
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Effects/ParticleFlamesSheet.png");
        if(texture==null)throw new InvalidOperationException("Existing flame atlas is missing");
        var sparkSource=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Effects/mat_fx_sparks_01.mat");
        var root=new GameObject("Torch_Flame");var controller=root.AddComponent<TorchFlame>();
        controller.flame=Particles("Flame",root.transform,Material("Torch_Flame_Unlit",texture),false);
        controller.embers=Particles("Embers",root.transform,Material("Torch_Embers_Unlit",sparkSource.GetTexture("_BaseMap")),true);
        var light=new GameObject("Warm_Firelight");light.transform.SetParent(root.transform,false);light.transform.localPosition=Vector3.up*.4f;
        controller.fireLight=light.AddComponent<Light>();controller.fireLight.type=LightType.Point;controller.fireLight.color=new Color(1,.47f,.12f);
        controller.fireLight.intensity=2.2f;controller.fireLight.range=35;controller.fireLight.shadows=LightShadows.None;controller.fireLight.enabled=false;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);UnityEngine.Object.DestroyImmediate(root);return prefab;
    }
    static void Place(TorchFlame effect,MeshFilter filter)
    {
        // Some imported torches retain an overridden renderer bound. Locate the wick
        // from actual mesh vertices so the flame cannot end up halfway down the pole.
        var vertices=filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();
        float top=vertices.Max(v=>v.y),bottom=vertices.Min(v=>v.y),length=top-bottom;
        var head=vertices.Where(v=>v.y>=top-length*.08f).ToArray();
        var tip=new Vector3(head.Average(v=>v.x),top,head.Average(v=>v.z));
        // The old camp has a decorative tall torch model at the very same anchor
        // as each small stock torch. Its shaft otherwise completely hides the fire.
        var decorative=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(r=>r.gameObject.scene==filter.gameObject.scene&&r.name.Contains("Bound_Relic")&&
                Vector2.Distance(new Vector2(r.bounds.center.x,r.bounds.center.z),new Vector2(tip.x,tip.z))<3&&r.bounds.size.y>length*2)
            .OrderByDescending(r=>r.bounds.max.y).FirstOrDefault();
        if(decorative!=null)
        {tip=new Vector3(decorative.bounds.center.x,decorative.bounds.max.y,decorative.bounds.center.z);length=decorative.bounds.size.y;}
        float height=Mathf.Clamp(length*.36f,.8f,7);
        effect.transform.SetPositionAndRotation(tip-Vector3.up*height*.25f,Quaternion.identity);
        var scale=filter.transform.lossyScale;effect.transform.localScale=new Vector3(height/scale.x,height/scale.y,height/scale.z);
        effect.baseIntensity=Mathf.Clamp(height*height*12,25,600);
        effect.fireLight.intensity=effect.baseIntensity;
        effect.fireLight.range=Mathf.Clamp(length*1.5f,25,110);
        if(PrefabUtility.IsPartOfPrefabInstance(effect))
        {PrefabUtility.RecordPrefabInstancePropertyModifications(effect);PrefabUtility.RecordPrefabInstancePropertyModifications(effect.fireLight);}
    }
    public static void RepositionAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        foreach(var effect in UnityEngine.Object.FindObjectsByType<TorchFlame>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            Place(effect,effect.transform.parent.GetComponent<MeshFilter>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(effect.transform);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorApplication.Exit(0);
    }
    public static void ApplyAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        Directory.CreateDirectory(".utmp/TorchRepair");EditorSceneManager.SaveScene(scene,".utmp/TorchRepair/before.unity",true);
        var prefab=BuildPrefab();int added=0,existing=0;
        foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(filter.gameObject.scene!=scene||filter.sharedMesh==null||!AssetDatabase.GetAssetPath(filter.sharedMesh).EndsWith("/prop_torch_01.fbx"))continue;
            if(filter.transform.Find("BeachBoat_Torch_Flame")!=null){existing++;continue;}
            var renderer=filter.GetComponent<Renderer>();if(renderer==null)continue;
            var host=PrefabUtility.GetNearestPrefabInstanceRoot(filter.gameObject);
            if(host!=null&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(host).Contains("pf_torch"))
            {
                foreach(var ps in host.GetComponentsInChildren<ParticleSystem>(true))ps.gameObject.SetActive(false);
                foreach(var light in host.GetComponentsInChildren<Light>(true))light.enabled=false;
            }
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,filter.transform);go.name="BeachBoat_Torch_Flame";
            Place(go.GetComponent<TorchFlame>(),filter);
            added++;
        }
        if(added+existing==0)throw new InvalidOperationException("No torch meshes found in beachBoat");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("beachBoat torch flames: added="+added+", existing="+existing);
        EditorApplication.Exit(0);
    }
}
