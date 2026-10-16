using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using Story = BeachBoatStory;

public static class BeachBoatStakeQuestInstaller
{
    const string Folder = "Assets/BeachBoatStory/StakeQuest";
    const string MeshPath = "Assets/Models/ModelAI/CayGo/CayGo/CayGo.fbx";
    const string MaterialPath = "Assets/Models/ModelAI/CayGo/CayGo/CayGo.mat";

    [MenuItem("Bach Dang/Install Designated Stake Quest")]
    public static void Install()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/beachBoat.unity")
            throw new InvalidOperationException("Open beachBoat in Edit Mode.");
        var session = UnityEngine.Object.FindFirstObjectByType<Story.BeachBoatQuestSession>();
        var zone = session.GetComponentInChildren<Story.RiverStakeZone>(true);
        if (zone.placements != null && zone.placements.Length > 0)
            throw new InvalidOperationException("Stake positions already installed; edit existing positions instead.");
        var mesh = AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().First();
        var originalMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (session.woodItems.Length < 5 || session.woodItems.Any(w => w == null) || originalMaterial == null)
            throw new InvalidOperationException("Incomplete source bindings.");
        System.IO.Directory.CreateDirectory(".utmp/StakeQuest");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/StakeQuest/beachBoat-before-stakes.unity", true))
            throw new InvalidOperationException("Could not back up beachBoat.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/BeachBoatStory", "StakeQuest");
        var material = new Material(originalMaterial) { name = "Thai_QuestStake", enableInstancing = true };
        AssetDatabase.CreateAsset(material, Folder + "/Thai_QuestStake.mat");
        var ringMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Stake_Position_Gold" };
        ringMaterial.SetColor("_BaseColor", new Color(1f, .71f, .13f));
        AssetDatabase.CreateAsset(ringMaterial, Folder + "/Stake_Position_Gold.mat");
        var terrain = Terrain.activeTerrains.Single(t => t.gameObject.scene == scene);
        Func<float, float, Vector3> ground = (x,z) => new Vector3(x, terrain.SampleHeight(new Vector3(x,0,z)) + terrain.transform.position.y, z);
        // Exact Thai mesh/material textures, shared by all twelve visuals. No mesh colliders.
        for (int i = 0; i < session.woodItems.Length; i++)
        {
            var old = session.woodItems[i];
            var root = new GameObject("Coc_NhiemVu_" + (i+1));
            Undo.RegisterCreatedObjectUndo(root, "Use Thai stake pickup");
            root.transform.SetParent(old.transform.parent, false);
            root.transform.position = ground(old.transform.position.x, old.transform.position.z);
            var visual = Visual(root.transform, mesh, material, Quaternion.Euler(0,i*37,0), 32f);
            visual.transform.position += Vector3.up * (root.transform.position.y + .2f - visual.GetComponent<Renderer>().bounds.min.y);
            var pickup = root.AddComponent<Story.WoodCollectible>(); pickup.interactDistance = old.interactDistance;
            var marker = root.AddComponent<Story.StoryLandmarkMarker>();
            marker.target = Story.StoryLandmarkMarker.Target.Wood; marker.label = "CỌC GỖ • E"; marker.worldDisplayScale = 2;
            session.woodItems[i] = pickup;
            Undo.DestroyObjectImmediate(old.gameObject);
        }
        var existingMarker = zone.GetComponent<Story.StoryLandmarkMarker>();
        if (existingMarker != null) Undo.DestroyObjectImmediate(existingMarker);
        Vector2[] positions = {new Vector2(-3780,1940),new Vector2(-3780,2020),new Vector2(-3780,2100),new Vector2(-3820,2180),new Vector2(-3860,2260)};
        Undo.RecordObject(zone, "Add designated stake positions");
        zone.interactDistance = 24; zone.plantingDuration = 1.4f;
        zone.placements = new Story.RiverStakeZone.Placement[positions.Length];
        for (int i=0;i<positions.Length;i++)
        {
            var p = ground(positions[i].x,positions[i].y);
            var anchor = new GameObject("ViTri_DongCoc_"+(i+1));
            Undo.RegisterCreatedObjectUndo(anchor, "Add stake work point");
            anchor.transform.SetParent(zone.transform, false); anchor.transform.position = p + Vector3.up*.3f;
            var marker = new GameObject("DanhDau_"+(i+1)); marker.transform.SetParent(anchor.transform,false);
            var ring = marker.AddComponent<LineRenderer>(); ring.sharedMaterial=ringMaterial;
            ring.useWorldSpace=false; ring.loop=true; ring.widthMultiplier=.5f; ring.positionCount=40;
            ring.shadowCastingMode=ShadowCastingMode.Off; ring.receiveShadows=false;
            for(int n=0;n<40;n++)
            {
                float angle=n*Mathf.PI*2/40; var v=new Vector3(Mathf.Cos(angle)*10,0,Mathf.Sin(angle)*10);
                var at=ground(p.x+v.x,p.z+v.z); ring.SetPosition(n,new Vector3(v.x,at.y-p.y+.2f,v.z));
            }
            var label=new GameObject("NhanViTri").AddComponent<TextMeshPro>();label.transform.SetParent(marker.transform,false);
            label.transform.localPosition=Vector3.up*30;label.rectTransform.sizeDelta=new Vector2(120,20);
            Story.StoryFont.Apply(label);label.text="ĐÓNG CỌC "+(i+1)+" • E";label.fontSize=34;
            label.alignment=TextAlignmentOptions.Center;label.textWrappingMode=TextWrappingModes.NoWrap;
            label.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            var visual=Visual(anchor.transform,mesh,material,Quaternion.FromToRotation(Vector3.right,new Vector3(.12f,1,0)),32);
            visual.name="Coc_DaDong_"+(i+1);
            var location=ground(p.x+30,p.z);
            var bounds=visual.GetComponent<Renderer>().bounds;
            visual.transform.position += new Vector3(location.x-bounds.center.x,location.y-8-bounds.min.y,location.z-bounds.center.z);
            visual.SetActive(false);
            zone.placements[i]=new Story.RiverStakeZone.Placement{approach=anchor.transform,marker=marker,stake=visual.transform};
        }
        // The old river arrival was behind the island perimeter fence.
        var arrival = GameObject.Find("RiverArrival").transform;
        Undo.RecordObject(arrival, "Move river arrival outside camp fence");
        arrival.SetPositionAndRotation(ground(-3780,1880) + Vector3.up*2.3f, Quaternion.identity);
        var returnGate = GameObject.Find("RiverToCamp").transform;
        Undo.RecordObject(returnGate, "Move return gate to accessible river bank");
        returnGate.SetPositionAndRotation(ground(-3800,2060) + Vector3.up*.15f, Quaternion.Euler(0,90,0));
        var gateVisual = returnGate.Find("Visible_Story_Gate");
        Undo.RecordObject(gateVisual, "Align river gate frame");
        var gateRenderers = gateVisual.GetComponentsInChildren<Renderer>(true);
        var gateBounds = gateRenderers[0].bounds;
        foreach (var r in gateRenderers.Skip(1)) gateBounds.Encapsulate(r.bounds);
        var gatePosition = returnGate.position;
        gateVisual.position += new Vector3(gatePosition.x-gateBounds.center.x,gatePosition.y-gateBounds.min.y,gatePosition.z-gateBounds.center.z);
        EditorUtility.SetDirty(session);EditorUtility.SetDirty(zone);
        RenderSettings.skybox=UnityEngine.Object.FindFirstObjectByType<DayNightCycle>().skyboxMaterial;
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save scene.");
        Debug.Log("Installed Thai stake pickups and five designated planting positions.");
    }

    static GameObject Visual(Transform parent,Mesh mesh,Material material,Quaternion rotation,float length)
    {
        var go=new GameObject("Coc_Thai_Visual");go.transform.SetParent(parent,false);go.transform.localRotation=rotation;
        float longest=Mathf.Max(mesh.bounds.size.x,Mathf.Max(mesh.bounds.size.y,mesh.bounds.size.z));
        go.transform.localScale=Vector3.one*(length/longest);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
        var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.13f,new Renderer[]{renderer})});lod.RecalculateBounds();
        return go;
    }
}
