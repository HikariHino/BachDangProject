using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-time scene repair for the playable camp stations and duplicate world hints.</summary>
public static class BeachBoatMinigameRepair
{
    const string ScenePath = "Assets/Scenes/beachBoat.unity";

    [MenuItem("BeachBoat/Repair Minigames And Labels")]
    public static void Repair()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var drum = GameObject.Find("Minigame_TrongTran");
        var terrain = Terrain.activeTerrain;
        if (drum == null || terrain == null) throw new InvalidOperationException("Drum or terrain missing in beachBoat.");

        // The former position intersects a barracks. This yard is beside the command stage,
        // clear of the central walkway and the rows of tents.
        var position = new Vector3(-5440f, 0f, 1240f);
        position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 8f;
        drum.transform.position = position;
        Physics.SyncTransforms();
        var renderer = drum.GetComponent<Renderer>();
        if (renderer == null) throw new InvalidOperationException("Drum renderer missing.");
        drum.transform.position += Vector3.up *
            (terrain.SampleHeight(position) + terrain.transform.position.y + .15f - renderer.bounds.min.y);

        int cleaned = 0;
        foreach (var marker in scene.GetRootGameObjects().SelectMany(root =>
            root.GetComponentsInChildren<BeachBoatStory.StoryLandmarkMarker>(true)))
        {
            if (string.IsNullOrEmpty(marker.label) || !marker.label.Contains("• E")) continue;
            marker.label = marker.label.Replace(" • E", "").Replace("• E", "");
            EditorUtility.SetDirty(marker);
            cleaned++;
        }
        foreach (var label in scene.GetRootGameObjects().SelectMany(root =>
            root.GetComponentsInChildren<TMPro.TextMeshPro>(true)))
        {
            if (string.IsNullOrEmpty(label.text) || !label.text.Contains("• E")) continue;
            label.text = label.text.Replace(" • E", "").Replace("• E", "");
            EditorUtility.SetDirty(label);
            cleaned++;
        }
        EditorUtility.SetDirty(drum);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Minigame repair: drum at {drum.transform.position}, bottom {renderer.bounds.min.y:F1}; cleaned {cleaned} labels.");
    }

    public static void RepairAndExit()
    {
        Repair();
        EditorApplication.Exit(0);
    }

    public static void CaptureAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var drum = GameObject.Find("Minigame_TrongTran");
        var source = UnityEngine.Object.FindFirstObjectByType<BeachBoatStory.BeachBoatQuestSession>()?.storyCamera;
        if (drum == null || source == null) throw new InvalidOperationException("Drum or story camera missing.");
        Directory.CreateDirectory("Logs");
        var go = new GameObject("DrumCaptureCamera");
        var camera = go.AddComponent<Camera>();
        camera.CopyFrom(source);
        camera.enabled = false;
        camera.farClipPlane = 500f;
        var rt = new RenderTexture(1000, 650, 24);
        var image = new Texture2D(1000, 650, TextureFormat.RGB24, false);
        void Shot(string file, Vector3 position, Vector3 aim)
        {
            go.transform.position = position;
            go.transform.LookAt(aim);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1000, 650), 0, 0);
            image.Apply();
            File.WriteAllBytes("Logs/" + file, image.EncodeToPNG());
        }
        var point = drum.transform.position;
        Shot("BeachBoat_Drum_Ground.png", point + new Vector3(-35, 12, -35), point);
        Shot("BeachBoat_Drum_Overhead.png", point + Vector3.up * 120, point);
        RenderTexture.active = null;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(go);
        EditorApplication.Exit(0);
    }

    public static void RepairCaptureAndExit()
    {
        Repair();
        CaptureAndExit();
    }
}
