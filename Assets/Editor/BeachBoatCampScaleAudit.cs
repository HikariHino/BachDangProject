using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BeachBoatCampScaleAudit
{
    const string ScenePath = "Assets/Scenes/beachBoat.unity";
    const string PlayFlag = "BeachBoatCampScaleRuntimeAudit";
    static double playStarted;
    static bool forcedSleep;

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    public static void AuditAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camp = GameObject.Find("TestQuest_Camp")?.transform.Find("Compact_Living_Camp");
        var session = UnityEngine.Object.FindAnyObjectByType<BeachBoatQuestSession>();
        if (camp == null || session == null) throw new InvalidOperationException("Camp or player missing.");
        var names = new[] { "Camp_Worker_01", "Camp_Worker_02", "Linh_Nghi_Ca_Tay_1", "Linh_Nghi_Ca_Dong_1",
            "Barracks_Tent_01", "Wooden_Barracks_-1_-170", "Nha_Nghi_Linh_Tay", "Nha_Nghi_Linh_Dong", "Central_Supply_Store",
            "Lo_Ren_Quan_Do", "Kho_Khi_Gioi_Nho" };
        var objects = names.Select(name =>
        {
            var go = GameObject.Find(name);
            var bounds = go == null ? new Bounds() : BoundsOf(go);
            var animator = go == null ? null : go.GetComponent<Animator>();
            var skin = go == null ? null : go.GetComponentInChildren<SkinnedMeshRenderer>();
            return new { name, found = go != null, parent = go == null ? "" : go.transform.parent.name,
                worldPosition = go == null ? "" : go.transform.position.ToString("F2"),
                localScale = go == null ? "" : go.transform.localScale.ToString("F2"),
                lossyScale = go == null ? "" : go.transform.lossyScale.ToString("F2"),
                bounds = bounds.size.ToString("F2"), min = bounds.min.ToString("F2"), max = bounds.max.ToString("F2"),
                controller = animator == null ? "" : animator.runtimeAnimatorController?.name,
                avatarValid = animator != null && animator.avatar != null && animator.avatar.isValid,
                humanScale = animator == null ? 0 : animator.humanScale,
                skinName = skin == null ? "" : skin.name,
                mesh = skin == null ? "" : skin.sharedMesh?.name,
                materialNames = skin == null ? Array.Empty<string>() : skin.sharedMaterials.Select(m => m?.name).ToArray(),
                renderers = go == null ? Array.Empty<string>() : go.GetComponentsInChildren<Renderer>(true)
                    .Select(r => r.name + ": " + string.Join(",", r.sharedMaterials.Select(m => m?.name))).ToArray() };
        }).ToArray();
        var player = session.player;
        var capsule = player.GetComponent<CapsuleCollider>();
        var report = new { player = new { player.name, scale = player.transform.lossyScale.ToString("F2"),
            bounds = BoundsOf(player).size.ToString("F2"), capsuleHeight = capsule == null ? 0 : capsule.height * player.transform.lossyScale.y,
            animator = player.GetComponentInChildren<Animator>()?.runtimeAnimatorController?.name },
            campScale = camp.lossyScale.ToString("F2"), objects,
            tents = camp.Find("Barracks_Rows").Cast<Transform>().Select(t => new { t.name,
                position = t.position.ToString("F1"), bounds = BoundsOf(t.gameObject).size.ToString("F1") }).ToArray(),
            fenceBounds = BoundsOf(camp.Find("Camp_Palisade_Reused").gameObject).size.ToString("F2") };
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BeachBoatCampScaleAudit.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        EditorApplication.Exit(0);
    }

    public static void PlayCaptureAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(PlayFlag, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void RegisterPlayProbe() { EditorApplication.update -= PlayProbe; EditorApplication.update += PlayProbe; }

    static void PlayProbe()
    {
        if (!SessionState.GetBool(PlayFlag, false) || !EditorApplication.isPlaying) return;
        if (playStarted == 0) playStarted = EditorApplication.timeSinceStartup;
        double elapsed = EditorApplication.timeSinceStartup - playStarted;
        if (elapsed < 8) return;
        if (!forcedSleep)
        {
            var sleeper = GameObject.Find("Linh_Nghi_Ca_Tay_1")?.GetComponent<NPCWander>();
            if (sleeper == null || sleeper.waypoints.Length < 2) throw new InvalidOperationException("Sleeper route missing.");
            sleeper.transform.position = sleeper.waypoints[1].position;
            typeof(NPCWander).GetField("targetIndex", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(sleeper, 1);
            typeof(NPCWander).GetMethod("Idle", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sleeper, null);
            forcedSleep = true;
            return;
        }
        if (elapsed < 11) return;
        try
        {
            var source = UnityEngine.Object.FindFirstObjectByType<BeachBoatQuestSession>()?.storyCamera;
            if (source == null) throw new InvalidOperationException("Story camera missing.");
            var cameraObject = new GameObject("Camp_Runtime_Audit_Camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.CopyFrom(source); camera.enabled = false;
            camera.fieldOfView = 62; camera.farClipPlane = 550;
            var rt = new RenderTexture(1200, 750, 24); var pixels = new Texture2D(1200, 750, TextureFormat.RGB24, false);
            camera.targetTexture = rt; Directory.CreateDirectory("Logs");
            void Shot(string name, Vector3 from, Vector3 to)
            {
                cameraObject.transform.position = from; cameraObject.transform.LookAt(to);
                camera.Render(); RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, 1200, 750), 0, 0); pixels.Apply();
                File.WriteAllBytes("Logs/" + name + ".png", pixels.EncodeToPNG());
            }
            var actors = new[] { "Camp_Worker_01", "Linh_Nghi_Ca_Tay_1", "Linh_Nghi_Ca_Dong_1",
                "Camp_Laying_Soldier", "Camp_Sitting_Soldier" }
                .Select(GameObject.Find).Where(x => x != null).ToArray();
            foreach (var actor in actors)
            {
                var p = actor.transform.position;
                var from = actor.name == "Linh_Nghi_Ca_Tay_1" ? p + new Vector3(0, 14, -14) : p + new Vector3(22, 17, -65);
                Shot("CampRuntime_" + actor.name, from, p + Vector3.up * 10);
            }
            var stage = GameObject.Find("TestQuest_Camp").transform.Cast<Transform>().First(t => t.name.Contains("Rustic_Wooden_Stage"));
            Shot("CampRuntime_Overview", stage.position + new Vector3(0, 400, -460), stage.position + Vector3.up * 5);
            var allHumans = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)
                .Where(a => a.isHuman && Vector3.Distance(a.transform.position, stage.position) < 500)
                .Select(a => new { a.name, controller = a.runtimeAnimatorController?.name,
                    scale = a.transform.lossyScale.ToString("F1"), bounds = BoundsOf(a.gameObject).size.ToString("F1"),
                    skin = a.GetComponentInChildren<SkinnedMeshRenderer>()?.sharedMesh?.name }).ToArray();
            var report = actors.Select(a =>
            {
                var animator = a.GetComponent<Animator>(); var bounds = BoundsOf(a);
                var terrain = Terrain.activeTerrain;
                float ground = terrain == null ? 0 : terrain.SampleHeight(a.transform.position) + terrain.transform.position.y;
                return new { a.name, position = a.transform.position.ToString("F2"), bounds = bounds.size.ToString("F2"),
                    bottom = bounds.min.y, ground, animatorState = animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    controller = animator.runtimeAnimatorController?.name,
                    parameters = animator.parameters.Select(p => p.name).ToArray(),
                    mesh = a.GetComponentInChildren<SkinnedMeshRenderer>()?.sharedMesh?.name,
                    activity = a.GetComponent<NPCWander>()?.CurrentActivity };
            }).ToArray();
            File.WriteAllText("Logs/BeachBoatCampScaleRuntimeAudit.json", JsonConvert.SerializeObject(new { report, allHumans }, Formatting.Indented));
            RenderTexture.active = null; camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(cameraObject);
            SessionState.SetBool(PlayFlag, false); EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            SessionState.SetBool(PlayFlag, false);
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/BeachBoatCampScaleRuntimeError.txt", error.ToString());
            EditorApplication.Exit(1);
        }
    }
}
