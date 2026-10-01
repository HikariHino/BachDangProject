using UnityEngine;
using UnityEditor;

public static class ModelBoundsTester
{
    public static void MeasureModels()
    {
        string[] paths = new string[]
        {
            "Assets/Model_NPC/NgoQuyen/NgoQuyenModel/Meshy_AI_Ngo_Quyen_938_AD_biped_Character_output.fbx",
            "Assets/Main_Character/Meshy_AI_Steppe_Ironclad_biped/Meshy_AI_Steppe_Ironclad_biped/Meshy_AI_Steppe_Ironclad_biped_Character_output.fbx",
            "Assets/Model_NPC/DanThuongLonTuoi/Meshy_AI_Wandering_Peasant_biped_Character_output.fbx",
            "Assets/Model_NPC/MalePea/MalePea/Meshy_AI_Blue_Scrub_Figure_biped_Character_output.fbx",
            "Assets/Model_NPC/YoungPea/Young_Pea/Meshy_AI_Silent_Young_Disciple_biped_Character_output.fbx",
            "Assets/Prefabs/Props/pf_torch_fire_01.prefab",
            "Assets/Prefabs/Buildings/pf_build_boat_01.prefab"
        };

        foreach (var p in paths)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go == null)
            {
                Debug.LogError($"[ModelBounds] Missing: {p}");
                continue;
            }

            var rends = go.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds();
            bool hasBounds = false;
            foreach (var r in rends)
            {
                if (!hasBounds) { b = r.bounds; hasBounds = true; }
                else { b.Encapsulate(r.bounds); }
            }
            Debug.Log($"[ModelBounds] {go.name} -> Size: ({b.size.x:F3}, {b.size.y:F3}, {b.size.z:F3}), Extents: ({b.extents.x:F3}, {b.extents.y:F3}, {b.extents.z:F3})");
        }
    }
}
