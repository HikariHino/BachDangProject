using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public static class MasterBattlefieldBeautifier
{
    private const string SCENE_PATH = "Assets/Scenes/beachBoat.unity";

    [MenuItem("🤖 Trợ lý AI/✨ CẬP NHẬT CHIẾN TRƯỜNG BẠCH ĐẰNG 938 HOÀN MỸ (AAA)")]
    public static void UpdateAndBeautifyBattlefield()
    {
        Debug.Log("=========================================================================");
        Debug.Log("🚀 BẮT ĐẦU NÂNG CẤP TOÀN DIỆN CHIẾN TRƯỜNG BẠCH ĐẰNG 938...");
        Debug.Log("=========================================================================");

        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"❌ Không mở được Scene tại {SCENE_PATH}!");
            return;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            var allTerrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            if (allTerrains.Length > 0) terrain = allTerrains[0];
        }

        if (terrain == null)
        {
            Debug.LogError("❌ Không tìm thấy Terrain trong Scene!");
            return;
        }

        // 1. DỌN DẸP CÁC ĐỐI TƯỢNG RÁC, LỖI MISSING PREFAB & CÁC VẬT THỂ Ở XA VÔ TẬN
        CleanStaleAndMissingObjects();

        // 2. GIÁP NÚI ĐÁ VÔI KARST CHUẨN 3D PBR (KHÔNG MISSING PREFAB)
        MapToTerrainBuilder.SpawnMountainsOnly();

        // 3. ĐẢM BẢO BÃI CỌC LIM CHẮC CHẮN CẮM TỪ ĐÁY SÂU
        MapToTerrainBuilder.SpawnHistoricSpikes();

        // 4. XÂY DỰNG & ĐIỀU ĐỘNG QUÂN ĐỘI CHO ĐẠI BẢN DOANH NGÔ QUYỀN & XƯỞNG CỌC
        BuildCampAndGarrison(terrain);

        // 5. ĐIỀU ĐỘNG HẠM ĐỘI THUYỀN CHIẾN ĐẠI VIỆT (NHỬ GIẶC & MAI PHỤC)
        SpawnDaiVietFlotilla(terrain);

        // 6. THIẾT LẬP CAMERA & BẢNG ĐIỀU KHIỂN SA BÀN (SABAN HUD)
        SetupDirectorAndHUD();

        // 7. LƯU SCENE
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("=========================================================================");
        Debug.Log("🎉 CHIẾN TRƯỜNG BẠCH ĐẰNG 938 ĐÃ ĐƯỢC CẬP NHẬT HOÀN HẢO & LƯU THÀNH CÔNG!");
        Debug.Log("=========================================================================");
    }

    private static void CleanStaleAndMissingObjects()
    {
        var allGos = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        int cleanedCount = 0;

        foreach (var go in allGos)
        {
            if (go == null) continue;

            // Kiểm tra missing prefab
            bool isMissing = PrefabUtility.IsPartOfPrefabInstance(go) && PrefabUtility.GetCorrespondingObjectFromSource(go) == null;
            if (isMissing || go.name.Contains("Missing Prefab") || go.name.StartsWith("SM_LittleRock"))
            {
                Object.DestroyImmediate(go);
                cleanedCount++;
                continue;
            }

            // Dọn dẹp object làng test nằm lạc lõng tại Z = -1000m
            if (go.name == "Lang" || go.name.Contains("bighouse_02") || (go.transform.position.z < -800f && go.name.Contains("build_")))
            {
                Object.DestroyImmediate(go);
                cleanedCount++;
                continue;
            }
        }

        Debug.Log($"🧹 [Dọn dẹp] Đã loại bỏ {cleanedCount} vật thể rác/missing prefab trong scene.");
    }

    private static void BuildCampAndGarrison(Terrain terrain)
    {
        string pDir = "Assets/Prefabs/";
        GameObject pfBarracks = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_barracks_01.prefab");
        GameObject pfGate = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_gate_01.prefab");
        GameObject pfTower = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_tower_01.prefab");
        GameObject pfStorage = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_storage_01.prefab");
        GameObject pfBlacksmith = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_blacksmith_01.prefab");
        GameObject pfCrane = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_crane_01.prefab");
        GameObject pfFence = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_fence_01.prefab");
        GameObject pfBarrels = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_barrels_01.prefab");

        // Props mới
        GameObject pfTorch = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_torch_fire_01.prefab");
        GameObject pfSword = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_sword_01.prefab");
        GameObject pfHalberd = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_halberd_01.prefab");
        GameObject pfShield1 = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_shield_01.prefab");
        GameObject pfShield2 = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_shield_02.prefab");
        GameObject pfLogpile = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_logpile_01.prefab");
        GameObject pfPlankpile1 = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_plankpile_01.prefab");
        GameObject pfPlankpile2 = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_plankpile_02.prefab");
        GameObject pfPlankpath = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_plankpath_01.prefab");
        GameObject spikeFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/wood-spike/source/Wood_Spike_02_sculpted.fbx");
        Material spikeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/wood-spike/Materials/Wood_Spike_URP.mat");

        // NPC Models & Materials
        GameObject fbxNgoQuyen = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model_NPC/NgoQuyen/NgoQuyenModel/Meshy_AI_Ngo_Quyen_938_AD_biped_Character_output.fbx");
        Material matNgoQuyen = AssetDatabase.LoadAssetAtPath<Material>("Assets/Model_NPC/NgoQuyen/NgoQuyenModel/NgoQuyen.mat");

        GameObject fbxIronclad = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Main_Character/Meshy_AI_Steppe_Ironclad_biped/Meshy_AI_Steppe_Ironclad_biped/Meshy_AI_Steppe_Ironclad_biped_Character_output.fbx");
        Material matIronclad = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main_Character/Meshy_AI_Steppe_Ironclad_biped/Meshy_AI_Steppe_Ironclad_biped/Linh_Material.mat");

        GameObject fbxSmith = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model_NPC/DanThuongLonTuoi/Meshy_AI_Wandering_Peasant_biped_Character_output.fbx");
        Material matSmith = AssetDatabase.LoadAssetAtPath<Material>("Assets/Model_NPC/DanThuongLonTuoi/OldPeasent.mat");

        GameObject fbxMalePea = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model_NPC/MalePea/MalePea/Meshy_AI_Blue_Scrub_Figure_biped_Character_output.fbx");
        Material matMalePea = AssetDatabase.LoadAssetAtPath<Material>("Assets/Model_NPC/MalePea/MalePea/Male_Mat.mat");

        GameObject fbxYoungPea = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model_NPC/YoungPea/Young_Pea/Meshy_AI_Silent_Young_Disciple_biped_Character_output.fbx");
        Material matYoungPea = AssetDatabase.LoadAssetAtPath<Material>("Assets/Model_NPC/YoungPea/Young_Pea/YoungPea.mat");

        string campParentName = "--- CHIẾN TRƯỜNG: ĐẠI BẢN DOANH & XƯỞNG CỌC BẠCH ĐẰNG ---";
        var oldCamp = GameObject.Find(campParentName);
        if (oldCamp != null) Object.DestroyImmediate(oldCamp);

        GameObject campRoot = new GameObject(campParentName);
        campRoot.transform.position = Vector3.zero;

        // -------------------------------------------------------------
        // PHÂN KHU 1: ĐẠI BẢN DOANH TIỀN PHƯƠNG NGÔ QUYỀN
        // -------------------------------------------------------------
        GameObject hqGroup = new GameObject("1_DaiBanDoanh_NgoQuyen");
        hqGroup.transform.parent = campRoot.transform;

        Vector3 hqCenter = new Vector3(-820f, 17.5f, 320f);
        if (terrain != null) hqCenter.y = terrain.SampleHeight(hqCenter);

        // Nhà chỉ huy
        if (pfBarracks != null)
        {
            var hq = (GameObject)PrefabUtility.InstantiatePrefab(pfBarracks, hqGroup.transform);
            hq.transform.position = hqCenter;
            hq.transform.rotation = Quaternion.Euler(0, 90f, 0);
        }

        // Cổng trại hướng ra sông
        if (pfGate != null)
        {
            var gate = (GameObject)PrefabUtility.InstantiatePrefab(pfGate, hqGroup.transform);
            gate.transform.position = hqCenter + new Vector3(35f, 0, 0);
            gate.transform.rotation = Quaternion.Euler(0, 90f, 0);
        }

        // Chòi canh cao vút
        Vector3 towerPos = hqCenter + new Vector3(32f, 0, 28f);
        if (terrain != null) towerPos.y = terrain.SampleHeight(towerPos);
        if (pfTower != null)
        {
            var tw = (GameObject)PrefabUtility.InstantiatePrefab(pfTower, hqGroup.transform);
            tw.transform.position = towerPos;
            tw.transform.rotation = Quaternion.Euler(0, 45f, 0);
        }

        // Kho vũ khí lương thảo
        if (pfStorage != null)
        {
            var st = (GameObject)PrefabUtility.InstantiatePrefab(pfStorage, hqGroup.transform);
            st.transform.position = hqCenter + new Vector3(-25f, 0, 20f);
            st.transform.rotation = Quaternion.Euler(0, 180f, 0);
        }

        // Hàng rào phòng thủ
        if (pfFence != null)
        {
            for (int i = -3; i <= 3; i++)
            {
                if (Mathf.Abs(i) <= 1) continue;
                var fence = (GameObject)PrefabUtility.InstantiatePrefab(pfFence, hqGroup.transform);
                Vector3 fPos = hqCenter + new Vector3(35f, 0, i * 10f);
                if (terrain != null) fPos.y = terrain.SampleHeight(fPos);
                fence.transform.position = fPos;
                fence.transform.rotation = Quaternion.Euler(0, 90f, 0);
            }
        }

        // Thùng hàng lương thực
        if (pfBarrels != null)
        {
            var b1 = (GameObject)PrefabUtility.InstantiatePrefab(pfBarrels, hqGroup.transform);
            b1.transform.position = hqCenter + new Vector3(12f, 0, 15f);
        }

        // -------------------------------------------------------------
        // GIAO NHÂN VẬT & TƯỚNG LĨNH VÀO ĐẠI BẢN DOANH
        // -------------------------------------------------------------
        // A. CHỦ TƯỚNG NGÔ QUYỀN
        Vector3 nqPos = hqCenter + new Vector3(12f, 0f, 0f);
        if (terrain != null) nqPos.y = terrain.SampleHeight(nqPos);
        GameObject nqGo = SpawnCharacter(fbxNgoQuyen, matNgoQuyen, nqPos, Quaternion.Euler(0, 90f, 0), "Tuong_Ngo_Quyen", hqGroup.transform);
        if (nqGo != null && pfSword != null)
        {
            // Trao kiếm cho chủ tướng
            AttachPropToCharacter(pfSword, nqGo.transform, new Vector3(0.2f, 0.9f, 0.1f), Quaternion.Euler(20f, 0, 10f), 0.9f);
        }

        // B. 2 THỊ VỆ THIẾT GIÁP BẢO VỆ TƯỚNG NGÔ QUYỀN
        Vector3 guard1Pos = nqPos + new Vector3(-1.5f, 0, 3.5f);
        Vector3 guard2Pos = nqPos + new Vector3(-1.5f, 0, -3.5f);
        if (terrain != null)
        {
            guard1Pos.y = terrain.SampleHeight(guard1Pos);
            guard2Pos.y = terrain.SampleHeight(guard2Pos);
        }
        var g1 = SpawnCharacter(fbxIronclad, matIronclad, guard1Pos, Quaternion.Euler(0, 80f, 0), "ThiVe_CanhVe_1", hqGroup.transform);
        var g2 = SpawnCharacter(fbxIronclad, matIronclad, guard2Pos, Quaternion.Euler(0, 100f, 0), "ThiVe_CanhVe_2", hqGroup.transform);
        if (g1 != null && pfHalberd != null) AttachPropToCharacter(pfHalberd, g1.transform, new Vector3(0.35f, 0.8f, 0.2f), Quaternion.Euler(0, 0, -10f), 1f);
        if (g2 != null && pfHalberd != null) AttachPropToCharacter(pfHalberd, g2.transform, new Vector3(0.35f, 0.8f, 0.2f), Quaternion.Euler(0, 0, -10f), 1f);

        // C. 2 LÍNH GÁC CỔNG TRẠI (HALBERD + SHIELD)
        Vector3 gateSentry1 = hqCenter + new Vector3(37f, 0, 6.5f);
        Vector3 gateSentry2 = hqCenter + new Vector3(37f, 0, -6.5f);
        if (terrain != null)
        {
            gateSentry1.y = terrain.SampleHeight(gateSentry1);
            gateSentry2.y = terrain.SampleHeight(gateSentry2);
        }
        var gs1 = SpawnCharacter(fbxIronclad, matIronclad, gateSentry1, Quaternion.Euler(0, 90f, 0), "Linh_Gac_Cong_Bac", hqGroup.transform);
        var gs2 = SpawnCharacter(fbxIronclad, matIronclad, gateSentry2, Quaternion.Euler(0, 90f, 0), "Linh_Gac_Cong_Nam", hqGroup.transform);
        if (gs1 != null)
        {
            if (pfHalberd != null) AttachPropToCharacter(pfHalberd, gs1.transform, new Vector3(0.35f, 0.8f, 0.2f), Quaternion.Euler(0, 0, -10f), 1f);
            if (pfShield1 != null) AttachPropToCharacter(pfShield1, gs1.transform, new Vector3(-0.35f, 0.9f, 0.15f), Quaternion.Euler(0, 90f, 0), 0.9f);
        }
        if (gs2 != null)
        {
            if (pfHalberd != null) AttachPropToCharacter(pfHalberd, gs2.transform, new Vector3(0.35f, 0.8f, 0.2f), Quaternion.Euler(0, 0, -10f), 1f);
            if (pfShield1 != null) AttachPropToCharacter(pfShield1, gs2.transform, new Vector3(-0.35f, 0.9f, 0.15f), Quaternion.Euler(0, 90f, 0), 0.9f);
        }

        // D. LÍNH TRINH SÁT TRÊN CHÒI CANH (TOWER SENTRY)
        Vector3 towerLookoutPos = towerPos + new Vector3(0f, 8.4f, 0f);
        SpawnCharacter(fbxIronclad, matIronclad, towerLookoutPos, Quaternion.Euler(0, 90f, 0), "Linh_Trinh_Sat_Choi_Canh", hqGroup.transform);

        // E. ĐUỐC LỬA CHIẾN TRƯỜNG TẠI CỔNG & BẢN DOANH
        SpawnTorch(pfTorch, hqCenter + new Vector3(36f, 0, 7.8f), terrain, hqGroup.transform, "Duoc_Cong_Bac");
        SpawnTorch(pfTorch, hqCenter + new Vector3(36f, 0, -7.8f), terrain, hqGroup.transform, "Duoc_Cong_Nam");
        SpawnTorch(pfTorch, nqPos + new Vector3(0, 0, 6f), terrain, hqGroup.transform, "Duoc_Doanh_Bac");
        SpawnTorch(pfTorch, nqPos + new Vector3(0, 0, -6f), terrain, hqGroup.transform, "Duoc_Doanh_Nam");
        SpawnTorch(pfTorch, towerPos + new Vector3(1.2f, 8.4f, 1.2f), null, hqGroup.transform, "Duoc_Choi_Canh");

        // F. GIÁ VŨ KHÍ & KHIÊN
        if (pfHalberd != null)
        {
            var rackHalberd = (GameObject)PrefabUtility.InstantiatePrefab(pfHalberd, hqGroup.transform);
            rackHalberd.transform.position = hqCenter + new Vector3(32f, 1.5f, 12f);
            rackHalberd.transform.rotation = Quaternion.Euler(-15f, 90f, 0);
        }
        if (pfShield2 != null)
        {
            var rackShield = (GameObject)PrefabUtility.InstantiatePrefab(pfShield2, hqGroup.transform);
            rackShield.transform.position = hqCenter + new Vector3(32f, 0.6f, 13f);
            rackShield.transform.rotation = Quaternion.Euler(0, 105f, 15f);
        }

        // -------------------------------------------------------------
        // PHÂN KHU 2: XƯỞNG RÈN BỊT SẮT & BÃI ĐẼO CỌC LIM
        // -------------------------------------------------------------
        GameObject workshopGroup = new GameObject("2_XuongRen_Va_BaiDeoCoc");
        workshopGroup.transform.parent = campRoot.transform;

        Vector3 wsCenter = new Vector3(-680f, 17.55f, 260f);
        if (terrain != null) wsCenter.y = terrain.SampleHeight(wsCenter);

        // Lò rèn
        if (pfBlacksmith != null)
        {
            var bs = (GameObject)PrefabUtility.InstantiatePrefab(pfBlacksmith, workshopGroup.transform);
            bs.transform.position = wsCenter;
            bs.transform.rotation = Quaternion.Euler(0, 100f, 0);
        }

        // Cần cẩu bốc xếp cọc gỗ lim
        Vector3 cranePos = wsCenter + new Vector3(25f, 0, -10f);
        if (terrain != null) cranePos.y = terrain.SampleHeight(cranePos);
        if (pfCrane != null)
        {
            var crane = (GameObject)PrefabUtility.InstantiatePrefab(pfCrane, workshopGroup.transform);
            crane.transform.position = cranePos;
            crane.transform.rotation = Quaternion.Euler(0, -60f, 0);
        }

        // LÃO THỢ RÈN BÊN LÒ RÈN & ĐE
        Vector3 smithPos = wsCenter + new Vector3(2.5f, 0, 1.5f);
        if (terrain != null) smithPos.y = terrain.SampleHeight(smithPos);
        SpawnCharacter(fbxSmith, matSmith, smithPos, Quaternion.Euler(0, 120f, 0), "Lao_Tho_Ren_Bit_Sat", workshopGroup.transform);

        // DÂN BINH KHIÊNG GỖ & CHẾ TÁC CỌC
        Vector3 worker1Pos = wsCenter + new Vector3(18f, 0, -6f);
        Vector3 worker2Pos = wsCenter + new Vector3(22f, 0, -14f);
        if (terrain != null)
        {
            worker1Pos.y = terrain.SampleHeight(worker1Pos);
            worker2Pos.y = terrain.SampleHeight(worker2Pos);
        }
        SpawnCharacter(fbxMalePea, matMalePea, worker1Pos, Quaternion.Euler(0, -45f, 0), "Dan_Binh_Deo_Coc_1", workshopGroup.transform);
        SpawnCharacter(fbxYoungPea, matYoungPea, worker2Pos, Quaternion.Euler(0, 45f, 0), "Dan_Phu_Khang_Go_2", workshopGroup.transform);

        // BÃI GỖ KHỔNG LỒ & CỌC ĐÃ CHẾ TÁC SẴN
        if (pfLogpile != null)
        {
            var logs = (GameObject)PrefabUtility.InstantiatePrefab(pfLogpile, workshopGroup.transform);
            Vector3 logPos = wsCenter + new Vector3(15f, 0, -18f);
            if (terrain != null) logPos.y = terrain.SampleHeight(logPos);
            logs.transform.position = logPos;
            logs.transform.rotation = Quaternion.Euler(0, 30f, 0);
        }

        if (pfPlankpile1 != null)
        {
            var planks1 = (GameObject)PrefabUtility.InstantiatePrefab(pfPlankpile1, workshopGroup.transform);
            Vector3 pPos1 = wsCenter + new Vector3(28f, 0, -4f);
            if (terrain != null) pPos1.y = terrain.SampleHeight(pPos1);
            planks1.transform.position = pPos1;
        }

        if (pfPlankpile2 != null)
        {
            var planks2 = (GameObject)PrefabUtility.InstantiatePrefab(pfPlankpile2, workshopGroup.transform);
            Vector3 pPos2 = wsCenter + new Vector3(30f, 0, -16f);
            if (terrain != null) pPos2.y = terrain.SampleHeight(pPos2);
            planks2.transform.position = pPos2;
            planks2.transform.rotation = Quaternion.Euler(0, 60f, 0);
        }

        // CỌC LIM CHẾ TÁC XONG XẾP CẠNH CẦN CẨU ĐỂ BỐC LÊN THUYỀN
        if (spikeFbx != null)
        {
            for (int s = 0; s < 3; s++)
            {
                var spikeSample = (GameObject)PrefabUtility.InstantiatePrefab(spikeFbx, workshopGroup.transform);
                Vector3 sPos = cranePos + new Vector3(3f + s * 1.8f, 0.4f, 2f - s * 0.8f);
                spikeSample.name = $"CocLim_ThanhPham_{s+1}";
                spikeSample.transform.position = sPos;
                spikeSample.transform.rotation = Quaternion.Euler(12f, 70f + s * 15f, 0);
                spikeSample.transform.localScale = new Vector3(1.2f, 1.2f, 2.5f);
                if (spikeMat != null)
                {
                    foreach (var r in spikeSample.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = spikeMat;
                }
            }
        }

        // ĐUỐC SÁNG XƯỞNG RÈN
        SpawnTorch(pfTorch, wsCenter + new Vector3(-4f, 0, 4f), terrain, workshopGroup.transform, "Duoc_Lo_Ren_1");
        SpawnTorch(pfTorch, wsCenter + new Vector3(5f, 0, -3f), terrain, workshopGroup.transform, "Duoc_Lo_Ren_2");

        // -------------------------------------------------------------
        // PHÂN KHU 3: BẾN THUYỀN VEN SÔNG & ĐƯỜNG VÁN GỖ (PIER & DOCK)
        // -------------------------------------------------------------
        GameObject dockGroup = new GameObject("3_BenThuyen_Va_DuongGo");
        dockGroup.transform.parent = campRoot.transform;

        if (pfPlankpath != null)
        {
            // Lát đường ván gỗ từ xưởng rèn ra mép nước
            for (int p = 0; p < 6; p++)
            {
                float tStep = (float)p / 5f;
                Vector3 pathPos = Vector3.Lerp(wsCenter + new Vector3(35f, 0, -10f), new Vector3(-595f, 14.3f, 250f), tStep);
                if (terrain != null)
                {
                    float gH = terrain.SampleHeight(pathPos);
                    pathPos.y = Mathf.Max(gH + 0.1f, 14.1f);
                }
                var path = (GameObject)PrefabUtility.InstantiatePrefab(pfPlankpath, dockGroup.transform);
                path.transform.position = pathPos;
                path.transform.rotation = Quaternion.Euler(0, -65f, 0);
            }
        }

        // ĐUỐC CẮM VEN BẾN THUYỀN
        SpawnTorch(pfTorch, new Vector3(-600f, 14.3f, 245f), terrain, dockGroup.transform, "Duoc_Ben_Thuyen_1");
        SpawnTorch(pfTorch, new Vector3(-592f, 14.3f, 258f), terrain, dockGroup.transform, "Duoc_Ben_Thuyen_2");

        Debug.Log("⛺ [Đại Bản Doanh] Đã khởi tạo hoàn tất doanh trại Ngô Quyền, các chốt gác, xưởng rèn, bến thuyền và hệ thống đuốc sáng.");
    }

    private static void SpawnDaiVietFlotilla(Terrain terrain)
    {
        string pDir = "Assets/Prefabs/";
        GameObject pfBoat1 = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_boat_01.prefab");
        GameObject pfBoat2 = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_boat_02.prefab");

        GameObject fbxIronclad = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Main_Character/Meshy_AI_Steppe_Ironclad_biped/Meshy_AI_Steppe_Ironclad_biped/Meshy_AI_Steppe_Ironclad_biped_Character_output.fbx");
        Material matIronclad = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main_Character/Meshy_AI_Steppe_Ironclad_biped/Meshy_AI_Steppe_Ironclad_biped/Linh_Material.mat");

        GameObject pfHalberd = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_halberd_01.prefab");
        GameObject pfShield1 = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_shield_01.prefab");

        string flotillaName = "--- HẠM ĐỘI THUYỀN CHIẾN ĐẠI VIỆT (938) ---";
        var oldFlotilla = GameObject.Find(flotillaName);
        if (oldFlotilla != null) Object.DestroyImmediate(oldFlotilla);

        GameObject flotillaRoot = new GameObject(flotillaName);
        flotillaRoot.transform.position = Vector3.zero;

        float waterY = 14.15f; // Mực nước nổi chuẩn

        // =====================================================================
        // 1. ĐỘI THUYỀN NAN NHỬ GIẶC (Vanguard Lure Squadron)
        // Bơi khiêu chiến phía trước bãi cọc rồi giả vờ thua, rút lui ngược dòng
        // =====================================================================
        Vector3 boat1Pos = new Vector3(-335f, waterY, 280f);
        CreateWarBoat(pfBoat1, boat1Pos, Quaternion.Euler(0, 205f, 0), "Thuyen_Nhu_Giac_1_TienPhong", flotillaRoot.transform, fbxIronclad, matIronclad, pfHalberd, pfShield1);

        Vector3 boat2Pos = new Vector3(-315f, waterY, 345f);
        CreateWarBoat(pfBoat2, boat2Pos, Quaternion.Euler(0, 215f, 0), "Thuyen_Nhu_Giac_2_HuuDuc", flotillaRoot.transform, fbxIronclad, matIronclad, pfHalberd, pfShield1);

        // =====================================================================
        // 2. THUYỀN TRỰC CHIẾN BẾN BẢN DOANH (Dock Flagship Boat)
        // Neo đậu ngay đầu cầu tàu gỗ
        // =====================================================================
        Vector3 boat3Pos = new Vector3(-585f, waterY, 250f);
        CreateWarBoat(pfBoat1, boat3Pos, Quaternion.Euler(0, 45f, 0), "Thuyen_Tuan_Tieu_Ben_Doanh_Trai", flotillaRoot.transform, fbxIronclad, matIronclad, pfHalberd, pfShield1);

        // =====================================================================
        // 3. ĐỘI THUYỀN PHỤC KÍCH RẠCH LAU SẬY (Flank Ambush Squadron)
        // Mai phục sẵn trong các ngách lạch lau sậy bên bờ Tây, chờ triều rút xông ra khóa đuôi
        // =====================================================================
        Vector3 boat4Pos = new Vector3(-510f, waterY, 430f);
        CreateWarBoat(pfBoat2, boat4Pos, Quaternion.Euler(0, 110f, 0), "Thuyen_Mai_Phuc_Rach_Bac", flotillaRoot.transform, fbxIronclad, matIronclad, pfHalberd, pfShield1);

        Vector3 boat5Pos = new Vector3(-480f, waterY, 150f);
        CreateWarBoat(pfBoat1, boat5Pos, Quaternion.Euler(0, 70f, 0), "Thuyen_Mai_Phuc_Rach_Nam", flotillaRoot.transform, fbxIronclad, matIronclad, pfHalberd, pfShield1);

        Debug.Log("🛶 [Hạm đội Đại Việt] Đã bố trí 5 chiến thuyền nhử giặc và mai phục đầy đủ thủy binh, giáo mác khiên gỗ.");
    }

    private static void CreateWarBoat(GameObject pfBoat, Vector3 pos, Quaternion rot, string name, Transform parent, GameObject fbxCrew, Material matCrew, GameObject pfHalberd, GameObject pfShield)
    {
        if (pfBoat == null) return;
        var boatGo = (GameObject)PrefabUtility.InstantiatePrefab(pfBoat, parent);
        boatGo.name = name;
        boatGo.transform.position = pos;
        boatGo.transform.rotation = rot;

        // Thêm collider cho thuyền nếu chưa có
        if (boatGo.GetComponentInChildren<Collider>() == null)
        {
            var box = boatGo.AddComponent<BoxCollider>();
            box.size = new Vector3(3.5f, 2.0f, 8.5f);
            box.center = new Vector3(0, 1.0f, 0);
        }

        // Bố trí 2 thủy binh Đại Việt trên thuyền
        if (fbxCrew != null)
        {
            Vector3 crewPos1 = pos + rot * new Vector3(0f, 0.4f, 1.5f);
            Vector3 crewPos2 = pos + rot * new Vector3(0f, 0.4f, -1.5f);

            var c1 = SpawnCharacter(fbxCrew, matCrew, crewPos1, rot * Quaternion.Euler(0, 15f, 0), $"{name}_ThuyBinh_1", boatGo.transform);
            var c2 = SpawnCharacter(fbxCrew, matCrew, crewPos2, rot * Quaternion.Euler(0, -15f, 0), $"{name}_ThuyBinh_2", boatGo.transform);

            if (c1 != null && pfHalberd != null) AttachPropToCharacter(pfHalberd, c1.transform, new Vector3(0.35f, 0.8f, 0.2f), Quaternion.Euler(0, 0, -10f), 1f);
            if (c2 != null && pfShield != null) AttachPropToCharacter(pfShield, c2.transform, new Vector3(-0.35f, 0.9f, 0.15f), Quaternion.Euler(0, 90f, 0), 0.9f);
        }
    }

    private static GameObject SpawnCharacter(GameObject fbx, Material mat, Vector3 pos, Quaternion rot, string name, Transform parent)
    {
        if (fbx == null) return null;
        var characterGo = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
        characterGo.name = name;
        characterGo.transform.position = pos;
        characterGo.transform.rotation = rot;
        characterGo.transform.localScale = Vector3.one;

        // Gán material URP Lit chuẩn
        if (mat != null)
        {
            var rends = characterGo.GetComponentsInChildren<Renderer>();
            foreach (var r in rends) r.sharedMaterial = mat;
        }

        // Thêm CapsuleCollider để nhân vật có thể va chạm vật lý
        if (characterGo.GetComponentInChildren<Collider>() == null)
        {
            var cap = characterGo.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 0.95f, 0);
            cap.radius = 0.35f;
            cap.height = 1.9f;
        }

        return characterGo;
    }

    private static void AttachPropToCharacter(GameObject pfProp, Transform charTransform, Vector3 localPos, Quaternion localRot, float scale = 1f)
    {
        if (pfProp == null || charTransform == null) return;
        var propInstance = (GameObject)PrefabUtility.InstantiatePrefab(pfProp, charTransform);
        propInstance.transform.localPosition = localPos;
        propInstance.transform.localRotation = localRot;
        propInstance.transform.localScale = Vector3.one * scale;

        // Xóa collider của vũ khí gắn trên người để không bị lỗi physics glitch
        var cols = propInstance.GetComponentsInChildren<Collider>();
        foreach (var c in cols) Object.DestroyImmediate(c);
    }

    private static void SpawnTorch(GameObject pfTorch, Vector3 pos, Terrain terrain, Transform parent, string name)
    {
        if (pfTorch == null) return;
        if (terrain != null) pos.y = terrain.SampleHeight(pos);

        var torch = (GameObject)PrefabUtility.InstantiatePrefab(pfTorch, parent);
        torch.name = name;
        torch.transform.position = pos;
        torch.transform.rotation = Quaternion.identity;
    }

    private static void SetupDirectorAndHUD()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            if (cam.GetComponent<BattleCamera>() == null) cam.gameObject.AddComponent<BattleCamera>();
            if (cam.GetComponent<SaBanHUD>() == null) cam.gameObject.AddComponent<SaBanHUD>();

            cam.farClipPlane = 9500f;
            cam.transform.position = new Vector3(-470f, 26f, 300f);
            cam.transform.LookAt(new Vector3(-350f, 14f, 300f));
        }

        // Đảm bảo có TideSystem trên Plane mặt nước
        var waterPlane = GameObject.Find("Plane");
        if (waterPlane != null && waterPlane.GetComponent<TideSystem>() == null)
        {
            waterPlane.AddComponent<TideSystem>();
        }
    }
}
