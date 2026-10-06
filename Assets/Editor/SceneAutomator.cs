using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Text;

public static class SceneAutomator
{
    public static void InspectScene()
    {
        string scenePath = "Assets/Scenes/beachBoat.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Debug.Log($"[SceneAutomator] Opened scene: {scene.name}, path: {scene.path}");

        var rootGos = scene.GetRootGameObjects();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"=== SCENE HIERARCHY ({rootGos.Length} root objects) ===");
        foreach (var go in rootGos)
        {
            sb.AppendLine($"- {go.name} (Active: {go.activeSelf}, Pos: {go.transform.position})");
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i);
                sb.AppendLine($"  +-- {child.name} (Pos: {child.position})");
                for (int j = 0; j < child.childCount; j++)
                {
                    var grand = child.GetChild(j);
                    sb.AppendLine($"      |-- {grand.name} (Pos: {grand.position})");
                }
            }
        }
        Debug.Log(sb.ToString());
    }
}
