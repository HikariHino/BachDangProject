using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

public class SetupQuestSceneEditor : EditorWindow
{
    [MenuItem("BachDang/Tự động tạo Scene Nhiệm Vụ (Test)")]
    public static void CreateQuestScene()
    {
        // Tạo Scene mới
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        
        // 1. Tạo Mặt đất
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Ground";
        
        // 2. Tạo Player (Dùng Model thật)
        string playerPath = "Assets/Main_Character/Meshy_AI_Steppe_Ironclad_biped/Meshy_AI_Steppe_Ironclad_biped/Meshy_AI_Steppe_Ironclad_biped_Character_output.fbx";
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
        GameObject player = null;
        if (playerPrefab != null)
        {
            player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player_Main";
        }
        else
        {
            player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.name = "Player_Cube_Fallback";
            Debug.LogWarning("Không tìm thấy model Player, tạo tạm Cube.");
        }
        
        player.transform.position = new Vector3(0, 0f, -5);
        player.tag = "Player"; // Quan trọng: Gắn tag Player
        
        Rigidbody playerRb = player.AddComponent<Rigidbody>();
        playerRb.constraints = RigidbodyConstraints.FreezeRotation; // Chống lăn
        
        // Thêm Box Collider cho nhân vật để không rớt xuống đất
        BoxCollider playerCol = player.GetComponent<BoxCollider>();
        if(playerCol == null) playerCol = player.AddComponent<BoxCollider>();
        playerCol.center = new Vector3(0, 1f, 0);
        playerCol.size = new Vector3(1f, 2f, 1f);
        
        player.AddComponent<PlayerMovement>();
        
        // 3. Tạo NPC (Dùng Model Ngô Quyền thật)
        string npcPath = "Assets/Model_NPC/NgoQuyen/NgoQuyenModel/Meshy_AI_Ngo_Quyen_938_AD_biped_Character_output.fbx";
        GameObject npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(npcPath);
        GameObject npc = null;
        if (npcPrefab != null)
        {
            npc = (GameObject)PrefabUtility.InstantiatePrefab(npcPrefab);
            npc.name = "NPC_NgoQuyen_Real";
        }
        else
        {
            npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "NPC_NgoQuyen_Fallback";
            Debug.LogWarning("Không tìm thấy model Ngô Quyền, tạo tạm Capsule.");
        }
        
        npc.transform.position = new Vector3(0, 0f, 0);
        
        // Thêm Trigger Collider cho NPC
        SphereCollider triggerCol = npc.AddComponent<SphereCollider>();
        triggerCol.isTrigger = true;
        triggerCol.radius = 3f; // Bán kính nhận diện
        
        NPCQuestGiver npcScript = npc.AddComponent<NPCQuestGiver>();
        
        // 4. Tạo GameManager
        GameObject gameManager = new GameObject("GameManager");
        QuestManager questManager = gameManager.AddComponent<QuestManager>();
        
        // 5. Tạo Canvas & UI
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        // Tạo EventSystem
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
        
        // Tạo Panel Nhiệm Vụ
        GameObject panelObj = new GameObject("QuestPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image panelImg = panelObj.AddComponent<UnityEngine.UI.Image>();
        panelImg.color = new Color(0, 0, 0, 0.8f);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1, 1);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.pivot = new Vector2(1, 1);
        panelRect.anchoredPosition = new Vector2(-20, -20);
        panelRect.sizeDelta = new Vector2(350, 150);
        
        // Tạo Title Text
        GameObject titleObj = new GameObject("QuestTitle_Text");
        titleObj.transform.SetParent(panelObj.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Nhiệm vụ: Chưa có";
        titleText.fontSize = 24;
        titleText.color = Color.yellow;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0, 40);
        titleRect.sizeDelta = new Vector2(330, 40);
        
        // Tạo Description Text
        GameObject descObj = new GameObject("QuestDesc_Text");
        descObj.transform.SetParent(panelObj.transform, false);
        TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.text = "Mô tả...";
        descText.fontSize = 18;
        descText.color = Color.white;
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.anchoredPosition = new Vector2(0, -20);
        descRect.sizeDelta = new Vector2(330, 80);
        
        // Tạo Text "Nhấn E"
        GameObject promptObj = new GameObject("InteractPrompt_Text");
        promptObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI promptText = promptObj.AddComponent<TextMeshProUGUI>();
        promptText.text = "Nhấn [E] để nói chuyện";
        promptText.fontSize = 32;
        promptText.alignment = TextAlignmentOptions.BottomRight;
        RectTransform promptRect = promptObj.GetComponent<RectTransform>();
        // Neo chữ ở dưới cùng, góc bên phải (Bottom Right)
        promptRect.anchorMin = new Vector2(1, 0);
        promptRect.anchorMax = new Vector2(1, 0);
        promptRect.pivot = new Vector2(1, 0);
        promptRect.anchoredPosition = new Vector2(-40, 40); // Cách lề 40 pixel
        promptRect.sizeDelta = new Vector2(600, 100);
        
        // 6. Gán các tham chiếu (Tự động kéo thả)
        questManager.questTitleText = titleText;
        questManager.questDescriptionText = descText;
        questManager.questUIPanel = panelObj;
        
        npcScript.interactPromptUI = promptObj;
        
        // Lưu Scene
        bool saved = EditorSceneManager.SaveScene(newScene, "Assets/AutoQuestScene.unity");
        if (saved)
        {
            Debug.Log("Đã tạo tự động Scene nhiệm vụ thành công tại Assets/AutoQuestScene.unity!");
        }
    }
}
