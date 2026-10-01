using System;
using UnityEditor;
using UnityEngine;

/// <summary>Places existing settlement props without modifying their source assets.</summary>
public static class BachDangSettlementAssets
{
    public static GameObject Place(string path, string name, Transform parent,
        Vector3 center, float yaw, float scale = 1f)
    {
        if (!Finite(center.x) || !Finite(center.y) || !Finite(center.z) ||
            !Finite(yaw) || !Finite(scale) || scale <= 0f)
            throw new ArgumentException("Settlement placement requires finite coordinates and a positive scale.");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new ArgumentException("Settlement prefab was not found: " + path, nameof(path));

        // Do not add people indirectly through a building or prop prefab.
        if (prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0)
            throw new InvalidOperationException("Settlement prefab contains a skinned model: " + path);
        foreach (var animator in prefab.GetComponentsInChildren<Animator>(true))
            if (animator.isHuman || (animator.avatar != null && animator.avatar.isHuman))
                throw new InvalidOperationException("Settlement prefab contains a humanoid Animator: " + path);

        var wrapper = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(wrapper, "Place settlement asset");
        try
        {
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wrapper.transform);
            var authoredScale = prefab.transform.localScale;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = authoredScale * scale;

            // Measure after rotation: imported vertices may be far from their pivot.
            // Offset the prefab child so the wrapper stays at the requested location.
            var bounds = BoundsOf(instance);
            instance.transform.position += new Vector3(
                center.x - bounds.center.x,
                center.y - 0.03f - bounds.min.y,
                center.z - bounds.center.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
            {
                var mesh = child.GetComponent<MeshRenderer>();
                bool animated = child.GetComponentInParent<Animator>() != null ||
                    child.GetComponentInParent<Animation>() != null;
                // Source prefabs often mark every object static. Keep only batching
                // on fixed mesh geometry; particles and lights remain nonstatic.
                var flags = mesh != null && !animated &&
                    child.GetComponent<ParticleSystem>() == null && child.GetComponent<Light>() == null
                    ? StaticEditorFlags.BatchingStatic : (StaticEditorFlags)0;
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            }

            return wrapper;
        }
        catch
        {
            Undo.DestroyObjectImmediate(wrapper);
            throw;
        }
    }

    /// <summary>World bounds of enabled mesh renderers on active objects, excluding particles.</summary>
    public static Bounds BoundsOf(GameObject go)
    {
        if (go == null) throw new ArgumentNullException(nameof(go));
        Bounds result = default;
        bool found = false;
        foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) { result = renderer.bounds; found = true; }
            else result.Encapsulate(renderer.bounds);
        }
        if (!found)
            throw new InvalidOperationException("Settlement asset has no active mesh renderer: " + go.name);
        return result;
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
