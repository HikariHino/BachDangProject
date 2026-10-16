// Adapted from origin/thai 03aad75 for beachBoat only.
namespace BeachBoatStory
{
using UnityEngine;
using TMPro;

/// <summary>
/// Quản lý tiến trình cốt truyện chính Bạch Đằng 938:
/// Hồi 1 (Doanh trại & Lên đường) -> Hồi 2 (Dân làng gom gỗ & Cắm cọc Bạch Đằng).
/// </summary>
public class BachDangStoryManager : MonoBehaviour
{
    public static BachDangStoryManager Instance;

    public enum StoryState
    {
        Act1_ExploreCamp = 0,         // Khám phá doanh trại -> Gặp Ngô Quyền
        Act1_GoToCampGate = 1,        // Nhận lệnh xong -> Đi ra Cổng Trại bấm E qua Làng
        Act2_CollectWoodInVillage = 2,// Ở Làng -> Thu thập 5 khúc cọc gỗ
        Act2_ReturnToCampGate = 3,    // Thu thập đủ 5 gỗ -> Ra Cổng Làng bấm E về Trại
        Act2_ReportToNgoQuyen = 4,    // Về Trại -> Báo cáo Ngô Quyền nhận lệnh cắm cọc
        Act2_PlantStakesAtRiver = 5,  // Ra bờ sông cắm cọc Bạch Đằng
        Act2_CompleteReturnToCamp = 6, // Cắm cọc xong -> báo cáo lần cuối
        Act2_Completed = 7
    }

    [Header("Trạng thái hiện tại")]
    public StoryState currentState = StoryState.Act1_ExploreCamp;

    [Header("Tiến độ nhặt gỗ (Hồi 2)")]
    public int collectedWood = 0;
    public int totalWoodRequired = 5;

    [Header("UI Cốt truyện")]
    public TextMeshProUGUI objectiveTitleText;
    public TextMeshProUGUI objectiveDescText;
    public GameObject objectiveUIPanel;
    public TextMeshProUGUI interactPromptText;
    public GameObject interactPromptPanel;

    [Header("Điểm dịch chuyển Landmark")]
    public Transform campSpawnPoint;
    public Transform campGatePoint;
    public Transform villageSpawnPoint;
    public Transform villageGatePoint;
    public Transform riverStakePoint;

    private Transform playerTransform;
    private bool promptVisible;
    private Object promptOwner;
    private int promptFrame = -1;
    private string promptMessage = string.Empty;
    private GUIStyle promptBoxStyle;
    private GUIStyle promptTextStyle;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        FindPlayer();
        UpdateUI();
    }

    public Transform GetPlayerTransform()
    {
        if (playerTransform == null) FindPlayer();
        return playerTransform;
    }

    private void FindPlayer()
    {
        GameObject p = OpeningEvent.FindPlayerInScene();
        if (p != null) playerTransform = p.transform;
    }

    /// <summary>
    /// Hiển thị / ẩn Prompt chữ tương tác ví dụ: "[E] Đi đến Làng Ven Sông"
    /// </summary>
    public void SetInteractPrompt(bool show, string message = "", Object owner = null)
    {
        // Leaving one interaction must not hide another nearby interaction's prompt.
        if (!show && owner != null && promptOwner != owner) return;
        promptOwner = show ? owner : null;
        promptFrame = show ? Time.frameCount : -1;
        promptVisible = show;
        promptMessage = message;
        if (interactPromptText != null && show) interactPromptText.text = message;
        if (interactPromptPanel != null)
            interactPromptPanel.SetActive(show && !OpeningEvent.IsOpeningActive);
    }

    private void LateUpdate()
    {
        // Interactables renew their prompt each Update. It expires when out of range,
        // disabled, destroyed, or hidden by dialogue, without polling the whole scene.
        if (promptFrame != Time.frameCount || BeachBoatQuestSession.InteractionBlocked)
        {
            promptVisible = false;
            promptOwner = null;
        }
        if (interactPromptPanel != null && interactPromptPanel.activeSelf != promptVisible)
            interactPromptPanel.SetActive(promptVisible);
    }

    private void OnGUI()
    {
        if (interactPromptPanel != null || !promptVisible || OpeningEvent.IsOpeningActive ||
            (StoryDialogueUI.Instance != null && StoryDialogueUI.Instance.IsOpen)) return;
        if (promptBoxStyle == null)
        {
            promptBoxStyle = new GUIStyle(GUI.skin.box);
            promptBoxStyle.normal.background = MakePromptTexture();
            promptTextStyle = new GUIStyle(GUI.skin.label)
            {
                font = StoryFont.LegacyFont,
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.height / 45, 16, 24),
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }
        float width = Mathf.Min(520f, Screen.width * 0.46f);
        var rect = new Rect(Screen.width - width - 24f, Screen.height - 76f, width, 52f);
        GUI.Box(rect, GUIContent.none, promptBoxStyle);
        GUI.Label(rect, promptMessage, promptTextStyle);
    }

