using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class CompleteCinematicBattlefield
{
    private const string SCENE_PATH = "Assets/Scenes/beachBoat.unity";

    [MenuItem("🤖 Trợ lý AI/🌟 NÂNG CẤP DOANH TRẠI & BỐI CẢNH THIÊN NHIÊN")]
    public static void ExecuteCompleteBeautification()
    {
        Debug.Log("=========================================================================");
        Debug.Log("🌟 BẮT ĐẦU HOÀN THIỆN CẢNH VẬT: DOANH TRẠI, BỜ SÔNG, BÃI SẬY & KHÍ QUYỂN...");
        Debug.Log("=========================================================================");

        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            var all = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            if (all.Length > 0) terrain = all[0];
        }

        if (terrain == null)
        {
            Debug.LogError("❌ Không tìm thấy Terrain!");
            return;
        }

        // 1. CẤU HÌNH BẦU TRỜI, ÁNH SÁNG & SƯƠNG MÙ SÔNG BẠCH ĐẰNG
        SetupAtmosphere();

        // 2. NÂNG CẤP ĐẠI BẢN DOANH: TƯỜNG GỖ KIÊN CỐ, LÁN TRẠI QUÂN ĐỘI, CÂY XANH BAO QUANH
        UpgradeCampAndWilderness(terrain);

        // 3. CẬP NHẬT CAMERA ĐIỆN ẢNH VÀO GÓC NHÌN ĐẸP NHẤT
        UpdateCameraAngles();

        // 4. LƯU SCENE
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("=========================================================================");
        Debug.Log("🎉 HOÀN THIỆN TOÀN DIỆN CẢNH VẬT CHIẾN TRƯỜNG BẠCH ĐẰNG 938 THÀNH CÔNG!");
        Debug.Log("=========================================================================");
    }

    private static void SetupAtmosphere()
    {
        // Skybox ban ngày mây cuộn
        Material skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/OptiWater/Demo/Fantasy Skybox FREE/Panoramics/FS003/FS003_Day.mat");
        if (skyMat != null) RenderSettings.skybox = skyMat;

        // Sương mù sông nước Bạch Đằng
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 200f;
        RenderSettings.fogEndDistance = 3500f;
        RenderSettings.fogColor = new Color(0.70f, 0.81f, 0.90f, 1.0f);

        // Mặt trời ban mai đổ bóng mềm
        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }
        }

        if (sun != null)
        {
            RenderSettings.sun = sun;
            sun.transform.rotation = Quaternion.Euler(32f, 130f, 0f);
            sun.color = new Color(1.0f, 0.95f, 0.87f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
        }

        QualitySettings.shadowDistance = 2000f;
    }

    private static void UpgradeCampAndWilderness(Terrain terrain)
    {
        string pB = "Assets/Prefabs/Buildings/";
        string pP = "Assets/Prefabs/Props/";
        string nrArt = "Assets/Visual Design Cafe/Nature Renderer Demo/Realistic/Art/";

        GameObject pfWall = AssetDatabase.LoadAssetAtPath<GameObject>(pB + "pf_build_wall_panel_01.prefab");
        if (pfWall == null) pfWall = AssetDatabase.LoadAssetAtPath<GameObject>(pP + "pf_wall_logs_04.prefab");

        GameObject pfSmallCottage = AssetDatabase.LoadAssetAtPath<GameObject>(pB + "pf_build_small_house_straw_roof_01.prefab");
        GameObject pfShed = AssetDatabase.LoadAssetAtPath<GameObject>(pP + "pf_shed_01.prefab");
        GameObject pfHalberd = AssetDatabase.LoadAssetAtPath<GameObject>(pP + "pf_halberd_01.prefab");

        GameObject pfCypress = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Trees/Cypress.prefab");
        GameObject pfConifer = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Trees/Conifer.prefab");
        GameObject pfBush = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TerrainSampleAssets/Prefabs/Bush_A.prefab");
        GameObject pfFern = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TerrainSampleAssets/Prefabs/Fern_A.prefab");

        GameObject campRoot = GameObject.Find("--- CHIẾN TRƯỜNG: ĐẠI BẢN DOANH & XƯỞNG CỌC BẠCH ĐẰNG ---");
        if (campRoot == null) return;

        Transform hqGroup = campRoot.transform.Find("1_DaiBanDoanh_NgoQuyen");
        if (hqGroup == null) return;

        Vector3 hqCenter = new Vector3(-820f, 17.5f, 320f);
        if (terrain != null) hqCenter.y = terrain.SampleHeight(hqCenter);

        // =====================================================================
        // 1. TƯỜNG GỖ PHÒNG THỦ LIỀN MẠCH NỐI VÀO CỔNG TRẠI (PALISADE WALLS)
        // =====================================================================
        var oldWalls = hqGroup.Find("Tuong_Go_Doanh_Trai");
        if (oldWalls != null) Object.DestroyImmediate(oldWalls.gameObject);

        GameObject wallGroup = new GameObject("Tuong_Go_Doanh_Trai");
        wallGroup.transform.parent = hqGroup;

        // Xóa các fence rời rạc cũ
        for (int i = hqGroup.childCount - 1; i >= 0; i--)
        {
            var child = hqGroup.GetChild(i);
            if (child.name.StartsWith("pf_fence_01")) Object.DestroyImmediate(child.gameObject);
        }

        // Dựng hàng rào gỗ kiên cố phía Đông (hai bên cổng)
        if (pfWall != null)
        {
            // Cánh tường phía Bắc: từ Z = 326 đến Z = 358
            for (float z = 326f; z <= 358f; z += 5.2f)
            {
                var wall = (GameObject)PrefabUtility.InstantiatePrefab(pfWall, wallGroup.transform);
                Vector3 wPos = new Vector3(-785f, 0, z);
                if (terrain != null) wPos.y = terrain.SampleHeight(wPos);
                wall.transform.position = wPos;
                wall.transform.rotation = Quaternion.Euler(0, 90f, 0);
            }

            // Cánh tường phía Nam: từ Z = 282 đến Z = 314
            for (float z = 282f; z <= 314f; z += 5.2f)
            {
                var wall = (GameObject)PrefabUtility.InstantiatePrefab(pfWall, wallGroup.transform);
                Vector3 wPos = new Vector3(-785f, 0, z);
                if (terrain != null) wPos.y = terrain.SampleHeight(wPos);
                wall.transform.position = wPos;
                wall.transform.rotation = Quaternion.Euler(0, 90f, 0);
            }
        }

        // =====================================================================
        // 2. KHU LÁN TRẠI QUÂN ĐỘI & KHO VŨ KHÍ PHỤ (SOLDIER BARRACKS)
        // =====================================================================
        var oldBarracks = hqGroup.Find("Khu_Lan_Trai_Quan_Doi");
        if (oldBarracks != null) Object.DestroyImmediate(oldBarracks.gameObject);

        GameObject barracksGroup = new GameObject("Khu_Lan_Trai_Quan_Doi");
        barracksGroup.transform.parent = hqGroup;

        if (pfSmallCottage != null)
        {
            // Lán lính phía Bắc
            var cotNorth = (GameObject)PrefabUtility.InstantiatePrefab(pfSmallCottage, barracksGroup.transform);
            Vector3 cnPos = hqCenter + new Vector3(-10f, 0, 32f);
            if (terrain != null) cnPos.y = terrain.SampleHeight(cnPos);
            cotNorth.transform.position = cnPos;
            cotNorth.transform.rotation = Quaternion.Euler(0, 110f, 0);

            // Lán lính phía Nam
            var cotSouth = (GameObject)PrefabUtility.InstantiatePrefab(pfSmallCottage, barracksGroup.transform);
            Vector3 csPos = hqCenter + new Vector3(-12f, 0, -32f);
            if (terrain != null) csPos.y = terrain.SampleHeight(csPos);
            cotSouth.transform.position = csPos;
            cotSouth.transform.rotation = Quaternion.Euler(0, 70f, 0);
        }

        if (pfShed != null)
        {
            var shed = (GameObject)PrefabUtility.InstantiatePrefab(pfShed, barracksGroup.transform);
            Vector3 shedPos = hqCenter + new Vector3(8f, 0, -28f);
            if (terrain != null) shedPos.y = terrain.SampleHeight(shedPos);
            shed.transform.position = shedPos;
            shed.transform.rotation = Quaternion.Euler(0, 0, 0);
        }

        if (pfHalberd != null)
        {
            var hRack = (GameObject)PrefabUtility.InstantiatePrefab(pfHalberd, barracksGroup.transform);
            hRack.transform.position = hqCenter + new Vector3(10f, 1.2f, -25f);
            hRack.transform.rotation = Quaternion.Euler(-15f, 0, 0);
        }

        // =====================================================================
        // 3. CÂY CỐI & BỤI RẬM KHUÔN VIÊN DOANH TRẠI (WILDERNESS INTEGRATION)
        // =====================================================================
        var oldNature = hqGroup.Find("Khung_Canh_Cay_Rung_Bao_Quanh");
        if (oldNature != null) Object.DestroyImmediate(oldNature.gameObject);

        GameObject natureFrameGroup = new GameObject("Khung_Canh_Cay_Rung_Bao_Quanh");
        natureFrameGroup.transform.parent = hqGroup;

        // Trồng cây cổ thụ và thông bách viền quanh doanh trại
        for (int i = 0; i < 26; i++)
        {
            float angle = Random.Range(0f, 360f);
            if (Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) < 35f) continue; // Tránh lối đi cổng chính

            float dist = Random.Range(42f, 85f);
            float rad = angle * Mathf.Deg2Rad;
            Vector3 treePos = hqCenter + new Vector3(Mathf.Cos(rad) * dist, 0, Mathf.Sin(rad) * dist);
            if (terrain != null) treePos.y = terrain.SampleHeight(treePos);

            GameObject treePrefab = (Random.value < 0.5f && pfCypress != null) ? pfCypress : pfConifer;
            if (treePrefab != null)
            {
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, natureFrameGroup.transform);
                tree.transform.position = treePos;
                tree.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                float sc = Random.Range(1.3f, 2.1f);
                tree.transform.localScale = Vector3.one * sc;
            }
        }

        // Bụi rậm và dương xỉ viền chân tường gỗ
        for (int i = 0; i < 30; i++)
        {
            float randZ = Random.Range(275f, 365f);
            float randX = Random.Range(-835f, -780f);
            Vector3 bushPos = new Vector3(randX, 0, randZ);
            if (terrain != null) bushPos.y = terrain.SampleHeight(bushPos);

            GameObject bushPrefab = (Random.value < 0.6f && pfBush != null) ? pfBush : pfFern;
            if (bushPrefab != null)
            {
                var bush = (GameObject)PrefabUtility.InstantiatePrefab(bushPrefab, natureFrameGroup.transform);
                bush.transform.position = bushPos;
                bush.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                bush.transform.localScale = Vector3.one * Random.Range(0.9f, 1.6f);
            }
        }

        Debug.Log("🌲 [Doanh Trại] Đã dựng tường gỗ liền mạch, 2 lán trại quân đội, trồng thông bách và bụi rậm bao quanh.");
    }

    private static void UpdateCameraAngles()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(-460f, 24f, 300f);
            cam.transform.LookAt(new Vector3(-350f, 14.2f, 300f));
        }
    }
}
