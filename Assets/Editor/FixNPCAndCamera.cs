using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class FixNPCAndCamera : EditorWindow
{
    [MenuItem("BachDang/SỬA LỖI NPC + FIX CAMERA (LẦN CUỐI ĐỂ ĐI NGỦ)")]
    public static void FixEverything()
    {
        // 1. FIX CAMERA CHO PLAYER (Lia chuột)
        GameObject player = GameObject.Find("Main_Character");
        if (player == null) player = GameObject.Find("Player_Main_Animated");
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            player.tag = "Player"; // LỖI LỚN NHẤT LÀ ĐÂY!!!
            
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = GameObject.Find("Main Camera");
                if (camObj != null) mainCam = camObj.GetComponent<Camera>();
            }
            
            if (mainCam != null)
            {
                mainCam.transform.SetParent(null);
                ThirdPersonCamera camFollow = mainCam.gameObject.GetComponent<ThirdPersonCamera>();
                if (camFollow == null) camFollow = mainCam.gameObject.AddComponent<ThirdPersonCamera>();
                camFollow.target = player.transform;
            }
        }

        // 2. LẤY UI CHỮ E (Để gán cho NPC)
        GameObject interactUI = GameObject.Find("InteractPrompt");
        if (interactUI == null)
        {
            // Nếu mất UI chữ E thì tạo lại
            GameObject canvasObj = GameObject.Find("Canvas_QuestUI");
            if (canvasObj != null)
            {
                interactUI = new GameObject("InteractPrompt");
                interactUI.transform.SetParent(canvasObj.transform, false);
                TextMeshProUGUI promptText = interactUI.AddComponent<TextMeshProUGUI>();
                promptText.text = "Nhấn [E] để nói chuyện";
                promptText.fontSize = 32;
                promptText.alignment = TextAlignmentOptions.BottomRight;
                RectTransform promptRect = interactUI.GetComponent<RectTransform>();
                promptRect.anchorMin = new Vector2(1, 0);
                promptRect.anchorMax = new Vector2(1, 0);
                promptRect.pivot = new Vector2(1, 0);
                promptRect.anchoredPosition = new Vector2(-40, 40);
                promptRect.sizeDelta = new Vector2(600, 100);
            }
        }

        // 3. FIX NPC
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Main_Character/Animation/Main_Character_Animation.controller");
        string[] npcNames = { "NPC_NgoQuyen", "NPC_DanLang_0", "NPC_DanLang_1", "NPC_DanLang_2" };

        foreach (string nName in npcNames)
        {
            GameObject npc = GameObject.Find(nName);
            if (npc != null)
            {
                // Chống đi xuyên
                CapsuleCollider physCol = npc.GetComponent<CapsuleCollider>();
                if (physCol == null) physCol = npc.AddComponent<CapsuleCollider>();
                physCol.isTrigger = false; 
                physCol.height = 2f;
                physCol.radius = 0.5f;
                physCol.center = new Vector3(0, 1f, 0); 

                // Vùng chữ E
                SphereCollider trigCol = npc.GetComponent<SphereCollider>();
                if (trigCol == null) trigCol = npc.AddComponent<SphereCollider>();
                trigCol.isTrigger = true;
                trigCol.radius = 2.5f; // Mở rộng ra tí xíu cho dễ chạm
                trigCol.center = new Vector3(0, 1f, 0); 

                // Ngừng chạy tại chỗ
                if (npc.GetComponent<NPCIdleForce>() == null)
                    npc.AddComponent<NPCIdleForce>();

                // Gắn UI chữ E vào script
                NPCQuestGiver questScript = npc.GetComponent<NPCQuestGiver>();
                if (questScript != null) questScript.interactPromptUI = interactUI;

                NPCDialogue dialogueScript = npc.GetComponent<NPCDialogue>();
                if (dialogueScript != null) dialogueScript.interactPromptUI = interactUI;

                // Gắn Animator & Avatar
                Animator anim = npc.GetComponentInChildren<Animator>();
                if (anim == null) anim = npc.AddComponent<Animator>();
                if (controller != null) anim.runtimeAnimatorController = controller;

                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(npc);
                if (string.IsNullOrEmpty(prefabPath)) 
                {
                    if (nName == "NPC_NgoQuyen") prefabPath = "Assets/Model_NPC/NgoQuyen/NgoQuyenModel/Meshy_AI_Ngo_Quyen_938_AD_biped_Character_output.fbx";
                    else if (nName == "NPC_DanLang_0") prefabPath = "Assets/Model_NPC/DanThuongLonTuoi/Meshy_AI_Wandering_Peasant_biped_Character_output.fbx";
                    else if (nName == "NPC_DanLang_1") prefabPath = "Assets/Model_NPC/FemalePea/FemalePea/Meshy_AI_Brown_Tunic_Figure_biped_Character_output.fbx";
                    else if (nName == "NPC_DanLang_2") prefabPath = "Assets/Model_NPC/MalePea/MalePea/Meshy_AI_Blue_Scrub_Figure_biped_Character_output.fbx";
                }
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    Object[] assets = AssetDatabase.LoadAllAssetsAtPath(prefabPath);
                    foreach (Object asset in assets)
                    {
                        if (asset is Avatar)
                        {
                            anim.avatar = (Avatar)asset;
                            break;
                        }
                    }
                }
            }
        }

        EditorUtility.DisplayDialog("Xong!", "Đã khôi phục Camera lia chuột.\nĐã nối lại dây điện cho chữ E hoạt động!\n\nNgủ thôi!", "Tuyệt");
    }
}
