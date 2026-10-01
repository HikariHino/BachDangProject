using UnityEngine;

/// <summary>
/// Keeps a flat water surface's shader height aligned with its tide-driven transform.
/// Does not rebuild the mesh or change the shared water material.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class TidalWaterSurface : MonoBehaviour
{
    private static readonly int WaterSurfaceHeight = Shader.PropertyToID("_WaterSurfaceHeight");

    private Renderer waterRenderer;
    private Material waterMaterial;
    private MaterialPropertyBlock properties;
    private bool ownsHeight;
    private bool hadHeightOverride;
    private float previousHeight;
    private float lastWrittenHeight;

    private void OnEnable()
    {
        waterRenderer = GetComponent<Renderer>();
        Synchronize();
    }

    private void LateUpdate()
    {
        // TideSystem moves the surface in Update, so use its final height this frame.
        Synchronize();
    }

    public void Synchronize()
    {
        if (!isActiveAndEnabled) return;
        if (waterRenderer == null) waterRenderer = GetComponent<Renderer>();
        if (waterRenderer == null) return;

        Material currentMaterial = waterRenderer.sharedMaterial;
        if (currentMaterial != waterMaterial)
        {
            RestoreHeight();
            waterMaterial = currentMaterial;
        }

        if (waterMaterial == null || !waterMaterial.HasProperty(WaterSurfaceHeight))
        {
            RestoreHeight();
            return;
        }

        if (properties == null) properties = new MaterialPropertyBlock();
        waterRenderer.GetPropertyBlock(properties);

        if (!ownsHeight)
        {
            hadHeightOverride = properties.HasFloat(WaterSurfaceHeight);
            previousHeight = hadHeightOverride
                ? properties.GetFloat(WaterSurfaceHeight)
                : waterMaterial.GetFloat(WaterSurfaceHeight);
            ownsHeight = true;
        }

        lastWrittenHeight = transform.position.y;
        properties.SetFloat(WaterSurfaceHeight, lastWrittenHeight);
        waterRenderer.SetPropertyBlock(properties);
    }

    private void OnDisable()
    {
        RestoreHeight();
    }

    private void OnDestroy()
    {
        RestoreHeight();
    }

    private void RestoreHeight()
    {
        if (!ownsHeight) return;
        ownsHeight = false;
        if (waterRenderer == null || properties == null) return;

        waterRenderer.GetPropertyBlock(properties);
        // Another owner may have changed or removed the height after our last update.
        if (!properties.HasFloat(WaterSurfaceHeight)
            || properties.GetFloat(WaterSurfaceHeight) != lastWrittenHeight) return;

        float restoredHeight = previousHeight;
        if (!hadHeightOverride && waterMaterial != null && waterMaterial.HasProperty(WaterSurfaceHeight))
            restoredHeight = waterMaterial.GetFloat(WaterSurfaceHeight);

        // MPB has no per-property removal API. Restore the effective prior value;
        // clearing the block would also erase unrelated overrides added meanwhile.
        properties.SetFloat(WaterSurfaceHeight, restoredHeight);
        waterRenderer.SetPropertyBlock(properties);
    }
}
