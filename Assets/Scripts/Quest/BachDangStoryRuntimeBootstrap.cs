using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

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
    private static Material fallbackWoodMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeOnPlay()
    {
        if (SceneManager.GetActiveScene().name != "TestQuest") return;
        SetupAllStoryElements(true);
    }

#if UNITY_EDITOR
    [MenuItem("BachDang/Khởi tạo cốt truyện Hồi 1 & 2 trong Scene")]
    public static void SetupInEditor()
    {
        if (EditorSceneManager.GetActiveScene().name != "TestQuest")
        {
            EditorUtility.DisplayDialog("Mở TestQuest trước", "Hệ thống Hồi 1 và Hồi 2 được thiết kế cho Assets/Scenes/TestQuest.unity.", "Đã hiểu");
            return;
        }
        SetupAllStoryElements(false);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[BachDangStory] Đã tạo và kết nối toàn bộ thành phần Hồi 1 & 2 trong Scene thành công!");
    }
#endif

    public static void SetupAllStoryElements(bool placePlayerAtCamp = false)
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
        StoryDialogueUI.EnsureExists();
        AttachOrdinaryNpcDialogues();

        // 4. Kết nối Ngô Quyền (Ưu tiên NPC_NgoQuyen (1) trên đài gỗ doanh trại tại x:-783, z:159)
        GameObject ngoQuyen = FindNgoQuyenNpc();
        if (ngoQuyen != null)
        {
            foreach (var ordinaryDialogue in ngoQuyen.GetComponentsInChildren<NPCDialogue>(true))
            {
                ordinaryDialogue.enabled = false;
                Object.Destroy(ordinaryDialogue);
            }
            if (ngoQuyen.GetComponent<NPCQuestGiver>() == null)
            {
                var qg = ngoQuyen.AddComponent<NPCQuestGiver>();
                qg.interactDistance = 8f;
            }
            else
            {
                ngoQuyen.GetComponent<NPCQuestGiver>().interactDistance = 8f;
            }
            ngoQuyen.GetComponent<NPCQuestGiver>().enabled = true;
            EnsureNpcCollider(ngoQuyen);
            EnsureLandmark(ngoQuyen, StoryLandmarkMarker.Target.NgoQuyen, "NGÔ QUYỀN");
        }

        // 5. Đưa nhân vật chính xuất hiện trong Doanh Trại tại vị trí (x: -1200, y: 40, z: 340)
        Transform campRoot = ResolveCampRoot(ngoQuyen);
        if (campRoot != null)
        {
            EnsureSolidStaticColliders(campRoot.gameObject);
        }
        AttachSeatedCampNpcDialogues(ngoQuyen);
        EnsureBoatColliders();

        Transform player = storyManager.GetPlayerTransform();
        if (player == null)
        {
            var pObj = OpeningEvent.FindPlayerInScene();
            if (pObj != null) player = pObj.transform;
        }
        EnsurePlayerPhysics(player);

        Vector3 campSpawnPos = new Vector3(-992.8307f, 17.42352f, 265.8618f);
        RaycastHit spawnHit;
        if (Physics.Raycast(new Vector3(campSpawnPos.x, campSpawnPos.y + 60f, campSpawnPos.z), Vector3.down, out spawnHit, 150f))
        {
            campSpawnPos.y = spawnHit.point.y + 0.1f;
        }
        else if (Terrain.activeTerrain != null)
        {
            campSpawnPos.y = Terrain.activeTerrain.SampleHeight(campSpawnPos) + Terrain.activeTerrain.transform.position.y + 0.1f;
        }

        if (player != null && placePlayerAtCamp)
        {
            // Chỉ tắt player nếu opening đang chạy (OpeningEvent sẽ bật lại sau)
            bool openingActive = OpeningEvent.IsOpeningActive;
            if (!openingActive)
            {
                player.gameObject.SetActive(true);
            }

            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = campSpawnPos;
            }

            player.position = campSpawnPos;
            player.rotation = Quaternion.Euler(0f, 110f, 0f);
            if (cc != null) cc.enabled = true;

            // Tự động kết nối target cho tất cả Camera_Script
            if (!openingActive)
            {
                var cameras = Object.FindObjectsByType<Camera_Script>(FindObjectsInactive.Include);
                foreach (var cam in cameras)
                {
                    if (cam != null)
                    {
                        cam.target = player;
                    }
                }
                foreach (var cam in Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsInactive.Include))
                    if (cam != null) cam.target = player;
                foreach (var cam in Object.FindObjectsByType<SimpleCameraFollow>(FindObjectsInactive.Include))
                    if (cam != null) cam.target = player;
            }
        }

        // 6. Tạo điểm tương tác đúng tại cổng doanh trại.
        GameObject campGate = GameObject.Find("CampGate_ToVillage");
        if (campGate == null)
        {
            campGate = new GameObject("CampGate_ToVillage");
        }
        // Cổng nằm cạnh đài chỉ huy. Tọa độ cũ (-469, -228) đặt điểm tương tác
        // cách doanh trại hàng trăm mét và cao khỏi mặt đất nên người chơi không
        // thể gặp lời nhắc hay chạm vào vật cản.
        Vector3 campGatePosition = GroundPosition(new Vector3(-785f, 0f, 320f), 0.1f);
        Transform physicalCampGate = FindPhysicalGateNear(campRoot, campGatePosition, campGate.transform);
        if (physicalCampGate != null)
            campGatePosition = GroundPosition(physicalCampGate.position, 0.1f);
        campGate.transform.position = campGatePosition;
        campGate.transform.rotation = physicalCampGate != null
            ? physicalCampGate.rotation : Quaternion.Euler(0f, 90f, 0f);
        var campTransition = campGate.GetComponent<CampGateTransition>() ?? campGate.AddComponent<CampGateTransition>();
        campTransition.gateType = CampGateTransition.GateType.CampGate;
        campTransition.interactDistance = 30f;
        campTransition.enabled = true;
        campTransition.RefreshBarrier();
        EnsureLandmark(campGate, StoryLandmarkMarker.Target.CampGate, "CỔNG DOANH TRẠI\n[E] SANG LÀNG");

        // 7. Tạo Cổng Làng (VillageGate) tại lối vào khu làng dân cư
        GameObject villageGate = GameObject.Find("VillageGate_ToCamp");
        if (villageGate == null)
        {
            villageGate = new GameObject("VillageGate_ToCamp");
        }
        villageGate.transform.position = GroundPosition(new Vector3(215f, 0f, -1245f), 0.1f);
        var villageTransition = villageGate.GetComponent<CampGateTransition>() ?? villageGate.AddComponent<CampGateTransition>();
        villageTransition.gateType = CampGateTransition.GateType.VillageGate;
        villageTransition.interactDistance = 12f;
        villageTransition.enabled = true;
        villageTransition.RefreshBarrier();
        EnsureLandmark(villageGate, StoryLandmarkMarker.Target.VillageGate, "CỔNG LÀNG → TRẠI");

        storyManager.campSpawnPoint = campGate.transform;
        storyManager.villageSpawnPoint = villageGate.transform;

        // Kết nối đích đến giữa 2 cổng
        var cTrans = campGate.GetComponent<CampGateTransition>();
        if (cTrans != null) cTrans.destinationTarget = villageGate.transform;

        var vTrans = villageGate.GetComponent<CampGateTransition>();
        if (vTrans != null) vTrans.destinationTarget = campGate.transform;

        GameObject campRiverGate = GameObject.Find("CampGate_ToRiver");
        if (campRiverGate == null) campRiverGate = new GameObject("CampGate_ToRiver");
        // Đầu đường ván ra bến thuyền là cổng thứ hai ở cuối doanh trại.
        campRiverGate.transform.position = GroundPosition(new Vector3(-595f, 0f, 250f), 0.1f);
        campRiverGate.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        var riverOut = campRiverGate.GetComponent<CampGateTransition>() ?? campRiverGate.AddComponent<CampGateTransition>();
        riverOut.gateType = CampGateTransition.GateType.CampRiverGate;
        riverOut.interactDistance = 4f;

        GameObject riverGate = GameObject.Find("RiverGate_ToCamp");
        if (riverGate == null) riverGate = new GameObject("RiverGate_ToCamp");
        riverGate.transform.position = GroundPosition(new Vector3(-262f, 0f, 290f), 0.1f);
        riverGate.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        var riverReturn = riverGate.GetComponent<CampGateTransition>() ?? riverGate.AddComponent<CampGateTransition>();
        riverReturn.gateType = CampGateTransition.GateType.RiverGate;
        riverReturn.interactDistance = 8f;
        riverOut.destinationTarget = riverGate.transform;
        riverReturn.destinationTarget = campGate.transform;

        // 8. Tạo 5 khúc cọc gỗ tại các vị trí trong Làng
        SetupVillageWoodLogs();

        // 9. Tạo Điểm Cắm Cọc Bờ Sông (RiverStakeZone)
        SetupRiverStakeZone(storyManager);

        storyManager.UpdateUI();
    }

    private static void AttachOrdinaryNpcDialogues()
    {
        var objects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (var item in objects)
        {
            if (item == null || item.GetComponent<NPCQuestGiver>() != null ||
                item.GetComponent<NPCDialogue>() != null || item.name.Contains("Wrapper"))
                continue;

            string name = item.name.ToLowerInvariant().Replace('_', ' ');
            bool looksLikeNpc = name.Contains("npc") || name.Contains("soldier") || name.Contains("guard") ||
                                name.Contains("linh") || name.Contains("dan lang") || name.Contains("sitting") ||
                                name.Contains("ngoi") || name.Contains("ngồi");
            if (!looksLikeNpc) continue;

            var dialogue = item.gameObject.AddComponent<NPCDialogue>();
            dialogue.speakerName = item.name.Replace('_', ' ');
            dialogue.dialogueText = name.Contains("guard") || name.Contains("linh")
                ? "Doanh trại đã sẵn sàng. Quân sĩ đang chờ lệnh của Chủ tướng."
                : "Bà con trong làng sẽ cùng góp sức chuẩn bị cọc gỗ chống giặc. Xin quân ta bảo vệ xóm làng.\n\nHãy tìm những khúc gỗ quanh nhà để mang ra bờ sông.";
            dialogue.interactDistance = 8f;
            EnsureNpcCollider(item.gameObject);
        }
    }

    private static GameObject FindNgoQuyenNpc()
    {
        string[] exactNames = { "NPC_NgoQuyen (1)", "NPC_NgoQuyen", "Tuong_Ngo_Quyen", "Tuong_NgoQuyen" };
        foreach (string name in exactNames)
        {
            GameObject exact = GameObject.Find(name);
            if (exact != null) return exact;
        }

        var scene = SceneManager.GetActiveScene();
        foreach (var sceneRoot in scene.GetRootGameObjects())
        foreach (var item in sceneRoot.GetComponentsInChildren<Transform>(true))
        {
            string name = item.name.ToLowerInvariant().Replace("_", "").Replace(" ", "");
            if (name.Contains("ngoquyen") && item.GetComponent<Animator>() != null)
                return item.gameObject;
        }

        GameObject hq = GameObject.Find("1_DaiBanDoanh_NgoQuyen");
        if (hq == null) return null;
        Vector3 expected = new Vector3(-808f, 17.5f, 320f);
        if (Terrain.activeTerrain != null)
            expected.y = Terrain.activeTerrain.SampleHeight(expected) + Terrain.activeTerrain.transform.position.y;
        Animator closest = null;
        float bestDistance = float.MaxValue;
        foreach (var animator in hq.GetComponentsInChildren<Animator>(true))
        {
            float distance = Vector3.Distance(animator.transform.position, expected);
            if (distance < bestDistance) { closest = animator; bestDistance = distance; }
        }
        return closest != null ? closest.gameObject : null;
    }

    private static Transform ResolveCampRoot(GameObject ngoQuyen)
    {
        GameObject hq = GameObject.Find("1_DaiBanDoanh_NgoQuyen");
        if (hq != null) return hq.transform.parent != null ? hq.transform.parent : hq.transform;
        return ngoQuyen != null && ngoQuyen.transform.parent != null
            ? ngoQuyen.transform.parent : ngoQuyen != null ? ngoQuyen.transform : null;
    }

    private static void AttachSeatedCampNpcDialogues(GameObject ngoQuyen)
    {
        Vector3[] positions =
        {
            new Vector3(-818.2422f, 17.47519f, 334.4858f),
            new Vector3(-738.9135f, 17.71f, 365.4983f)
        };
        string[] lines =
        {
            "Quân Nam Hán đang dòm ngó bờ cõi. Chúng tôi sẽ giữ vững doanh trại để Chủ tướng yên tâm lo kế sách.",
            "Dân làng ven sông đã sẵn lòng giúp sức. Mong quân ta sớm chuẩn bị đủ cọc để chặn thuyền giặc."
        };
        var scene = SceneManager.GetActiveScene();
        var animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include);
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        for (int i = 0; i < positions.Length; i++)
        {
            Transform closest = null;
            float bestDistance = 8f;
            foreach (var animator in animators)
            {
                if (animator == null || !animator.gameObject.activeInHierarchy || animator.gameObject == ngoQuyen ||
                    !animator.gameObject.scene.IsValid() || animator.gameObject.scene != scene) continue;
                float distance = Vector3.Distance(animator.transform.position, positions[i]);
                if (distance < bestDistance) { closest = animator.transform; bestDistance = distance; }
            }
            if (closest == null)
            {
                foreach (var renderer in renderers)
                {
                    if (renderer == null || !renderer.gameObject.activeInHierarchy || renderer.gameObject == ngoQuyen ||
                        !renderer.gameObject.scene.IsValid() || renderer.gameObject.scene != scene) continue;
                    float distance = Vector3.Distance(renderer.bounds.center, positions[i]);
                    if (distance < bestDistance) { closest = renderer.transform; bestDistance = distance; }
                }
            }
            GameObject actor;
            if (closest == null || closest.GetComponentInParent<NPCQuestGiver>() != null)
            {
                actor = GameObject.Find("SeatedCampSoldier_Interact_" + (i + 1));
                if (actor == null) actor = new GameObject("SeatedCampSoldier_Interact_" + (i + 1));
                actor.transform.position = positions[i];
            }
            else actor = closest.gameObject;
            var dialogue = actor.GetComponentInParent<NPCDialogue>() ?? actor.AddComponent<NPCDialogue>();
            dialogue.speakerName = "Người lính trong doanh trại";
            dialogue.dialogueText = lines[i];
            dialogue.interactDistance = 10f;
            dialogue.enabled = true;
            if (actor.GetComponentInChildren<Renderer>(true) != null) EnsureNpcCollider(actor);
        }
    }

    private static void EnsureCampTalkers(Transform campRoot, GameObject ngoQuyen)
    {
        if (campRoot == null) return;
        GameObject source = FindNamedObject(campRoot, "ThiVe_CanhVe_1") ??
                            FindNamedObject(campRoot, "ThiVe_CanhVe_2") ?? ngoQuyen;
        string[] names = { "Linh_Gac_Cong_Bac", "Linh_Gac_Cong_Nam" };
        string[] lines =
        {
            "Quân sĩ đã sẵn sàng. Tôi đang canh cổng trại, chờ lệnh của Chủ tướng.",
            "Bà con ven sông đang giúp quân ta chuẩn bị. Mong kế sách của Chủ tướng giữ yên bờ cõi."
        };
        Vector3[] offsets = { new Vector3(37f, 0f, 6.5f), new Vector3(37f, 0f, -6.5f) };

        for (int i = 0; i < names.Length; i++)
        {
            GameObject actor = FindNamedObject(campRoot, names[i]);
            Vector3 position = new Vector3(-820f, 17.5f, 320f) + offsets[i];
            if (Terrain.activeTerrain != null)
                position.y = Terrain.activeTerrain.SampleHeight(position) + Terrain.activeTerrain.transform.position.y;
            if (actor == null && source != null)
            {
                actor = Object.Instantiate(source, campRoot, true);
                actor.name = names[i];
            }
            if (actor == null || actor == ngoQuyen) continue;

            actor.transform.position = position;
            actor.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            foreach (var questGiver in actor.GetComponentsInChildren<NPCQuestGiver>(true))
            {
                questGiver.enabled = false;
                Object.Destroy(questGiver);
            }
            foreach (var marker in actor.GetComponentsInChildren<StoryLandmarkMarker>(true))
            {
                marker.enabled = false;
                Object.Destroy(marker);
            }

            var dialogue = actor.GetComponent<NPCDialogue>() ?? actor.AddComponent<NPCDialogue>();
            dialogue.speakerName = "Lính gác doanh trại";
            dialogue.dialogueText = lines[i];
            dialogue.interactDistance = 9f;
            EnsureNpcCollider(actor);
        }

        foreach (var animator in campRoot.GetComponentsInChildren<Animator>(true))
        {
            if (animator != null && animator.GetComponentInParent<NPCQuestGiver>() == null)
                EnsureNpcCollider(animator.gameObject);
        }
    }

    private static GameObject FindNamedObject(Transform root, string objectName)
    {
        if (root == null) return null;
        foreach (var item in root.GetComponentsInChildren<Transform>(true))
            if (item.name == objectName) return item.gameObject;
        return null;
    }

    private static Vector3 GroundPosition(Vector3 position, float heightOffset)
    {
        if (Terrain.activeTerrain != null)
        {
            var terrain = Terrain.activeTerrain;
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + heightOffset;
        }
        else
        {
            RaycastHit hit;
            if (Physics.Raycast(position + Vector3.up * 100f, Vector3.down, out hit, 200f))
                position.y = hit.point.y + heightOffset;
        }
        return position;
    }

    private static Transform FindPhysicalGateNear(Transform campRoot, Vector3 expectedPosition, Transform transitionGate)
    {
        Transform closest = null;
        float bestDistance = 180f;
        var candidates = campRoot != null
            ? campRoot.GetComponentsInChildren<Transform>(true)
            : Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (var item in candidates)
        {
            if (item == null || item == transitionGate) continue;
            string name = item.name.ToLowerInvariant();
            if ((!name.Contains("gate") && !name.Contains("cổng") && !name.Contains("cong")) ||
                name.Contains("invisiblebarrier") || item.GetComponentInParent<CampGateTransition>() != null)
                continue;
            // Ignore tiny gate sub-meshes where possible; the transition point
            // should follow a visible/collidable gate root.
            if (item.GetComponent<Renderer>() == null && item.GetComponent<Collider>() == null && item.childCount == 0)
                continue;
            float distance = Vector2.Distance(
                new Vector2(item.position.x, item.position.z),
                new Vector2(expectedPosition.x, expectedPosition.z));
            if (distance < bestDistance) { closest = item; bestDistance = distance; }
        }
        return closest;
    }

    private static void EnsureNpcCollider(GameObject npc)
    {
        if (npc == null) return;
        foreach (var collider in npc.GetComponentsInChildren<Collider>(true))
            if (collider != null && !collider.isTrigger) return;

        var renderers = npc.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        Vector3 scale = npc.transform.lossyScale;
        float sx = Mathf.Max(0.001f, Mathf.Abs(scale.x));
        float sy = Mathf.Max(0.001f, Mathf.Abs(scale.y));
        float sz = Mathf.Max(0.001f, Mathf.Abs(scale.z));
        var capsule = npc.AddComponent<CapsuleCollider>();
        capsule.isTrigger = false;
        capsule.center = npc.transform.InverseTransformPoint(bounds.center);
        capsule.height = Mathf.Max(1.8f, bounds.size.y / sy * 0.9f);
        capsule.radius = Mathf.Max(0.35f, Mathf.Max(bounds.size.x / sx, bounds.size.z / sz) * 0.35f);
    }

    private static void EnsureSolidStaticColliders(GameObject root)
    {
        if (root == null) return;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == null || filter.sharedMesh == null || filter.GetComponentInParent<Animator>() != null ||
                IsBoatHierarchy(filter.transform)) continue;
            var ownCollider = filter.GetComponent<Collider>();
            if (ownCollider != null)
            {
                ownCollider.isTrigger = false;
                continue;
            }
            var parentCollider = filter.transform.parent != null
                ? filter.transform.parent.GetComponentInParent<Collider>() : null;
            if (parentCollider != null && !parentCollider.isTrigger) continue;
            var meshCollider = filter.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = filter.sharedMesh;
            meshCollider.convex = filter.GetComponentInParent<Rigidbody>() != null;
            meshCollider.isTrigger = false;
        }
    }

    private static void EnsureBoatColliders()
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var sceneRoot in scene.GetRootGameObjects())
        foreach (var item in sceneRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!IsBoatName(item.name)) continue;
            Transform boatRoot = item;
            for (Transform parent = item.parent; parent != null; parent = parent.parent)
            {
                if (!IsBoatName(parent.name)) break;
                boatRoot = parent;
            }
            var renderers = boatRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) continue;

            // FBX LODs can contain empty MeshColliders and default 1 m boxes.
            // Remove empty mesh colliders and size a solid hull blocker from visible geometry.
            foreach (var meshCollider in boatRoot.GetComponentsInChildren<MeshCollider>(true))
            {
                if (meshCollider.sharedMesh == null) Object.Destroy(meshCollider);
                else meshCollider.isTrigger = false;
            }
            Bounds bounds = new Bounds();
            bool hasBoatBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].GetComponentInParent<Animator>() != null) continue;
                if (!hasBoatBounds) { bounds = renderers[i].bounds; hasBoatBounds = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }
            if (!hasBoatBounds) bounds = renderers[0].bounds;
            Vector3 scale = boatRoot.lossyScale;
            var box = boatRoot.GetComponent<BoxCollider>() ?? boatRoot.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.center = boatRoot.InverseTransformPoint(bounds.center);
            box.size = new Vector3(bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                                   bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
                                   bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
            Debug.Log($"[BachDangStory] Đã sửa collider thuyền {boatRoot.name}: {box.size}");
        }
    }

    private static bool IsBoatHierarchy(Transform item)
    {
        for (Transform current = item; current != null; current = current.parent)
            if (IsBoatName(current.name)) return true;
        return false;
    }

    private static bool IsBoatName(string objectName)
    {
        string name = objectName.ToLowerInvariant();
        return name.Contains("boat") || name.Contains("thuyen") || name.Contains("thuyền");
    }

    private static void EnsurePlayerPhysics(Transform player)
    {
        if (player == null) return;
        var body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.detectCollisions = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        foreach (var collider in player.GetComponentsInChildren<Collider>(true))
            if (collider != null) collider.isTrigger = false;
    }

    private static void EnsureLandmark(GameObject target, StoryLandmarkMarker.Target kind, string label)
    {
        if (target == null) return;
        var marker = target.GetComponent<StoryLandmarkMarker>() ?? target.AddComponent<StoryLandmarkMarker>();
        marker.Configure(kind, label);
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
        var storyScaler = canvasObj.GetComponent<CanvasScaler>();
        if (storyScaler != null)
        {
            storyScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            storyScaler.referenceResolution = new Vector2(1920f, 1080f);
            storyScaler.matchWidthOrHeight = 0.5f;
        }

        // Suppress every legacy objective panel before creating the single compact story card.
        foreach (var candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (candidate == null || candidate.gameObject == canvasObj) continue;
            if (candidate.name == "QuestPanel" || candidate.name == "QuestUIPanel" ||
                candidate.name == "NhiemVuPanel" || candidate.name == "QuestDesc" ||
                candidate.name == "QuestTitle" || candidate.name == "ObjectivePanel")
                candidate.gameObject.SetActive(false);
        }
        if (QuestManager.Instance != null && QuestManager.Instance.questUIPanel != null)
            QuestManager.Instance.questUIPanel.SetActive(false);

        // Kiểm tra xem Scene đã có sẵn QuestPanel/QuestDesc chưa để dùng luôn, tránh bị 2 bảng đè nhau
        var existingQuestDesc = GameObject.Find("QuestDesc")?.GetComponent<TextMeshProUGUI>();
        var existingQuestTitle = GameObject.Find("QuestTitle")?.GetComponent<TextMeshProUGUI>();

        if (existingQuestDesc != null)
        {
            foreach (var candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (candidate == null) continue;
                if (candidate.name == "QuestPanel" || candidate.name == "QuestUIPanel" || candidate.name == "ObjectivePanel")
                    candidate.gameObject.SetActive(false);
            }
            if (QuestManager.Instance != null && QuestManager.Instance.questUIPanel != null)
                QuestManager.Instance.questUIPanel.SetActive(false);
            existingQuestDesc = null;
            existingQuestTitle = null;
        }

        if (existingQuestDesc != null)
        {
            var sceneQuestPanel = GameObject.Find("QuestPanel");
            objectivePanel = sceneQuestPanel != null ? sceneQuestPanel :
                existingQuestDesc.transform.parent != null ? existingQuestDesc.transform.parent.gameObject : existingQuestDesc.gameObject;
            // A previous setup left a second generated objective card on StoryUI_Canvas.
            // Keep the scene QuestPanel as the only objective card.
            var duplicateObjective = canvasObj.transform.Find("ObjectivePanel");
            if (duplicateObjective != null && duplicateObjective.gameObject != objectivePanel)
                duplicateObjective.gameObject.SetActive(false);
            foreach (var candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (candidate == null || candidate.gameObject == objectivePanel) continue;
                if (candidate.name == "QuestPanel" || candidate.name == "QuestUIPanel" || candidate.name == "ObjectivePanel")
                    candidate.gameObject.SetActive(false);
            }
            descText = existingQuestDesc;
            titleText = existingQuestTitle;
            if (titleText != null) titleText.transform.SetParent(objectivePanel.transform, false);
            descText.transform.SetParent(objectivePanel.transform, false);
            var panelRect = objectivePanel.GetComponent<RectTransform>();
            var questScaler = objectivePanel.GetComponentInParent<Canvas>()?.GetComponent<CanvasScaler>();
            if (questScaler != null)
            {
                questScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                questScaler.referenceResolution = new Vector2(1920f, 1080f);
                questScaler.matchWidthOrHeight = 0.5f;
            }
            if (panelRect != null)
            {
                panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 1f);
                panelRect.pivot = new Vector2(0f, 1f);
                panelRect.anchoredPosition = new Vector2(24f, -24f);
                panelRect.sizeDelta = new Vector2(380f, 126f);
            }
            if (titleText != null)
            {
                titleText.fontSize = 19;
                var titleRect = titleText.rectTransform;
                titleRect.anchorMin = new Vector2(0.05f, 0.72f);
                titleRect.anchorMax = new Vector2(0.95f, 0.96f);
                titleRect.offsetMin = Vector2.zero;
                titleRect.offsetMax = Vector2.zero;
            }
            descText.fontSize = 15;
            var descRect = descText.rectTransform;
            descRect.anchorMin = new Vector2(0.05f, 0.08f);
            descRect.anchorMax = new Vector2(0.95f, 0.72f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;
        }
        else
        {
            Transform objPanelTr = canvasObj.transform.Find("ObjectivePanel_Rebuilt");
            if (objPanelTr == null)
            {
                objectivePanel = new GameObject("ObjectivePanel_Rebuilt");
                objectivePanel.transform.SetParent(canvasObj.transform, false);

                var img = objectivePanel.AddComponent<Image>();
                img.color = new Color(0.08f, 0.08f, 0.1f, 0.75f); // Nền mờ phong cách cổ điển

                RectTransform rt = objectivePanel.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(25f, -25f);
                rt.sizeDelta = new Vector2(360f, 112f);

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

        var objectiveRect = objectivePanel.GetComponent<RectTransform>();
        objectiveRect.anchorMin = objectiveRect.anchorMax = new Vector2(0f, 1f);
        objectiveRect.pivot = new Vector2(0f, 1f);
        objectiveRect.anchoredPosition = new Vector2(24f, -24f);
        objectiveRect.sizeDelta = new Vector2(360f, 112f);
        if (titleText != null)
        {
            titleText.fontSize = 18;
            titleText.enableAutoSizing = false;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            titleText.rectTransform.anchorMin = new Vector2(0.06f, 0.70f);
            titleText.rectTransform.anchorMax = new Vector2(0.94f, 0.95f);
            titleText.rectTransform.offsetMin = titleText.rectTransform.offsetMax = Vector2.zero;
        }
        if (descText != null)
        {
            descText.fontSize = 14;
            descText.enableAutoSizing = false;
            descText.alignment = TextAlignmentOptions.TopLeft;
            descText.rectTransform.anchorMin = new Vector2(0.06f, 0.08f);
            descText.rectTransform.anchorMax = new Vector2(0.94f, 0.68f);
            descText.rectTransform.offsetMin = descText.rectTransform.offsetMax = Vector2.zero;
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
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(620f, 64f);

            GameObject pTextObj = new GameObject("PromptText");
            pTextObj.transform.SetParent(promptPanel.transform, false);
            promptText = pTextObj.AddComponent<TextMeshProUGUI>();
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.fontSize = 24;
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
        var existingPromptRect = promptPanel.GetComponent<RectTransform>();
        if (existingPromptRect != null)
        {
            existingPromptRect.anchorMin = existingPromptRect.anchorMax = new Vector2(1f, 0f);
            existingPromptRect.pivot = new Vector2(1f, 0f);
            existingPromptRect.anchoredPosition = new Vector2(-28f, 28f);
            existingPromptRect.sizeDelta = new Vector2(520f, 58f);
        }
        if (promptText != null)
        {
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.fontSize = 24;
            promptText.fontStyle = FontStyles.Bold;
            var promptTextRect = promptText.rectTransform;
            promptTextRect.anchorMin = Vector2.zero;
            promptTextRect.anchorMax = Vector2.one;
            promptTextRect.offsetMin = Vector2.zero;
            promptTextRect.offsetMax = Vector2.zero;
        }
    }

    private static void SetupVillageWoodLogs()
    {
        Vector3[] woodPositions = new Vector3[]
        {
            new Vector3(122f, 17.5f, -1228f), // Sân trống gần lối vào
            new Vector3(165f, 17.5f, -1248f), // Khoảng sân cạnh nhà dân
            new Vector3(214f, 17.5f, -1180f), // Lối đất phía bắc xóm
            new Vector3(280f, 17.5f, -1160f), // Bãi đất trống phía bắc nhà
            new Vector3(350f, 17.5f, -1260f)  // Sân giữa các nếp nhà
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
                    if (fallbackWoodMaterial == null)
                    {
                        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                        if (shader != null)
                        {
                            fallbackWoodMaterial = new Material(shader);
                            fallbackWoodMaterial.color = new Color(0.34f, 0.19f, 0.09f);
                            fallbackWoodMaterial.SetFloat("_Smoothness", 0.2f);
                        }
                    }
                    var renderer = logObj.GetComponent<Renderer>();
                    if (renderer != null && fallbackWoodMaterial != null)
                        renderer.sharedMaterial = fallbackWoodMaterial;
                }

                if (logObj.GetComponent<Collider>() == null)
                {
                    var col = logObj.AddComponent<BoxCollider>();
                    col.size = new Vector3(2f, 2f, 2f);
                }

            }

            logObj.transform.position = woodPositions[i];
            if (logObj.GetComponent<WoodCollectible>() == null)
                logObj.AddComponent<WoodCollectible>();
            logObj.GetComponent<WoodCollectible>().interactDistance = 8f;
        }
    }

    private static void SetupRiverStakeZone(BachDangStoryManager storyManager)
    {
        GameObject riverZone = GameObject.Find("RiverStakePlantingZone");
        if (riverZone == null) riverZone = new GameObject("RiverStakePlantingZone");
        riverZone.transform.position = new Vector3(-262f, 6f, 290f); // Bờ sông cạnh khu cọc đã bố trí sẵn
        var rsz = riverZone.GetComponent<RiverStakeZone>() ?? riverZone.AddComponent<RiverStakeZone>();
        rsz.interactDistance = 28f;
        EnsureLandmark(riverZone, StoryLandmarkMarker.Target.RiverStakes, "BÃI CỌC BẠCH ĐẰNG");
        var cocGroup = GameObject.Find("--- TRẬN ĐỊA CỌC NGẦM BẠCH ĐẰNG (938) ---");
        if (cocGroup != null)
        {
            rsz.stakeGroupObject = cocGroup;
            var state = storyManager.currentState;
            if (state != BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp &&
                state != BachDangStoryManager.StoryState.Act2_Completed)
                cocGroup.SetActive(false);
        }
        storyManager.riverStakePoint = riverZone.transform;
    }
}
