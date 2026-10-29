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
        Act2_Completed = 7,
        // ============ HỒI 3: TỔNG TIẾN CÔNG ============
        Act3_PrepareForBattle = 8,    // Gặp Ngô Quyền nhận lệnh tấn công
        Act3_WaitForLowTide = 9,      // Lên thuyền chờ triều rút
        Act3_BattleInProgress = 10,    // Trận chiến đang diễn ra
        Act3_Victory = 11,             // Chiến thắng - báo cáo Ngô Quyền
        Act1_Training = 12,            // Học hiệu lệnh trống trước khi rời trại
        Act2_SupplyPreparation = 13,   // Chuyển cọc và vật tư ra điểm tập kết
        Act3_Celebration = 14          // Báo công, kết thúc và ăn mừng
    }

    [Header("Trạng thái hiện tại")]
    public StoryState currentState = StoryState.Act1_ExploreCamp;

    [Header("Tiến độ nhặt gỗ (Hồi 2)")]
    public int collectedWood = 0;
    public int totalWoodRequired = 5;

    [Header("Tiến độ trận chiến (Hồi 3)")]
    [Tooltip("Số thuyền địch cần tiêu diệt")]
    public int totalEnemyShips = 5;
    [Tooltip("Số thuyền địch đã tiêu diệt")]
    public int destroyedEnemyShips = 0;

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
        if (GetComponent<BachDangBattleTimeline>() == null) gameObject.AddComponent<BachDangBattleTimeline>();
    }

    private void Start()
    {
        FindPlayer();
        UpdateUI();
    }

    public void TransitionTo(StoryState nextState, bool immediateEnvironment = false)
    {
        bool enteringBattle = currentState != nextState && nextState == StoryState.Act3_WaitForLowTide;
        bool beginningVillageWork = currentState != nextState && nextState == StoryState.Act2_SupplyPreparation;
        CampaignProgress.Instance?.Transition((int)currentState,(int)nextState);
        currentState = nextState;
        BachDangBattleTimeline.Instance?.ApplyStoryState(nextState, immediateEnvironment);
        if (enteringBattle) QuestRowboat.Instance?.PrepareForBattle();
        if (beginningVillageWork) VillagePreparationQuest.Instance?.BeginPreparation();
        QuestCheckpoint.Instance?.Capture(nextState);
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
                desc = "- Theo chỉ hướng tới cổng gỗ.\n- Qua cổng để sang làng ven sông.";
                break;

            case StoryState.Act1_Training:
                title = "Huấn luyện trước khi xuất quân";
                desc = "- Tới trống trận trong doanh trại.\n- Đánh đúng nhịp số 1–2–3 trước khi hết giờ.";
                break;

            case StoryState.Act2_CollectWoodInVillage:
                title = $"Gom cọc gỗ trong làng • {collectedWood}/{totalWoodRequired}";
                desc = "- Tìm các khúc gỗ đặt quanh nhà và sân làng.\n- Lại sát khúc gỗ, nhấn E để nhặt.\n- Gom đủ 5 khúc rồi chuẩn bị vật tư ở các sạp.";
                break;

            case StoryState.Act2_ReturnToCampGate:
                desc = "- Đã gom đủ 5 cọc gỗ!\n- Qua cổng làng để quay về doanh trại.";
                break;

            case StoryState.Act2_SupplyPreparation:
                title = "Chuẩn bị vật tư đóng cọc";
                var villageWork = VillagePreparationQuest.Instance;
                desc = $"- Giúp các sạp trong làng chuẩn bị vật tư: {villageWork?.CompletedCount ?? 0}/4.\n- Thắng trò chơi ở sạp gỗ, dây, gạo, lương khô rồi nhận hàng.";
                break;

            case StoryState.Act2_ReportToNgoQuyen:
                desc = "- Gặp lại Chủ tướng Ngô Quyền ở sân trại.\n- Báo cáo số cọc gỗ đã chuẩn bị.";
                break;

            case StoryState.Act2_PlantStakesAtRiver:
                var zone = RiverStakeZone.Instance;
                title = "Nhiệm vụ 2: Đóng cọc";
                desc = zone != null && zone.RecoveryRequired
                    ? "SỰ CỐ: Một bó cọc bị trôi!\n- Chèo tới bó cọc, dừng thuyền và vớt lại."
                    : zone != null && zone.IsPlanting
                    ? $"Tự dựng ba cọc mẫu • {StakeConstruction.Active?.CompletedBundles??0}/3\nChọn chỗ, chỉnh nghiêng, căn búa và thả đúng lực."
                    : "- Qua cổng ra bến và lên thuyền.\n- Chèo tới một điểm đánh dấu, dừng và bấm E.\n- Dựng ba cọc mẫu; quân dân sẽ hoàn thiện trận địa trong những ngày tiếp theo.";
                break;

            case StoryState.Act2_CompleteReturnToCamp:
                desc = "- Trận địa cọc ngầm đã sẵn sàng mai phục.\n- Quay về gặp Ngô Quyền để báo tin hoàn thành.";
                break;

            case StoryState.Act2_Completed:
                title = "Hồi 2 hoàn tất";
                desc = EnemyFleetDirector.Instance != null && EnemyFleetDirector.Instance.IsConfigured
                    ? (ArcheryTraining.Instance?.Completed==true?"Đã đạt bài tập cung.\n- Gặp Ngô Quyền nhận lệnh xuất quân.":"Trận địa cọc đã sẵn sàng.\n- Đến bãi tập cung trong doanh trại.\n- Trúng ít nhất 4/6 bia trước khi xuất quân.")
                    : "Trận địa cọc Bạch Đằng đã sẵn sàng. Quân ta chờ thời cơ đánh giặc.";
                break;

            case StoryState.Act3_PrepareForBattle:
                title = "Hồi 3: Tổng Tiến Công";
                desc = ArcheryTraining.Instance?.Completed==true?"- Đã đạt bài tập cung.\n- Gặp Ngô Quyền nhận lệnh xuất quân.":"- Tập cung bắt buộc trước khi xuất quân.\n- Đến bãi tập, bấm E tại vị trí bắn.\n- Bắn 6 mũi, trúng ít nhất 4 bia.";
                break;

            case StoryState.Act3_WaitForLowTide:
                title = "Nhiệm vụ 3: Nhử địch vào trận địa";
                desc = ArcheryTraining.Instance?.Completed==true?"- Triều cao đang che kín bãi cọc.\n- Qua cổng ra bến và lên thuyền nhẹ.\n- Nhử hạm đội Nam Hán tiến sâu vào cửa sông.":"- Cần đạt bài tập cung trước khi xuất chiến.\n- Về doanh trại, đứng tại vòng bắn và bấm E.\n- Bắn trúng ít nhất 4/6 bia rồi trở lại thuyền.";
                break;

            case StoryState.Act3_BattleInProgress:
                title = RiverBattleObjective.Title;
                desc = RiverBattleObjective.Description;
                break;

            case StoryState.Act3_Victory:
                title = "CHIẾN THẮNG BẠCH ĐẰNG 938";
                desc = "- Toàn bộ thuyền và lính Nam Hán đã bị đánh bại.\n- Thuyền quân ta đang rút về bến.\n- Trở về doanh trại báo tin chiến thắng.";
                break;

            case StoryState.Act3_Celebration:
                title = "ĐẠI THẮNG BẠCH ĐẰNG 938";
                desc = "Quân dân trở về đại bản doanh mừng chiến thắng.\nNước Việt bước vào thời kỳ độc lập, tự chủ.";
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
            TransitionTo(StoryState.Act1_Training);
            Debug.Log("[BachDangStory] Da nhan lenh. Hay luyen hieu trong truoc khi xuat quan!");
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
                TransitionTo(StoryState.Act2_SupplyPreparation);
            }
            else UpdateUI();
        }
    }

    public void OnTrainingComplete()
    {
        if (currentState != StoryState.Act1_Training) return;
        TransitionTo(StoryState.Act1_GoToCampGate);
        StoryDialogueUI.Instance?.Show("Quản quân", "Ngươi đã thuộc hiệu lệnh. Hãy qua cổng sang làng, cùng dân binh chuẩn bị cọc gỗ.");
    }

    public void OnSupplyPreparationComplete()
    {
        if (currentState != StoryState.Act2_SupplyPreparation) return;
        var village=VillagePreparationQuest.Instance;
        if(village!=null&&village.CompletedCount<village.stalls.Length)return;
        TransitionTo(StoryState.Act2_ReturnToCampGate);
        StoryDialogueUI.Instance?.Show("Dân binh", "Gỗ, dây buộc, gạo và lương khô đã sẵn sàng. Hãy về doanh trại báo Chủ tướng.");
    }

    public void OnVictoryReported()
    {
        if (currentState != StoryState.Act3_Victory) return;
        RecoverAtReport();
        TransitionTo(StoryState.Act3_Celebration);
        StoryDialogueUI.Instance?.Show("Quân dân Bạch Đằng", "Đại thắng! Quân dân cùng trở về doanh trại, nổi trống mừng nền tự chủ của nước Việt.");
    }

    /// <summary>
    /// Gọi khi người chơi về trại báo cáo Ngô Quyền lần 2
    /// </summary>
    public void OnReportToNgoQuyenAct2()
    {
        if (currentState == StoryState.Act2_ReportToNgoQuyen)
        {
            RecoverAtReport();
            TransitionTo(StoryState.Act2_PlantStakesAtRiver);
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
            TransitionTo(StoryState.Act2_CompleteReturnToCamp);
            Debug.Log("[BachDangStory] Hoan thanh cam coc song Bach Dang!");
        }
    }

    public void OnReportStakesComplete()
    {
        if (currentState != StoryState.Act2_CompleteReturnToCamp) return;
        RecoverAtReport();
        TransitionTo(StoryState.Act2_Completed);
        Debug.Log("[BachDangStory] Hoan tat Hoi 2: tran dia coc Bach Dang da san sang!");
    }

    // =================== HỒI 3: TỔNG TIẾN CÔNG ===================

    void RecoverAtReport()
    {
        GetPlayerTransform()?.GetComponent<PlayerVitals>()?.RecoverAfterReport();
    }

    /// <summary>
    /// Gọi khi gặp Ngô Quyền lần 3 để nhận lệnh tấn công
    /// </summary>
    public bool LureBriefed {get;private set;}
    public void RestoreLureBriefing(bool done){LureBriefed=done;}
    public void ShowArcheryReminder(){if(StoryDialogueUI.Instance?.IsOpen!=true)StoryDialogueUI.Instance?.Show("Ngô Quyền",PreparationBriefing.Archery);}
    public void OnLureOrdersAcknowledged(){LureBriefed=true;OnReadyForBattle();}
    public void OnTalkToNgoQuyenAct3()
    {
        if (EnemyFleetDirector.Instance == null || !EnemyFleetDirector.Instance.IsConfigured ||
            RiverStakeZone.Instance == null || !RiverStakeZone.Instance.IsComplete) return;
        if (currentState == StoryState.Act2_Completed || currentState == StoryState.Act3_PrepareForBattle)
        {
            if(ArcheryTraining.Instance?.Completed!=true){TransitionTo(StoryState.Act3_PrepareForBattle);return;}
            LureBriefed=true;TransitionTo(StoryState.Act3_WaitForLowTide);
            Debug.Log("[BachDangStory] Ngo Quyen ra lenh tong tan cong! Hay len thuyen cho trieu rut.");
        }
    }

    /// <summary>
    /// Gọi khi người chơi lên thuyền sẵn sàng chiến đấu
    /// </summary>
    public void OnReadyForBattle()
    {
        var director = EnemyFleetDirector.Instance;
        if (director == null || !director.IsConfigured || QuestRowboat.Instance == null || !QuestRowboat.Instance.IsAboard ||
            RiverStakeZone.Instance == null || !RiverStakeZone.Instance.IsComplete) return;
        if (currentState == StoryState.Act3_WaitForLowTide)
        {
            if(ArcheryTraining.Instance?.Completed!=true){UpdateUI();ShowArcheryReminder();return;}
            if(!LureBriefed){if(StoryDialogueUI.Instance?.IsOpen!=true)StoryDialogueUI.Instance?.Show("Ngô Quyền",PreparationBriefing.Lure,OnLureOrdersAcknowledged);return;}
            totalEnemyShips = director.ShipCount; destroyedEnemyShips = 0;
            TransitionTo(StoryState.Act3_BattleInProgress);
            director.StartBattle();
            Debug.Log("[BachDangStory] Tran chien bat dau: nhu dich luc trieu cao, phan cong khi trieu rut.");
        }
    }

    /// <summary>
    /// Gọi mỗi khi tiêu diệt được 1 thuyền địch
    /// </summary>
    public void OnEnemyShipDestroyed()
    {
        if (currentState != StoryState.Act3_BattleInProgress) return;
        destroyedEnemyShips++;
        UpdateUI();
        Debug.Log($"[BachDangStory] Tieu diet thuyen dich: {destroyedEnemyShips}/{totalEnemyShips}");

        if (destroyedEnemyShips >= totalEnemyShips)
        {
            OnBattleVictory();
        }
    }

    /// <summary>
    /// Gọi khi tiêu diệt đủ số thuyền địch
    /// </summary>
    public void OnBattleVictory()
    {
        if (currentState != StoryState.Act3_BattleInProgress) return;
        if(destroyedEnemyShips<totalEnemyShips)return;
        if(EnemyFleetDirector.Instance!=null&&(!EnemyFleetDirector.Instance.AllShipsSunk||EnemyFleetDirector.Instance.RemainingEnemies>0))return;
        TransitionTo(StoryState.Act3_Victory);
        BachDangBattleTimeline.Instance?.MarkVictory();
        Debug.Log("[BachDangStory] DAI THANG BACH DANG 938! Nuoc Nam doc lap tu nay!");
    }

    void OnDestroy()
    {
        if (TideSystem.Instance != null) TideSystem.Instance.ReleaseTide(this);
        if (Instance == this) Instance = null;
    }
}

}
