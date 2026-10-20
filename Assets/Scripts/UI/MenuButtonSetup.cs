using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gắn vào GameObject MainMenu – tự động resize & style các button khi Awake.
/// Không cần Editor Script, hoạt động cả trong Play Mode.
/// </summary>
public class MenuButtonSetup : MonoBehaviour
{
    [Header("Button References (tự tìm nếu để trống)")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button quitButton;

    [Header("Layout")]
    [SerializeField] private Vector2 buttonSize    = new Vector2(320f, 65f);
    [SerializeField] private float   buttonSpacing = 90f;    // khoảng cách giữa các button
    [SerializeField] private float   centerY       = 0f;     // Y trung tâm (0 = giữa màn)

    [Header("Style")]
    // Màu nút (dark red semi-transparent)
    [SerializeField] private Color playNormal    = new Color(0.55f, 0.10f, 0.06f, 0.92f);
    [SerializeField] private Color settingNormal = new Color(0.28f, 0.06f, 0.04f, 0.90f);
    [SerializeField] private Color quitNormal    = new Color(0.18f, 0.04f, 0.04f, 0.88f);

    // Màu highlight (+sáng hơn khi hover)
    [SerializeField] private Color highlightAdd  = new Color(0.18f, 0.08f, 0.04f, 0.0f);

    [Header("Text")]
    [SerializeField] private string playLabel    = "⚔  CHƠI NGAY";
    [SerializeField] private string settingLabel = "⚙  TÙY CHỈNH";
    [SerializeField] private string quitLabel    = "✕  THOÁT GAME";
    [SerializeField] private float  fontSize     = 28f;

    private void Awake()
    {
        // Auto-find nếu chưa gán
        if (!playButton || !settingButton || !quitButton)
            AutoFind();

        ApplyStyle(playButton,    playLabel,    playNormal,    centerY + buttonSpacing);
        ApplyStyle(settingButton, settingLabel, settingNormal, centerY);
        ApplyStyle(quitButton,    quitLabel,    quitNormal,    centerY - buttonSpacing);
    }

    private void AutoFind()
    {
        Button[] btns = GetComponentsInChildren<Button>(true);
        foreach (var b in btns)
        {
            string n = b.name.ToLower();
            if (n.Contains("play")    && !playButton)    playButton    = b;
            if (n.Contains("setting") && !settingButton) settingButton = b;
            if (n.Contains("quit")    && !quitButton)    quitButton    = b;
        }
        // Fallback: tìm toàn scene
        if (!playButton || !settingButton || !quitButton)
        {
            Button[] all = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            foreach (var b in all)
            {
                string n = b.name.ToLower();
                if (n.Contains("play")    && !playButton)    playButton    = b;
                if (n.Contains("setting") && !settingButton) settingButton = b;
                if (n.Contains("quit")    && !quitButton)    quitButton    = b;
            }
        }
    }

    private void ApplyStyle(Button btn, string label, Color bgColor, float posY)
    {
        if (btn == null) return;

        // RectTransform
        var rt = btn.GetComponent<RectTransform>();
        rt.sizeDelta       = buttonSize;
        rt.localScale      = Vector3.one;
        rt.anchorMin       = new Vector2(0.5f, 0.5f);
        rt.anchorMax       = new Vector2(0.5f, 0.5f);
        rt.pivot           = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, posY);

        // Image color
        var img = btn.GetComponent<Image>();
        if (img) img.color = bgColor;

        // Button colors
        var colors = btn.colors;
        colors.normalColor      = bgColor;
        colors.highlightedColor = bgColor + highlightAdd + new Color(0.18f, 0.08f, 0.04f, 0f);
        colors.pressedColor     = bgColor * 0.7f;
        colors.fadeDuration     = 0.15f;
        btn.colors = colors;

        // Text
        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp)
        {
            tmp.text      = label;
            tmp.fontSize  = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color     = new Color(1f, 0.937f, 0.749f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
        }

        // Hover effect
        if (!btn.GetComponent<ButtonHoverEffect>())
            btn.gameObject.AddComponent<ButtonHoverEffect>();
    }
}
