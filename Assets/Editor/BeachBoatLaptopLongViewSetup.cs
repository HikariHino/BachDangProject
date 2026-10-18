using System.IO;
using System.Linq;
using BeachBoatStory;
using BeachBoatStoryEditor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BeachBoatLaptopLongViewSetup
{
    const string RootName = "BeachBoat_LaptopLongView";

    [MenuItem("BeachBoat/Apply Laptop Long View")]
    public static void Apply()
    {
        var scene = BeachBoatToolSafety.Begin("LaptopLongView");
        var old = GameObject.Find(RootName); if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, "Create laptop long-view profile");
        var profile = Undo.AddComponent<LaptopLongViewProfile>(root);
        profile.cameraDistance = 9500; profile.treeDistance = 1400; profile.treeBillboardDistance = 280;
        profile.maximumFullLodTrees = 500; profile.textureStreamingBudgetMB = 384;

        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera.gameObject.scene != scene || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) continue;
            Undo.RecordObject(camera, "Extend terrain view"); camera.farClipPlane = Mathf.Max(camera.farClipPlane, 9500); EditorUtility.SetDirty(camera);
        }
        profile.Apply();
        var terrain = Terrain.activeTerrain;
        if (terrain != null) EditorUtility.SetDirty(terrain);
        BeachBoatToolSafety.Save(scene);

        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/LaptopLongViewResult.json", JsonConvert.SerializeObject(new {
            success = terrain != null && terrain.treeDistance <= 1400 && terrain.treeMaximumFullLODCount <= 500,
            cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c.gameObject.scene == scene).Select(c => new { c.name, c.farClipPlane }).ToArray(),
            terrainTrees = terrain != null ? terrain.terrainData.treeInstanceCount : 0,
            terrainTreeDistance = terrain != null ? terrain.treeDistance : 0,
            terrainBillboardDistance = terrain != null ? terrain.treeBillboardDistance : 0,
            fullLodTrees = terrain != null ? terrain.treeMaximumFullLODCount : 0,
            textureStreamingBudgetMB = QualitySettings.streamingMipmapsMemoryBudget,
            textureMipLimit = QualitySettings.globalTextureMipmapLimit,
            streamedZones = Object.FindObjectsByType<MapZoneStreamer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Sum(s => s.zones.Length)
        }, Formatting.Indented));
    }
}
