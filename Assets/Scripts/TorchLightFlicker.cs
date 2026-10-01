using UnityEngine;

/// <summary>
/// Replacement for the torch prefabs' missing behaviour. The original component
/// serialized no custom fields. Its script GUID and enabled state are retained;
/// particle playback and the lights' enabled states remain controlled by the prefab.
/// </summary>
[DisallowMultipleComponent]
public class TorchLightFlicker : MonoBehaviour
{
    [Range(0f, 0.5f)] public float variation = 0.12f;
    [Min(0f)] public float flickerSpeed = 3f;

    private Light[] torchLights;
    private float[] originalIntensities;
    private float phase;

    void OnEnable()
    {
        torchLights = GetComponentsInChildren<Light>(true);
        originalIntensities = new float[torchLights.Length];
        for (int i = 0; i < torchLights.Length; i++)
            originalIntensities[i] = torchLights[i].intensity;

        Vector3 position = transform.position;
        phase = Mathf.Repeat(position.x * 0.173f + position.z * 0.117f, 100f);
    }

    void Update()
    {
        float noise = Mathf.PerlinNoise(phase, Time.time * Mathf.Max(0f, flickerSpeed)) * 2f - 1f;
        float multiplier = 1f + noise * Mathf.Clamp(variation, 0f, 0.5f);
        for (int i = 0; i < torchLights.Length; i++)
        {
            Light torchLight = torchLights[i];
            if (torchLight != null && torchLight.isActiveAndEnabled)
                torchLight.intensity = originalIntensities[i] * multiplier;
        }
    }

    void OnDisable()
    {
        if (torchLights == null || originalIntensities == null) return;
        for (int i = 0; i < torchLights.Length; i++)
        {
            if (torchLights[i] != null)
                torchLights[i].intensity = originalIntensities[i];
        }
    }
}
