using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Story = BeachBoatStory;

/// <summary>Enlarge inhabited objects around their own foundations, without enlarging the map.</summary>
public static class BeachBoatModelFit
{
    const string Stamp = "BeachBoat_Models_Fitted_2x";
    static Terrain terrain;
    static readonly Dictionary<Transform, Vector3> plan = new Dictionary<Transform, Vector3>();
    static float Floor(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
    static Bounds Bounds(Transform t)
    {
        var rr = t.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
        if (rr.Length == 0) return new Bounds(t.position, Vector3.zero);
        var b = rr[0].bounds; foreach (var r in rr) b.Encapsulate(r.bounds); return b;
    }
    static void Add(Transform t, Vector3? pivot = null)
    {
        if (t == null || plan.ContainsKey(t)) return;
        var b = Bounds(t); if (b.size == Vector3.zero) return;
        plan[t] = pivot ?? new Vector3(t.position.x, b.min.y, t.position.z);
    }
    static bool GroundPatch(Transform t) => t.GetComponentsInChildren<Renderer>(true).Length == 0 || Bounds(t).size.y < 1.5f;
    static void Record(UnityEngine.Object o)
    {
        Undo.RecordObject(o, "Fit beachBoat models"); EditorUtility.SetDirty(o);
    }
    static void Override(UnityEngine.Object o)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
        EditorUtility.SetDirty(o);
    }
    static void Fields(Component c, params string[] names)
    {
        Record(c); var so = new SerializedObject(c);
        foreach (var n in names) { var p = so.FindProperty(n); if (p == null) throw new Exception("Missing field: " + n); p.floatValue *= 2; }
        so.ApplyModifiedProperties(); Override(c);
    }

