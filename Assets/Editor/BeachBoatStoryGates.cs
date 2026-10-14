using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Visible shared-mesh landmarks at the existing beachBoat transition points.</summary>
public static class BeachBoatStoryGates
{
    [MenuItem("Bach Dang/Mark Story Gates")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/beachBoat.unity")
            throw new InvalidOperationException("Open beachBoat in Edit Mode first.");
        var session = UnityEngine.Object.FindFirstObjectByType<BeachBoatStory.BeachBoatQuestSession>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/pf_build_gate_01.prefab");
        if (session == null || prefab == null) throw new InvalidOperationException("Missing story bindings or gate prefab.");
        Undo.SetCurrentGroupName("Mark beachBoat story gates");
        foreach (string name in new[] { "CampToVillage", "VillageToCamp", "RiverToCamp" })
        {
            var anchor = session.transform.Find(name);
            if (anchor == null) throw new InvalidOperationException("Missing " + name);
            if (anchor.Find("Visible_Story_Gate") != null) continue;
            var gate = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(gate, "Create visible story gate");
            gate.name = "Visible_Story_Gate";
            gate.transform.SetParent(anchor, false);
            gate.transform.localScale = Vector3.one * 7;
            var facing = name == "CampToVillage" ? session.playerSpawn.position - anchor.position : Vector3.forward;
            gate.transform.rotation = Quaternion.Euler(0, 90 + Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg, 0);
            var renderers = gate.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            // Prefab origin is near its middle, not the base. Seat its feet on terrain.
            float floor = Terrain.activeTerrain.SampleHeight(anchor.position) + Terrain.activeTerrain.transform.position.y;
            gate.transform.position += Vector3.up * (floor - bounds.min.y);
            foreach (var r in renderers)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
            var lod = gate.GetComponent<LODGroup>();
            if (lod == null) lod = gate.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(.018f, renderers) }); lod.RecalculateBounds();
            PrefabUtility.RecordPrefabInstancePropertyModifications(gate.transform);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
