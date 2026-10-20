using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Hiệu ứng hover đẹp cho button trong Main Menu - scale + glow khi hover
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Scale Settings")]
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float pressScale = 0.95f;
    [SerializeField] private float animDuration = 0.15f;

    [Header("Glow / Color Settings")]
    [SerializeField] private Color normalTextColor = new Color(1f, 0.937f, 0.749f, 1f);   // vàng nhạt
    [SerializeField] private Color hoverTextColor  = new Color(1f, 1f, 0.6f, 1f);          // vàng sáng
    [SerializeField] private Color pressTextColor  = new Color(1f, 0.8f, 0.3f, 1f);        // vàng đậm khi nhấn

    [Header("Optional Border Glow Image")]
    [SerializeField] private Image glowImage;   // kéo vào Inspector nếu có Glow Image con

    private Vector3 _originalScale;
    private Coroutine _scaleCoroutine;
    private TextMeshProUGUI _label;

    private void Awake()
    {
        _originalScale = transform.localScale;
        _label = GetComponentInChildren<TextMeshProUGUI>();
        if (_label != null)
            _label.color = normalTextColor;

        if (glowImage != null)
        {
            Color c = glowImage.color;
            c.a = 0f;
            glowImage.color = c;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StartScale(_originalScale * hoverScale);
        if (_label != null) _label.color = hoverTextColor;
        if (glowImage != null) StartCoroutine(FadeGlow(0f, 1f));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StartScale(_originalScale);
        if (_label != null) _label.color = normalTextColor;
        if (glowImage != null) StartCoroutine(FadeGlow(glowImage.color.a, 0f));
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        StartScale(_originalScale * pressScale);
        if (_label != null) _label.color = pressTextColor;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StartScale(_originalScale * hoverScale);
        if (_label != null) _label.color = hoverTextColor;
    }

    private void StartScale(Vector3 target)
    {
        if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
        _scaleCoroutine = StartCoroutine(ScaleTo(target));
    }

    private IEnumerator ScaleTo(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
            transform.localScale = Vector3.Lerp(start, target, t);
            yield return null;
        }
        transform.localScale = target;
    }

    private IEnumerator FadeGlow(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animDuration);
            Color c = glowImage.color;
            c.a = Mathf.Lerp(from, to, t);
            glowImage.color = c;
            yield return null;
        }
        Color final = glowImage.color;
        final.a = to;
        glowImage.color = final;
    }
}
