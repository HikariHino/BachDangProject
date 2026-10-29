// Adapted from origin/thai 03aad75 for beachBoat only.
namespace BeachBoatStory
{
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
    DialogueVoice voice;
    float revealElapsed,revealDuration;
    int revealed;

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
        voice=gameObject.AddComponent<DialogueVoice>();
        BuildUI();
    }

    private void BuildUI()
    {
        var canvasObject = new GameObject("DialogueCanvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        panel = new GameObject("DialoguePanel");
        panel.transform.SetParent(canvasObject.transform, false);

        var background = panel.AddComponent<Image>();
        background.color = new Color(0.06f, 0.045f, 0.025f, 0.94f);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.12f, 0.04f);
        panelRect.anchorMax = new Vector2(0.88f, 0.36f);
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
        hint.text = "E: hiện hết chữ / tiếp tục";
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
        StoryFont.Apply(text);
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

    public void Show(string speaker, string line, System.Action onClosed = null,Transform actor=null)
    {
        if (panel == null) BuildUI();

        lastInputFrame = Time.frameCount;
        speakerText.text = speaker;
        bodyText.text = line;
        onDialogueClosed = onClosed;
        panel.SetActive(true);
        string identity=DialogueVoice.ActorIdentity(speaker,actor);bool female=DialogueVoice.ActorIsFemale(speaker,actor);
        StartVoice(speaker,line,identity,female,DialogueVoice.ActorProfile(speaker,actor,identity,female));
        BeachBoatSoundscape.Instance?.PlayInteraction();
    }

    void StartVoice(string speaker,string line,string identity,bool female,string profile=null)
    {
        voice.Play(speaker,line,identity,female,profile);bodyText.maxVisibleCharacters=int.MaxValue;bodyText.ForceMeshUpdate();
        revealDuration=voice.Duration>0?Mathf.Max(.25f,voice.Duration*.92f):Mathf.Max(.25f,bodyText.textInfo.characterCount/38f);
        revealElapsed=0;revealed=0;bodyText.maxVisibleCharacters=0;
    }
    public bool TryAdvance()
    {
        if(!IsOpen||BachDangFrontEnd.IsBlocking)return false;
        if(bodyText.maxVisibleCharacters<bodyText.textInfo.characterCount)
        {bodyText.maxVisibleCharacters=int.MaxValue;revealed=bodyText.textInfo.characterCount;revealElapsed=revealDuration;return true;}
        Close();return true;
    }

    public bool IsOpen => panel != null && panel.activeSelf;

    public void Close()
    {
        voice?.Stop();
        if(IsOpen)BeachBoatSoundscape.Instance?.PlayInteraction();
        lastInputFrame = Time.frameCount;

        if (panel != null)
        {
            panel.SetActive(false);
        }

        var callback = onDialogueClosed;
        onDialogueClosed = null;
        callback?.Invoke();
    }
    public void DismissForCheckpoint(){onDialogueClosed=null;Close();}
    public BachDangSaveData.Dialogue CaptureSave()=>IsOpen?new BachDangSaveData.Dialogue {
        active=true,speaker=speakerText.text,line=bodyText.text,action=onDialogueClosed?.Method.Name,voiceIdentity=voice.Identity,voiceProfile=voice.ProfileId,femaleVoice=voice.Female}:null;
    public void RestoreSave(BachDangSaveData.Dialogue data)
    {
        if(data==null||!data.active)return;
        if(data.speaker=="Ngô Quyền"&&data.line!=null&&(data.line.Contains("dựng ba cọc mẫu")||data.line.StartsWith("Tốt lắm. Mang năm cọc"))){data.line=PreparationBriefing.Stakes;data.voiceProfile="commander_brief";}
        if(data.speaker=="Ngô Quyền"&&data.line!=null&&data.line.Contains("cung")&&data.line.Contains("bấm E")){data.line=PreparationBriefing.Archery;data.voiceProfile="commander_brief";}
        var story=BachDangStoryManager.Instance;System.Action callback=null;
        // Save only known story actions, never deserialize executable delegate data.
        switch(data.action)
        {
            case nameof(BachDangStoryManager.OnTalkToNgoQuyenAct1):callback=story.OnTalkToNgoQuyenAct1;break;
            case nameof(BachDangStoryManager.OnTrainingComplete):callback=story.OnTrainingComplete;break;
            case nameof(BachDangStoryManager.OnSupplyPreparationComplete):callback=story.OnSupplyPreparationComplete;break;
            case nameof(BachDangStoryManager.OnReportToNgoQuyenAct2):callback=story.OnReportToNgoQuyenAct2;break;
            case nameof(BachDangStoryManager.OnReportStakesComplete):callback=story.OnReportStakesComplete;break;
            case nameof(BachDangStoryManager.OnTalkToNgoQuyenAct3):callback=story.OnTalkToNgoQuyenAct3;break;
            case nameof(BachDangStoryManager.OnLureOrdersAcknowledged):callback=story.OnLureOrdersAcknowledged;break;
            case nameof(BachDangStoryManager.OnVictoryReported):callback=story.OnVictoryReported;break;
        }
        Show(data.speaker,data.line,callback);
        if(!string.IsNullOrEmpty(data.voiceIdentity))StartVoice(data.speaker,data.line,data.voiceIdentity,data.femaleVoice,data.voiceProfile);
    }

    private void Update()
    {
        if(BachDangFrontEnd.IsBlocking)return;
        if(IsOpen&&bodyText.maxVisibleCharacters<bodyText.textInfo.characterCount)
        {
            revealElapsed+=Time.unscaledDeltaTime;
            int count=Mathf.Min(bodyText.textInfo.characterCount,Mathf.FloorToInt(bodyText.textInfo.characterCount*revealElapsed/revealDuration));
            if(count>revealed)
            {
                bool audible=false;
                for(int i=revealed;i<count;i++)if(!char.IsWhiteSpace(bodyText.textInfo.characterInfo[i].character)){audible=true;break;}
                bodyText.maxVisibleCharacters=count;revealed=count;if(audible)voice.SubtitleTick();
            }
        }
        if (IsOpen &&
            !InputConsumedThisFrame &&
            (Input.GetKeyDown(KeyCode.E) ||
             Input.GetKeyDown(KeyCode.Space) ||
             Input.GetKeyDown(KeyCode.Escape)))
        {
            TryAdvance();
        }
    }
    void OnDisable()=>voice?.Stop();
}

}
