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
        Act2_CompleteReturnToCamp = 6 // Cắm cọc xong -> Hoàn thành Hồi 2, sẵn sàng Hồi 3
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
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) p = GameObject.Find("Main_Character");
        if (p == null) p = GameObject.Find("Player_Main_Animated");
        if (p != null) playerTransform = p.transform;
    }

    /// <summary>
    /// Hiển thị / ẩn Prompt chữ tương tác ví dụ: "[E] Đi đến Làng Ven Sông"
    /// </summary>
    public void SetInteractPrompt(bool show, string message = "")
    {
        if (interactPromptPanel != null)
        {
            interactPromptPanel.SetActive(show);
            if (show && interactPromptText != null)
            {
                interactPromptText.text = message;
            }
        }
    }

    /// <summary>
    /// Cập nhật UI hiển thị nhiệm vụ trên màn hình
    /// </summary>
    public void UpdateUI()
    {
        string title = "";
        string desc = "";

        switch (currentState)
        {
            case StoryState.Act1_ExploreCamp:
                title = "HỒI 1: KHỞI ĐẦU";
                desc = "- Khám phá doanh trại quân ta.\n- Lên đài diện kiến Chủ tướng Ngô Quyền.";
                break;

            case StoryState.Act1_GoToCampGate:
                title = "HỒI 1: LÊN ĐƯỜNG";
                desc = "- Di chuyển ra CỔNG DOANH TRẠI.\n- Bấm [E] để lên đường sang Làng Ven Sông.";
                break;

            case StoryState.Act2_CollectWoodInVillage:
                title = "HỒI 2: DÂN LÀNG ĐỒNG LÒNG";
                desc = $"- Khám phá làng ven sông và hỏi thăm dân làng.\n- Thu thập cọc gỗ: ({collectedWood}/{totalWoodRequired}) khúc gỗ";
                break;

            case StoryState.Act2_ReturnToCampGate:
                title = "HỒI 2: VẬN CHUYỂN CỌC GỖ";
                desc = "- Đã gom đủ 5 cọc gỗ!\n- Ra CỔNG LÀNG bấm [E] để quay về Doanh Trại.";
                break;

            case StoryState.Act2_ReportToNgoQuyen:
                title = "HỒI 2: NHẬN QUÂN LỆNH";
                desc = "- Đến đài chỉ huy gặp lại Chủ tướng Ngô Quyền.\n- Báo cáo số cọc gỗ đã chuẩn bị.";
                break;

            case StoryState.Act2_PlantStakesAtRiver:
                title = "HỒI 2: ĐÓNG CỌC BẠCH ĐẰNG";
                desc = "- Chạy ra mép nước sông Bạch Đằng.\n- Bấm [E] tại bãi cọc để đóng cọc xuống lòng sông.";
                break;

            case StoryState.Act2_CompleteReturnToCamp:
                title = "HỒI 2: HOÀN THÀNH TRẬN ĐỊA CỌC";
                desc = "★ Trận địa cọc ngầm đã sẵn sàng mai phục!\n- Quay về doanh trại nghe Chủ tướng phát lệnh xuất trận.";
                break;
        }

        if (objectiveUIPanel != null) objectiveUIPanel.SetActive(true);
        if (objectiveTitleText != null) objectiveTitleText.text = title;
        if (objectiveDescText != null) objectiveDescText.text = desc;

        // Đồng bộ luôn với các Text Quest có sẵn trong Scene (nếu có)
        var sceneQuestDesc = GameObject.Find("QuestDesc")?.GetComponent<TextMeshProUGUI>();
        if (sceneQuestDesc != null) sceneQuestDesc.text = desc;

        var sceneQuestTitle = GameObject.Find("QuestTitle")?.GetComponent<TextMeshProUGUI>();
        if (sceneQuestTitle != null) sceneQuestTitle.text = title;

        if (QuestManager.Instance != null)
        {
            if (QuestManager.Instance.questTitleText != null) QuestManager.Instance.questTitleText.text = title;
            if (QuestManager.Instance.questDescriptionText != null) QuestManager.Instance.questDescriptionText.text = desc;
        }
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
        if (currentState == StoryState.Act2_PlantStakesAtRiver)
        {
            currentState = StoryState.Act2_CompleteReturnToCamp;
            UpdateUI();
            Debug.Log("[BachDangStory] Hoan thanh cam coc song Bach Dang!");
        }
    }
}
