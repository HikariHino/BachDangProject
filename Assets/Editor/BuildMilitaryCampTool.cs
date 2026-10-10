using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class BuildMilitaryCampTool : EditorWindow
{
    [MenuItem("BachDang/Xay Doanh Trai (Thong Minh)")]
    public static void BuildCamp()
    {
        Vector3 islandCenter = new Vector3(-950f, 17.5f, 350f);
        
        int deletedCount = 0;
        MeshRenderer[] allMeshes = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (MeshRenderer mr in allMeshes)
        {
            if (mr == null || mr.gameObject == null) continue;
            string n = mr.gameObject.name.ToLower();
            
            bool isTree = n.Contains("conifer") || n.Contains("tree") || n.Contains("bush") || 
                          n.Contains("rock") || n.Contains("grass") || n.Contains("oak") || 
                          n.Contains("pine") || n.Contains("wood") || n.Contains("plant") || 
                          n.Contains("stump") || n.Contains("log");
                          
            if (isTree)
            {
                float dist = Vector3.Distance(new Vector3(mr.transform.position.x, 0, mr.transform.position.z), new Vector3(islandCenter.x, 0, islandCenter.z));
                if (dist <= 1200f) 
                {
                    Undo.DestroyObjectImmediate(mr.gameObject);
                    deletedCount++;
                }
            }
        }

        GameObject oldRoot = GameObject.Find("--- HANG RAO DOANH TRAI ---");
        if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot);

        GameObject root = new GameObject("--- HANG RAO DOANH TRAI ---");
        root.transform.position = islandCenter;

        GameObject towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/pf_build_tower_01.prefab");
        GameObject fencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/pf_build_wall_panel_01.prefab"); 

        if (towerPrefab == null || fencePrefab == null) return;

        float targetScale = 3f; 
        float fenceLength = 5f * targetScale * 0.98f; 

        List<Vector3> coastPoints = new List<Vector3>();
        int numSegments = 40; 
        
        for (int i = 0; i < numSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / numSegments;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            
            Vector3 edgePoint = islandCenter;
            for (float d = 10f; d < 1200f; d += 10f)
            {
                Vector3 testPos = islandCenter + dir * d;
                float y = 17.5f;
                if (Terrain.activeTerrain != null)
                    y = Terrain.activeTerrain.SampleHeight(testPos) + Terrain.activeTerrain.transform.position.y;
                
                if (y < 15.5f) 
                {
                    edgePoint = testPos - dir * 15f; 
                    if (Terrain.activeTerrain != null)
                        edgePoint.y = Terrain.activeTerrain.SampleHeight(edgePoint) + Terrain.activeTerrain.transform.position.y;
                    break;
                }
            }
            coastPoints.Add(edgePoint);
        }

        int spawnedCount = 0;
        for (int i = 0; i < coastPoints.Count; i++)
        {
            Vector3 p1 = coastPoints[i];
            Vector3 p2 = coastPoints[(i + 1) % coastPoints.Count];
            
            Vector3 dir = (p2 - p1).normalized;
            float dist = Vector3.Distance(p1, p2);
            int numFences = Mathf.FloorToInt(dist / fenceLength);
            
            for (int f = 0; f < numFences; f++)
            {
                if (i == 30 && f >= numFences/2 - 2 && f <= numFences/2 + 2) continue; 

                Vector3 fPos = p1 + dir * (f * fenceLength + fenceLength/2f);
                Quaternion fRot = Quaternion.LookRotation(dir); 
                Spawn(fencePrefab, fPos, fRot, targetScale, root.transform, $"HangRao_Vien_{i}_{f}");
                spawnedCount++;
            }
            
            Spawn(towerPrefab, p1, Quaternion.identity, targetScale, root.transform, $"ThapCanh_Neo_{i}");
        }
        
        Undo.RegisterCreatedObjectUndo(root, "Xay Hang Rao Bo Dao");
        EditorUtility.DisplayDialog("Hoan tat!", $"Da don dep {deletedCount} cay coi va xay xong {spawnedCount} doan rao men theo bo dao!", "OK");
    }

    static void Spawn(GameObject prefab, Vector3 pos, Quaternion rot, float scale, Transform parent, string name)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.rotation = rot;
        go.transform.localScale = new Vector3(scale, scale, scale);
        go.transform.position = pos;
    }
}
