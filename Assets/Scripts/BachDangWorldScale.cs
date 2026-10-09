using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Scene-local unit conversion for the battlefield; source models remain unchanged.</summary>
[ExecuteAlways, DefaultExecutionOrder(-10000), DisallowMultipleComponent]
public class BachDangWorldScale : MonoBehaviour
{
    [Min(.01f)] public float unitsPerReferenceUnit = 1f;
    public float referenceCharacterHeight = 1.941f;
    public float fittedCharacterHeight = 13.587f;
    public string referenceScene = "Assets/Scenes/TestQuest.unity";

    RenderPipelineAsset previousPipeline;
    UniversalRenderPipelineAsset privatePipeline;
    Vector3 previousGravity;
    Vector3 appliedGravity;
    bool ownsGravity;

    public static float ForScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return 1f;
        foreach (var root in scene.GetRootGameObjects())
        {
            var scale = root.GetComponent<BachDangWorldScale>();
            if (scale != null && scale.isActiveAndEnabled)
                return Mathf.Max(.01f, scale.unitsPerReferenceUnit);
        }
        return 1f;
    }

    void OnEnable()
    {
        SceneManager.activeSceneChanged += ActiveSceneChanged;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += PlayModeChanged;
#endif
        ApplyRenderScale();
    }
    void ActiveSceneChanged(Scene oldScene, Scene newScene) => ApplyRenderScale();

#if UNITY_EDITOR
    // This project disables scene/domain reload on entering Play Mode.
    // Its existing enabled component therefore needs an explicit transition hook.
    void PlayModeChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.EnteredPlayMode) ApplyRenderScale();
        else if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                 state == UnityEditor.PlayModeStateChange.EnteredEditMode) ReleaseSettings();
    }
#endif

    public void ApplyRenderScale()
    {
        ReleaseSettings();
        // Global quality/physics overrides are owned only during Play Mode. Editor
        // saves therefore never persist a temporary pipeline in project settings.
        if (!Application.isPlaying || gameObject.scene != SceneManager.GetActiveScene() || Mathf.Approximately(unitsPerReferenceUnit, 1f)) return;
        previousPipeline = QualitySettings.renderPipeline;
        var source = (previousPipeline != null ? previousPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
        if (source != null)
        {
            privatePipeline = Instantiate(source);
            privatePipeline.name = source.name + " (BachDang scaled scene)";
            privatePipeline.hideFlags = HideFlags.HideAndDontSave;
            privatePipeline.shadowDistance = source.shadowDistance * unitsPerReferenceUnit;
            QualitySettings.renderPipeline = privatePipeline;
        }
        previousGravity = Physics.gravity;
        appliedGravity = previousGravity * unitsPerReferenceUnit;
        Physics.gravity = appliedGravity;
        ownsGravity = true;
    }

    void OnDisable()
    {
        SceneManager.activeSceneChanged -= ActiveSceneChanged;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= PlayModeChanged;
#endif
        ReleaseSettings();
    }
    void ReleaseSettings()
    {
        if (privatePipeline != null)
        {
            if (QualitySettings.renderPipeline == privatePipeline) QualitySettings.renderPipeline = previousPipeline;
            if (Application.isPlaying) Destroy(privatePipeline); else DestroyImmediate(privatePipeline);
            privatePipeline = null;
        }
        if (ownsGravity && Physics.gravity == appliedGravity) Physics.gravity = previousGravity;
        ownsGravity = false;
    }
}