    [MenuItem("Bach Dang/Fit Models 2x Without Resizing Map")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/beachBoat.unity" || GameObject.Find(Stamp) != null)
            throw new InvalidOperationException("Requires unfitted beachBoat in Edit Mode; this conversion runs once.");
        terrain = Terrain.activeTerrain; plan.Clear();
        var settlements = GameObject.Find("BachDang_Living_Settlements").transform;
        var camp = GameObject.Find("TestQuest_Camp").transform;
        var session = UnityEngine.Object.FindFirstObjectByType<Story.BeachBoatQuestSession>();
        var stage = camp.Cast<Transform>().Single(t => t.name.Contains("Wooden_Stage"));
        var stageBounds = Bounds(stage);
        var stagePivot = new Vector3(stageBounds.center.x, stageBounds.min.y, stageBounds.center.z);
        foreach (var name in new[] { "01_Lang_Cho_Ben_Song", "02_Xom_Chai_Bo_Dong", "03_Xom_Vuon_Bo_Tay", "06_Trai_Bo_Dong" })
            foreach (Transform t in settlements.Find(name)) if (!GroundPatch(t) && !t.name.StartsWith("Rao_")) Add(t);
        foreach (Transform group in settlements.Find("09_Sinh_Hoat"))
            foreach (Transform t in group) if (!GroundPatch(t)) Add(t);
        // Courtyard props and small fences use the house foundation, so awnings remain attached.
        foreach (Transform group in settlements.Find("11_San_Nha_Va_Cho"))
            foreach (Transform t in group)
            {
                var house = t.name.StartsWith("San_Nha_") ? settlements.Find(group.name + "/" + t.name.Substring(4)) : null;
                Add(t, house != null && plan.ContainsKey(house) ? plan[house] : (Vector3?)null);
            }
        foreach (Transform group in settlements.Find("10_Lang_Tu_Nhien"))
            foreach (Transform t in group)
                if (t.name.StartsWith("Rao_San_Nha_"))
                {
                    var baseName = group.name.Replace("_Canh_Quan", "");
                    var house = settlements.Find(baseName + "/" + t.name.Substring(8));
                    Add(t, house != null && plan.ContainsKey(house) ? plan[house] : (Vector3?)null);
                }
        foreach (Transform t in settlements.Find("08_Ben_Ca_Dan_Sinh"))
            if (!GroundPatch(t) && !t.name.Contains("Cau_Go") && !t.name.Contains("Cau_Cang") && !t.name.Contains("Ben_Go")) Add(t);
        foreach (Transform t in camp)
            if (!t.name.Contains("HANG RAO") && !t.name.StartsWith("Island_")) Add(t, t == stage ? stagePivot : (Vector3?)null);
        foreach (Transform group in camp.Find("Island_Camp_Life"))
            if (group.childCount > 0) foreach (Transform t in group) if (!GroundPatch(t)) Add(t);
        foreach (var boat in UnityEngine.Object.FindObjectsByType<TidalBoatFloat>())
            Add(boat.transform, new Vector3(boat.transform.position.x, boat.tideSystem.transform.position.y, boat.transform.position.z));
        foreach (var boat in UnityEngine.Object.FindObjectsByType<BoatCrash>())
            Add(boat.transform, new Vector3(boat.transform.position.x, GameObject.Find("Plane").transform.position.y, boat.transform.position.z));
        foreach (var actor in session.GetComponentsInChildren<Animator>(true))
        {
            bool onStage = actor.name == "NgoQuyen_QuestGiver" || actor.name.StartsWith("Camp_Guard_Back_") || actor.name == "Camp_Guard_Front_Left";
            Add(actor.transform, onStage ? stagePivot : new Vector3(actor.transform.position.x, Floor(actor.transform.position), actor.transform.position.z));
        }
        foreach (var wood in session.woodItems) Add(wood.transform);
        foreach (var gate in session.GetComponentsInChildren<Story.CampGateTransition>()) Add(gate.transform.Find("Visible_Story_Gate"));
        // A parent group already scales its entire prefab, including LODs, crew and colliders.
        foreach (var t in plan.Keys.ToArray()) if (plan.Keys.Any(p => p != t && t.IsChildOf(p))) plan.Remove(t);
        Directory.CreateDirectory(".utmp/ModelFit");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/ModelFit/beachBoat-before-fit.unity", true)) throw new Exception("Backup failed");
        var before = plan.Select(p => new { path = AnimationUtility.CalculateTransformPath(p.Key, null), position = p.Key.position.ToString(), scale = p.Key.lossyScale.ToString(), height = Bounds(p.Key).size.y }).ToArray();
        File.WriteAllText(".utmp/ModelFit/before.json", Newtonsoft.Json.JsonConvert.SerializeObject(before, Newtonsoft.Json.Formatting.Indented));
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
        try
        {
            foreach (var pair in plan)
            {
                var t = pair.Key; Record(t);
                t.position = pair.Value + (t.position - pair.Value) * 2;
                t.localScale *= 2; Override(t);
                // Preserve previous world-space LOD switching distances despite larger render bounds.
                foreach (var lod in t.GetComponentsInChildren<LODGroup>(true))
                {
                    Record(lod); var levels = lod.GetLODs();
                    for (int i=0;i<levels.Length;i++) levels[i].screenRelativeTransitionHeight = Mathf.Min(.99f, levels[i].screenRelativeTransitionHeight * 2);
                    lod.SetLODs(levels); Override(lod);
                }
                foreach (var light in t.GetComponentsInChildren<Light>(true)) { Record(light); light.range *= 2; Override(light); }
                foreach (var particle in t.GetComponentsInChildren<ParticleSystem>(true)) { Record(particle); var main=particle.main; main.scalingMode=ParticleSystemScalingMode.Hierarchy; Override(particle); }
            }
            // Keep the island perimeter footprint; increase only fence height from the ground.
            var pier=settlements.Find("08_Ben_Ca_Dan_Sinh/Cau_Go_Dan_Sinh");
            if(pier!=null)
            {
                Record(pier); var p=pier.position;p.z=525+(p.z-525)*2;pier.position=p;
                var scale=pier.localScale;scale.z*=2;pier.localScale=scale;Override(pier);
            }
            foreach (var root in new[] { camp.Cast<Transform>().Single(t=>t.name.Contains("HANG RAO")), camp.Find("Island_Fence_Infill"), camp.Find("Island_Fence_Collisions") })
                foreach (Transform t in root)
                {
                    Record(t); var p=t.position; float floor=Floor(p); p.y=floor+(p.y-floor)*2; t.position=p;
                    var scale=t.localScale; scale.y*=2; t.localScale=scale; Override(t);
                }
            foreach (var g in session.GetComponentsInChildren<Story.CampGateTransition>()) Fields(g, "interactDistance");
            foreach (var n in session.GetComponentsInChildren<Story.NPCDialogue>()) Fields(n, "interactDistance");
            Fields(session.GetComponentInChildren<Story.NPCQuestGiver>(), "interactDistance");
            foreach (var w in session.woodItems) Fields(w, "interactDistance");
            Fields(session.GetComponentInChildren<Story.RiverStakeZone>(), "interactDistance");
            var motor=session.player.GetComponent<Story.Character_Movement>();
            Fields(motor,"walkSpeed","runSpeed","acceleration","braking","jumpHeight");
            Fields(session.storyCamera.GetComponent<Story.Camera_Script>(),"distance","heightOffset","rightOffset","collisionRadius","aimDistance");
            foreach (var boat in UnityEngine.Object.FindObjectsByType<TidalBoatFloat>()) { Record(boat); boat.waterlineOffset*=2; boat.bobAmplitude*=2; Override(boat); }
            foreach (var boat in UnityEngine.Object.FindObjectsByType<BoatCrash>()) Fields(boat,"waterFloatOffset","forwardCheckDistance");
            foreach (var name in new[]{"CampArrival","VillageArrival","RiverArrival"})
            { var t=session.transform.Find(name); Record(t); var p=t.position;p.y=Floor(p)+(p.y-Floor(p))*2;t.position=p;Override(t); }
            foreach (var marker in session.GetComponentsInChildren<Story.StoryLandmarkMarker>(true)) Fields(marker,"worldDisplayScale");
            var metadata=UnityEngine.Object.FindFirstObjectByType<BachDangWorldScale>();
            Record(metadata);metadata.fittedCharacterHeight*=2;Override(metadata);
            var stamp=new GameObject(Stamp); Undo.RegisterCreatedObjectUndo(stamp,"Record model fit");
            Physics.SyncTransforms();
            File.WriteAllText(".utmp/ModelFit/after.json",Newtonsoft.Json.JsonConvert.SerializeObject(plan.Keys.Select(t=>new{path=AnimationUtility.CalculateTransformPath(t,null),position=t.position.ToString(),scale=t.lossyScale.ToString(),height=Bounds(t).size.y}),Newtonsoft.Json.Formatting.Indented));
            var sky=UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();if(sky!=null)RenderSettings.skybox=sky.skyboxMaterial;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); Undo.CollapseUndoOperations(undo);
            Debug.Log("Fitted " + plan.Count + " model groups; terrain dimensions and shared assets unchanged.");
        }
        catch { Undo.RevertAllDownToGroup(undo); throw; }
    }
}
