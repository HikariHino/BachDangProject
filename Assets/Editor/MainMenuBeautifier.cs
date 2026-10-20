using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool: Làm đẹp Main Menu – chạy menu Tools > Bach Dang > Beautify Main Menu
/// </summary>
public class MainMenuBeautifier : EditorWindow
{
    [MenuItem("Tools/Bach Dang/Beautify Main Menu")]
    public static void Run()
    {
        // Mở scene Main menu nếu chưa mở
        string scenePath = "Assets/Scenes/Main menu.unity";
        var currentScene = SceneManager.GetActiveScene();

        if (currentScene.path != scenePath)
        {
            bool save = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            if (!save) return;
            EditorSceneManager.OpenScene(scenePath);
        }

        BeautifyScene();
    }

    private static void BeautifyScene()
    {
        // ─── Tìm Canvas ───────────────────────────────────────────────
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("[Beautifier] Không tìm thấy Canvas!"); return; }

        RectTransform canvasRT = canvas.GetComponent<RectTransform>();

        // ─── Tìm các button theo tên ──────────────────────────────────
        Button[] allButtons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        Button playBtn = null, settingBtn = null, quitBtn = null;

        foreach (var b in allButtons)
        {
            string n = b.name.ToLower();
            if (n.Contains("play"))    playBtn    = b;
            if (n.Contains("setting")) settingBtn = b;
            if (n.Contains("quit"))    quitBtn    = b;
        }

        // ─── Style từng button ────────────────────────────────────────
        StyleButton(playBtn,    "⚔  CHƠI NGAY",  new Color(0.55f, 0.10f, 0.06f, 0.92f), new Vector2(0f, 80f),  new Vector2(380f, 72f));
        StyleButton(settingBtn, "⚙  TÙY CHỈNH",  new Color(0.25f, 0.06f, 0.04f, 0.90f), new Vector2(0f, -10f), new Vector2(380f, 72f));
        StyleButton(quitBtn,    "✕  THOÁT GAME", new Color(0.18f, 0.04f, 0.04f, 0.88f), new Vector2(0f,-100f), new Vector2(380f, 72f));

        // ─── Thêm ButtonHoverEffect nếu chưa có ──────────────────────
        AddHoverEffect(playBtn);
        AddHoverEffect(settingBtn);
        AddHoverEffect(quitBtn);

        // ─── Thêm MenuButtonSetup vào Canvas nếu chưa có ─────────────
        if (canvas.GetComponent<MenuButtonSetup>() == null)
        {
            var setup = canvas.gameObject.AddComponent<MenuButtonSetup>();
            EditorUtility.SetDirty(setup);
        }

        // ─── Tạo / chỉnh overlay tối phía sau các button ─────────────
        CreateMenuOverlay(canvasRT);

        // ─── Lưu scene ────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[Beautifier] Main Menu đã được làm đẹp thành công! ✅");
        EditorUtility.DisplayDialog("Beautify Main Menu", "✅ Main Menu đã được làm đẹp!\n\nMở Play Mode để kiểm tra.", "OK");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static void StyleButton(Button btn, string label, Color bgColor, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        if (btn == null) { Debug.LogWarning("[Beautifier] Thiếu button!"); return; }

        // Reset scale về 1 (trước đó scale = 4)
        RectTransform rt = btn.GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);

        // Màu button
        Image img = btn.GetComponent<Image>();
        if (img)
        {
            img.color  = bgColor;
            img.type   = Image.Type.Sliced; // rounded corners nếu sprite hỗ trợ
        }

        // Màu transition
        var colors = btn.colors;
        colors.normalColor      = bgColor;
        colors.highlightedColor = new Color(bgColor.r + 0.18f, bgColor.g + 0.08f, bgColor.b + 0.04f, 0.97f);
        colors.pressedColor     = new Color(bgColor.r * 0.7f,  bgColor.g * 0.7f,  bgColor.b * 0.7f,  1f);
        colors.selectedColor    = new Color(0.83f, 0.66f, 0.31f, 1f);
        colors.fadeDuration     = 0.15f;
        btn.colors = colors;

        // Text (TMP)
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp)
        {
            tmp.text      = label;
            tmp.fontSize  = 28;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color     = new Color(1f, 0.937f, 0.749f, 1f); // vàng nhạt
            tmp.alignment = TextAlignmentOptions.Center;
            // Thêm outline nhẹ
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color(0.8f, 0.4f, 0.1f, 1f);
        }

        EditorUtility.SetDirty(btn.gameObject);
        Debug.Log($"[Beautifier] Styled button: {btn.name}");
    }

    private static void AddHoverEffect(Button btn)
    {
        if (btn == null) return;
        if (btn.GetComponent<ButtonHoverEffect>() == null)
            btn.gameObject.AddComponent<ButtonHoverEffect>();
    }

    private static void CreateMenuOverlay(RectTransform canvasRT)
    {
        // Tìm hoặc tạo overlay panel mờ phía sau button
        Transform existing = canvasRT.Find("MenuOverlay");
        GameObject overlayGO;

        if (existing != null)
        {
            overlayGO = existing.gameObject;
        }
        else
        {
            overlayGO = new GameObject("MenuOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayGO.transform.SetParent(canvasRT, false);

            // Đặt overlay phía trước background nhưng phía sau button
            // Tìm thứ tự: đặt sau Background
            Transform bg = canvasRT.Find("Backgroud");
            if (bg != null)
                overlayGO.transform.SetSiblingIndex(bg.GetSiblingIndex() + 1);
            else
                overlayGO.transform.SetAsFirstSibling();
        }

        RectTransform overlayRT = overlayGO.GetComponent<RectTransform>();
        overlayRT.anchorMin = new Vector2(0.3f, 0.15f);
        overlayRT.anchorMax = new Vector2(0.7f, 0.85f);
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        Image overlayImg = overlayGO.GetComponent<Image>();
        if (overlayImg)
        {
            overlayImg.color = new Color(0f, 0f, 0f, 0.45f); // nền tối mờ
            overlayImg.raycastTarget = false;
        }

        EditorUtility.SetDirty(overlayGO);
    }
}
