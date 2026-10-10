using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý hiệu ứng chuyển cảnh đen (Fade to Black) và hiện chữ dẫn truyện điện ảnh.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI subtitleText;
    private Image blackImage;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            EnsureUIExists();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void EnsureUIExists()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // Luôn đè lên trên mọi UI khác
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Tạo ảnh nền đen phủ toàn màn hình
        Transform bgTransform = transform.Find("BlackOverlay");
        if (bgTransform == null)
        {
            GameObject bgObj = new GameObject("BlackOverlay");
            bgObj.transform.SetParent(transform, false);
            blackImage = bgObj.AddComponent<Image>();
            blackImage.color = Color.black;

            RectTransform rt = bgObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            blackImage = bgTransform.GetComponent<Image>();
        }

        // Tạo Text dẫn truyện
        Transform textTransform = transform.Find("SubtitleText");
        if (textTransform == null)
        {
            GameObject textObj = new GameObject("SubtitleText");
            textObj.transform.SetParent(transform, false);
            subtitleText = textObj.AddComponent<TextMeshProUGUI>();
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.fontSize = 28;
            subtitleText.fontStyle = FontStyles.Bold;
            subtitleText.color = new Color(0.95f, 0.9f, 0.75f, 1f); // Màu vàng cổ phong

            RectTransform rt = textObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.4f);
            rt.anchorMax = new Vector2(0.9f, 0.6f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            subtitleText = textTransform.GetComponent<TextMeshProUGUI>();
        }

        // Mặc định ban đầu trong suốt (nhìn thấy game bình thường)
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        if (subtitleText != null) subtitleText.text = "";
    }

    /// <summary>
    /// Thực hiện chuỗi Fade Đen -> Chạy hành động (Teleport) -> Giữ đen một lúc đọc chữ -> Sáng lại
    /// </summary>
    public void FadeTransition(string narrativeText, Action duringBlack, float fadeDuration = 0.8f, float holdDuration = 1.6f, Action onComplete = null)
    {
        StartCoroutine(RoutineFadeTransition(narrativeText, duringBlack, fadeDuration, holdDuration, onComplete));
    }

    private IEnumerator RoutineFadeTransition(string narrativeText, Action duringBlack, float fadeDuration, float holdDuration, Action onComplete)
    {
        EnsureUIExists();

        // 1. Fade to Black
        canvasGroup.blocksRaycasts = true;
        if (subtitleText != null) subtitleText.text = "";

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // 2. Hiện chữ dẫn truyện và thực hiện hành động dịch chuyển
        if (subtitleText != null && !string.IsNullOrEmpty(narrativeText))
        {
            subtitleText.text = narrativeText;
        }

        duringBlack?.Invoke();

        // Giữ màn hình đen một khoảng thời gian để người chơi đọc truyện
        yield return new WaitForSeconds(holdDuration);

        // 3. Fade In trở lại
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / fadeDuration));
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        if (subtitleText != null) subtitleText.text = "";

        onComplete?.Invoke();
    }
}
