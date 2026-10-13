// Scene-local adaptation; original TestQuest implementation stays unchanged.
namespace BeachBoatStory
{
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Readable, manually advanced introduction; restores only UI it actually hid.</summary>
public class OpeningEvent : MonoBehaviour
{
    public GameObject openingPanel;
    public TMP_Text storyText;
    public GameObject player;
    [Header("Reading")]
    public bool autoAdvance = false;
    [Min(2)] public float minimumReadingSeconds = 4;
    [Min(.03f)] public float secondsPerCharacter = .075f;
    static OpeningEvent current;
    public static bool IsOpeningActive => current != null && current.isActiveAndEnabled && !current.IsOpeningEnded;
    public bool IsOpeningEnded { get; private set; }
    public int CurrentLine { get; private set; } = -1;
    readonly Dictionary<Canvas, bool> hiddenCanvases = new Dictionary<Canvas, bool>();
    readonly Dictionary<SaBanHUD, bool> hiddenHUDs = new Dictionary<SaBanHUD, bool>();
    readonly Dictionary<Graphic, bool> hiddenGraphics = new Dictionary<Graphic, bool>();
    Canvas openingCanvas;
    bool advanceRequested, readyToAdvance;
    float lineReadyAt, skipHeld;

    readonly string[] storyLines = {
        "Năm 938.",
        "Sau nhiều năm đất nước chìm trong thời kỳ Bắc thuộc, vùng đất Giao Châu vẫn đang đứng trước những biến động lớn.",
        "Nhà Nam Hán ở phương Bắc đang tìm cách đưa quân tiến xuống Giao Châu, với ý định giành lại quyền kiểm soát vùng đất này.",
        "Trước nguy cơ xâm lược, Ngô Quyền tập hợp lực lượng, chuẩn bị chống lại quân Nam Hán.",
        "Ngô Quyền lựa chọn sông Bạch Đằng làm nơi quyết chiến.",
        "Tại đây, quân ta chuẩn bị một kế sách dựa vào địa hình và thủy triều của dòng sông.",
        "Những cọc gỗ được đẽo nhọn và bố trí dưới lòng sông, chờ thời cơ thích hợp.",
        "Nhưng trước khi trận chiến bắt đầu..."
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() => current = null;

    void Awake()
    {
        if (current != null && current != this) { enabled = false; return; }
        if (storyText == null && openingPanel != null) storyText = openingPanel.GetComponentInChildren<TMP_Text>(true);
        openingCanvas = openingPanel != null ? openingPanel.GetComponentInParent<Canvas>(true) : null;
        if (openingCanvas == null && storyText != null) openingCanvas = storyText.GetComponentInParent<Canvas>(true);
        if (openingPanel == null || storyText == null || openingCanvas == null)
        {
            Debug.LogError("OpeningEvent needs an opening panel, story text and parent Canvas.", this);
            IsOpeningEnded = true; enabled = false; return;
        }
        current = this;
        if (player == null) player = FindPlayerInScene();
        openingCanvas.enabled = true;
        openingCanvas.sortingOrder = 9999;
        openingPanel.SetActive(true);
        var background = openingPanel.GetComponent<Image>();
        if (background != null) background.color = new Color(0, 0, 0, 1);
        storyText.color = Color.white;
        storyText.text = "";
        RefreshHiddenUI();
    }

    IEnumerator Start()
    {
        // Story bootstrap creates its UI after Awake. Capture that UI once, not every frame.
        yield return null;
        RefreshHiddenUI();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        for (int i = 0; i < storyLines.Length; i++)
        {
            CurrentLine = i; advanceRequested = false; readyToAdvance = false;
            storyText.text = storyLines[i] + "\n\n<size=60%><color=#BBBBBB>[Space / Enter: tiếp tục]     [Giữ Esc: bỏ qua]</color></size>";
            yield return FadeText(0, 1, .35f);
            lineReadyAt = Time.unscaledTime; readyToAdvance = true;
            float readingTime = Mathf.Max(minimumReadingSeconds, storyLines[i].Length * secondsPerCharacter);
            while (!advanceRequested && (!autoAdvance || Time.unscaledTime - lineReadyAt < readingTime)) yield return null;
            readyToAdvance = false;
            yield return FadeText(1, 0, .25f);
        }
        EndOpening();
    }

    void Update()
    {
        if (!IsOpeningActive || current != this) return;
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) AdvanceLine();
        skipHeld = Input.GetKey(KeyCode.Escape) ? skipHeld + Time.unscaledDeltaTime : 0;
        if (skipHeld >= .8f) SkipOpening();
    }
    public void AdvanceLine()
    {
        if (readyToAdvance && Time.unscaledTime - lineReadyAt >= .25f) advanceRequested = true;
    }
    public void SkipOpening()
    {
        if (IsOpeningEnded) return;
        StopAllCoroutines(); EndOpening();
    }
    IEnumerator FadeText(float from, float to, float seconds)
    {
        for (float elapsed = 0; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        { if (storyText == null) yield break; storyText.alpha = Mathf.Lerp(from, to, elapsed / seconds); yield return null; }
        if (storyText != null) storyText.alpha = to;
    }
    public void RefreshHiddenUI()
    {
        if (!IsOpeningActive || openingCanvas == null) return;
        // Some scenes share their opening Canvas with quest panels. Keep only the intro branch visible.
        foreach (var graphic in openingCanvas.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.transform == openingPanel.transform || graphic.transform.IsChildOf(openingPanel.transform)) continue;
            if (!hiddenGraphics.ContainsKey(graphic)) hiddenGraphics.Add(graphic, graphic.enabled);
            graphic.enabled = false;
        }
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas == openingCanvas || canvas.transform.IsChildOf(openingCanvas.transform)) continue;
                if (!hiddenCanvases.ContainsKey(canvas)) hiddenCanvases.Add(canvas, canvas.enabled);
                canvas.enabled = false;
            }
            foreach (var hud in root.GetComponentsInChildren<SaBanHUD>(true))
            {
                if (!hiddenHUDs.ContainsKey(hud)) hiddenHUDs.Add(hud, hud.enabled);
                hud.enabled = false;
            }
        }
    }
    void RestoreUI()
    {
        foreach (var pair in hiddenCanvases) if (pair.Key != null) pair.Key.enabled = pair.Value;
        foreach (var pair in hiddenHUDs) if (pair.Key != null) pair.Key.enabled = pair.Value;
        foreach (var pair in hiddenGraphics) if (pair.Key != null) pair.Key.enabled = pair.Value;
        hiddenCanvases.Clear(); hiddenHUDs.Clear(); hiddenGraphics.Clear();
    }
    void EndOpening()
    {
        if (IsOpeningEnded) return;
        IsOpeningEnded = true;
        RestoreUI();
        if (player == null) player = FindPlayerInScene();
        if (player != null)
        {
            player.SetActive(true);
            var neutralCtrl = Resources.Load<RuntimeAnimatorController>("Player_Neutral");
            if (neutralCtrl != null)
            {
                foreach (var anim in player.GetComponentsInChildren<Animator>(true))
                {
                    anim.runtimeAnimatorController = neutralCtrl;
                    anim.SetBool("isWalking", false);
                    anim.SetBool("isRunning", false);
                }
            }
        }
        if (openingPanel != null) openingPanel.SetActive(false);
        if (BachDangStoryManager.Instance != null) BachDangStoryManager.Instance.UpdateUI();
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }
    void OnDisable()
    {
        if (current != this) return;
        StopAllCoroutines(); RestoreUI(); current = null;
    }

    public static GameObject FindPlayerInScene()
    {
        if (BeachBoatQuestSession.Instance != null && BeachBoatQuestSession.Instance.player != null)
            return BeachBoatQuestSession.Instance.player;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return null;
        GameObject fallback = null;
        foreach (var root in scene.GetRootGameObjects()) foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (fallback == null && t.GetComponent<Character_Movement>() != null) fallback = t.gameObject;
        }
        return fallback;
    }
}

}
