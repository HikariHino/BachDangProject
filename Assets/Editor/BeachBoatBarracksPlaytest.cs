using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Checks full-size player passage and actual NPC walking/sleeping inside all eight rooms.</summary>
public static class BeachBoatBarracksPlaytest
{
    const string Flag = "BeachBoatBarracksPlaytest";
    static double started;
    static readonly HashSet<string> Entered = new HashSet<string>();
    static readonly HashSet<string> Slept = new HashSet<string>();
    static readonly HashSet<string> Left = new HashSet<string>();
    static readonly Dictionary<string, double> SleepStarted = new Dictionary<string, double>();
    static readonly HashSet<string> DormEntered = new HashSet<string>(), DormSlept = new HashSet<string>(), DormLeft = new HashSet<string>();
    static readonly HashSet<string> TentEntered = new HashSet<string>(), TentSlept = new HashSet<string>(), TentLeft = new HashSet<string>();
    const string PendingExit="BarracksPlaytestExit";
    static bool checkedDoors, captured;
    static int armSamples;
    static float largestArmAngle;
    static string failure;

    public static void RunAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        SessionState.SetBool(Flag, true);
        SessionState.SetInt(PendingExit,-1);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged-=OnMode;EditorApplication.playModeStateChanged+=OnMode;
    }
    static void OnMode(PlayModeStateChange state)
    {
        int code=SessionState.GetInt(PendingExit,-1);
        if(state!=PlayModeStateChange.EnteredEditMode||code<0)return;
        SessionState.SetInt(PendingExit,-1);EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
    }

    static void Capture(Transform house, NPCWander soldier)
    {
        var cameraObject = new GameObject("BarracksQA_Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.CopyFrom(BeachBoatQuestSession.Instance.storyCamera);
        camera.enabled = false; camera.fieldOfView = 65; camera.farClipPlane = 450;
        var rt = new RenderTexture(1280, 800, 24);
        var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        camera.targetTexture = rt;
        void Shot(string name, Vector3 from, Vector3 target)
        {
            camera.transform.position = house.TransformPoint(from);
            camera.transform.LookAt(house.TransformPoint(target));
            camera.Render(); RenderTexture.active = rt;
            pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); pixels.Apply();
            File.WriteAllBytes("Logs/" + name + ".png", pixels.EncodeToPNG());
        }
        soldier.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        soldier.animator.Update(.5f);
        Shot("Barracks_Open_Door", new Vector3(0, 28, -98), new Vector3(0, 25, 0));
        Shot("Barracks_Sleeping_Inside", new Vector3(5, 20, -18), new Vector3(-18, 8, 8));
        RenderTexture.active = null; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(pixels);
        UnityEngine.Object.DestroyImmediate(cameraObject);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
        if (started == 0) { started = EditorApplication.timeSinceStartup; Entered.Clear(); Slept.Clear(); Left.Clear(); SleepStarted.Clear(); DormEntered.Clear(); DormSlept.Clear(); DormLeft.Clear(); }
        double elapsed = EditorApplication.timeSinceStartup - started;
        var session = BeachBoatQuestSession.Instance;
        if (elapsed < 3 || session?.player == null) return;
        Directory.CreateDirectory("Logs");
        foreach (var opening in UnityEngine.Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            opening.SkipOpening();
        BeachBoatStory.StoryDialogueUI.Instance?.Close();
        var houses = GameObject.Find("TestQuest_Camp").transform.Find("Compact_Living_Camp/Supply_And_Work")
            .Cast<Transform>().Where(t => t.name.StartsWith("Wooden_Barracks_")).OrderBy(t => t.name).ToArray();
        try
        {
            foreach(var posture in UnityEngine.Object.FindObjectsByType<NPCUprightPosture>(FindObjectsSortMode.None))
            {
                var a=posture.GetComponent<Animator>();var w=posture.GetComponent<NPCWander>();var state=a.GetCurrentAnimatorStateInfo(0);
                if(!a.isActiveAndEnabled||(w!=null&&w.IsSleeping)||state.IsName("Laying")||state.IsName("Sitting")||a.IsInTransition(0))continue;
                foreach(var pair in new[]{(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm),(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm)})
                {
                    var axis=a.GetBoneTransform(pair.Item2).position-a.GetBoneTransform(pair.Item1).position;
                    float angle=Vector3.Angle(axis,-a.transform.up);largestArmAngle=Mathf.Max(largestArmAngle,angle);armSamples++;
                    if(angle>32)throw new InvalidOperationException("Arm folded out of its relaxed range: "+a.name+" angle="+angle);
                }
                var left=a.GetBoneTransform(HumanBodyBones.LeftUpperArm);var right=a.GetBoneTransform(HumanBodyBones.RightUpperArm);
                var shoulders=a.transform.InverseTransformDirection(right.position-left.position);
                if(Mathf.Abs(shoulders.y)>.012f)throw new InvalidOperationException("Shoulders not level: "+a.name+" offset="+shoulders.y);
                var torso=a.transform.InverseTransformDirection(a.GetBoneTransform(HumanBodyBones.Head).position-a.GetBoneTransform(HumanBodyBones.Hips).position);
                float lean=Mathf.Abs(Mathf.Atan2(torso.x,torso.y)*Mathf.Rad2Deg);
                if(lean>2)throw new InvalidOperationException("Torso leaning sideways: "+a.name+" angle="+lean+" culling="+a.cullingMode+" visible="+a.GetComponentsInChildren<Renderer>().Any(r=>r.isVisible));
            }
            if (!checkedDoors)
            {
                if (houses.Length != 8 || houses.Any(h => h.Find("SleepingPlace") == null))
                    throw new InvalidOperationException("Expected eight open sleeping rooms.");
                var capsule = session.player.GetComponent<CapsuleCollider>();
                if (capsule == null) throw new InvalidOperationException("Player capsule missing.");
                float radius = capsule.radius * Mathf.Max(capsule.transform.lossyScale.x, capsule.transform.lossyScale.z);
                float height = Mathf.Max(radius * 2, capsule.height * capsule.transform.lossyScale.y);
                foreach (var house in houses)
                {
                    var from = house.TransformPoint(new Vector3(0, .4f, -45));
                    var end = house.TransformPoint(new Vector3(0, .4f, 2));
                    var direction = end - from;
                    var hits = Physics.CapsuleCastAll(from + Vector3.up * radius,
                        from + Vector3.up * (height - radius), radius, direction.normalized,
                        direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    var blocker = hits.FirstOrDefault(h => h.transform.IsChildOf(house));
                    if (blocker.collider != null) throw new InvalidOperationException("Player blocked at " + house.name + "/" + blocker.collider.name);
                }
                for (int i = 1; i <= 8; i++)
                    GameObject.Find("Camp_Worker_" + i.ToString("00")).GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (string flank in new[] { "Tay", "Dong" })
                    for (int i = 1; i <= 2; i++)
                        GameObject.Find("Linh_Nghi_Ca_" + flank + "_" + i).GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
                checkedDoors = true;
            }
            for (int i = 0; i < houses.Length; i++)
            {
                var soldier = GameObject.Find("Camp_Worker_" + (i + 1).ToString("00")).GetComponent<NPCWander>();
                var local = houses[i].InverseTransformPoint(soldier.transform.position);
                bool inside = Mathf.Abs(local.x) < 27 && local.z > -33 && local.z < 34;
                if (inside) Entered.Add(houses[i].name);
                if (inside && soldier.IsSleeping)
                {
                    if (!SleepStarted.ContainsKey(soldier.name)) SleepStarted[soldier.name] = elapsed;
                    float headHeight = houses[i].InverseTransformPoint(soldier.animator.GetBoneTransform(HumanBodyBones.Head).position).y;
                    if (elapsed - SleepStarted[soldier.name] > 3 && headHeight < 14)
                    {
                        Slept.Add(houses[i].name);
                        if (!captured) { Capture(houses[i], soldier); captured = true; }
                    }
                }
                if (Slept.Contains(houses[i].name) && local.z < -38) Left.Add(houses[i].name);
            }
            foreach (string flank in new[] { "Tay", "Dong" })
            for (int i = 1; i <= 2; i++)
            {
                var dorm = GameObject.Find("Nha_Nghi_Linh_" + flank).transform;
                var soldier = GameObject.Find("Linh_Nghi_Ca_" + flank + "_" + i).GetComponent<NPCWander>();
                var local = dorm.InverseTransformPoint(soldier.transform.position);
                bool inside = Mathf.Abs(local.x) < 30 && Mathf.Abs(local.z) < 30;
                if (inside) DormEntered.Add(soldier.name);
                if (inside && soldier.IsSleeping)
                {
                    if (!SleepStarted.ContainsKey(soldier.name)) SleepStarted[soldier.name] = elapsed;
                    float headHeight = dorm.InverseTransformPoint(soldier.animator.GetBoneTransform(HumanBodyBones.Head).position).y;
                    if (elapsed - SleepStarted[soldier.name] > 3 && headHeight < 14) DormSlept.Add(soldier.name);
                }
                if (DormSlept.Contains(soldier.name) && Vector3.Distance(soldier.transform.position, soldier.waypoints[0].position) < 8)
                    DormLeft.Add(soldier.name);
            }
            for(int i=9;i<=12;i++)
            {
                var soldier=GameObject.Find("Camp_Worker_"+i.ToString("00")).GetComponent<NPCWander>();
                var tent=soldier.waypoints[0].parent.parent;var local=tent.InverseTransformPoint(soldier.transform.position);
                bool inside=Mathf.Abs(local.x)<16&&local.z<22&&local.z>-22;
                if(inside)TentEntered.Add(soldier.name);
                if(inside&&soldier.IsSleeping)TentSlept.Add(soldier.name);
                if(TentSlept.Contains(soldier.name)&&local.z>34)TentLeft.Add(soldier.name);
            }
        }
        catch (Exception error) { failure = error.ToString(); }
        bool complete = checkedDoors && Entered.Count == 8 && Slept.Count == 8 && Left.Count == 8 && captured &&
            DormEntered.Count == 4 && DormSlept.Count == 4 && DormLeft.Count == 4 &&
            TentEntered.Count==4&&TentSlept.Count==4&&TentLeft.Count==4;
        if (failure == null && !complete && elapsed < 120) return;
        if (failure == null && !complete) failure = "NPC route timed out before all eight soldiers entered, slept and left.";
        var actors = Enumerable.Range(1, 8).Select(i => GameObject.Find("Camp_Worker_" + i.ToString("00"))?.GetComponent<NPCWander>())
            .Where(w => w != null).Select(w => {
                var house = w.waypoints[0].parent;
                string Bone(HumanBodyBones bone) => house.InverseTransformPoint(w.animator.GetBoneTransform(bone).position).ToString("F2");
                return new { w.name, w.CurrentActivity, w.IsSleeping, w.WaypointIndex,
                    position = w.transform.position.ToString("F2"), head = Bone(HumanBodyBones.Head),
                    hips = Bone(HumanBodyBones.Hips), foot = Bone(HumanBodyBones.LeftFoot),
                    target = w.waypoints[w.WaypointIndex < 0 ? 0 : w.WaypointIndex].position.ToString("F2") };
            }).ToArray();
        var dormActors = new[] { "Tay", "Dong" }.SelectMany(flank => Enumerable.Range(1, 2)
            .Select(i => GameObject.Find("Linh_Nghi_Ca_" + flank + "_" + i).GetComponent<NPCWander>()))
            .Select(w => new { w.name, w.CurrentActivity, w.IsSleeping, w.WaypointIndex, position = w.transform.position.ToString("F2"),
                target = w.waypoints[w.WaypointIndex < 0 ? 0 : w.WaypointIndex].position.ToString("F2") }).ToArray();
        File.WriteAllText("Logs/BeachBoatBarracksPlaytest.json", JsonConvert.SerializeObject(new
        { success = failure == null, failure, checkedDoors, entered = Entered.Count, slept = Slept.Count, leftAfterSleep = Left.Count,
            dormEntered = DormEntered.Count, dormSlept = DormSlept.Count, dormLeft = DormLeft.Count,
            tentEntered=TentEntered.Count,tentSlept=TentSlept.Count,tentLeft=TentLeft.Count,armSamples,largestArmAngle,actors, dormActors,missingDormReturns=DormSlept.Except(DormLeft).ToArray() }, Formatting.Indented));
        SessionState.SetBool(Flag, false);SessionState.SetInt(PendingExit,failure==null?0:1);EditorApplication.isPlaying=false;
    }
}
