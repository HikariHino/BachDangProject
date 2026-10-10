using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A continuous sky, sun, moon and atmosphere cycle for the battlefield.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class DayNightCycle : MonoBehaviour
{
    [Header("Time")]
    [Range(0f, 24f)] public float timeOfDay = 9f;
    [Min(0.1f)] public float dayLengthMinutes = 12f;
    [Tooltip("Time advances only in Play Mode. Edit Mode previews the selected hour.")]
    public bool autoAdvance = true;

    [Header("Scene references")]
    public Light sunLight;
    public Light moonLight;
    [Tooltip("Source material using BachDang/Dynamic Sky. A private copy is used while enabled.")]
    public Material skyboxMaterial;

    [Header("Sky motion")]
    [Range(0f, 360f)] public float sunPathAzimuth = 235f;
    [Range(10f, 85f)] public float maximumSunElevation = 62f;
    [Range(0f, 360f)] public float skyRotation;
    [Range(-0.5f, 0.5f)] public float cloudDegreesPerSecond = 0.08f;
    [Range(0.1f, 2f)] public float daySkyExposure = 1.05f;
    [Range(0.05f, 2f)] public float nightSkyExposure = 0.55f;

    [Header("Lighting")]
    [Min(0f)] public float sunlightIntensity = 1.2f;
    [Min(0f)] public float moonlightIntensity = 0.35f;
    public Color daylightColor = new Color(1f, 0.93f, 0.82f);
    public Color twilightColor = new Color(1f, 0.43f, 0.20f);
    public Color moonlightColor = new Color(0.58f, 0.72f, 1f);

    [Header("Atmosphere")]
    public Color dayFogColor = new Color(0.64f, 0.72f, 0.73f);
    public Color nightFogColor = new Color(0.055f, 0.078f, 0.12f);
    public Color twilightFogColor = new Color(0.62f, 0.37f, 0.28f);
    [Min(0f)] public float dayFogDensity = 0.0001f;
    [Min(0f)] public float nightFogDensity = 0.00015f;
    [Tooltip("Sky reflection capture interval. Never captured every frame.")]
    [Min(1f)] public float environmentUpdateInterval = 5f;

    private static readonly int DayBlendId = Shader.PropertyToID("_DayBlend");
    private static readonly int TwilightWeightId = Shader.PropertyToID("_TwilightWeight");
    private static readonly int TwilightTintId = Shader.PropertyToID("_TwilightTint");
    private static readonly int RotationId = Shader.PropertyToID("_Rotation");
    private static readonly int DayExposureId = Shader.PropertyToID("_DayExposure");
    private static readonly int NightExposureId = Shader.PropertyToID("_NightExposure");
    private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
    private static readonly int MoonDirectionId = Shader.PropertyToID("_MoonDirection");
    private static readonly int SunColorId = Shader.PropertyToID("_SunColor");
    private static readonly int MoonColorId = Shader.PropertyToID("_MoonColor");
    private static readonly int SunVisibilityId = Shader.PropertyToID("_SunVisibility");
    private static readonly int MoonVisibilityId = Shader.PropertyToID("_MoonVisibility");
    private static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");

    private Material skyInstance;
    private Material sourceInUse;
    private EnvironmentState previousEnvironment;
    private LightState previousSun;
    private LightState previousMoon;
    private bool initialized;
    private bool previewDirty = true;
    private bool environmentDirty;
    private bool savingSourceSky;
    private float cloudRotation;
    private float lastAppliedTime;
    private double nextEnvironmentUpdate;

    private void OnEnable()
    {
#if UNITY_EDITOR
        UnsubscribeEditorCallbacks();
        UnityEditor.EditorApplication.update += EditorPreview;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += BeforeSceneSave;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += AfterSceneSave;
#endif
        ApplyTimeOfDay();
    }

    private void OnValidate()
    {
        timeOfDay = Mathf.Repeat(timeOfDay, 24f);
        dayLengthMinutes = Mathf.Max(0.1f, dayLengthMinutes);
        environmentUpdateInterval = Mathf.Max(1f, environmentUpdateInterval);
        previewDirty = true;
    }

    private void Update()
    {
        if (!Application.IsPlaying(gameObject)) return;
        if (autoAdvance)
            timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime * 24f / (Mathf.Max(0.1f, dayLengthMinutes) * 60f), 24f);
        cloudRotation = Mathf.Repeat(cloudRotation + cloudDegreesPerSecond * Time.deltaTime, 360f);
        ApplyTimeOfDay();
    }

    public void SetTimeOfDay(float hour)
    {
        timeOfDay = Mathf.Repeat(hour, 24f);
        ApplyTimeOfDay();
    }

    [ContextMenu("Preview selected time of day")]
    public void ApplyTimeOfDay()
    {
        previewDirty = false;
        if (!CanControlScene() || !EnsureSkyInstance()) return;

        timeOfDay = Mathf.Repeat(timeOfDay, 24f);
        lastAppliedTime = timeOfDay;
        float angle = (timeOfDay - 6f) * Mathf.PI / 12f;
        float inclination = maximumSunElevation * Mathf.Deg2Rad;
        Vector3 sunDirection = Quaternion.Euler(0f, sunPathAzimuth, 0f) * new Vector3(
            Mathf.Cos(angle), Mathf.Sin(angle) * Mathf.Sin(inclination), Mathf.Sin(angle) * Mathf.Cos(inclination));
        Vector3 moonDirection = -sunDirection;
        float sunHeight = sunDirection.y;
        float daylight = SmoothRange(-0.16f, 0.20f, sunHeight);
        float twilight = (1f - SmoothRange(0.04f, 0.34f, Mathf.Abs(sunHeight)))
                         * SmoothRange(-0.30f, -0.08f, sunHeight);

        sunLight.transform.rotation = Quaternion.LookRotation(-sunDirection, Vector3.up);
        sunLight.color = Color.Lerp(twilightColor, daylightColor, SmoothRange(0f, 0.45f, sunHeight));
        sunLight.useColorTemperature = false;
        sunLight.intensity = sunlightIntensity * SmoothRange(-0.04f, 0.25f, sunHeight);
        sunLight.enabled = sunLight.intensity > 0.001f;
        moonLight.transform.rotation = Quaternion.LookRotation(-moonDirection, Vector3.up);
        moonLight.color = moonlightColor;
        moonLight.useColorTemperature = false;
        moonLight.intensity = moonlightIntensity * (1f - daylight) * SmoothRange(-0.04f, 0.30f, moonDirection.y);
        moonLight.enabled = moonLight.intensity > 0.001f;
        // URP selects this directional light as the main light, including for water.
        RenderSettings.sun = sunLight.intensity >= moonLight.intensity ? sunLight : moonLight;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.22f, 0.25f, 0.32f), new Color(0.70f, 0.76f, 0.80f), daylight);
        RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.14f, 0.18f, 0.24f), new Color(0.50f, 0.54f, 0.48f), daylight);
        RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, twilightFogColor, twilight * 0.22f);
        RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.08f, 0.10f, 0.14f), new Color(0.31f, 0.34f, 0.28f), daylight);
        // Preserve the scene's fog mode, range and enabled state while changing its atmosphere.
        RenderSettings.fogColor = Color.Lerp(Color.Lerp(nightFogColor, dayFogColor, daylight), twilightFogColor, twilight * 0.45f);
        RenderSettings.fogDensity = Mathf.Lerp(nightFogDensity, dayFogDensity, daylight);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = Mathf.Lerp(0.65f, 1f, daylight);

        skyInstance.SetFloat(DayBlendId, daylight);
        skyInstance.SetFloat(TwilightWeightId, twilight);
        skyInstance.SetColor(TwilightTintId, twilightColor);
        skyInstance.SetFloat(RotationId, Mathf.Repeat(skyRotation + cloudRotation, 360f));
        skyInstance.SetFloat(DayExposureId, daySkyExposure);
        skyInstance.SetFloat(NightExposureId, nightSkyExposure);
        skyInstance.SetVector(SunDirectionId, sunDirection);
        skyInstance.SetVector(MoonDirectionId, moonDirection);
        skyInstance.SetColor(SunColorId, sunLight.color);
        skyInstance.SetColor(MoonColorId, moonlightColor);
        skyInstance.SetFloat(SunVisibilityId, SmoothRange(-0.025f, 0.05f, sunHeight));
        skyInstance.SetFloat(MoonVisibilityId, (1f - daylight) * SmoothRange(-0.025f, 0.05f, moonDirection.y));
        skyInstance.SetColor(HorizonColorId, RenderSettings.fogColor);
        RenderSettings.skybox = skyInstance;
        environmentDirty = true;
        RefreshEnvironmentIfDue();
    }

    private bool CanControlScene()
    {
        if (this == null || !isActiveAndEnabled || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return false;
#if UNITY_EDITOR
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null) return false;
#endif
        return true;
    }

    private bool EnsureSkyInstance()
    {
        if (sunLight == null) sunLight = GetComponent<Light>();
        if (initialized && (sourceInUse != skyboxMaterial || previousSun.light != sunLight || previousMoon.light != moonLight))
            ReleaseSkyInstance();
        if (sunLight == null || moonLight == null || sunLight == moonLight || skyboxMaterial == null
            || sunLight.type != LightType.Directional || moonLight.type != LightType.Directional
            || !skyboxMaterial.HasProperty(DayBlendId)) return false;
        if (initialized) return true;

        previousEnvironment = EnvironmentState.Capture();
        previousSun = LightState.Capture(sunLight);
        previousMoon = LightState.Capture(moonLight);
        sourceInUse = skyboxMaterial;
        skyInstance = new Material(skyboxMaterial)
        {
            name = skyboxMaterial.name + " (DayNight instance)",
            hideFlags = HideFlags.HideAndDontSave
        };
        nextEnvironmentUpdate = 0;
        initialized = true;
        return true;
    }

    private void RefreshEnvironmentIfDue()
    {
        if (!initialized || !environmentDirty || RenderSettings.skybox != skyInstance) return;
        double now = Time.realtimeSinceStartupAsDouble;
#if UNITY_EDITOR
        if (!Application.isPlaying) now = UnityEditor.EditorApplication.timeSinceStartup;
#endif
        if (now < nextEnvironmentUpdate) return;
        DynamicGI.UpdateEnvironment();
        nextEnvironmentUpdate = now + Mathf.Max(1f, environmentUpdateInterval);
        environmentDirty = false;
    }

    private static float SmoothRange(float from, float to, float value)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
    }

