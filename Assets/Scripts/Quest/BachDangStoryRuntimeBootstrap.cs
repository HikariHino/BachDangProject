using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Tự động khởi tạo và kết nối toàn bộ hệ thống Hồi 1 & Hồi 2:
/// - Màn hình Fade điện ảnh (ScreenFader)
/// - UI Nhiệm vụ góc trên & Chữ nhắc phím [E] góc dưới
/// - Cổng Doanh trại -> Cổng Làng (CampGateTransition)
/// - 5 khúc cọc gỗ trong Làng (WoodCollectible)
/// - Điểm cắm cọc bờ sông Bạch Đằng (RiverStakeZone)
/// </summary>
public static class BachDangStoryRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeOnPlay()
    {
        SetupAllStoryElements();
    }

#if UNITY_EDITOR
    [MenuItem("BachDang/Khởi tạo cốt truyện Hồi 1 & 2 trong Scene")]
    public static void SetupInEditor()
    {
        SetupAllStoryElements();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[BachDangStory] Đã tạo và kết nối toàn bộ thành phần Hồi 1 & 2 trong Scene thành công!");
    }
#endif

    public static void SetupAllStoryElements()
    {
        // 1. Tạo hoặc lấy ScreenFader
        if (ScreenFader.Instance == null)
        {
            GameObject faderObj = GameObject.Find("ScreenFader");
            if (faderObj == null) faderObj = new GameObject("ScreenFader");
            faderObj.AddComponent<ScreenFader>();
        }

        // 2. Tạo UI HUD Cốt truyện & Nhắc phím tương tác
        SetupStoryUI(out GameObject objectivePanel, out TextMeshProUGUI titleText, out TextMeshProUGUI descText,
                     out GameObject promptPanel, out TextMeshProUGUI promptText);

        // 3. Tạo hoặc lấy StoryManager
        GameObject storyObj = GameObject.Find("BachDangStoryManager");
        if (storyObj == null) storyObj = new GameObject("BachDangStoryManager");
        var storyManager = storyObj.GetComponent<BachDangStoryManager>() ?? storyObj.AddComponent<BachDangStoryManager>();

        storyManager.objectiveUIPanel = objectivePanel;
        storyManager.objectiveTitleText = titleText;
        storyManager.objectiveDescText = descText;
        storyManager.interactPromptPanel = promptPanel;
        storyManager.interactPromptText = promptText;

        // 4. Kết nối Ngô Quyền
        GameObject ngoQuyen = GameObject.Find("NPC_NgoQuyen") ?? GameObject.Find("1_DaiBanDoanh_NgoQuyen");
        if (ngoQuyen != null)
        {
            if (ngoQuyen.GetComponent<NPCQuestGiver>() == null)
            {
                ngoQuyen.AddComponent<NPCQuestGiver>();
            }
        }

        // 5. Tạo Cổng Doanh Trại (CampGate)
        GameObject campGate = GameObject.Find("CampGate_ToVillage");
        if (campGate == null)
        {
            campGate = new GameObject("CampGate_ToVillage");
            campGate.transform.position = new Vector3(220f, 17.5f, -1245f);
            var trans = campGate.AddComponent<CampGateTransition>();
            trans.gateType = CampGateTransition.GateType.CampGate;
            trans.interactDistance = 10f;
        }

        // 6. Tạo Cổng Làng (VillageGate) & Điểm Spawn Làng
        GameObject villageGate = GameObject.Find("VillageGate_ToCamp");
        if (villageGate == null)
        {
            villageGate = new GameObject("VillageGate_ToCamp");
            villageGate.transform.position = new Vector3(238f, 18.2f, -1215f);
            var trans = villageGate.AddComponent<CampGateTransition>();
            trans.gateType = CampGateTransition.GateType.VillageGate;
            trans.interactDistance = 10f;
        }

        storyManager.campSpawnPoint = campGate.transform;
        storyManager.villageSpawnPoint = villageGate.transform;

        // Kết nối đích đến giữa 2 cổng
        var cTrans = campGate.GetComponent<CampGateTransition>();
        if (cTrans != null) cTrans.destinationTarget = villageGate.transform;

        var vTrans = villageGate.GetComponent<CampGateTransition>();
        if (vTrans != null) vTrans.destinationTarget = campGate.transform;

        // 7. Tạo 5 khúc cọc gỗ tại các vị trí trong Làng
        SetupVillageWoodLogs();

        // 8. Tạo Điểm Cắm Cọc Bờ Sông (RiverStakeZone)
        SetupRiverStakeZone(storyManager);

        storyManager.UpdateUI();
    }

    private static void SetupStoryUI(out GameObject objectivePanel, out TextMeshProUGUI titleText, out TextMeshProUGUI descText,
                                     out GameObject promptPanel, out TextMeshProUGUI promptText)
    {
        GameObject canvasObj = GameObject.Find("StoryUI_Canvas");
        Canvas canvas;
        if (canvasObj == null)
        {
            canvasObj = new GameObject("StoryUI_Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        else
        {
            canvas = canvasObj.GetComponent<Canvas>();
        }

        // Kiểm tra xem Scene đã có sẵn QuestPanel/QuestDesc chưa để dùng luôn, tránh bị 2 bảng đè nhau
        var existingQuestDesc = GameObject.Find("QuestDesc")?.GetComponent<TextMeshProUGUI>();
        var existingQuestTitle = GameObject.Find("QuestTitle")?.GetComponent<TextMeshProUGUI>();

        if (existingQuestDesc != null)
        {
            objectivePanel = existingQuestDesc.transform.parent != null ? existingQuestDesc.transform.parent.gameObject : existingQuestDesc.gameObject;
            descText = existingQuestDesc;
            titleText = existingQuestTitle;
        }
        else
        {
            Transform objPanelTr = canvasObj.transform.Find("ObjectivePanel");
            if (objPanelTr == null)
            {
                objectivePanel = new GameObject("ObjectivePanel");
                objectivePanel.transform.SetParent(canvasObj.transform, false);

                var img = objectivePanel.AddComponent<Image>();
                img.color = new Color(0.08f, 0.08f, 0.1f, 0.75f); // Nền mờ phong cách cổ điển

                RectTransform rt = objectivePanel.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(25f, -25f);
                rt.sizeDelta = new Vector2(380f, 120f);

                // Tiêu đề
                GameObject tObj = new GameObject("TitleText");
                tObj.transform.SetParent(objectivePanel.transform, false);
                titleText = tObj.AddComponent<TextMeshProUGUI>();
                titleText.fontSize = 20;
                titleText.fontStyle = FontStyles.Bold;
                titleText.color = new Color(1f, 0.85f, 0.35f, 1f);
                RectTransform trt = tObj.GetComponent<RectTransform>();
                trt.anchorMin = new Vector2(0.05f, 0.65f);
                trt.anchorMax = new Vector2(0.95f, 0.95f);
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;

                // Mô tả
                GameObject dObj = new GameObject("DescText");
                dObj.transform.SetParent(objectivePanel.transform, false);
                descText = dObj.AddComponent<TextMeshProUGUI>();
                descText.fontSize = 15;
                descText.color = Color.white;
                RectTransform drt = dObj.GetComponent<RectTransform>();
                drt.anchorMin = new Vector2(0.05f, 0.08f);
                drt.anchorMax = new Vector2(0.95f, 0.65f);
                drt.offsetMin = Vector2.zero;
                drt.offsetMax = Vector2.zero;
            }
            else
            {
                objectivePanel = objPanelTr.gameObject;
                titleText = objPanelTr.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
                descText = objPanelTr.Find("DescText")?.GetComponent<TextMeshProUGUI>();
            }
        }

        // --- Panel Nhắc phím [E] (Ở giữa gần đáy màn hình) ---
        Transform promptPanelTr = canvasObj.transform.Find("InteractPromptPanel");
        if (promptPanelTr == null)
        {
            promptPanel = new GameObject("InteractPromptPanel");
            promptPanel.transform.SetParent(canvasObj.transform, false);

            var img = promptPanel.AddComponent<Image>();
            img.color = new Color(0.12f, 0.1f, 0.06f, 0.85f);

            RectTransform rt = promptPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.18f);
            rt.anchorMax = new Vector2(0.5f, 0.18f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(480f, 50f);

            GameObject pTextObj = new GameObject("PromptText");
            pTextObj.transform.SetParent(promptPanel.transform, false);
            promptText = pTextObj.AddComponent<TextMeshProUGUI>();
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.fontSize = 20;
            promptText.fontStyle = FontStyles.Bold;
            promptText.color = new Color(1f, 0.95f, 0.6f, 1f);

            RectTransform prt = pTextObj.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            promptPanel.SetActive(false);
        }
        else
        {
            promptPanel = promptPanelTr.gameObject;
            promptText = promptPanelTr.Find("PromptText")?.GetComponent<TextMeshProUGUI>();
        }
    }

    private static void SetupVillageWoodLogs()
    {
        Vector3[] woodPositions = new Vector3[]
        {
            new Vector3(252f, 18.5f, -1225f), // Bên hiên nhà lớn
            new Vector3(215f, 17.6f, -1202f), // Góc nhà tranh nhỏ
            new Vector3(228f, 17.7f, -1185f), // Khoảng sân giữa hai nhà
            new Vector3(268f, 18.2f, -1212f), // Đống củi ven rào
            new Vector3(242f, 18.0f, -1238f)  // Cạnh bờ dốc vào làng
        };

        // Tìm model gỗ CayGo
        GameObject woodPrefab = null;
#if UNITY_EDITOR
        woodPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/ModelAI/CayGo/CayGo/CayGo.fbx");
#endif

        for (int i = 0; i < woodPositions.Length; i++)
        {
            string logName = $"Village_Wood_{i + 1}";
            GameObject logObj = GameObject.Find(logName);
            if (logObj == null)
            {
                if (woodPrefab != null)
                {
                    logObj = Object.Instantiate(woodPrefab);
                    logObj.name = logName;
                }
                else
                {
                    logObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    logObj.name = logName;
                    logObj.transform.localScale = new Vector3(0.6f, 1.5f, 0.6f);
                    logObj.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 180f), 0f);
                }

                logObj.transform.position = woodPositions[i];

                if (logObj.GetComponent<Collider>() == null)
                {
                    var col = logObj.AddComponent<BoxCollider>();
                    col.size = new Vector3(2f, 2f, 2f);
                }

                if (logObj.GetComponent<WoodCollectible>() == null)
                {
                    var wc = logObj.AddComponent<WoodCollectible>();
                    wc.interactDistance = 8f;
                }
            }
        }
    }

    private static void SetupRiverStakeZone(BachDangStoryManager storyManager)
    {
        GameObject riverZone = GameObject.Find("RiverStakePlantingZone");
        if (riverZone == null)
        {
            riverZone = new GameObject("RiverStakePlantingZone");
            riverZone.transform.position = new Vector3(450f, 2.0f, -450f); // Mép nước bãi cọc sông Bạch Đằng
            var rsz = riverZone.AddComponent<RiverStakeZone>();
            rsz.interactDistance = 25f;

            var cocGroup = GameObject.Find("--- TRẬN ĐỊA CỌC NGẦM BẠCH ĐẰNG (938) ---");
            if (cocGroup != null)
            {
                rsz.stakeGroupObject = cocGroup;
                riverZone.transform.position = cocGroup.transform.position + new Vector3(0, 3f, 0);
            }

            storyManager.riverStakePoint = riverZone.transform;
        }
    }
}
