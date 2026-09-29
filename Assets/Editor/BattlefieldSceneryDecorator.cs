using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public static class BattlefieldSceneryDecorator
{
    private const string SCENE_PATH = "Assets/Scenes/beachBoat.unity";

    [MenuItem("🤖 Trợ lý AI/🌿 NÂNG CẤP CẢNH VẬT: BẦU TRỜI, SƯƠNG MÙ, BÃI SẬY & ĐÁ VEN SÔNG")]
    public static void DecorateLandscapeAndAtmosphere()
    {
        Debug.Log("=========================================================================");
        Debug.Log("🌿 BẮT ĐẦU NÂNG CẤP TOÀN DIỆN CẢNH VẬT THIÊN NHIÊN CHIẾN TRƯỜNG BẠCH ĐẰNG...");
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

        // 1. NÂNG CẤP BẦU TRỜI, ÁNH SÁNG & SƯƠNG MÙ SÔNG NƯỚC (ATMOSPHERE & LIGHTING)
        ApplyAtmosphereAndLighting();

        // 2. PHỦ BÃI LAU SẬY, BỤI CÂY NGẬP MẶN & MỎM ĐÁ BỜ SÔNG (SHORELINE REEDS & ROCKS)
        SpawnShorelineFloraAndRocks(terrain);

        // 3. XÂY ĐỐNG LỬA TRẠI BẬP BÙNG & KHÓI CHIẾN TRẬN TRONG DOANH TRẠI
        SpawnCampfire(terrain);

        // 4. LƯU SCENE
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("=========================================================================");
        Debug.Log("🎉 HOÀN TẤT NÂNG CẤP CẢNH VẬT THIÊN NHIÊN & KHÍ QUYỂN BẠCH ĐẰNG 938!");
        Debug.Log("=========================================================================");
    }

    private static void ApplyAtmosphereAndLighting()
    {
        // A. BẦU TRỜI (SKYBOX)
        string skyboxPath = "Assets/OptiWater/Demo/Fantasy Skybox FREE/Panoramics/FS003/FS003_Day.mat";
        Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(skyboxPath);
        if (skyboxMat != null)
        {
            RenderSettings.skybox = skyboxMat;
            Debug.Log($"🌅 Đã gán Skybox mây cuộn tráng lệ: {skyboxMat.name}");
        }

        // B. SƯƠNG MÙ SÔNG NƯỚC BẠCH ĐẰNG (RIVER MIST / AERIAL PERSPECTIVE)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 200f;
        RenderSettings.fogEndDistance = 3200f;
        RenderSettings.fogColor = new Color(0.70f, 0.81f, 0.90f, 1.0f); // Sương lam ban mai

        // C. ÁNH SÁNG MÔI TRƯỜNG (AMBIENT LIGHT)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.1f;

        // D. MẶT TRỜI & ĐỔ BÓNG (DIRECTIONAL LIGHT)
        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }
        }

        if (sun != null)
        {
            RenderSettings.sun = sun;
            sun.transform.rotation = Quaternion.Euler(32f, 135f, 0f); // Chiếu xiên góc tạo bóng đổ dài hùng vĩ
            sun.color = new Color(1.0f, 0.95f, 0.87f); // Nắng vàng ban mai ấm áp
            sun.intensity = 1.30f;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            Debug.Log($"☀️ Đã chỉnh góc chiếu mặt trời (32°, 135°) và đổ bóng mềm (SoftShadows).");
        }

        // Tầm đổ bóng xa bao trọn tầm mắt
        QualitySettings.shadowDistance = 1800f;
    }

    private static void SpawnShorelineFloraAndRocks(Terrain terrain)
    {
        string pDir = "Assets/TerrainSampleAssets/Prefabs/";
        string nrArt = "Assets/Visual Design Cafe/Nature Renderer Demo/Realistic/Art/";

        // Danh sách bụi cây, dương xỉ, lau sậy
        var bushPrefabs = new List<GameObject>
        {
            AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Bush_A.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Bush_B.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "BushDry_A.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Fern_A.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Fern_B.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Plant_A.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Plant_C.prefab")
        };
        bushPrefabs.RemoveAll(p => p == null);

        // Đá tảng bờ sông
        GameObject rockA = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Rocks/Rock_A_02.prefab");
        GameObject rockC = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Rocks/Rock_C_01.prefab");

        string groupName = "--- CẢNH QUAN THIÊN NHIÊN: BÃI SẬY & ĐÁ BỜ SÔNG ---";
        var oldGroup = GameObject.Find(groupName);
        if (oldGroup != null) Object.DestroyImmediate(oldGroup);

        GameObject root = new GameObject(groupName);
        root.transform.position = Vector3.zero;

        // =====================================================================
        // VÙNG 1: RẠCH LAU SẬY MAI PHỤC PHÍA BẮC (X: -550 -> -470, Z: 390 -> 470)
        // Che giấu Thuyền Mai Phục Rạch Bắc
        // =====================================================================
        GameObject northCove = new GameObject("1_BaiSay_RachMaiPhuc_Bac");
        northCove.transform.parent = root.transform;
        PopulateShorePatch(terrain, northCove.transform, -550f, -470f, 390f, 470f, 45, bushPrefabs, rockA, rockC);

        // =====================================================================
        // VÙNG 2: RẠCH LAU SẬY MAI PHỤC PHÍA NAM (X: -530 -> -450, Z: 110 -> 190)
        // Che giấu Thuyền Mai Phục Rạch Nam
        // =====================================================================
        GameObject southCove = new GameObject("2_BaiSay_RachMaiPhuc_Nam");
        southCove.transform.parent = root.transform;
        PopulateShorePatch(terrain, southCove.transform, -530f, -450f, 110f, 190f, 40, bushPrefabs, rockA, rockC);

        // =====================================================================
        // VÙNG 3: BỜ BIỂN & BẾN THUYỀN DOANH TRẠI (X: -650 -> -560, Z: 220 -> 280)
        // Bụi cây hoang sơ và đá cuội chân sóng
        // =====================================================================
        GameObject dockShore = new GameObject("3_BoSong_BenThuyen_DoanhTrai");
        dockShore.transform.parent = root.transform;
        PopulateShorePatch(terrain, dockShore.transform, -650f, -560f, 220f, 280f, 35, bushPrefabs, rockA, rockC);

        // =====================================================================
        // VÙNG 4: BÃI CÁT ĐỐI DIỆN TRẬN ĐỊA CỌC (BỜ ĐÔNG: X: -210 -> -160, Z: 160 -> 420)
        // Phá vỡ cảm giác dải cát trơ trọi nhìn từ tàu chiến
        // =====================================================================
        GameObject eastShore = new GameObject("4_BoCat_Dong_DoiDien_BaiCoc");
        eastShore.transform.parent = root.transform;
        PopulateShorePatch(terrain, eastShore.transform, -210f, -160f, 160f, 420f, 60, bushPrefabs, rockA, rockC);

        Debug.Log($"🌾 [Bãi Sậy & Bờ Sông] Đã phủ thành công các bãi lau sậy, dương xỉ và mỏm đá tự nhiên ven bờ.");
    }

    private static void PopulateShorePatch(Terrain terrain, Transform parent, float minX, float maxX, float minZ, float maxZ, int count, List<GameObject> bushPrefabs, GameObject rockA, GameObject rockC)
    {
        for (int i = 0; i < count; i++)
        {
            float posX = Random.Range(minX, maxX);
            float posZ = Random.Range(minZ, maxZ);
            float groundH = terrain.SampleHeight(new Vector3(posX, 0, posZ));

            // Chỉ đặt ven bờ nước (cao độ từ 13.8m đến 17.5m - ngay mép bãi cát và bờ cỏ)
            if (groundH < 13.8f || groundH > 18.2f) continue;

            Vector3 spawnPos = new Vector3(posX, groundH, posZ);

            if (Random.value < 0.22f && (rockA != null || rockC != null))
            {
                // Đặt mỏm đá cuội ven bờ
                GameObject rockPrefab = (Random.value < 0.5f && rockA != null) ? rockA : rockC;
                if (rockPrefab != null)
                {
                    var rock = (GameObject)PrefabUtility.InstantiatePrefab(rockPrefab, parent);
                    rock.transform.position = spawnPos - new Vector3(0, Random.Range(0.2f, 0.6f), 0); // Vùi một phần xuống cát
                    rock.transform.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
                    float rScale = Random.Range(0.8f, 1.8f);
                    rock.transform.localScale = Vector3.one * rScale;
                }
            }
            else if (bushPrefabs.Count > 0)
            {
                // Đặt bụi cây, lau sậy, dương xỉ
                GameObject floraPrefab = bushPrefabs[Random.Range(0, bushPrefabs.Count)];
                var flora = (GameObject)PrefabUtility.InstantiatePrefab(floraPrefab, parent);
                flora.transform.position = spawnPos;
                flora.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                float fScale = Random.Range(1.0f, 1.9f);
                flora.transform.localScale = Vector3.one * fScale;
            }
        }
    }

    private static void SpawnCampfire(Terrain terrain)
    {
        string pDir = "Assets/Prefabs/Props/";
        GameObject pfTorch = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "pf_torch_fire_01.prefab");
        GameObject pfLog = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "pf_logpile_01.prefab");

        string cfName = "Lua_Trai_DaiBanDoanh";
        var oldCf = GameObject.Find(cfName);
        if (oldCf != null) Object.DestroyImmediate(oldCf);

        // Vị trí đống lửa trại: Giữa sân Đại Bản Doanh, trước sảnh tướng Ngô Quyền
        Vector3 cfPos = new Vector3(-804f, 17.5f, 320f);
        if (terrain != null) cfPos.y = terrain.SampleHeight(cfPos);

        GameObject fireGroup = new GameObject(cfName);
        fireGroup.transform.position = cfPos;

        // Vòng gỗ củi lửa trại
        if (pfLog != null)
        {
            var logs = (GameObject)PrefabUtility.InstantiatePrefab(pfLog, fireGroup.transform);
            logs.transform.position = cfPos;
            logs.transform.rotation = Quaternion.Euler(0, 45f, 0);
            logs.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        }

        // Ngọn lửa bập bùng
        if (pfTorch != null)
        {
            var fire = (GameObject)PrefabUtility.InstantiatePrefab(pfTorch, fireGroup.transform);
            fire.name = "Ngon_Lua_Trai";
            fire.transform.position = cfPos;
            fire.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
        }

        // Nguồn sáng ấm áp của đống lửa
        GameObject lightGo = new GameObject("Campfire_PointLight");
        lightGo.transform.parent = fireGroup.transform;
        lightGo.transform.position = cfPos + Vector3.up * 0.8f;
        Light pLight = lightGo.AddComponent<Light>();
        pLight.type = LightType.Point;
        pLight.color = new Color(1.0f, 0.65f, 0.25f);
        pLight.range = 16f;
        pLight.intensity = 2.5f;

        Debug.Log("🔥 [Lửa trại] Đã dựng đống lửa trại ấm áp giữa sân Đại Bản Doanh Ngô Quyền.");
    }
}
