using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Reduces only the newly combined decorative grass, with evenly distributed survivors.</summary>
public static class BachDangVillageGrassBudget
{
    public static int Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/beachBoat.unity")
            throw new InvalidOperationException("Open beachBoat in Edit Mode.");
        var root = GameObject.Find("BachDang_Living_Settlements").transform.Find("10_Lang_Tu_Nhien");
        if (root == null) throw new InvalidOperationException("Natural village dressing is missing.");
        int total = 0;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>().Where(f => f.name.StartsWith("Co_Thap_Chan_Rao")))
        {
            var mesh = filter.sharedMesh;
            if (!AssetDatabase.GetAssetPath(mesh).StartsWith("Assets/BachDangVillageNatural/", StringComparison.Ordinal))
                throw new InvalidOperationException("Only owned natural-village grass may be reduced.");
            string kind = filter.name.EndsWith("Co_Bui") ? "Grass_C" : "Grass_A";
            var source = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/TerrainSampleAssets/Models/" + kind + ".asset");
            int stride = source.vertexCount;
            if (mesh.vertexCount % stride != 0) throw new InvalidOperationException("Unexpected combined grass vertex layout.");
            int count = mesh.vertexCount / stride;
            int keep = Mathf.Min(count, 8000 / stride);
            total += keep;
            if (keep == count) continue;
            if (keep < 1) throw new InvalidOperationException("Source grass exceeds the entire mesh budget.");
            var oldIndices = mesh.triangles;
            if (oldIndices.Length % count != 0) throw new InvalidOperationException("Unexpected combined grass triangle layout.");
            int indexStride = oldIndices.Length / count;
            var selected = Enumerable.Range(0, keep).Select(i => Mathf.FloorToInt((i + .5f) * count / keep)).ToArray();
            var map = selected.SelectMany(i => Enumerable.Range(i * stride, stride)).ToArray();
            var vertices = mesh.vertices; var normals = mesh.normals; var tangents = mesh.tangents;
            var uv = mesh.uv; var uv2 = mesh.uv2; var colors = mesh.colors;
            var indices = new int[keep * indexStride];
            for (int block = 0; block < keep; block++) for (int i = 0; i < indexStride; i++)
            {
                int local = oldIndices[selected[block] * indexStride + i] - selected[block] * stride;
                if (local < 0 || local >= stride) throw new InvalidOperationException("Grass triangle crosses instance boundaries.");
                indices[block * indexStride + i] = block * stride + local;
            }
            Undo.RegisterCompleteObjectUndo(mesh, "Limit village grass memory");
            mesh.Clear(); mesh.indexFormat = IndexFormat.UInt16;
            mesh.vertices = map.Select(i => vertices[i]).ToArray();
            if (normals.Length == vertices.Length) mesh.normals = map.Select(i => normals[i]).ToArray();
            if (tangents.Length == vertices.Length) mesh.tangents = map.Select(i => tangents[i]).ToArray();
            if (uv.Length == vertices.Length) mesh.uv = map.Select(i => uv[i]).ToArray();
            if (uv2.Length == vertices.Length) mesh.uv2 = map.Select(i => uv2[i]).ToArray();
            if (colors.Length == vertices.Length) mesh.colors = map.Select(i => colors[i]).ToArray();
            mesh.triangles = indices; mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh);
        }
        Debug.Log("Decorative grass retained: " + total + " tufts, at most 8,000 vertices per mesh; terrain trees unchanged.");
        return total;
    }
}
