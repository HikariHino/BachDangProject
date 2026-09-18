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
        // 4. BỐ TRÍ CÂY CỔ THỤ AAA (NATURE RENDERER) & RỪNG NGUYÊN SINH
        // ==========================================
        string nrArt = "Assets/Visual Design Cafe/Nature Renderer Demo/Realistic/Art/";
        GameObject cypressTree = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Trees/Cypress.prefab");
        GameObject coniferTree = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Trees/Conifer.prefab");
        GameObject rockA = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Rocks/Rock_A_02.prefab");
        GameObject rockC = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Rocks/Rock_C_01.prefab");

        string prefabFolder = "Assets/TerrainSampleAssets/Prefabs/";
        var protos = new List<TreePrototype>();

        // 1. Cây Cổ Thụ AAA từ Nature Renderer đứng đầu danh sách
        if (cypressTree != null) protos.Add(new TreePrototype { prefab = cypressTree });
        if (coniferTree != null) protos.Add(new TreePrototype { prefab = coniferTree });

        // 2. Tảng đá rêu phong ven bờ / chân đồi
        if (rockA != null) protos.Add(new TreePrototype { prefab = rockA });
        if (rockC != null) protos.Add(new TreePrototype { prefab = rockC });

        // 3. Cây bụi, dương xỉ, hoa dại rừng nhiệt đới
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
                    tiShore.prototypeIndex = Random.Range(4, pCount);
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

                // Ưu tiên 65% là Cây Cổ Thụ AAA (Cypress & Conifer - prototypeIndex 0 & 1)
                if (Random.value < 0.65f && protos.Count >= 2)
                {
                    tiInland.prototypeIndex = Random.Range(0, 2);
                    tiInland.widthScale = Random.Range(1.1f, 1.6f);
                    tiInland.heightScale = Random.Range(1.1f, 1.7f);
                }
                else if (Random.value < 0.10f && protos.Count >= 4)
                {
                    // 10% là Tảng đá rêu phong (Rock_A & Rock_C - prototypeIndex 2 & 3)
                    tiInland.prototypeIndex = Random.Range(2, 4);
                    tiInland.widthScale = Random.Range(1.5f, 3.0f);
                    tiInland.heightScale = Random.Range(1.2f, 2.5f);
                }
                else
                {
                    // Cây bụi và dương xỉ rậm rạp dưới tán rừng
                    tiInland.prototypeIndex = Random.Range(4, pCount);
                    tiInland.widthScale = Random.Range(1.8f, 3.5f);
                    tiInland.heightScale = Random.Range(1.8f, 3.5f);
                }

                tiInland.color = tiInland.lightmapColor = Color.white;
                trees.Add(tiInland);
            }
        }
        td.SetTreeInstances(trees.ToArray(), true);

        // ==========================================
        // 5. THẢM CỎ 3D CHI TIẾT & HOA DẠI TỪ NATURE RENDERER
        // ==========================================
        int dRes = 512;
        td.SetDetailResolution(dRes, 16);

        var dProtos = new List<DetailPrototype>();

        // Layer 0: CỎ 3D SIÊU CHI TIẾT (Detailed Grass 01 từ Nature Renderer)
        var pDetailedGrass = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Grass/Detailed Grass 01 - Variant 2.prefab");
        if (pDetailedGrass == null) pDetailedGrass = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFolder + "Grass_C.prefab");
        if (pDetailedGrass != null)
        {
            DetailPrototype dp = new DetailPrototype();
            dp.prototype = pDetailedGrass;
            dp.usePrototypeMesh = true;
            dp.renderMode = DetailRenderMode.VertexLit;
            dp.healthyColor = new Color(0.35f, 0.72f, 0.20f);
            dp.dryColor = new Color(0.42f, 0.68f, 0.25f);
            dp.minWidth = 0.8f; dp.maxWidth = 1.3f;
            dp.minHeight = 0.7f; dp.maxHeight = 1.2f;
            dProtos.Add(dp);
        }

        // Layer 1: HOA DẠI TRẮNG TỪ NATURE RENDERER (White Flowers 01)
        var pWhiteFlowers = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Flowers/White Flowers 01 - Variant 1.prefab");
        if (pWhiteFlowers != null)
        {
            DetailPrototype dp = new DetailPrototype();
            dp.prototype = pWhiteFlowers;
            dp.usePrototypeMesh = true;
            dp.renderMode = DetailRenderMode.VertexLit;
            dp.healthyColor = Color.white;
            dp.dryColor = new Color(0.9f, 0.9f, 0.85f);
            dp.minWidth = 0.7f; dp.maxWidth = 1.2f;
            dp.minHeight = 0.6f; dp.maxHeight = 1.1f;
            dProtos.Add(dp);
        }

        // Layer 2: BỤI HOA TỰ NHIÊN (Flowering Plant 02 từ Nature Renderer)
        var pFloweringPlant = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Flowering Plants/Flowering Plant 02 - Variant 1.prefab");
        if (pFloweringPlant != null)
        {
            DetailPrototype dp = new DetailPrototype();
            dp.prototype = pFloweringPlant;
            dp.usePrototypeMesh = true;
            dp.renderMode = DetailRenderMode.VertexLit;
            dp.healthyColor = Color.white;
            dp.dryColor = Color.white;
            dp.minWidth = 0.8f; dp.maxWidth = 1.3f;
            dp.minHeight = 0.7f; dp.maxHeight = 1.2f;
            dProtos.Add(dp);
        }

        // Layer 3: DƯƠNG XỈ XANH NHIỆT ĐỚI DƯỚI TÁN CÂY (Fern_A)
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

        td.detailPrototypes = dProtos.ToArray();

        // 1. Phân bổ Cỏ 3D Detailed Grass trên toàn bộ vùng đồi và triền dốc (d > beachPixelWidth + 0.5f)
        if (dProtos.Count > 0)
        {
            int[,] grassMap = new int[dRes, dRes];
            for (int y = 0; y < dRes; y++)
            {
                for (int x = 0; x < dRes; x++)
                {
                    int hy = Mathf.Clamp((int)((float)y / dRes * tSize), 0, tSize - 1);
                    int hx = Mathf.Clamp((int)((float)x / dRes * tSize), 0, tSize - 1);

                    if (isWaterMap[hy, hx]) { grassMap[y, x] = 0; continue; }
                    float d = distToWater[hy, hx];

                    if (d > beachPixelWidth + 0.5f)
                    {
                        grassMap[y, x] = Random.Range(3, 7); // Cỏ mọc dày mượt mà
                    }
                    else
                    {
                        grassMap[y, x] = 0;
                    }
                }
            }
            td.SetDetailLayer(0, 0, 0, grassMap);
        }

        // 2. Phân bổ Hoa dại trắng (White Flowers) điểm xuyết trên triền cỏ
        if (dProtos.Count > 1)
        {
            int[,] flowerMap = new int[dRes, dRes];
            for (int y = 0; y < dRes; y++)
            {
                for (int x = 0; x < dRes; x++)
                {
                    int hy = Mathf.Clamp((int)((float)y / dRes * tSize), 0, tSize - 1);
                    int hx = Mathf.Clamp((int)((float)x / dRes * tSize), 0, tSize - 1);

                    if (isWaterMap[hy, hx]) { flowerMap[y, x] = 0; continue; }
                    float d = distToWater[hy, hx];

                    if (d > beachPixelWidth + 1.0f && Random.value < 0.16f)
                    {
                        flowerMap[y, x] = Random.Range(1, 3);
                    }
                    else
                    {
                        flowerMap[y, x] = 0;
                    }
                }
            }
            td.SetDetailLayer(0, 0, 1, flowerMap);
        }

        // 3. Phân bổ Bụi hoa (Flowering Plants) rải rác ven triền đồi
        if (dProtos.Count > 2)
        {
            int[,] plantMap = new int[dRes, dRes];
            for (int y = 0; y < dRes; y++)
            {
                for (int x = 0; x < dRes; x++)
                {
                    int hy = Mathf.Clamp((int)((float)y / dRes * tSize), 0, tSize - 1);
                    int hx = Mathf.Clamp((int)((float)x / dRes * tSize), 0, tSize - 1);

                    if (isWaterMap[hy, hx]) { plantMap[y, x] = 0; continue; }
                    float d = distToWater[hy, hx];

                    if (d > beachPixelWidth + 0.8f && d < beachPixelWidth + 8f && Random.value < 0.10f)
                    {
                        plantMap[y, x] = 1;
                    }
                    else
                    {
                        plantMap[y, x] = 0;
                    }
                }
            }
            td.SetDetailLayer(0, 0, 2, plantMap);
        }

        // 4. Phân bổ Dương xỉ (Fern_A) rải rác dưới gốc cây cổ thụ
        if (dProtos.Count > 3)
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

                    if (d > beachPixelWidth + 2.5f && Random.value < 0.28f)
                    {
                        fernMap[y, x] = Random.Range(1, 3);
                    }
                    else
                    {
                        fernMap[y, x] = 0;
                    }
                }
            }
            td.SetDetailLayer(0, 0, 3, fernMap);
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
        tComp.treeBillboardDistance = 3500f;
        tComp.treeCrossFadeLength = 0f;
        tComp.treeMaximumFullLODCount = 100000;
        tComp.detailObjectDistance = 450f;
        tComp.detailObjectDensity = 1.0f;
        tComp.heightmapPixelError = 2f;

        // TỰ ĐỘNG KÍCH HOẠT GPU COMPUTE SHADER INSTANCING VỚI NATURE RENDERER
        System.Type nrType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            nrType = asm.GetType("VisualDesignCafe.Rendering.Nature.NatureRenderer");
            if (nrType != null) break;
        }
        if (nrType != null)
        {
            if (terrainGo.GetComponent(nrType) == null)
            {
                terrainGo.AddComponent(nrType);
                Debug.Log("🚀 [NATURE RENDERER] ĐÃ KÍCH HOẠT GPU INSTANCING CHO TOÀN BỘ CÂY & CỎ TRÊN SA BÀN BẠCH ĐẰNG!");
            }
        }

        // ==========================================
        // 7. CẬP NHẬT MẶT NƯỚC & BỐ TRÍ CHIẾN TRẬN
        // ==========================================
        float targetWaterY = 14.0f;
        SetupOptiWaterSurface(targetWaterY);
        PlaceBoatAndSpikesInRiver();

        AssetDatabase.SaveAssets();
        Debug.Log($"🎉 SA BÀN HOÀN THIỆN: 80.000 CÂY CỔ THỤ AAA (NATURE RENDERER) + THẢM CỎ 3D HOA DẠI ĐÃ ĐƯỢC TÍCH HỢP THÀNH CÔNG!");
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
