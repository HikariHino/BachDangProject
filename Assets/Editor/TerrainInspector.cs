using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class TerrainInspector
{
    public static void InspectTerrainAndAtmosphere()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        Terrain t = Terrain.activeTerrain;
        if (t == null)
        {
            var all = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            if (all.Length > 0) t = all[0];
        }

        if (t != null)
        {
            var td = t.terrainData;
            Debug.Log($"[TerrainInspector] Terrain found: {t.name}, size: {td.size}");
            Debug.Log($"[TerrainInspector] Tree prototypes: {td.treePrototypes.Length}, Tree instances: {td.treeInstanceCount}");
            Debug.Log($"[TerrainInspector] Detail prototypes: {td.detailPrototypes.Length}, Detail res: {td.detailResolution}");
            Debug.Log($"[TerrainInspector] Tree distance: {t.treeDistance}, Detail distance: {t.detailObjectDistance}");
            Debug.Log($"[TerrainInspector] Terrain Layers: {td.terrainLayers.Length}");
            for (int i = 0; i < td.terrainLayers.Length; i++)
            {
                var l = td.terrainLayers[i];
                Debug.Log($"  Layer {i}: {(l != null ? l.name : "null")}");
            }
        }
        else
        {
            Debug.LogError("[TerrainInspector] No active terrain found!");
        }

        Debug.Log($"[Atmosphere] RenderSettings.fog: {RenderSettings.fog}, mode: {RenderSettings.fogMode}, density: {RenderSettings.fogDensity}, start: {RenderSettings.fogStartDistance}, end: {RenderSettings.fogEndDistance}, color: {RenderSettings.fogColor}");
        Debug.Log($"[Atmosphere] RenderSettings.skybox: {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "null")}");
        Debug.Log($"[Atmosphere] RenderSettings.ambientLight: {RenderSettings.ambientLight}, intensity: {RenderSettings.ambientIntensity}");
        
        Light sun = RenderSettings.sun;
        if (sun == null) sun = Object.FindAnyObjectByType<Light>();
        if (sun != null)
        {
            Debug.Log($"[Atmosphere] Sun light: {sun.name}, color: {sun.color}, intensity: {sun.intensity}, shadows: {sun.shadows}, rot: {sun.transform.eulerAngles}");
        }
    }
}
