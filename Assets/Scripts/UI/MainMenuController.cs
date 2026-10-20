using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controller cho Main Menu – xử lý Play, Settings, Quit với hiệu ứng fade
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button quitButton;

    [Header("Scene Transition")]
    [SerializeField] private string playSceneName = "SampleScene";
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.6f;

    [Header("Title Animation")]
    [SerializeField] private RectTransform titleRect;   // logo / title nếu có
    [SerializeField] private float titleBobAmount = 8f;
    [SerializeField] private float titleBobSpeed = 1.2f;

    private Vector2 _titleAnchoredStart;
    private bool _isTransitioning = false;

    private void Start()
    {
        // Kết nối sự kiện
        if (playButton)    playButton.onClick.AddListener(OnPlay);
        if (settingButton) settingButton.onClick.AddListener(OnSetting);
        if (quitButton)    quitButton.onClick.AddListener(OnQuit);

        // Lưu vị trí ban đầu của title
        if (titleRect) _titleAnchoredStart = titleRect.anchoredPosition;

        // Fade in từ màn đen
        if (fadeCanvasGroup)
        {
            fadeCanvasGroup.alpha = 1f;
            StartCoroutine(FadeIn());
        }
    }

    private void Update()
    {
        // Hiệu ứng lơ lửng nhẹ cho title
        if (titleRect && !_isTransitioning)
        {
            float offset = Mathf.Sin(Time.time * titleBobSpeed) * titleBobAmount;
            titleRect.anchoredPosition = _titleAnchoredStart + new Vector2(0f, offset);
        }
    }

    private void OnPlay()
    {
        if (_isTransitioning) return;
        StartCoroutine(TransitionToScene(playSceneName));
    }

    private void OnSetting()
    {
        if (_isTransitioning) return;
        Debug.Log("[MainMenu] Setting button pressed – mở panel Setting ở đây");
        // TODO: Kéo / gán SettingPanel và bật lên
    }

    private void OnQuit()
    {
        if (_isTransitioning) return;
        StartCoroutine(QuitGame());
    }

    // ──────────── Coroutines ────────────

    private IEnumerator TransitionToScene(string sceneName)
    {
        _isTransitioning = true;
        if (fadeCanvasGroup) yield return StartCoroutine(FadeOut());
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator QuitGame()
    {
        _isTransitioning = true;
        if (fadeCanvasGroup) yield return StartCoroutine(FadeOut());
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator FadeIn()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 0f;
    }

    private IEnumerator FadeOut()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 1f;
    }
}