    private static Texture2D MakePromptTexture()
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, new Color(0.025f, 0.035f, 0.055f, 0.9f));
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Cập nhật UI hiển thị nhiệm vụ trên màn hình
    /// </summary>
    public void UpdateUI()
    {
        // Nếu đoạn mở đầu (OpeningEvent) đang chiếu, tuyệt đối KHÔNG bật bảng nhiệm vụ!
        if (OpeningEvent.IsOpeningActive)
        {
            if (objectiveUIPanel != null) objectiveUIPanel.SetActive(false);
            if (interactPromptPanel != null) interactPromptPanel.SetActive(false);
            return;
        }

        string title = "Nhiệm vụ";
        string desc = "";

        switch (currentState)
        {
            case StoryState.Act1_ExploreCamp:
                desc = "- Khám phá doanh trại quân ta.\n- Gặp Chủ tướng Ngô Quyền ở sân trại.";
                break;

            case StoryState.Act1_GoToCampGate:
                desc = "- Theo chỉ hướng tới cổng gỗ.\n- Bấm [E] ở cổng để sang làng ven sông.";
                break;

            case StoryState.Act2_CollectWoodInVillage:
                desc = $"- Thăm hỏi bà con làng ven sông.\n- Thu thập cọc gỗ: ({collectedWood}/{totalWoodRequired}) khúc";
                break;

            case StoryState.Act2_ReturnToCampGate:
                desc = "- Đã gom đủ 5 cọc gỗ!\n- Ra cổng làng bấm [E] để quay về doanh trại.";
                break;

            case StoryState.Act2_ReportToNgoQuyen:
                desc = "- Gặp lại Chủ tướng Ngô Quyền ở sân trại.\n- Báo cáo số cọc gỗ đã chuẩn bị.";
                break;

            case StoryState.Act2_PlantStakesAtRiver:
                var zone = RiverStakeZone.Instance;
                title = "Nhiệm vụ 2: Đóng cọc";
                desc = zone != null && zone.IsPlanting
                    ? $"Đang đóng cọc...\nĐã hoàn thành: {zone.PlantedCount}/{zone.RequiredCount} vị trí"
                    : $"- Qua cổng ra bến, bấm [E] lên thuyền.\n- Chèo tới điểm đánh dấu dưới sông, dừng và bấm [E].\n- Tiến độ: {zone?.PlantedCount ?? 0}/{zone?.RequiredCount ?? totalWoodRequired}";
                break;

            case StoryState.Act2_CompleteReturnToCamp:
                desc = "- Trận địa cọc ngầm đã sẵn sàng mai phục.\n- Quay về gặp Ngô Quyền để báo tin hoàn thành.";
                break;

            case StoryState.Act2_Completed:
                title = "Hồi 2 hoàn tất";
                desc = "Trận địa cọc Bạch Đằng đã sẵn sàng. Quân ta chờ thời cơ đánh giặc.";
                break;
        }

        if (objectiveUIPanel != null) objectiveUIPanel.SetActive(true);
        if (objectiveTitleText != null) objectiveTitleText.text = title;
        if (objectiveDescText != null) objectiveDescText.text = desc;

    }

    /// <summary>
    /// Gọi khi nói chuyện với Ngô Quyền ở Hồi 1
    /// </summary>
    public void OnTalkToNgoQuyenAct1()
    {
        if (currentState == StoryState.Act1_ExploreCamp)
        {
            currentState = StoryState.Act1_GoToCampGate;
            UpdateUI();
            Debug.Log("[BachDangStory] Da nhan lenh Ngo Quyen. Hay di ra Cong Trai!");
        }
    }

    /// <summary>
    /// Gọi khi người chơi nhặt được 1 khúc gỗ trong Làng
    /// </summary>
    public void OnCollectWood()
    {
        if (currentState == StoryState.Act2_CollectWoodInVillage)
        {
            collectedWood++;
            if (collectedWood >= totalWoodRequired)
            {
                currentState = StoryState.Act2_ReturnToCampGate;
            }
            UpdateUI();
        }
    }

    /// <summary>
    /// Gọi khi người chơi về trại báo cáo Ngô Quyền lần 2
    /// </summary>
    public void OnReportToNgoQuyenAct2()
    {
        if (currentState == StoryState.Act2_ReportToNgoQuyen)
        {
            currentState = StoryState.Act2_PlantStakesAtRiver;
            UpdateUI();
            Debug.Log("[BachDangStory] Ngo Quyen giao lenh cam coc song Bach Dang!");
        }
    }

    /// <summary>
    /// Gọi khi người chơi cắm xong cọc ở bờ sông
    /// </summary>
    public void OnPlantStakesComplete()
    {
        if (currentState == StoryState.Act2_PlantStakesAtRiver && RiverStakeZone.Instance != null && RiverStakeZone.Instance.IsComplete)
        {
            currentState = StoryState.Act2_CompleteReturnToCamp;
            UpdateUI();
            Debug.Log("[BachDangStory] Hoan thanh cam coc song Bach Dang!");
        }
    }

    public void OnReportStakesComplete()
    {
        if (currentState != StoryState.Act2_CompleteReturnToCamp) return;
        currentState = StoryState.Act2_Completed;
        UpdateUI();
        Debug.Log("[BachDangStory] Hoan tat Hoi 2: tran dia coc Bach Dang da san sang!");
    }
}

}
