using UnityEngine;
using UnityEditor;

public static class PurpleSpotChecker
{
    public static void CheckRenderers()
    {
        var allRends = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        foreach (var r in allRends)
        {
            if (r == null) continue;
            foreach (var m in r.sharedMaterials)
            {
                if (m == null)
                {
                    Debug.LogWarning($"[PurpleChecker] NULL material on {r.gameObject.name} in {r.transform.root.name}");
                }
                else if (m.shader == null || m.shader.name == "Hidden/InternalErrorShader" || m.shader.name.Contains("Error"))
                {
                    Debug.LogError($"[PurpleChecker] ERROR SHADER on {r.gameObject.name}: {m.name} -> {m.shader?.name}");
                }
            }
        }
        Debug.Log("[PurpleChecker] Finished checking all renderers in scene.");
    }
}
