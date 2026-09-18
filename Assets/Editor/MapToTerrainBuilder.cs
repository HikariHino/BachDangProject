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
        // 0. XÓA TRIỆT ĐỂ TOÀN BỘ TERRAIN CŨ TRONG SCENE ĐỂ KHÔNG BỊ TRÙNG LẶP / TẠO HỐ VUÔNG
        // ==========================================
        var allTerrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTerrains)
        {
            if (t != null && t.gameObject != null)
            {
                Undo.DestroyObjectImmediate(t.gameObject);
            }
        }

        var allGos = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allGos)
        {
            if (go == null) continue;
            string n = go.name.ToLower();
            if (n.StartsWith("saban_") || n == "terrain" || n.Contains("he thong nui") || n.Contains("photoscanned"))
            {
                Undo.DestroyObjectImmediate(go);
            }
        }

        // ==========================================
        // 1. TÍNH TOÁN KHOẢNG CÁCH TỪ NƯỚC VÀO BỜ (DISTANCE TRANSFORM)
        // ==========================================
        int tSize = 2049;
        TerrainData td = new TerrainData();
        td.heightmapResolution = tSize;
        td.size = new Vector3(6000, 220, 6000); // 6000m x 220m x 6000m

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
        // 2. KHỞI TẠO ĐỊA HÌNH: ĐỒNG BẰNG BẰNG PHẲNG ĐỂ XÂY DOANH TRẠI + VÀI NGỌN NÚI ĐÁ VÔI CAO CHÓT VÓT
        // ==========================================
        float[,] rawH = new float[tSize, tSize];
        float beachPixelWidth = 9f; // Thu hẹp bãi cát về đúng đường kẻ đỏ (~26 mét)

        // Danh sách chân đồi thoai thoải tự nhiên (cao độ ~44m - 57m) làm bệ đỡ cho các khối núi đá 3D Photoscanned PBR
        // Lưới Terrain êm ái 100% không bị co dãn texture, còn đỉnh núi cao vút 220m sẽ do các khối đá 3D thực tế đảm nhiệm!
        var karstPeaks = new[]
        {
            new { name = "Núi Tràng Kênh (Tây Bắc)", u = 0.15f, v = 0.65f, radius = 0.085f, targetH = 0.24f, seed = 12.3f }, // ~52m
            new { name = "Núi Thủy Nguyên (Tây Nam)", u = 0.16f, v = 0.28f, radius = 0.075f, targetH = 0.21f, seed = 45.7f }, // ~46m
            new { name = "Núi U Bò (Đông Bắc)", u = 0.78f, v = 0.80f, radius = 0.090f, targetH = 0.26f, seed = 88.1f }, // ~57m
            new { name = "Núi Phượng Hoàng (Đông)", u = 0.83f, v = 0.46f, radius = 0.085f, targetH = 0.23f, seed = 33.9f }, // ~50m
            new { name = "Núi Vọng Hải (Đông Nam)", u = 0.79f, v = 0.20f, radius = 0.075f, targetH = 0.20f, seed = 64.2f }, // ~44m
            new { name = "Núi Yên Hưng (Bắc)", u = 0.62f, v = 0.86f, radius = 0.070f, targetH = 0.22f, seed = 71.5f }  // ~48m
        };

        for (int y = 0; y < tSize; y++)
        {
            for (int x = 0; x < tSize; x++)
            {
                float u = (float)x / (tSize - 1);
                float v = (float)y / (tSize - 1);

                if (isWaterMap[y, x])
                {
                    // LÒNG SÔNG BẠCH ĐẰNG: Sâu ~8m dưới mặt nước
                    rawH[y, x] = 0.025f + Mathf.PerlinNoise(u * 20f, v * 20f) * 0.005f;
                }
                else
                {
                    float d = distToWater[y, x];
                    if (d <= beachPixelWidth)
                    {
                        // BÃI CÁT PHẲNG THOAI THOẢI ĐẾN ĐÚNG ĐƯỜNG KẺ ĐỎ CỦA SẾP
                        float t = d / beachPixelWidth;
                        rawH[y, x] = Mathf.Lerp(0.0591f, 0.0727f, Mathf.Pow(t, 1.2f));
                    }
                    else
                    {
                        // 1. ĐỒNG BẰNG BẰNG PHẲNG NHƯ LÚC TRƯỚC (CAO ĐỘ ~17.5m - THUẬN LỢI 100% ĐỂ XÂY DOANH TRẠI)
                        float inlandDist = d - beachPixelWidth;
                        float plainT = Mathf.Clamp01(inlandDist / 8f);
                        float baseLandH = Mathf.Lerp(0.0727f, 0.0795f, plainT);

                        // Độ mấp mô vi mô siêu nhẹ (+-0.2m) tạo cảm giác tự nhiên nhưng mặt bằng phẳng lì
                        float microRoll = (Mathf.PerlinNoise(u * 12f, v * 12f) - 0.5f) * 0.0015f;
                        float landH = baseLandH + microRoll;

                        // 2. CHÂN ĐỒI BỆ ĐỠ CHO NÚI ĐÁ 3D: Dốc thoai thoải, tự nhiên, xanh mướt cỏ
                        float mountainBonus = 0f;
                        for (int k = 0; k < karstPeaks.Length; k++)
                        {
                            float du = (u - karstPeaks[k].u);
                            float dv = (v - karstPeaks[k].v);
                            float dist = Mathf.Sqrt(du * du + dv * dv);
                            if (dist < karstPeaks[k].radius)
                            {
                                float tDist = dist / karstPeaks[k].radius; // 0 ở đỉnh, 1 ở rìa chân núi
                                float hillShape = Mathf.Cos(tDist * Mathf.PI * 0.5f);
                                hillShape = Mathf.Pow(hillShape, 1.6f); // Dốc thoai thoải, hoàn hảo không bị co kéo texture

                                float ridgeNoise = 1.0f - Mathf.Abs(Mathf.PerlinNoise(u * 15f + karstPeaks[k].seed, v * 15f + karstPeaks[k].seed) * 2f - 1f);
                                float crags = Mathf.PerlinNoise(u * 30f + karstPeaks[k].seed * 2f, v * 30f + karstPeaks[k].seed * 2f) * 0.08f;

                                float peakH = hillShape * (0.90f + 0.10f * ridgeNoise + crags) * (karstPeaks[k].targetH - baseLandH);
                                if (peakH > mountainBonus) mountainBonus = peakH;
                            }
                        }

                        rawH[y, x] = landH + mountainBonus;
                    }
                }
            }
        }

        float[,] smoothH = SmoothHeights(rawH, tSize, 2);
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

        td.alphamapResolution = 1024;
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
                else if (slope > 0.85f && smoothH[hy, hx] > 0.11f)
                {
                    // Chân đồi đá vôi chuyển tiếp tự nhiên giữa cỏ và vách đá
                    splats[y, x, 4] = 0.70f;
                    splats[y, x, 3] = 0.30f;
                }
                else
                {
                    // TOÀN BỘ ĐỒNG BẰNG BẰNG PHẲNG ĐỂ XÂY DOANH TRẠI: 100% CỎ XANH MƯỚT & RÊU TỰ NHIÊN!
                    splats[y, x, 0] = 0.75f; // layerGrass (TL_Grass_A)
                    splats[y, x, 3] = 0.25f; // layerMoss (TL_Grass_Moss)
                }
            }
        }
        td.SetAlphamaps(0, 0, splats);

        // ==========================================
        // 4. BỐ TRÍ CÂY CỔ THỤ AAA (NATURE RENDERER) & QUY HOẠCH DOANH TRẠI
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

        // Khu vực quy hoạch Doanh Trại Quân Ngô Quyền trên đồng bằng bằng phẳng ven sông
        // Tâm: u = 0.54, v = 0.48, bán kính ~600m được giải phóng mặt bằng phẳng lì để sếp dựng trại
        float campU = 0.54f, campV = 0.48f;
        float campRadius = 0.10f; // Bán kính ~600m

        var trees = new List<TreeInstance>();
        int pCount = protos.Count;

        for (int i = 0; pCount > 0 && i < 85000; i++)
        {
            float tx = Random.value;
            float ty = Random.value;

            int hx = Mathf.Clamp((int)(tx * tSize), 0, tSize - 1);
            int hy = Mathf.Clamp((int)(ty * tSize), 0, tSize - 1);

            if (isWaterMap[hy, hx]) continue;
            float d = distToWater[hy, hx];
            float slope = GetSlope(smoothH, hx, hy, tSize);
            if (slope > 1.2f) continue; // Tuyệt đối không mọc trên vách đá dựng đứng

            // Kiểm tra xem có rơi vào khu quy hoạch Doanh Trại không
            float duCamp = tx - campU;
            float dvCamp = ty - campV;
            if (Mathf.Sqrt(duCamp * duCamp + dvCamp * dvCamp) < campRadius)
            {
                // Để trống 95% diện tích doanh trại, chỉ để lại 5% cây bóng mát viền ngoài
                if (Random.value > 0.05f) continue;
            }

            // Tuyệt đối không mọc cây xuyên vào bên trong các khối núi đá 3D
            bool insideMountain = false;
            for (int k = 0; k < karstPeaks.Length; k++)
            {
                float duM = tx - karstPeaks[k].u;
                float dvM = ty - karstPeaks[k].v;
                if (Mathf.Sqrt(duM * duM + dvM * dvM) < karstPeaks[k].radius * 0.70f)
                {
                    insideMountain = true;
                    break;
                }
            }
            if (insideMountain) continue;

            float normH = smoothH[hy, hx];

            // VÙNG 1: Mép trên bãi cát (d từ 6 đến beachPixelWidth) -> Cụm hoa dại & cỏ lác nhỏ lác đác
            if (d >= 6f && d <= beachPixelWidth)
            {
                if (Random.value < 0.06f) // Chỉ mọc thưa thớt 6%
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

            // VÙNG 2: Rừng rậm & Cây đại thụ trên đồi núi (ngay sau vạch đỏ d > beachPixelWidth + 0.8f)
            if (d > beachPixelWidth + 0.8f)
            {
                TreeInstance tiInland = new TreeInstance();
                tiInland.position = new Vector3(tx, normH, ty);

                // Ưu tiên 65% là Cây Cổ Thụ AAA (Cypress & Conifer - prototypeIndex 0 & 1)
                if (Random.value < 0.65f && protos.Count >= 2)
                {
                    tiInland.prototypeIndex = Random.Range(0, 2);
                    tiInland.widthScale = Random.Range(1.2f, 1.9f);
                    tiInland.heightScale = Random.Range(1.2f, 2.0f);
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
        int dRes = 1024;
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

                    if (d > beachPixelWidth + 0.8f && d < beachPixelWidth + 16f && Random.value < 0.10f)
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
        // 6. TẠO TERRAIN MỚI TRONG SCENE
        // ==========================================
        GameObject terrainGo = Terrain.CreateTerrainGameObject(td);
        terrainGo.name = "SaBan_BachDang_AAA";
        terrainGo.transform.position = new Vector3(-3000, 0, -3000);

        Terrain tComp = terrainGo.GetComponent<Terrain>();
        tComp.drawTreesAndFoliage = true;
        tComp.treeDistance = 6000f;
        tComp.treeBillboardDistance = 6000f;
        tComp.treeCrossFadeLength = 0f;
        tComp.treeMaximumFullLODCount = 200000;
        tComp.detailObjectDistance = 500f;
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

        // ==========================================
        // 8. TẠO HỆ THỐNG NÚI ĐÁ VÔI PHOTOSCANNED (PBR) TỪ TÀI NGUYÊN MỚI
        // ==========================================
        SpawnPhotoscannedMountains(tComp, terrainGo.transform.position);

        AssetDatabase.SaveAssets();
        Debug.Log($"🎉 SA BÀN HOÀN THIỆN: ĐỒNG BẰNG BẰNG PHẲNG (XÂY DOANH TRẠI) + 6 QUẦN THỂ NÚI ĐÁ PHOTOSCANNED PBR 3D + 85.000 CÂY CỔ THỤ NATURE RENDERER ĐÃ HOÀN TẤT!");
    }

    [MenuItem("🤖 Trợ lý AI/🏔️ Dựng 6 Cụm Núi Đá 3D Ngay (Không Cần Tạo Lại Sa Bàn)")]
    public static void SpawnMountainsOnly()
    {
        Terrain t = Terrain.activeTerrain;
        if (t == null)
        {
            var allTerrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allTerrains.Length > 0) t = allTerrains[0];
        }

        if (t == null)
        {
            Debug.LogError("❌ Không tìm thấy Terrain trong Scene! Hãy tạo sa bàn trước.");
            return;
        }

        SpawnPhotoscannedMountains(t, t.transform.position);
        Debug.Log("🎉 [THÀNH CÔNG] Đã cập nhật 6 cụm núi đá 3D Photoscanned PBR vào Scene!");
    }

    [MenuItem("🤖 Trợ lý AI/🏔️ Cập Nhật Material Núi Đá Sang URP Lit PBR")]
    public static void FixRockMaterialsToURP()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("❌ Không tìm thấy shader Universal Render Pipeline/Lit!");
            return;
        }

        string[] matPaths = new[]
        {
            "Assets/TheTalesFactory/Photoscanned MoutainsRocks PBR/Materials/MountainsRocks01.mat",
            "Assets/TheTalesFactory/Photoscanned MoutainsRocks PBR/Materials/MountainsRocks02.mat",
            "Assets/TheTalesFactory/Photoscanned MoutainsRocks PBR/Materials/RockMoutain03.mat",
            "Assets/TheTalesFactory/Photoscanned MoutainsRocks PBR/Materials/MountainsRocks01_MountainsRocks01_Albedo.mat",
            "Assets/TheTalesFactory/Photoscanned MoutainsRocks PBR/Materials/MountainsRocks02_MountainsRocks02_Albedo.mat"
        };

        foreach (var p in matPaths)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (mat == null) continue;

            Texture mainTex = mat.GetTexture("_MainTex");
            if (mainTex == null) mainTex = mat.GetTexture("_BaseMap");
            Texture bumpMap = mat.GetTexture("_BumpMap");
            Texture metallic = mat.GetTexture("_MetallicGlossMap");
            Texture occ = mat.GetTexture("_OcclusionMap");

            mat.shader = urpLit;
            if (mainTex != null)
            {
                mat.SetTexture("_BaseMap", mainTex);
                mat.SetColor("_BaseColor", Color.white);
            }
            if (bumpMap != null)
            {
                mat.SetTexture("_BumpMap", bumpMap);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (metallic != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallic);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (occ != null)
            {
                mat.SetTexture("_OcclusionMap", occ);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("✅ [PBR URP] Đã nâng cấp 100% Material núi đá Photoscanned sang URP Lit PBR!");
    }

    private static void SpawnPhotoscannedMountains(Terrain tComp, Vector3 terrainOrigin)
    {
        // Tự động đảm bảo Material ở chuẩn URP Lit không bị lỗi màu hồng
        FixRockMaterialsToURP();

        string parentName = "--- HỆ THỐNG NÚI ĐÁ VÔI PHOTOSCANNED (PBR) ---";
        GameObject oldParent = GameObject.Find(parentName);
        if (oldParent != null)
        {
            Undo.DestroyObjectImmediate(oldParent);
        }

        GameObject mountainRoot = new GameObject(parentName);
        mountainRoot.transform.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(mountainRoot, "Create Photoscanned Mountains");

        string pDir = "Assets/TheTalesFactory/Photoscanned MoutainsRocks PBR/Prefabs/";
        GameObject pfMain03 = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainsRocks03.prefab");
        GameObject pfMain01 = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks01.prefab");
        GameObject pfMain02 = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks02.prefab");

        GameObject pfSub01A = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks01_A.prefab");
        GameObject pfSub01B = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks01_B.prefab");
        GameObject pfSub01C = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks01_C.prefab");
        GameObject pfSub01D = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks01_D.prefab");
        GameObject pfSub02A = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks02_A.prefab");
        GameObject pfSub02B = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks02_B.prefab");

        if (pfMain03 == null && pfMain01 == null)
        {
            Debug.LogWarning("⚠️ Không tìm thấy Prefab núi đá Photoscanned PBR trong " + pDir);
            return;
        }

        var peakZones = new[]
        {
            new { name = "Quần Thể Núi Tràng Kênh (Tây Bắc)", u = 0.15f, v = 0.65f, scaleFactor = 26f, seed = 101 },
            new { name = "Quần Thể Núi Thủy Nguyên (Tây Nam)", u = 0.16f, v = 0.28f, scaleFactor = 22f, seed = 202 },
            new { name = "Quần Thể Núi U Bò (Đông Bắc)", u = 0.78f, v = 0.80f, scaleFactor = 28f, seed = 303 },
            new { name = "Quần Thể Núi Phượng Hoàng (Đông)", u = 0.83f, v = 0.46f, scaleFactor = 24f, seed = 404 },
            new { name = "Quần Thể Núi Vọng Hải (Đông Nam)", u = 0.79f, v = 0.20f, scaleFactor = 21f, seed = 505 },
            new { name = "Quần Thể Núi Yên Hưng (Bắc)", u = 0.62f, v = 0.86f, scaleFactor = 23f, seed = 606 }
        };

        foreach (var zone in peakZones)
        {
            GameObject clusterGo = new GameObject(zone.name);
            clusterGo.transform.parent = mountainRoot.transform;

            float centerX = terrainOrigin.x + zone.u * 6000f;
            float centerZ = terrainOrigin.z + zone.v * 6000f;
            clusterGo.transform.position = new Vector3(centerX, 0, centerZ);

            Random.InitState(zone.seed);

            // 1. Khối đỉnh chính sừng sững ở tâm (MountainsRocks03 hoặc 01)
            GameObject centerPrefab = (zone.seed % 2 == 0) ? pfMain03 : pfMain01;
            if (centerPrefab == null) centerPrefab = pfMain01 != null ? pfMain01 : pfMain02;
            if (centerPrefab != null)
            {
                SpawnSingleRock(centerPrefab, clusterGo.transform, centerX, centerZ, zone.scaleFactor * 1.15f, tComp);
            }

            // 2. Các đỉnh phụ và vách đá sừng sững bao quanh (bán kính 70m - 220m)
            var surroundingPrefabs = new[] { pfMain02, pfSub01A, pfSub01B, pfSub02A, pfSub02B, pfSub01C, pfSub01D };
            int satelliteCount = 7;
            for (int i = 0; i < satelliteCount; i++)
            {
                GameObject subPf = surroundingPrefabs[i % surroundingPrefabs.Length];
                if (subPf == null) continue;

                float angle = (float)i / satelliteCount * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
                float radius = Random.Range(70f, 220f);
                float posX = centerX + Mathf.Cos(angle) * radius;
                float posZ = centerZ + Mathf.Sin(angle) * radius;

                float subScale = zone.scaleFactor * Random.Range(0.70f, 0.95f);
                SpawnSingleRock(subPf, clusterGo.transform, posX, posZ, subScale, tComp);
            }
        }

        Debug.Log("🏔️ [PHOTOSCANNED PBR] ĐÃ DỰNG THÀNH CÔNG 6 QUẦN THỂ NÚI ĐÁ VÔI PHOTOSCANNED VỚI VÁCH ĐÁ CHÂN THỰC!");
    }

    private static void SpawnSingleRock(GameObject prefab, Transform parent, float worldX, float worldZ, float scale, Terrain tComp)
    {
        float terrainH = tComp.SampleHeight(new Vector3(worldX, 0, worldZ));
        // Chôn sâu chân núi khoảng 10% chiều cao xuống lòng đất để hòa quyện vào cỏ, tránh hở chân
        float spawnY = terrainH - scale * 0.12f;

        GameObject rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (rock == null) rock = Object.Instantiate(prefab);

        rock.name = prefab.name;
        rock.transform.parent = parent;
        rock.transform.position = new Vector3(worldX, spawnY, worldZ);
        rock.transform.rotation = Quaternion.Euler(Random.Range(-2f, 2f), Random.Range(0f, 360f), Random.Range(-2f, 2f));
        rock.transform.localScale = new Vector3(
            scale * Random.Range(0.92f, 1.08f),
            scale * Random.Range(0.95f, 1.20f),
            scale * Random.Range(0.92f, 1.08f)
        );
        rock.isStatic = true;
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
        waterGo.transform.localScale = new Vector3(600, 1, 600);

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

        Vector3 boatPos = new Vector3(-400f, 14.2f, 300f);

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
            float startZ = 260f;
            float stepZ = 80f / Mathf.Max(1, spikes.Count - 1);

            for (int i = 0; i < spikes.Count; i++)
            {
                Undo.RecordObject(spikes[i].transform, "Arrange Spikes");
                float z = startZ + i * stepZ + Random.Range(-4f, 4f);
                float x = -280f + Random.Range(-10f, 10f);
                spikes[i].transform.position = new Vector3(x, 13.5f, z);
                spikes[i].transform.rotation = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0, 360), Random.Range(-10f, -25f));
            }
            Debug.Log($"🪵 Đã giăng bãi cọc gỗ ({spikes.Count} cọc) đón đầu thuyền tại X = -280!");
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            Undo.RecordObject(cam.transform, "Move Camera To Battle");
            cam.transform.position = new Vector3(-480f, 28f, 300f);
            cam.transform.LookAt(new Vector3(-340f, 14.2f, 300f));
            cam.farClipPlane = 9500f;
            if (cam.GetComponent<BattleCamera>() == null)
            {
                cam.gameObject.AddComponent<BattleCamera>();
            }
        }

        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.pivot = new Vector3(-340f, 15f, 300f);
            SceneView.lastActiveSceneView.size = 120f;
            SceneView.lastActiveSceneView.Repaint();
        }
    }
}
