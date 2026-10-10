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

        // 4. Kết nối Ngô Quyền (Ưu tiên NPC_NgoQuyen (1) trên đài gỗ doanh trại tại x:-783, z:159)
        GameObject ngoQuyen = GameObject.Find("NPC_NgoQuyen (1)") ?? GameObject.Find("NPC_NgoQuyen") ?? GameObject.Find("1_DaiBanDoanh_NgoQuyen");
        if (ngoQuyen != null)
        {
            if (ngoQuyen.GetComponent<NPCQuestGiver>() == null)
            {
                var qg = ngoQuyen.AddComponent<NPCQuestGiver>();
                qg.interactDistance = 15f;
            }
        }

        // 5. Đưa nhân vật chính xuất hiện trong Doanh Trại tại vị trí (x: -1200, y: 40, z: 340)
        Transform player = storyManager.GetPlayerTransform();
        Vector3 campSpawnPos = new Vector3(-1200f, 40f, 340f);
        if (player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = campSpawnPos;
            player.rotation = Quaternion.Euler(0f, 110f, 0f);
            if (cc != null) cc.enabled = true;

            // Tự động kết nối target cho tất cả Camera_Script để camera bám theo nhân vật chính
            var cameras = Object.FindObjectsByType<Camera_Script>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam != null && cam.target == null)
                {
                    cam.target = player;
                }
            }
        }

        // 6. Tạo Cổng Doanh Trại (CampGate) tại lối ra của trại lính
        GameObject campGate = GameObject.Find("CampGate_ToVillage");
        if (campGate == null)
        {
            campGate = new GameObject("CampGate_ToVillage");
            campGate.transform.position = new Vector3(-755f, 33.0f, 215f);
            var trans = campGate.AddComponent<CampGateTransition>();
            trans.gateType = CampGateTransition.GateType.CampGate;
            trans.interactDistance = 12f;
        }
        else
        {
            campGate.transform.position = new Vector3(-755f, 33.0f, 215f);
        }

        // 7. Tạo Cổng Làng (VillageGate) tại lối vào khu làng dân cư
        GameObject villageGate = GameObject.Find("VillageGate_ToCamp");
        if (villageGate == null)
        {
            villageGate = new GameObject("VillageGate_ToCamp");
            villageGate.transform.position = new Vector3(215f, 17.6f, -1245f);
            var trans = villageGate.AddComponent<CampGateTransition>();
            trans.gateType = CampGateTransition.GateType.VillageGate;
            trans.interactDistance = 12f;
        }
        else
        {
            villageGate.transform.position = new Vector3(215f, 17.6f, -1245f);
        }

        storyManager.campSpawnPoint = campGate.transform;
        storyManager.villageSpawnPoint = villageGate.transform;

        // Kết nối đích đến giữa 2 cổng
        var cTrans = campGate.GetComponent<CampGateTransition>();
        if (cTrans != null) cTrans.destinationTarget = villageGate.transform;

        var vTrans = villageGate.GetComponent<CampGateTransition>();
        if (vTrans != null) vTrans.destinationTarget = campGate.transform;

        // 8. Tạo 5 khúc cọc gỗ tại các vị trí trong Làng
        SetupVillageWoodLogs();

        // 9. Tạo Điểm Cắm Cọc Bờ Sông (RiverStakeZone)
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
            riverZone.transform.position = new Vector3(-748f, 17.5f, 335f); // Bờ sông ở cuối doanh trại
            var rsz = riverZone.AddComponent<RiverStakeZone>();
            rsz.interactDistance = 20f;

            var cocGroup = GameObject.Find("--- TRẬN ĐỊA CỌC NGẦM BẠCH ĐẰNG (938) ---");
            if (cocGroup != null)
            {
                rsz.stakeGroupObject = cocGroup;
            }

            storyManager.riverStakePoint = riverZone.transform;
        }
    }
}