#if UNITY_EDITOR
    private void EditorPreview()
    {
        // A scene can unload within the current Editor update invocation list.
        if (this == null) { UnsubscribeEditorCallbacks(); return; }
        if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || !CanControlScene()) return;
        if (previewDirty || (initialized && (timeOfDay != lastAppliedTime || sourceInUse != skyboxMaterial)))
        {
            ApplyTimeOfDay();
            UnityEditor.SceneView.RepaintAll();
        }
        RefreshEnvironmentIfDue();
    }

    private void BeforeSceneSave(UnityEngine.SceneManagement.Scene scene, string path)
    {
        if (this == null || scene != gameObject.scene || !initialized || RenderSettings.skybox != skyInstance) return;
        // Serialize a real asset reference, not the temporary material used for preview.
        RenderSettings.skybox = skyboxMaterial;
        savingSourceSky = true;
    }

    private void AfterSceneSave(UnityEngine.SceneManagement.Scene scene)
    {
        if (this == null || scene != gameObject.scene || !savingSourceSky) return;
        savingSourceSky = false;
        ApplyTimeOfDay();
    }

    private void UnsubscribeEditorCallbacks()
    {
        UnityEditor.EditorApplication.update -= EditorPreview;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= BeforeSceneSave;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= AfterSceneSave;
    }
