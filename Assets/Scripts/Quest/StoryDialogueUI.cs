using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StoryDialogueUI : MonoBehaviour
{
    public static StoryDialogueUI Instance { get; private set; }

    private GameObject panel;
    private TextMeshProUGUI speakerText;
    private TextMeshProUGUI bodyText;
    private System.Action onDialogueClosed;
    private static int lastInputFrame = -1;

    public static bool InputConsumedThisFrame => lastInputFrame == Time.frameCount;

    public static bool TryConsumeInput()
    {
        if (InputConsumedThisFrame) return false;

        lastInputFrame = Time.frameCount;
        return true;
    }

    public static void EnsureExists()
    {
        if (Instance != null) return;

        var existing = GameObject.Find("StoryDialogueUI");
        var host = existing != null ? existing : new GameObject("StoryDialogueUI");

        if (host.GetComponent<StoryDialogueUI>() == null)
        {
            host.AddComponent<StoryDialogueUI>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildUI();
    }

    private void BuildUI()
    {
        var canvasObject = new GameObject("DialogueCanvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        panel = new GameObject("DialoguePanel");
        panel.transform.SetParent(canvasObject.transform, false);

        var background = panel.AddComponent<Image>();
        background.color = new Color(0.06f, 0.045f, 0.025f, 0.94f);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.12f, 0.04f);
        panelRect.anchorMax = new Vector2(0.88f, 0.25f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        speakerText = CreateText(
            "Speaker",
            panel.transform,
            30,
            FontStyles.Bold,
            new Color(1f, 0.82f, 0.38f)
        );
        SetRect(
            speakerText.rectTransform,
            new Vector2(0.04f, 0.69f),
            new Vector2(0.96f, 0.94f)
        );

        bodyText = CreateText(
            "Body",
            panel.transform,
            26,
            FontStyles.Normal,
            Color.white
        );
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        SetRect(
            bodyText.rectTransform,
            new Vector2(0.04f, 0.18f),
            new Vector2(0.96f, 0.69f)
        );

        var hint = CreateText(
            "Hint",
            panel.transform,
            18,
            FontStyles.Italic,
            new Color(0.8f, 0.77f, 0.7f)
        );
        hint.text = "Nhấn E để tiếp tục";
        hint.alignment = TextAlignmentOptions.Right;
        SetRect(
            hint.rectTransform,
            new Vector2(0.04f, 0.02f),
            new Vector2(0.96f, 0.18f)
        );

        panel.SetActive(false);
    }

    private static TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        float fontSize,
        FontStyles fontStyle,
        Color color
    )
    {
        var item = new GameObject(objectName);
        item.transform.SetParent(parent, false);

        var text = item.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;

        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public void Show(string speaker, string line, System.Action onClosed = null)
    {
        if (panel == null) BuildUI();

        lastInputFrame = Time.frameCount;
        speakerText.text = speaker;
        bodyText.text = line;
        onDialogueClosed = onClosed;
        panel.SetActive(true);
    }

    public bool IsOpen => panel != null && panel.activeSelf;

    public void Close()
    {
        lastInputFrame = Time.frameCount;

        if (panel != null)
        {
            panel.SetActive(false);
        }

        var callback = onDialogueClosed;
        onDialogueClosed = null;
        callback?.Invoke();
    }

    private void Update()
    {
        if (IsOpen &&
            !InputConsumedThisFrame &&
            (Input.GetKeyDown(KeyCode.E) ||
             Input.GetKeyDown(KeyCode.Space) ||
             Input.GetKeyDown(KeyCode.Escape)))
        {
            Close();
        }
    }
}