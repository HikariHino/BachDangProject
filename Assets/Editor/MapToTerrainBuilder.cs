using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MapToTerrainBuilder : EditorWindow
{
    [MenuItem("🤖 Trợ lý AI/🗺️ Xây Sa Bàn Bạch Đằng (Bản Chuẩn AAA Không Viền)")]
    public static void BuildTerrainFromImage()
    {
        string imagePath = "Assets/BachDangMap.png";
        TextureImporter imp = AssetImporter.GetAtPath(imagePath) as TextureImporter;
        if (imp != null && !imp.isReadable)
        {
            imp.isReadable = true;
            imp.SaveAndReimport();
        }

        Texture2D mapTex = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
        if (mapTex == null)
        {
            Debug.LogError("❌ Không tìm thấy BachDangMap.png!");
            return;
        }

        // ==========================================
        // 1. TÍNH TOÁN KHOẢNG CÁCH TỪ NƯỚC VÀO BỜ (DISTANCE TRANSFORM)
        // ==========================================
        int tSize = 1025;
        TerrainData td = new TerrainData();
        td.heightmapResolution = tSize;
        td.size = new Vector3(3000, 60, 3000); // 3000m x 60m x 3000m

        bool[,] isWaterMap = new bool[tSize, tSize];
        for (int y = 0; y < tSize; y++)
        {
            for (int x = 0; x < tSize; x++)
            {
                float u = (float)x / (tSize - 1);
                float v = (float)y / (tSize - 1);
                Color c = mapTex.GetPixelBilinear(u, v);
                isWaterMap[y, x] = (c.b >= c.g * 0.95f) || (c.b > 0.45f && c.b > c.r + 0.15f);
            }
        }

        float[,] distToWater = ComputeDistanceTransform(isWaterMap, tSize);

        // ==========================================
        // 2. KHỞI TẠO ĐỊA HÌNH 3 TẦNG CHUẨN LỊCH SỬ BẠCH ĐẰNG 938
        // ==========================================
        float[,] rawH = new float[tSize, tSize];
        float beachPixelWidth = 9f; // Thu hẹp bãi cát về đúng đường kẻ đỏ (~26 mét)

        for (int y = 0; y < tSize; y++)
        {
            for (int x = 0; x < tSize; x++)
            {
                float u = (float)x / (tSize - 1);
                float v = (float)y / (tSize - 1);

                if (isWaterMap[y, x])
                {
                    rawH[y, x] = 0.08f;
                }
                else
                {
                    float d = distToWater[y, x];
                    if (d <= beachPixelWidth)
                    {
                        // BÃI CÁT PHẲNG THOAI THOẢI ĐẾN ĐÚNG ĐƯỜNG KẺ ĐỎ CỦA SẾP
                        float t = d / beachPixelWidth;
                        rawH[y, x] = Mathf.Lerp(0.158f, 0.255f, Mathf.Pow(t, 1.2f));
                    }
                    else
                    {
                        // VÙNG ĐỒI NÚI PHỦ XANH MƯỚT (BẮT ĐẦU NGAY TỪ SAU VẠCH ĐỎ)
                        float inlandDist = d - beachPixelWidth;
                        float inlandT = Mathf.Clamp01(inlandDist / 12f);
                        float hillBase = Mathf.Lerp(0.255f, 0.38f, inlandT);

                        float mountainBonus = 0f;
                        if (d > 18f)
                        {
                            float mFactor = Mathf.Clamp01((d - 18f) / 16f);
                            float perlin1 = Mathf.PerlinNoise(u * 6f, v * 6f);
                            float perlin2 = Mathf.Abs(Mathf.PerlinNoise(u * 12f + 50f, v * 12f + 50f) * 2f - 1f);
                            mountainBonus = (perlin1 * 0.28f + perlin2 * 0.16f) * mFactor;
                        }

                        rawH[y, x] = hillBase + mountainBonus;
                    }
                }
            }
        }

        float[,] smoothH = SmoothHeights(rawH, tSize, 3);
        td.SetHeights(0, 0, smoothH);

        // ==========================================
        // 3. CẤU HÌNH TEXTURE PBR AAA (6 LỚP)
        // ==========================================
        string texFolder = "Assets/TerrainSampleAssets/Textures/Terrain/";

        var layerGrass = CreateOrGetLayer("Assets/TL_Grass_A.terrainlayer", 
            texFolder + "Grass_A_BaseColor.tif", texFolder + "Grass_A_Normal.tif", texFolder + "Grass_A_MaskMap.tif", new Vector2(15, 15));
        
        var layerMud = CreateOrGetLayer("Assets/TL_Muddy.terrainlayer", 
            texFolder + "Muddy_BaseColor.tif", texFolder + "Muddy_Normal.tif", texFolder + "Muddy_MaskMap.tif", new Vector2(12, 12));

        var layerSand = CreateOrGetLayer("Assets/TL_Sand.terrainlayer", 
            texFolder + "Sand_BaseColor.tif", texFolder + "Sand_Normal.tif", texFolder + "Sand_MaskMap.tif", new Vector2(8, 8));

        var layerMoss = CreateOrGetLayer("Assets/TL_Grass_Moss.terrainlayer", 
            texFolder + "Grass_Moss_BaseColor.tif", texFolder + "Grass_Moss_Normal.tif", texFolder + "Grass_Moss_MaskMap.tif", new Vector2(16, 16));

        var layerRock = CreateOrGetLayer("Assets/TL_Rock.terrainlayer", 
            texFolder + "Rock_BaseColor.tif", texFolder + "Rock_Normal.tif", texFolder + "Rock_MaskMap.tif", new Vector2(12, 12));

        var layerSoilRocks = CreateOrGetLayer("Assets/TL_Soil_Rocks.terrainlayer", 
            texFolder + "Soil_Rocks_BaseColor.tif", texFolder + "Soil_Rocks_Normal.tif", texFolder + "Soil_Rocks_MaskMap.tif", new Vector2(10, 10));

        td.terrainLayers = new TerrainLayer[] { layerGrass, layerMud, layerSand, layerMoss, layerRock, layerSoilRocks };

        int aRes = td.alphamapResolution;
        float[,,] splats = new float[aRes, aRes, 6];

        for (int y = 0; y < aRes; y++)
        {
            for (int x = 0; x < aRes; x++)
            {
                int hy = Mathf.Clamp((int)((float)y / aRes * tSize), 0, tSize - 1);
                int hx = Mathf.Clamp((int)((float)x / aRes * tSize), 0, tSize - 1);

                bool isWater = isWaterMap[hy, hx];
                float d = distToWater[hy, hx];
                float slope = GetSlope(smoothH, hx, hy, tSize);

                for (int l = 0; l < 6; l++) splats[y, x, l] = 0f;

                if (isWater)
                {
                    splats[y, x, 1] = 0.85f;
                    splats[y, x, 2] = 0.15f;
                }
                else if (d <= beachPixelWidth)
                {
                    // 100% Cát vàng bãi biển (đến đúng vạch kẻ đỏ của sếp)
                    splats[y, x, 2] = 1.0f;
                }
                else if (d <= beachPixelWidth + 2.0f)
                {
                    // Chuyển tiếp mượt mà từ Cát sang Cỏ Xanh tươi
                    float t = (d - beachPixelWidth) / 2.0f;
                    splats[y, x, 2] = 1f - t;
                    splats[y, x, 0] = t * 0.75f;
                    splats[y, x, 3] = t * 0.25f;
                }
                else if (d > 35f && slope > 1.2f)
                {
                    // Chỉ vách núi đá dựng đứng sâu trong đất liền mới lộ đá vôi xám Tràng Kênh
                    splats[y, x, 4] = 0.80f;
                    splats[y, x, 3] = 0.20f;
                }
                else
                {
                    // TOÀN BỘ VÙNG TỪ BÃI CÁT TRỞ LÊN PHỦ XANH MƯỚT (Cỏ xanh tươi + Rêu xanh tự nhiên)
                    // Tuyệt đối không còn dải đá sỏi xám xịt nữa!
                    splats[y, x, 0] = 0.75f; // layerGrass (TL_Grass_A)
                    splats[y, x, 3] = 0.25f; // layerMoss (TL_Grass_Moss)
                }
            }
        }
        td.SetAlphamaps(0, 0, splats);

        // ==========================================
        // 4. BỐ TRÍ CÂY LỚN CỔ THỤ & RỪNG NGUYÊN SINH (TREES)
        // ==========================================
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // Tạo 2 loại Cây Đại Thụ (Ancient Trees) cao 18m - 24m có thân gỗ và tán lá xum xuê
        GameObject bigTreeA = GetOrCreateAncientTreePrefab("Assets/Prefabs/AncientTree_A.prefab", false);
        GameObject bigTreeB = GetOrCreateAncientTreePrefab("Assets/Prefabs/AncientTree_B.prefab", true);

        string prefabFolder = "Assets/TerrainSampleAssets/Prefabs/";
        var protos = new List<TreePrototype>();

        // Cây lớn đứng đầu danh sách
        if (bigTreeA != null) protos.Add(new TreePrototype { prefab = bigTreeA });
        if (bigTreeB != null) protos.Add(new TreePrototype { prefab = bigTreeB });

        // Cây bụi, dương xỉ, hoa dại
        string[] vegList = { 
            "Bush_A", "Bush_B", "Fern_A", "Fern_B", "Fern_C", 
            "BushDry_A", "Grass_A", "Grass_C", "GrassDry_A", "Heather_A", "Plant_A", "Plant_B" 
        };

        foreach (var v in vegList)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFolder + v + ".prefab");
            if (p != null) protos.Add(new TreePrototype { prefab = p });
        }
        td.treePrototypes = protos.ToArray();

        var trees = new List<TreeInstance>();
        int pCount = protos.Count;

        for (int i = 0; pCount > 0 && i < 80000; i++)
        {
            float tx = Random.value;
            float ty = Random.value;

            int hx = Mathf.Clamp((int)(tx * tSize), 0, tSize - 1);
            int hy = Mathf.Clamp((int)(ty * tSize), 0, tSize - 1);

            if (isWaterMap[hy, hx]) continue;
            float d = distToWater[hy, hx];
            float slope = GetSlope(smoothH, hx, hy, tSize);
            if (d > 35f && slope > 1.0f) continue; // Chỉ không mọc trên vách đá dựng đứng sâu trong đất liền
            float normH = smoothH[hy, hx];

            // VÙNG 1: Mép trên bãi cát (d từ 6 đến beachPixelWidth) -> Cụm hoa dại & cỏ lác nhỏ lác đác
            if (d >= 6f && d <= beachPixelWidth)
            {
                if (Random.value < 0.08f) // Chỉ mọc thưa thớt 8%
                {
                    TreeInstance tiShore = new TreeInstance();
                    tiShore.position = new Vector3(tx, normH, ty);
                    tiShore.prototypeIndex = Random.Range(6, pCount);
                    tiShore.widthScale = Random.Range(0.6f, 1.0f);
                    tiShore.heightScale = Random.Range(0.6f, 1.0f);
                    tiShore.color = tiShore.lightmapColor = Color.white;
                    trees.Add(tiShore);
                }
                continue;
            }

            // VÙNG 2: Rừng rậm nguyên sinh & Cây đại thụ trên đồi (ngay sau vạch đỏ d > beachPixelWidth + 0.8f)
            if (d > beachPixelWidth + 0.8f)
            {
                TreeInstance tiInland = new TreeInstance();
                tiInland.position = new Vector3(tx, normH, ty);

                // Ưu tiên 65% là Cây Đại Thụ To Lớn (prototypeIndex 0 và 1)
                if (Random.value < 0.65f && protos.Count >= 2)
                {
                    tiInland.prototypeIndex = Random.Range(0, 2);
                    tiInland.widthScale = Random.Range(1.0f, 1.5f);
                    tiInland.heightScale = Random.Range(1.0f, 1.5f); // Mesh gốc đã cao 18m - 24m rồi!
                }
                else
                {
                    // Cây bụi và dương xỉ rậm rạp
                    tiInland.prototypeIndex = Random.Range(2, pCount);
                    tiInland.widthScale = Random.Range(2.0f, 4.0f);
                    tiInland.heightScale = Random.Range(2.0f, 4.5f);
                }

                tiInland.color = tiInland.lightmapColor = Color.white;
                trees.Add(tiInland);
            }
        }
        td.SetTreeInstances(trees.ToArray(), true);

        // ==========================================
        // 5. THẢM CỎ 3D CHI TIẾT (DETAIL PROTOTYPES)
        // - Cỏ xanh tươi mướt phủ rợp các ngọn đồi có rừng cây cổ thụ
        // - Dương xỉ nhiệt đới xanh thẫm dưới gốc cây
        // - Cỏ ven bãi cát được thu ngắn lại (thấp mềm mại) và giảm mật độ cực thưa thớt
        // ==========================================
        int dRes = 512;
        td.SetDetailResolution(dRes, 16);

        var dProtos = new List<DetailPrototype>();

        // Layer 0: CỎ XANH TƯƠI MƯỚT TRÊN ĐỒI CÂY CỔ THỤ (Grass_C / Grass_A)
        var pGrassGreen = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFolder + "Grass_C.prefab");
        if (pGrassGreen == null) pGrassGreen = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFolder + "Grass_A.prefab");
        if (pGrassGreen != null)
        {
            DetailPrototype dpGreen = new DetailPrototype();
            dpGreen.prototype = pGrassGreen;
            dpGreen.usePrototypeMesh = true;
            dpGreen.renderMode = DetailRenderMode.VertexLit;
            dpGreen.healthyColor = new Color(0.30f, 0.68f, 0.18f); // Xanh lá non tươi mướt
            dpGreen.dryColor = new Color(0.38f, 0.65f, 0.22f);
            dpGreen.minWidth = 0.6f; dpGreen.maxWidth = 1.1f;
            dpGreen.minHeight = 0.45f; dpGreen.maxHeight = 0.85f; // Chiều cao vừa phải, mềm mại tự nhiên
            dProtos.Add(dpGreen);
        }

        // Layer 1: DƯƠNG XỈ XANH NHIỆT ĐỚI DƯỚI TÁN CÂY CỔ THỤ (Fern_A)
        var pFern = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFolder + "Fern_A.prefab");
        if (pFern != null)
        {
            DetailPrototype dpFern = new DetailPrototype();
            dpFern.prototype = pFern;
            dpFern.usePrototypeMesh = true;
            dpFern.renderMode = DetailRenderMode.VertexLit;
            dpFern.healthyColor = new Color(0.22f, 0.58f, 0.15f);
            dpFern.dryColor = new Color(0.32f, 0.62f, 0.20f);
            dpFern.minWidth = 0.7f; dpFern.maxWidth = 1.2f;
            dpFern.minHeight = 0.55f; dpFern.maxHeight = 0.95f;
            dProtos.Add(dpFern);
        }

        // Layer 2: CỎ VEN BÃI CÁT - THU NGẮN 70% VÀ MẬT ĐỘ CỰC KỲ THƯA THỚT (Grass_A)
        var pGrassShore = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFolder + "Grass_A.prefab");
        if (pGrassShore != null)
        {
            DetailPrototype dpShore = new DetailPrototype();
            dpShore.prototype = pGrassShore;
            dpShore.usePrototypeMesh = true;
            dpShore.renderMode = DetailRenderMode.VertexLit;
            dpShore.healthyColor = new Color(0.55f, 0.65f, 0.45f);
            dpShore.dryColor = new Color(0.50f, 0.58f, 0.40f);
            dpShore.minWidth = 0.40f; dpShore.maxWidth = 0.70f;
            dpShore.minHeight = 0.35f; dpShore.maxHeight = 0.60f; // Cỏ ngắn sát đất, không bị dài lê thê
            dProtos.Add(dpShore);
        }

        td.detailPrototypes = dProtos.ToArray();

        // 1. Phân bổ Cỏ xanh tươi mát trên toàn bộ vùng đồi có cây cổ thụ (d > beachPixelWidth + 1.5f)
        if (dProtos.Count > 0)
        {
            int[,] greenGrassMap = new int[dRes, dRes];
            for (int y = 0; y < dRes; y++)
            {
                for (int x = 0; x < dRes; x++)
                {
                    int hy = Mathf.Clamp((int)((float)y / dRes * tSize), 0, tSize - 1);
                    int hx = Mathf.Clamp((int)((float)x / dRes * tSize), 0, tSize - 1);

                    if (isWaterMap[hy, hx]) { greenGrassMap[y, x] = 0; continue; }
                    float d = distToWater[hy, hx];

                    if (d > beachPixelWidth + 0.5f)
                    {
                        greenGrassMap[y, x] = Random.Range(3, 6); // Cỏ xanh mướt mọc dày dặn ngay từ sau vạch đỏ
                    }
                    else
                    {
                        greenGrassMap[y, x] = 0;
                    }
                }
            }
            td.SetDetailLayer(0, 0, 0, greenGrassMap);
        }

        // 2. Phân bổ Dương xỉ rải rác dưới gốc cây cổ thụ
        if (dProtos.Count > 1)
        {
            int[,] fernMap = new int[dRes, dRes];
            for (int y = 0; y < dRes; y++)
            {
                for (int x = 0; x < dRes; x++)
                {
                    int hy = Mathf.Clamp((int)((float)y / dRes * tSize), 0, tSize - 1);
                    int hx = Mathf.Clamp((int)((float)x / dRes * tSize), 0, tSize - 1);

                    if (isWaterMap[hy, hx]) { fernMap[y, x] = 0; continue; }
                    float d = distToWater[hy, hx];

                    if (d > beachPixelWidth + 2.0f && Random.value < 0.35f)
                    {
                        fernMap[y, x] = Random.Range(1, 3);
                    }
                    else
                    {
                        fernMap[y, x] = 0;
                    }
                }
            }
            td.SetDetailLayer(0, 0, 1, fernMap);
        }

        // 3. Phân bổ Cỏ ven cát - GIẢM MẠNH MẬT ĐỘ: Chỉ lác đác vài cụm nhỏ ở đường giao cát-cỏ
        if (dProtos.Count > 2)
        {
            int[,] shoreMap = new int[dRes, dRes];
            for (int y = 0; y < dRes; y++)
            {
                for (int x = 0; x < dRes; x++)
                {
                    int hy = Mathf.Clamp((int)((float)y / dRes * tSize), 0, tSize - 1);
                    int hx = Mathf.Clamp((int)((float)x / dRes * tSize), 0, tSize - 1);

                    if (isWaterMap[hy, hx]) { shoreMap[y, x] = 0; continue; }
                    float d = distToWater[hy, hx];

                    if (d >= beachPixelWidth - 1f && d <= beachPixelWidth + 1.2f && Random.value < 0.05f)
                    {
                        shoreMap[y, x] = 1; // Chỉ 1 nhành cỏ nhỏ lác đác
                    }
                    else
                    {
                        shoreMap[y, x] = 0;
                    }
                }
            }
            td.SetDetailLayer(0, 0, 2, shoreMap);
        }

        // ==========================================
        // 6. TẠO HOẶC CẬP NHẬT TERRAIN TRONG SCENE
        // ==========================================
        GameObject oldTerrain = GameObject.Find("SaBan_BachDang_AAA");
        if (oldTerrain == null) oldTerrain = GameObject.Find("SaBan_BachDang_Final");
        if (oldTerrain == null) oldTerrain = GameObject.Find("SaBan_BachDang_XinNhat");
        if (oldTerrain != null) DestroyImmediate(oldTerrain);

        GameObject terrainGo = Terrain.CreateTerrainGameObject(td);
        terrainGo.name = "SaBan_BachDang_AAA";
        terrainGo.transform.position = new Vector3(-1500, 0, -1500);

        Terrain tComp = terrainGo.GetComponent<Terrain>();
        tComp.drawTreesAndFoliage = true;
        tComp.treeDistance = 3500f;
        tComp.treeBillboardDistance = 3500f; // Duy trì Mesh 3D chi tiết lên đến 3.500 mét không bao giờ bị cắt giảm!
        tComp.treeCrossFadeLength = 0f;
        tComp.treeMaximumFullLODCount = 100000; // Vẽ toàn bộ 80.000 cây ở chất lượng 3D tối đa, không bị mất lá!
        tComp.detailObjectDistance = 450f; // Bán kính nhìn thấy thảm cỏ 3D dày đặc 450 mét!
        tComp.detailObjectDensity = 1.0f;
        tComp.heightmapPixelError = 2f; // Độ sắc nét địa hình cao

        // ==========================================
        // 7. CẬP NHẬT MẶT NƯỚC & BỐ TRÍ CHIẾN TRẬN
        // ==========================================
        float targetWaterY = 14.0f;
        SetupOptiWaterSurface(targetWaterY);
        PlaceBoatAndSpikesInRiver();

        AssetDatabase.SaveAssets();
        Debug.Log($"🎉 SA BÀN HOÀN THIỆN: 80.000 CÂY ĐẠI THỤ CỔ THỤ + THẢM CỎ 3D DÀY ĐẶC PHỦ RỢP CHIẾN ĐỊA!");
    }

    [MenuItem("🤖 Trợ lý AI/🌲 Tạo Lại 2 Prefab Cây Cổ Thụ (Ancient Trees)")]
    public static void ForceRecreateAncientTreePrefabs()
    {
        GetOrCreateAncientTreePrefab("Assets/Prefabs/AncientTree_A.prefab", false, true);
        GetOrCreateAncientTreePrefab("Assets/Prefabs/AncientTree_B.prefab", true, true);
        Debug.Log("✅ Đã tạo mới hoàn toàn 2 Prefab Cây Cổ Thụ Hữu Cơ khổng lồ (AncientTree_A & B)!");
    }

    private static GameObject GetOrCreateAncientTreePrefab(string prefabPath, bool isBanyan, bool forceRecreate = false)
    {
        if (!forceRecreate)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null && existing.GetComponent<MeshFilter>() != null && existing.GetComponent<MeshFilter>().sharedMesh != null)
            {
                return existing;
            }
        }

        string dir = System.IO.Path.GetDirectoryName(prefabPath);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        // 1. Tạo Mesh thân & cành cây hữu cơ gân guốc (Organic Trunk & Branches)
        Mesh subWood = BuildOrganicWoodMesh(isBanyan);

        // 2. Lấy mesh vòm lá từ Bush_A
        GameObject bushPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TerrainSampleAssets/Prefabs/Bush_A.prefab");
        if (bushPrefab == null) bushPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TerrainSampleAssets/Prefabs/Bush_B.prefab");
        Mesh bushMesh = bushPrefab != null ? bushPrefab.GetComponent<MeshFilter>().sharedMesh : null;

        // 3. Material Vỏ cây gỗ thật từ textures wood-spike
        string matPath = prefabPath.Replace(".prefab", "_Bark.mat");
        Material woodMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (woodMat == null)
        {
            woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Texture2D diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/wood-spike/textures/Channel_modelling_mat_1_Diffuse.png");
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/wood-spike/textures/Channel_modelling_mat_1_Normal_Map.png");
            if (diffuseTex != null) woodMat.mainTexture = diffuseTex;
            if (normalTex != null)
            {
                woodMat.EnableKeyword("_NORMALMAP");
                woodMat.SetTexture("_BumpMap", normalTex);
            }
            woodMat.color = new Color(0.48f, 0.35f, 0.24f);
            woodMat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(woodMat, matPath);
        }

        // 4. Material Tán lá URP Lit sắc nét (Không bao giờ bị mờ/mất lá khi đứng xa!)
        Material leavesMat = GetOrCreateLeavesMaterial();

        // 5. Bố trí vòm lá sum suê đan xen theo các cành chạc
        List<CombineInstance> leafParts = new List<CombineInstance>();
        if (bushMesh != null)
        {
            if (!isBanyan)
            {
                Vector3 fork = new Vector3(0.2f, 13.5f, -0.1f);
                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(0.2f, 5.8f, 0.1f), Quaternion.identity, Vector3.one * 12.0f) });
                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(0.5f, 4.5f, -0.4f), Quaternion.Euler(10, 45, -10), Vector3.one * 10.5f) });

                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(-4.5f, 4.2f, 1.2f), Quaternion.Euler(15, 30, -15), Vector3.one * 10.0f) });
                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(-2.5f, 2.8f, 0.8f), Quaternion.Euler(-10, 60, 10), Vector3.one * 8.5f) });

                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(4.8f, 4.5f, -1.5f), Quaternion.Euler(-15, 75, 12), Vector3.one * 10.0f) });
                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(2.8f, 3.0f, -0.9f), Quaternion.Euler(20, -45, -10), Vector3.one * 8.5f) });

                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(0.8f, 4.5f, 4.2f), Quaternion.Euler(5, 120, -10), Vector3.one * 9.5f) });
                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(0.5f, 2.6f, 2.4f), Quaternion.Euler(-15, 140, 15), Vector3.one * 8.0f) });

                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(-1.2f, 4.2f, -4.0f), Quaternion.Euler(-20, -50, 15), Vector3.one * 9.5f) });
                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(-0.8f, 2.5f, -2.2f), Quaternion.Euler(10, -70, -12), Vector3.one * 8.0f) });

                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(0, 1.5f, 0), Quaternion.identity, Vector3.one * 11.0f) });
            }
            else
            {
                Vector3 fork = new Vector3(-0.3f, 15.5f, 0.2f);
                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(0.4f, 8.8f, 0.3f), Quaternion.identity, Vector3.one * 16.0f) });
                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(-0.2f, 6.5f, -0.5f), Quaternion.Euler(15, 40, -10), Vector3.one * 14.0f) });

                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(-7.2f, 5.5f, 2.2f), Quaternion.Euler(20, 25, -15), Vector3.one * 14.5f) });
                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(-4.2f, 3.5f, 1.2f), Quaternion.Euler(-10, 50, 15), Vector3.one * 12.0f) });

                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(7.5f, 5.8f, -2.8f), Quaternion.Euler(-15, 60, 20), Vector3.one * 14.5f) });
                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(4.5f, 3.8f, -1.6f), Quaternion.Euler(15, -45, -10), Vector3.one * 12.0f) });

                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(2.2f, 5.0f, 6.8f), Quaternion.Euler(10, 110, -10), Vector3.one * 13.5f) });
                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(1.4f, 3.2f, 4.0f), Quaternion.Euler(-15, 130, 12), Vector3.one * 11.5f) });

                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(-3.0f, 4.8f, -6.5f), Quaternion.Euler(-20, -50, 15), Vector3.one * 13.5f) });
                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(-1.8f, 3.0f, -3.8f), Quaternion.Euler(10, -60, -15), Vector3.one * 11.5f) });

                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(0, 2.0f, 0), Quaternion.identity, Vector3.one * 16.0f) });
                leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(fork + new Vector3(1.5f, 1.0f, -1.0f), Quaternion.Euler(5, 75, -5), Vector3.one * 13.5f) });
            }
        }

        // Ghép lá thành Submesh 1
        Mesh subLeaves = new Mesh();
        subLeaves.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        if (leafParts.Count > 0)
        {
            subLeaves.CombineMeshes(leafParts.ToArray(), true, true);
        }

        // Hợp nhất thành 1 Mesh đa Submesh duy nhất
        CombineInstance[] finalCombines = new CombineInstance[2];
        finalCombines[0].mesh = subWood;
        finalCombines[0].transform = Matrix4x4.identity;
        finalCombines[1].mesh = subLeaves;
        finalCombines[1].transform = Matrix4x4.identity;

        Mesh treeMesh = new Mesh();
        treeMesh.name = System.IO.Path.GetFileNameWithoutExtension(prefabPath) + "_Mesh";
        treeMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        treeMesh.CombineMeshes(finalCombines, false, false);

        string meshAssetPath = prefabPath.Replace(".prefab", "_Mesh.asset");
        AssetDatabase.DeleteAsset(meshAssetPath);
        AssetDatabase.CreateAsset(treeMesh, meshAssetPath);

        // Gắn MeshFilter và MeshRenderer TRỰC TIẾP LÊN ROOT GAMEOBJECT
        GameObject treeGO = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
        MeshFilter mf = treeGO.AddComponent<MeshFilter>();
        mf.sharedMesh = treeMesh;

        MeshRenderer mr = treeGO.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new Material[] { woodMat, leavesMat };

        AssetDatabase.DeleteAsset(prefabPath);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(treeGO, prefabPath);
        Object.DestroyImmediate(treeGO);
        return saved;
    }

    private static Material GetOrCreateLeavesMaterial()
    {
        string matPath = "Assets/Prefabs/AncientTree_Leaves.mat";
        Material leavesMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (leavesMat == null)
        {
            leavesMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Texture2D leafBase = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TerrainSampleAssets/Textures/Details/Bush_A_BaseColor.tif");
            Texture2D leafNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TerrainSampleAssets/Textures/Details/Bush_A_Normal.tif");
            if (leafBase != null) leavesMat.SetTexture("_BaseMap", leafBase);
            if (leafNorm != null)
            {
                leavesMat.EnableKeyword("_NORMALMAP");
                leavesMat.SetTexture("_BumpMap", leafNorm);
            }

            leavesMat.SetFloat("_AlphaClip", 1f);
            leavesMat.SetFloat("_Cutoff", 0.33f);
            leavesMat.EnableKeyword("_ALPHATEST_ON");

            leavesMat.SetFloat("_Cull", 0f);
            leavesMat.EnableKeyword("_DOUBLESIDED_ON");

            leavesMat.SetColor("_BaseColor", new Color(0.92f, 1.05f, 0.90f));
            leavesMat.SetFloat("_Smoothness", 0.18f);

            leavesMat.renderQueue = 2450;
            AssetDatabase.CreateAsset(leavesMat, matPath);
        }
        else
        {
            leavesMat.SetFloat("_AlphaClip", 1f);
            leavesMat.SetFloat("_Cutoff", 0.33f);
            leavesMat.SetFloat("_Cull", 0f);
            leavesMat.EnableKeyword("_ALPHATEST_ON");
            leavesMat.EnableKeyword("_DOUBLESIDED_ON");
            EditorUtility.SetDirty(leavesMat);
        }
        return leavesMat;
    }

    private static Mesh BuildOrganicWoodMesh(bool isBanyan)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        if (!isBanyan)
        {
            float trunkH = 13.5f;
            float baseR = 1.15f;
            float topR = 0.6f;

            AddBranchTube(verts, norms, uvs, tris,
                new Vector3(0, 0, 0), new Vector3(0.2f, trunkH, -0.1f), new Vector3(0.4f, trunkH * 0.5f, 0.3f),
                baseR, topR, 12, 10, rootFlare: 0.75f);

            Vector3 fork = new Vector3(0.2f, trunkH, -0.1f);

            AddBranchTube(verts, norms, uvs, tris,
                fork + new Vector3(-0.3f, 0, 0), fork + new Vector3(-4.5f, 4.2f, 1.2f), fork + new Vector3(-2.5f, 2.0f, 0.5f),
                topR * 0.65f, 0.18f, 8, 6);

            AddBranchTube(verts, norms, uvs, tris,
                fork + new Vector3(0.3f, 0, -0.2f), fork + new Vector3(4.8f, 4.5f, -1.5f), fork + new Vector3(2.8f, 2.2f, -0.8f),
                topR * 0.62f, 0.18f, 8, 6);

            AddBranchTube(verts, norms, uvs, tris,
                fork + new Vector3(0, 0.3f, 0.3f), fork + new Vector3(0.8f, 4.5f, 4.2f), fork + new Vector3(0.4f, 2.0f, 2.4f),
                topR * 0.58f, 0.16f, 8, 6);

            AddBranchTube(verts, norms, uvs, tris,
                fork + new Vector3(-0.1f, 0.4f, -0.3f), fork + new Vector3(-1.2f, 4.2f, -4.0f), fork + new Vector3(-0.7f, 2.0f, -2.2f),
                topR * 0.55f, 0.15f, 8, 6);

            AddBranchTube(verts, norms, uvs, tris,
                fork, fork + new Vector3(0.2f, 5.5f, 0.1f), fork + new Vector3(0.1f, 2.8f, -0.1f),
                topR * 0.6f, 0.15f, 8, 6);
        }
        else
        {
            float trunkH = 15.5f;
            float baseR = 1.75f;
            float topR = 0.95f;

            AddBranchTube(verts, norms, uvs, tris,
                new Vector3(0, 0, 0), new Vector3(-0.3f, trunkH, 0.2f), new Vector3(-0.6f, trunkH * 0.5f, 0.5f),
                baseR, topR, 14, 12, rootFlare: 1.05f);

            Vector3 fork = new Vector3(-0.3f, trunkH, 0.2f);

            AddBranchTube(verts, norms, uvs, tris,
                fork + new Vector3(-0.4f, 0, 0.2f), fork + new Vector3(-7.2f, 5.5f, 2.2f), fork + new Vector3(-4.2f, 2.5f, 1.2f),
                topR * 0.72f, 0.22f, 10, 7);

            AddBranchTube(verts, norms, uvs, tris,
                fork + new Vector3(0.5f, 0.2f, -0.3f), fork + new Vector3(7.5f, 5.8f, -2.8f), fork + new Vector3(4.5f, 2.8f, -1.5f),
                topR * 0.70f, 0.22f, 10, 7);

            AddBranchTube(verts, norms, uvs, tris,
                fork + new Vector3(0.2f, 0.3f, 0.4f), fork + new Vector3(2.2f, 5.0f, 6.8f), fork + new Vector3(1.2f, 2.5f, 3.8f),
                topR * 0.65f, 0.20f, 8, 7);

            AddBranchTube(verts, norms, uvs, tris,
                fork + new Vector3(-0.3f, 0.4f, -0.4f), fork + new Vector3(-3.0f, 4.8f, -6.5f), fork + new Vector3(-1.8f, 2.4f, -3.5f),
                topR * 0.62f, 0.20f, 8, 7);

            AddBranchTube(verts, norms, uvs, tris,
                fork, fork + new Vector3(0.4f, 8.5f, 0.3f), fork + new Vector3(0.2f, 4.5f, 0.1f),
                topR * 0.65f, 0.20f, 8, 7);
        }

        Mesh m = new Mesh();
        m.name = isBanyan ? "BanyanWood_Mesh" : "HardwoodWood_Mesh";
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    private static void AddBranchTube(
        List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        Vector3 p0, Vector3 p1, Vector3 ctrl,
        float r0, float r1, int radialSegs, int heightSegs, float rootFlare = 0f)
    {
        int baseVertIndex = verts.Count;

        for (int i = 0; i <= heightSegs; i++)
        {
            float t = (float)i / heightSegs;

            Vector3 center = (1f - t) * (1f - t) * p0 + 2f * (1f - t) * t * ctrl + t * t * p1;

            Vector3 tangent = 2f * (1f - t) * (ctrl - p0) + 2f * t * (p1 - ctrl);
            if (tangent.sqrMagnitude < 0.001f) tangent = (p1 - p0).normalized;
            else tangent.Normalize();

            Vector3 upVec = Mathf.Abs(tangent.y) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(tangent, upVec).normalized;
            Vector3 forward = Vector3.Cross(right, tangent).normalized;

            float baseR = Mathf.Lerp(r0, r1, t);

            for (int j = 0; j <= radialSegs; j++)
            {
                float phi = (float)j / radialSegs * Mathf.PI * 2f;
                float cosP = Mathf.Cos(phi);
                float sinP = Mathf.Sin(phi);

                float currentR = baseR;
                if (rootFlare > 0f && t < 0.35f)
                {
                    float flareT = 1f - (t / 0.35f);
                    float flare = flareT * rootFlare * (Mathf.Cos(4f * phi) * 0.55f + Mathf.Sin(2f * phi) * 0.25f);
                    currentR += flare * baseR;
                }

                Vector3 normal = (right * cosP + forward * sinP).normalized;
                Vector3 pos = center + normal * currentR;

                verts.Add(pos);
                norms.Add(normal);
                uvs.Add(new Vector2((float)j / radialSegs, t * 4f));
            }
        }

        for (int i = 0; i < heightSegs; i++)
        {
            int ring1 = baseVertIndex + i * (radialSegs + 1);
            int ring2 = baseVertIndex + (i + 1) * (radialSegs + 1);

            for (int j = 0; j < radialSegs; j++)
            {
                int a = ring1 + j;
                int b = ring1 + j + 1;
                int c = ring2 + j;
                int d = ring2 + j + 1;

                tris.Add(a);
                tris.Add(c);
                tris.Add(b);

                tris.Add(b);
                tris.Add(c);
                tris.Add(d);
            }
        }
    }

    private static float GetSlope(float[,] h, int x, int y, int sz)
    {
        int x0 = Mathf.Max(0, x - 1);
        int x1 = Mathf.Min(sz - 1, x + 1);
        int y0 = Mathf.Max(0, y - 1);
        int y1 = Mathf.Min(sz - 1, y + 1);

        float dx = (h[y, x1] - h[y, x0]) * 30f;
        float dy = (h[y1, x] - h[y0, x]) * 30f;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static float[,] ComputeDistanceTransform(bool[,] isWater, int sz)
    {
        float[,] dist = new float[sz, sz];
        float maxVal = 9999f;

        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
                dist[y, x] = isWater[y, x] ? 0f : maxVal;

        // Forward pass
        for (int y = 0; y < sz; y++)
        {
            for (int x = 0; x < sz; x++)
            {
                if (dist[y, x] == 0f) continue;
                float d = dist[y, x];
                if (x > 0) d = Mathf.Min(d, dist[y, x - 1] + 1f);
                if (y > 0) d = Mathf.Min(d, dist[y - 1, x] + 1f);
                if (x > 0 && y > 0) d = Mathf.Min(d, dist[y - 1, x - 1] + 1.414f);
                if (x < sz - 1 && y > 0) d = Mathf.Min(d, dist[y - 1, x + 1] + 1.414f);
                dist[y, x] = d;
            }
        }

        // Backward pass
        for (int y = sz - 1; y >= 0; y--)
        {
            for (int x = sz - 1; x >= 0; x--)
            {
                if (dist[y, x] == 0f) continue;
                float d = dist[y, x];
                if (x < sz - 1) d = Mathf.Min(d, dist[y, x + 1] + 1f);
                if (y < sz - 1) d = Mathf.Min(d, dist[y + 1, x] + 1f);
                if (x < sz - 1 && y < sz - 1) d = Mathf.Min(d, dist[y + 1, x + 1] + 1.414f);
                if (x > 0 && y < sz - 1) d = Mathf.Min(d, dist[y + 1, x - 1] + 1.414f);
                dist[y, x] = d;
            }
        }

        return dist;
    }

    private static float[,] SmoothHeights(float[,] src, int sz, int radius)
    {
        float[,] dst = new float[sz, sz];
        for (int y = 0; y < sz; y++)
        {
            for (int x = 0; x < sz; x++)
            {
                float sum = 0f;
                int count = 0;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int ny = Mathf.Clamp(y + dy, 0, sz - 1);
                        int nx = Mathf.Clamp(x + dx, 0, sz - 1);
                        sum += src[ny, nx];
                        count++;
                    }
                }
                dst[y, x] = sum / count;
            }
        }
        return dst;
    }

    private static TerrainLayer CreateOrGetLayer(string savePath, string diffusePath, string normalPath, string maskPath, Vector2 tileSize)
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(savePath);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, savePath);
        }

        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
        layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        layer.maskMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
        layer.tileSize = tileSize;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static void SetupOptiWaterSurface(float waterY)
    {
        GameObject waterGo = GameObject.Find("OptiWaterSurface");
        if (waterGo == null) waterGo = GameObject.Find("Plane");

        if (waterGo == null)
        {
            waterGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterGo.name = "OptiWaterSurface";
        }

        waterGo.transform.position = new Vector3(0, waterY, 0);
        waterGo.transform.localScale = new Vector3(300, 1, 300);

        Material waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/OptiWater/Runtime/OptiWaterSurface.mat");
        if (waterMat != null)
        {
            var renderer = waterGo.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = waterMat;
        }

        Collider waterCol = waterGo.GetComponent<Collider>();
        if (waterCol != null)
        {
            DestroyImmediate(waterCol);
        }

        if (waterGo.GetComponent<TideSystem>() == null)
        {
            waterGo.AddComponent<TideSystem>();
        }
    }

    [MenuItem("🤖 Trợ lý AI/⚓ Đặt Thuyền & Bãi Cọc Ra Giữa Sông Bạch Đằng")]
    public static void PlaceBoatAndSpikesInRiver()
    {
        float waterY = 14.0f;

        Vector3 boatPos = new Vector3(-200f, 14.2f, 150f);

        GameObject boatGo = null;
        var boatComp = Object.FindFirstObjectByType<BoatCrash>();
        if (boatComp != null)
        {
            boatGo = boatComp.gameObject;
        }
        else
        {
            var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in all)
            {
                string n = go.name.ToLower();
                if (n.Contains("sketchfab") || n.Contains("junk") || n.Contains("boat"))
                {
                    boatGo = go;
                    break;
                }
            }
        }

        if (boatGo != null)
        {
            Undo.RecordObject(boatGo.transform, "Move Boat To River");
            boatGo.transform.position = boatPos;
            boatGo.transform.rotation = Quaternion.Euler(0, 0, 0);
            Debug.Log($"⚓ Đã đưa Thuyền Nam Hán ra giữa dòng sông Bạch Đằng tại: {boatPos}");
        }

        var spikes = new List<GameObject>();
        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            string n = go.name.ToLower();
            if (n.Contains("spike") || n.Contains("wood_spike"))
            {
                spikes.Add(go);
            }
        }

        if (spikes.Count > 0)
        {
            float startZ = 130f;
            float stepZ = 40f / Mathf.Max(1, spikes.Count - 1);

            for (int i = 0; i < spikes.Count; i++)
            {
                Undo.RecordObject(spikes[i].transform, "Arrange Spikes");
                float z = startZ + i * stepZ + Random.Range(-2f, 2f);
                float x = -140f + Random.Range(-5f, 5f);
                spikes[i].transform.position = new Vector3(x, 13.5f, z);
                spikes[i].transform.rotation = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0, 360), Random.Range(-10f, -25f));
            }
            Debug.Log($"🪵 Đã giăng bãi cọc gỗ ({spikes.Count} cọc) đón đầu thuyền tại X = -140!");
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            Undo.RecordObject(cam.transform, "Move Camera To Battle");
            cam.transform.position = new Vector3(-240f, 22f, 150f);
            cam.transform.LookAt(new Vector3(-170f, 14f, 150f));
        }

        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.pivot = new Vector3(-170f, 15f, 150f);
            SceneView.lastActiveSceneView.size = 60f;
            SceneView.lastActiveSceneView.Repaint();
        }
    }
}