#endif

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnsubscribeEditorCallbacks();
#endif
        ReleaseSkyInstance();
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        UnsubscribeEditorCallbacks();
#endif
        ReleaseSkyInstance();
    }

    private void ReleaseSkyInstance()
    {
        if (!initialized) return;
        if (RenderSettings.skybox == skyInstance || (savingSourceSky && RenderSettings.skybox == skyboxMaterial))
        {
            previousEnvironment.Restore();
            previousSun.Restore();
            previousMoon.Restore();
            DynamicGI.UpdateEnvironment();
        }
        if (skyInstance != null)
        {
            if (Application.isPlaying) Destroy(skyInstance);
            else DestroyImmediate(skyInstance);
        }
        skyInstance = null;
        sourceInUse = null;
        initialized = false;
        savingSourceSky = false;
        environmentDirty = false;
    }

    private struct LightState
    {
        public Light light;
        private Quaternion rotation;
        private Color color;
        private float intensity;
        private bool enabled;
        private bool useColorTemperature;

        public static LightState Capture(Light light)
        {
            return new LightState { light = light, rotation = light.transform.rotation, color = light.color,
                intensity = light.intensity, enabled = light.enabled, useColorTemperature = light.useColorTemperature };
        }

        public void Restore()
        {
            if (light == null) return;
            light.transform.rotation = rotation;
            light.color = color;
            light.intensity = intensity;
            light.enabled = enabled;
            light.useColorTemperature = useColorTemperature;
        }
    }

    private struct EnvironmentState
    {
        private Material sky;
        private Light sun;
        private AmbientMode ambientMode;
        private Color ambientSky, ambientEquator, ambientGround, fogColor;
        private float ambientIntensity, reflectionIntensity, fogDensity;
        private DefaultReflectionMode reflectionMode;

        public static EnvironmentState Capture()
        {
            return new EnvironmentState { sky = RenderSettings.skybox, sun = RenderSettings.sun,
                ambientMode = RenderSettings.ambientMode, ambientSky = RenderSettings.ambientSkyColor,
                ambientEquator = RenderSettings.ambientEquatorColor, ambientGround = RenderSettings.ambientGroundColor,
                ambientIntensity = RenderSettings.ambientIntensity, reflectionIntensity = RenderSettings.reflectionIntensity,
                reflectionMode = RenderSettings.defaultReflectionMode, fogColor = RenderSettings.fogColor, fogDensity = RenderSettings.fogDensity };
        }

        public void Restore()
        {
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.defaultReflectionMode = reflectionMode;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }
    }
}
