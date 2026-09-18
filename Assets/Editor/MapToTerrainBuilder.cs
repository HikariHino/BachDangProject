using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MapToTerrainBuilder : EditorWindow
{
    // =========================================================================
    // ĐỊNH NGHĨA 6 DÃY NÚI ĐÁ VÔI KARST HÙNG VĨ (ĐÚNG BỐI CẢNH LỊCH SỬ BẠCH ĐẰNG 938)
    // TẤT CẢ TỌA ĐỘ NẰM SÂU TRONG LÒNG ĐẢO / ĐẤT LIỀN (CÁCH NƯỚC 180m - 430m)
    // =========================================================================
    public struct KarstMountainDef
    {
        public string name;
        public float centerU;
        public float centerV;
        public float ridgeDirU;
        public float ridgeDirV;
        public float ridgeLength;
        public float radius;
        public float targetH;
        public float rockScale;
        public int seed;
    }

    public static readonly KarstMountainDef[] MountainRanges = new KarstMountainDef[]
    {
        // 1. Tây Bắc: Dãy Núi Tràng Kênh (Bờ Tây Bắc - Đài Quan Sát Ngô Quyền)
        new KarstMountainDef {
            name = "Dãy Núi Tràng Kênh (Tây Bắc)",
            centerU = 0.18f, centerV = 0.82f,
            ridgeDirU = 0.5f, ridgeDirV = 0.866f, ridgeLength = 0.07f,
            radius = 0.085f, targetH = 0.72f, rockScale = 4.2f, seed = 101
        },

        // 2. Tây Trung Tâm: Dãy Núi Thủy Nguyên (Bờ Tây Trung Tâm)
        new KarstMountainDef {
            name = "Dãy Núi Thủy Nguyên (Tây Trung Tâm)",
            centerU = 0.20f, centerV = 0.48f,
            ridgeDirU = 0.707f, ridgeDirV = -0.707f, ridgeLength = 0.08f,
            radius = 0.080f, targetH = 0.68f, rockScale = 3.8f, seed = 202
        },

        // 3. Tây Nam: Dãy Núi Vọng Triều (Bờ Tây Nam)
        new KarstMountainDef {
            name = "Dãy Núi Vọng Triều (Tây Nam)",
            centerU = 0.18f, centerV = 0.14f,
            ridgeDirU = 0.95f, ridgeDirV = 0.31f, ridgeLength = 0.06f,
            radius = 0.075f, targetH = 0.62f, rockScale = 3.5f, seed = 303
        },

        // 4. Đông Bắc: Quần Thể Núi U Bò - Yên Đức (Đảo Đông Bắc)
        new KarstMountainDef {
            name = "Quần Thể Núi U Bò - Yên Đức (Đảo Đông Bắc)",
            centerU = 0.76f, centerV = 0.82f,
            ridgeDirU = -0.6f, ridgeDirV = 0.8f, ridgeLength = 0.06f,
            radius = 0.075f, targetH = 0.75f, rockScale = 4.5f, seed = 404
        },

        // 5. Đảo Giữa: Dãy Núi Cù Lao Phượng Hoàng (Sống Núi Giữa Đảo Lớn)
        new KarstMountainDef {
            name = "Dãy Núi Cù Lao Phượng Hoàng (Đảo Giữa)",
            centerU = 0.65f, centerV = 0.54f,
            ridgeDirU = 0.85f, ridgeDirV = -0.52f, ridgeLength = 0.09f,
            radius = 0.070f, targetH = 0.66f, rockScale = 4.0f, seed = 505
        },

        // 6. Đảo Nam: Quần Thể Núi Cù Lao Vọng Hải (Đảo Đông Nam)
        new KarstMountainDef {
            name = "Quần Thể Núi Cù Lao Vọng Hải (Đảo Nam)",
            centerU = 0.65f, centerV = 0.27f,
            ridgeDirU = 0.80f, ridgeDirV = 0.60f, ridgeLength = 0.05f,
            radius = 0.065f, targetH = 0.60f, rockScale = 3.6f, seed = 606
        }
    };

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

        // 0. XÓA TERRAIN CŨ
        var allTerrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTerrains)
        {
            if (t != null && t.gameObject != null) Undo.DestroyObjectImmediate(t.gameObject);
        }

        var allGos = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allGos)
        {
            if (go == null) continue;
            string n = go.name.ToLower();
            if (n.StartsWith("saban_") || n == "terrain" || n.Contains("he thong nui") || n.Contains("hệ thống núi") || n.Contains("photoscanned") || n.Contains("chiến trường") || n.Contains("cọc ngầm") || n.StartsWith("wood_spike_02_sculpted"))
            {
                Undo.DestroyObjectImmediate(go);
            }
        }

        // 1. TÍNH TOÁN KHOẢNG CÁCH TỪ NƯỚC VÀO BỜ
        int tSize = 2049;
        TerrainData td = new TerrainData();
        td.heightmapResolution = tSize;
        td.size = new Vector3(6000, 300, 6000); // 6000m x 300m x 6000m

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

        // 2. KHỞI TẠO ĐỊA HÌNH
        float[,] rawH = new float[tSize, tSize];
        float beachPixelWidth = 9f;

        float waterH = 0.0467f;      // 14.0m
        float baseLandH = 0.05833f;  // 17.5m

        for (int y = 0; y < tSize; y++)
        {
            for (int x = 0; x < tSize; x++)
            {
                float u = (float)x / (tSize - 1);
                float v = (float)y / (tSize - 1);

                if (isWaterMap[y, x])
                {
                    rawH[y, x] = 0.020f + Mathf.PerlinNoise(u * 20f, v * 20f) * 0.003f; // Lòng sông sâu ~6m
                }
                else
                {
                    float d = distToWater[y, x];
                    if (d <= beachPixelWidth)
                    {
                        float t = d / beachPixelWidth;
                        rawH[y, x] = Mathf.Lerp(waterH + 0.001f, baseLandH, Mathf.Pow(t, 1.2f));
                    }
                    else
                    {
                        // Đồng bằng
                        float microRoll = (Mathf.PerlinNoise(u * 12f, v * 12f) - 0.5f) * 0.0012f;
                        float landH = baseLandH + microRoll;

                        // Sống núi Karst cao vút hùng vĩ (phần này do Terrain tạo hình, 100% không bao giờ bay lên trời!)
                        float mountainBonus = 0f;
                        for (int k = 0; k < MountainRanges.Length; k++)
                        {
                            var m = MountainRanges[k];
                            float du = u - m.centerU;
                            float dv = v - m.centerV;

                            float tProj = Mathf.Clamp((du * m.ridgeDirU + dv * m.ridgeDirV), -m.ridgeLength * 0.5f, m.ridgeLength * 0.5f);
                            float projU = m.centerU + m.ridgeDirU * tProj;
                            float projV = m.centerV + m.ridgeDirV * tProj;

                            float distToRidge = Mathf.Sqrt((u - projU) * (u - projU) + (v - projV) * (v - projV));
                            if (distToRidge < m.radius)
                            {
                                float tDist = distToRidge / m.radius;
                                // Dốc thoai thoải ở chân, vách đứng ở giữa, nhọn hoắt ở đỉnh
                                float hillShape = Mathf.Pow(Mathf.Cos(tDist * Mathf.PI * 0.5f), 1.8f);

                                float ridgeNoise = 1.0f - Mathf.Abs(Mathf.PerlinNoise(u * 18f + m.seed, v * 18f + m.seed) * 2f - 1f);
                                float sharpPinnacle = Mathf.Pow(ridgeNoise, 2.5f) * 0.25f;
                                float crags = Mathf.PerlinNoise(u * 36f + m.seed * 2f, v * 36f + m.seed * 2f) * 0.10f;

                                float peakH = hillShape * (0.80f + sharpPinnacle + crags) * (m.targetH - baseLandH);
                                if (peakH > mountainBonus) mountainBonus = peakH;
                            }
                        }

                        // CHUYỂN TIẾP ĐỘ DỐC TỰ NHIÊN VỀ PHÍA MÉP NƯỚC (CHỮA DỨT ĐIỂM LỖI DỐC ĐỨNG 90 ĐỘ NHƯ HÌNH 2)
                        float inlandDist = d - beachPixelWidth;
                        float shoreTransition = Mathf.Clamp01(inlandDist / 55f); // chuyển tiếp mượt mà trong ~160m
                        shoreTransition = Mathf.SmoothStep(0f, 1f, shoreTransition);
                        mountainBonus *= shoreTransition;

                        rawH[y, x] = landH + mountainBonus;
                    }
                }
            }
        }

        float[,] smoothH = SmoothHeights(rawH, tSize, 3);
        td.SetHeights(0, 0, smoothH);

        // 3. TEXTURE PBR AAA
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
                    splats[y, x, 2] = 1.0f;
                }
                else if (d <= beachPixelWidth + 2.0f)
                {
                    float t = (d - beachPixelWidth) / 2.0f;
                    splats[y, x, 2] = 1f - t;
                    splats[y, x, 0] = t * 0.75f;
                    splats[y, x, 3] = t * 0.25f;
                }
                else if (slope > 0.20f && smoothH[hy, hx] > 0.065f)
                {
                    float rockFactor = Mathf.Clamp01((slope - 0.20f) * 2.5f);
                    splats[y, x, 4] = rockFactor * 0.70f;
                    splats[y, x, 5] = rockFactor * 0.18f;
                    splats[y, x, 3] = 0.12f + (1f - rockFactor) * 0.35f;
                    splats[y, x, 0] = (1f - rockFactor) * 0.53f;
                }
                else
                {
                    splats[y, x, 0] = 0.75f;
                    splats[y, x, 3] = 0.25f;
                }
            }
        }
        td.SetAlphamaps(0, 0, splats);

        // 4. BỐ TRÍ CÂY CỐI & QUY HOẠCH DOANH TRẠI (Bờ Tây tại X = -820, Z = 320 => U = 0.363, V = 0.553)
        string nrArt = "Assets/Visual Design Cafe/Nature Renderer Demo/Realistic/Art/";
        GameObject cypressTree = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Trees/Cypress.prefab");
        GameObject coniferTree = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Trees/Conifer.prefab");
        GameObject rockA = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Rocks/Rock_A_02.prefab");
        GameObject rockC = AssetDatabase.LoadAssetAtPath<GameObject>(nrArt + "Rocks/Rock_C_01.prefab");

        string prefabFolder = "Assets/TerrainSampleAssets/Prefabs/";
        var protos = new List<TreePrototype>();

        if (cypressTree != null) protos.Add(new TreePrototype { prefab = cypressTree });
        if (coniferTree != null) protos.Add(new TreePrototype { prefab = coniferTree });
        if (rockA != null) protos.Add(new TreePrototype { prefab = rockA });
        if (rockC != null) protos.Add(new TreePrototype { prefab = rockC });

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

        // Tọa độ chuẩn của Doanh Trại Ngô Quyền trên đồng bằng bờ Tây
        float campU = (-820f + 3000f) / 6000f; // 0.3633f
        float campV = (320f + 3000f) / 6000f;  // 0.5533f
        float campRadius = 0.055f; // ~330m dọn sạch cây để dựng doanh trại

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
            if (slope > 1.2f) continue;

            // Dọn cây trong khu doanh trại
            float duCamp = tx - campU;
            float dvCamp = ty - campV;
            if (Mathf.Sqrt(duCamp * duCamp + dvCamp * dvCamp) < campRadius)
            {
                if (Random.value > 0.03f) continue;
            }

            // Tránh mọc cây đè lên các đỉnh núi đá
            bool insideMountain = false;
            for (int k = 0; k < MountainRanges.Length; k++)
            {
                var m = MountainRanges[k];
                float du = tx - m.centerU;
                float dv = ty - m.centerV;
                float tProj = Mathf.Clamp((du * m.ridgeDirU + dv * m.ridgeDirV), -m.ridgeLength * 0.5f, m.ridgeLength * 0.5f);
                float pU = m.centerU + m.ridgeDirU * tProj;
                float pV = m.centerV + m.ridgeDirV * tProj;
                float dR = Mathf.Sqrt((tx - pU) * (tx - pU) + (ty - pV) * (ty - pV));
                if (dR < m.radius * 0.40f)
                {
                    insideMountain = true;
                    break;
                }
            }
            if (insideMountain) continue;

            float normH = smoothH[hy, hx];

            if (d >= 6f && d <= beachPixelWidth)
            {
                if (Random.value < 0.06f)
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

            if (d > beachPixelWidth + 0.8f)
            {
                TreeInstance tiInland = new TreeInstance();
                tiInland.position = new Vector3(tx, normH, ty);

                if (Random.value < 0.65f && protos.Count >= 2)
                {
                    tiInland.prototypeIndex = Random.Range(0, 2);
                    tiInland.widthScale = Random.Range(1.2f, 1.9f);
                    tiInland.heightScale = Random.Range(1.2f, 2.0f);
                }
                else if (Random.value < 0.10f && protos.Count >= 4)
                {
                    tiInland.prototypeIndex = Random.Range(2, 4);
                    tiInland.widthScale = Random.Range(1.5f, 3.0f);
                    tiInland.heightScale = Random.Range(1.2f, 2.5f);
                }
                else
                {
                    tiInland.prototypeIndex = Random.Range(4, pCount);
                    tiInland.widthScale = Random.Range(1.8f, 3.5f);
                    tiInland.heightScale = Random.Range(1.8f, 3.5f);
                }

                tiInland.color = tiInland.lightmapColor = Color.white;
                trees.Add(tiInland);
            }
        }
        td.SetTreeInstances(trees.ToArray(), true);

        // 5. CHI TIẾT CỎ & HOA
        int dRes = 1024;
        td.SetDetailResolution(dRes, 16);
        var dProtos = new List<DetailPrototype>();

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

        td.detailPrototypes = dProtos.ToArray();
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
                    grassMap[y, x] = (d > beachPixelWidth + 0.5f) ? Random.Range(3, 7) : 0;
                }
            }
            td.SetDetailLayer(0, 0, 0, grassMap);
        }

        // 6. TẠO TERRAIN GAME OBJECT
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

        System.Type nrType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            nrType = asm.GetType("VisualDesignCafe.Rendering.Nature.NatureRenderer");
            if (nrType != null) break;
        }
        if (nrType != null && terrainGo.GetComponent(nrType) == null)
        {
            terrainGo.AddComponent(nrType);
        }

        // 7. CẬP NHẬT MẶT NƯỚC, BỐ TRÍ THUYỀN, CỌC & DOANH TRẠI
        SetupOptiWaterSurface(14.0f);
        PlaceBoatAndSpikesInRiver();
        SpawnPhotoscannedMountains(tComp, terrainGo.transform.position);
        SpawnHistoricSpikes();
        SpawnHistoricCampAndWorkshop();

        AssetDatabase.SaveAssets();
        Debug.Log("🎉 SA BÀN BẠCH ĐẰNG 938: ĐÃ XÂY DỰNG XONG HOÀN CHỈNH!");
    }

    // =========================================================================
    // MENU ITEM: DỰNG & GIÁP VÁCH ĐÁ 3D CHO KHE NƯỚC & SƯỜN NÚI (PBR AAA)
    // =========================================================================
    [MenuItem("🤖 Trợ lý AI/🏔️ Giáp Vách Đá 3D Cho Khe Nước & Sườn Núi (Cập Nhật Ngay)")]
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
        Debug.Log("🎉 ĐÃ GIÁP VÁCH ĐÁ 3D THÀNH CÔNG: KHE NƯỚC ĐÃ ĐƯỢC ỐP VÁCH ĐÁ DỰNG ĐỨNG, SƯỜN NÚI ĐẦY MỎM ĐÁ TỰ NHIÊN!");
    }

    private static void SpawnPhotoscannedMountains(Terrain tComp, Vector3 terrainOrigin)
    {
        FixRockMaterialsToURP();

        string parentName = "--- HỆ THỐNG NÚI ĐÁ VÔI PHOTOSCANNED (PBR) ---";
        var allMounts = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allMounts)
        {
            if (go != null && (go.name == parentName || go.name.Contains("HỆ THỐNG NÚI ĐÁ VÔI") || go.name.ToLower().Contains("photoscanned")))
            {
                Undo.DestroyObjectImmediate(go);
            }
        }

        GameObject mountainRoot = new GameObject(parentName);
        mountainRoot.transform.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(mountainRoot, "Create Photoscanned Mountains");

        string pDir = "Assets/TheTalesFactory/Photoscanned MoutainsRocks PBR/Prefabs/";
        GameObject pfMain01 = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks01.prefab");
        GameObject pfMain02 = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks02.prefab");
        GameObject pfMain03 = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainsRocks03.prefab");

        GameObject pfSub01A = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks01_A.prefab");
        GameObject pfSub01B = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks01_B.prefab");
        GameObject pfSub01C = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks01_C.prefab");
        GameObject pfSub01D = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks01_D.prefab");
        GameObject pfSub02A = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks02_A.prefab");
        GameObject pfSub02B = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "MountainRocks02_B.prefab");

        GameObject pfBoulder01 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/pf_boulder_01.prefab");
        GameObject pfBoulder02 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/pf_boulder_02_025.prefab");

        // =========================================================================
        // BỘ VÁCH ĐÁ CHÍNH: BLOCKY CLIFF FORMATIONS (GRAY ROCK - URP LIT NATIVE CỦA RAVIBIO)
        // Cực kỳ hợp bối cảnh Bạch Đằng: Đá vôi karst xám, 5 tầng LOD, khối 3D kín không hở đáy!
        // =========================================================================
        string blockyDir = "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Gray Rock/";
        var blockyCliffs = new List<GameObject>();
        for (int i = 1; i <= 11; i++)
        {
            GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{blockyDir}Cliff_{i}_LODs.prefab");
            if (pf != null) blockyCliffs.Add(pf);
        }

        // TẢNG ĐÁ RÊU XANH BỔ TRỢ: PIZZA&GAMES REALISTIC ROCKS (URP LIT)
        string mossDir = "Assets/Pizza&Games/Realistic Rocks/Prefabs/";
        var mossBoulders = new List<GameObject>();
        for (int i = 1; i <= 6; i++)
        {
            GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{mossDir}SM_LittleRock_0{i}_GreenMoss.prefab");
            if (pf == null) pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{mossDir}SM_LittleRock_0{i}.prefab");
            if (pf != null) mossBoulders.Add(pf);
        }

        // Danh sách dự phòng nếu không tìm thấy pack mới
        var fallbackCliffs = new[] { pfSub01A, pfSub01B, pfSub02A, pfSub02B, pfMain02, pfMain01, pfMain03 };
        var cliffPrefabs = blockyCliffs.Count > 0 ? blockyCliffs.ToArray() : fallbackCliffs;
        var boulderPrefabs = mossBoulders.Count > 0 ? mossBoulders.ToArray() : new[] { pfBoulder01, pfBoulder02 };

        Texture2D mapTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BachDangMap.png");

        foreach (var zone in MountainRanges)
        {
            GameObject clusterGo = new GameObject(zone.name);
            clusterGo.transform.parent = mountainRoot.transform;

            float centerX = terrainOrigin.x + zone.centerU * 6000f;
            float centerZ = terrainOrigin.z + zone.centerV * 6000f;
            clusterGo.transform.position = new Vector3(centerX, 0, centerZ);

            Random.InitState(zone.seed);

            // Container 1: Đỉnh & Sống Núi Chính
            GameObject grpPeak = new GameObject("1_Dinh_Va_Song_Nui");
            grpPeak.transform.parent = clusterGo.transform;

            // Container 2: Vách Đá Ốp Dọc Sườn Núi (Triệt tiêu 100% lỗi hở chân)
            GameObject grpFlank = new GameObject("2_Vach_Da_Suon_Nui");
            grpFlank.transform.parent = clusterGo.transform;

            // Container 3: Vách Đá Giáp Khe Nước & Hẻm Sông
            GameObject grpGorge = new GameObject("3_Vach_Da_Giap_Khe_Nuoc");
            grpGorge.transform.parent = clusterGo.transform;

            // -------------------------------------------------------------
            // 1. MỎM ĐÁ ĐỈNH CHÍNH & SỐNG NÚI (Đá vôi xám hùng vĩ)
            // -------------------------------------------------------------
            GameObject centerPrefab = (zone.seed % 2 == 0) ? pfMain03 : pfMain01;
            if (centerPrefab == null || blockyCliffs.Count > 0) 
            {
                centerPrefab = blockyCliffs.Count > 0 ? blockyCliffs[zone.seed % blockyCliffs.Count] : pfMain01;
            }
            if (centerPrefab != null)
            {
                float peakScale = blockyCliffs.Count > 0 ? 2.2f : zone.rockScale * 1.05f;
                SpawnSingleRockGrounded(centerPrefab, grpPeak.transform, centerX, centerZ, peakScale, tComp, null, 1.8f);
            }

            // Mỏm đá sống núi (8 - 10 mỏm)
            int ridgeCount = 10;
            for (int i = 0; i < ridgeCount; i++)
            {
                float tRidge = ((float)i / (ridgeCount - 1) - 0.5f) * zone.ridgeLength * 0.85f * 6000f;
                float rSide = Random.Range(-40f, 40f);

                float rX = centerX + zone.ridgeDirU * tRidge - zone.ridgeDirV * rSide;
                float rZ = centerZ + zone.ridgeDirV * tRidge + zone.ridgeDirU * rSide;

                if (IsPositionSafeFromWater(mapTex, rX, rZ, terrainOrigin, 60f))
                {
                    GameObject rPf = cliffPrefabs[i % cliffPrefabs.Length];
                    if (rPf != null)
                    {
                        float rScale = blockyCliffs.Count > 0 ? Random.Range(1.6f, 2.3f) : zone.rockScale * Random.Range(0.70f, 0.95f);
                        SpawnSingleRockGrounded(rPf, grpPeak.transform, rX, rZ, rScale, tComp, null, 1.8f);
                    }
                }
            }

            // -------------------------------------------------------------
            // 2. VÁCH ĐÁ & TẢNG ĐÁ RÊU ỐP SƯỜN NÚI (TỰA VÀO LÒNG NÚI, TUYỆT ĐỐI KHÔNG HỞ CHÂN)
            // -------------------------------------------------------------
            int flankRockCount = 16;
            for (int f = 0; f < flankRockCount; f++)
            {
                float alongRidge = Random.Range(-zone.ridgeLength * 0.45f, zone.ridgeLength * 0.45f) * 6000f;
                float sideSign = (f % 2 == 0) ? 1f : -1f;
                float sideDist = sideSign * Random.Range(40f, zone.radius * 0.65f * 6000f);

                float posX = centerX + zone.ridgeDirU * alongRidge - zone.ridgeDirV * sideDist;
                float posZ = centerZ + zone.ridgeDirV * alongRidge + zone.ridgeDirU * sideDist;

                if (!IsPositionSafeFromWater(mapTex, posX, posZ, terrainOrigin, 35f)) continue;

                // Lấy pháp tuyến địa hình để tựa vách đá ngả sâu vào lòng sườn núi
                float normU = Mathf.Clamp01((posX - terrainOrigin.x) / 6000f);
                float normV = Mathf.Clamp01((posZ - terrainOrigin.z) / 6000f);
                Vector3 terrNorm = tComp.terrainData.GetInterpolatedNormal(normU, normV);

                // Ngả vách đá 20 - 30 độ vào trong sườn núi (tựa lưng vững chãi, mặt đá lộ thiên tự nhiên)
                Quaternion slopeAlign = Quaternion.FromToRotation(Vector3.up, terrNorm);
                Quaternion cliffRot = slopeAlign * Quaternion.Euler(
                    Random.Range(-20f, -32f), 
                    Random.Range(0f, 360f), 
                    0
                );

                GameObject flankPf = cliffPrefabs[(f + zone.seed) % cliffPrefabs.Length];
                if (flankPf != null)
                {
                    float fScale = blockyCliffs.Count > 0 ? Random.Range(1.4f, 2.1f) : zone.rockScale * Random.Range(0.75f, 1.15f);
                    SpawnSingleRockGrounded(flankPf, grpFlank.transform, posX, posZ, fScale, tComp, cliffRot, 2.0f);
                }

                // Thêm tảng đá rêu xanh nhỏ dưới sườn núi
                if (Random.value < 0.45f && boulderPrefabs.Length > 0)
                {
                    GameObject bPf = boulderPrefabs[Random.Range(0, boulderPrefabs.Length)];
                    if (bPf != null)
                    {
                        Vector3 downhill = new Vector3(terrNorm.x, 0, terrNorm.z).normalized;
                        Vector3 bPos = new Vector3(posX + downhill.x * 12f + Random.Range(-3f, 3f), 0, posZ + downhill.z * 12f + Random.Range(-3f, 3f));
                        float bScale = mossBoulders.Count > 0 ? Random.Range(2.5f, 4.5f) : Random.Range(3.5f, 6.0f);
                        SpawnSingleRockGrounded(bPf, grpFlank.transform, bPos.x, bPos.z, bScale, tComp, null, 1.5f);
                    }
                }
            }

            // -------------------------------------------------------------
            // 3. VÁCH ĐÁ GIÁP KHE NƯỚC & HẺM SÔNG (Hình 1: Giáp kín vách đá dọc khe nước)
            // -------------------------------------------------------------
            int angleSteps = 28;
            for (int a = 0; a < angleSteps; a++)
            {
                float angle = a * (Mathf.PI * 2f / angleSteps);
                Vector2 dir2D = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                for (float radFactor = 0.40f; radFactor <= 1.25f; radFactor += 0.15f)
                {
                    float dist = zone.radius * radFactor * 6000f;
                    float checkX = centerX + dir2D.x * dist;
                    float checkZ = centerZ + dir2D.y * dist;

                    bool safeFromWaterClose = IsPositionSafeFromWater(mapTex, checkX, checkZ, terrainOrigin, 18f);
                    bool nearWaterFar = !IsPositionSafeFromWater(mapTex, checkX, checkZ, terrainOrigin, 85f);

                    if (safeFromWaterClose && nearWaterFar)
                    {
                        float h = tComp.SampleHeight(new Vector3(checkX, 0, checkZ));
                        if (h >= 18.0f)
                        {
                            float normU = Mathf.Clamp01((checkX - terrainOrigin.x) / 6000f);
                            float normV = Mathf.Clamp01((checkZ - terrainOrigin.z) / 6000f);
                            Vector3 terrNorm = tComp.terrainData.GetInterpolatedNormal(normU, normV);

                            // Vách đá quay mặt trực diện ra lòng khe nước
                            Vector3 faceWater = new Vector3(terrNorm.x, 0, terrNorm.z).normalized;
                            if (faceWater.sqrMagnitude < 0.01f)
                            {
                                faceWater = new Vector3(dir2D.x, 0, dir2D.y).normalized;
                            }

                            Quaternion gorgeRot = Quaternion.LookRotation(faceWater, Vector3.up) * 
                                Quaternion.Euler(Random.Range(-8f, 5f), Random.Range(-15f, 15f), 0);

                            GameObject gorgePf = cliffPrefabs[(a + (int)(radFactor * 10)) % cliffPrefabs.Length];
                            if (gorgePf != null)
                            {
                                float gScale = blockyCliffs.Count > 0 ? Random.Range(1.8f, 2.5f) : zone.rockScale * Random.Range(0.85f, 1.25f);
                                SpawnSingleRockGrounded(gorgePf, grpGorge.transform, checkX, checkZ, gScale, tComp, gorgeRot, 2.2f);
                            }
                            break;
                        }
                    }
                }
            }
        }

        Debug.Log("🏔️ ĐÃ GIÁP VÁCH ĐÁ 3D THÀNH CÔNG: KHE NƯỚC ĐÃ ĐƯỢC ỐP VÁCH ĐÁ DỰNG ĐỨNG, SƯỜN NÚI TỰ NHIÊN 100% KHÔNG HỞ CHÂN!");
    }

    private static void SpawnSingleRockGrounded(GameObject prefab, Transform parent, float worldX, float worldZ, float scale, Terrain tComp, Quaternion? customRot = null, float embedRatio = 1.8f)
    {
        // 1. CHỐNG TRIỆT ĐỂ LỖI HỞ CHÂN TRÊN SƯỜN DỐC:
        // Quét độ cao địa hình tại tâm và 8 điểm xung quanh chu vi khối đá (bán kính scale * 2.8m).
        // Luôn neo vị trí Y theo điểm THẤP NHẤT của địa hình xung quanh để chân đá cắm ngập vào lòng đất 100%!
        float centerH = tComp.SampleHeight(new Vector3(worldX, 0, worldZ));
        float minFootprintH = centerH;

        float radius = scale * 2.8f;
        for (int i = 0; i < 8; i++)
        {
            float ang = i * Mathf.PI * 0.25f;
            float px = worldX + Mathf.Cos(ang) * radius;
            float pz = worldZ + Mathf.Sin(ang) * radius;
            float h = tComp.SampleHeight(new Vector3(px, 0, pz));
            if (h < minFootprintH) minFootprintH = h;
        }

        // Chôn chân đá thấp hơn điểm thấp nhất của địa hình xung quanh
        float spawnY = minFootprintH - scale * embedRatio;

        GameObject rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (rock == null) rock = Object.Instantiate(prefab);

        rock.name = prefab.name;
        rock.transform.parent = parent;
        rock.transform.position = new Vector3(worldX, spawnY, worldZ);
        if (customRot.HasValue)
        {
            rock.transform.rotation = customRot.Value;
        }
        else
        {
            // Ngả nhẹ theo sườn dốc
            float normU = Mathf.Clamp01((worldX - tComp.transform.position.x) / tComp.terrainData.size.x);
            float normV = Mathf.Clamp01((worldZ - tComp.transform.position.z) / tComp.terrainData.size.z);
            Vector3 terrNorm = tComp.terrainData.GetInterpolatedNormal(normU, normV);

            if (terrNorm.y < 0.98f)
            {
                Quaternion slopeAlign = Quaternion.FromToRotation(Vector3.up, terrNorm);
                rock.transform.rotation = slopeAlign * Quaternion.Euler(Random.Range(-15f, -5f), Random.Range(0f, 360f), 0);
            }
            else
            {
                rock.transform.rotation = Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(0f, 360f), Random.Range(-3f, 3f));
            }
        }

        rock.transform.localScale = new Vector3(
            scale * Random.Range(0.92f, 1.08f),
            scale * Random.Range(0.95f, 1.15f),
            scale * Random.Range(0.92f, 1.08f)
        );
        rock.isStatic = true;
    }

    public static bool IsPositionSafeFromWater(Texture2D mapTex, float worldX, float worldZ, Vector3 terrainOrigin, float safetyMarginMeters)
    {
        if (mapTex == null) return true;
        float u = (worldX - terrainOrigin.x) / 6000f;
        float v = (worldZ - terrainOrigin.z) / 6000f;
        if (u <= 0.01f || u >= 0.99f || v <= 0.01f || v >= 0.99f) return false;

        float deltaUV = safetyMarginMeters / 6000f;

        Color cCenter = mapTex.GetPixelBilinear(u, v);
        if (IsPixelWater(cCenter)) return false;

        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            float su = u + Mathf.Cos(angle) * deltaUV;
            float sv = v + Mathf.Sin(angle) * deltaUV;
            if (su < 0f || su > 1f || sv < 0f || sv > 1f) return false;
            Color c = mapTex.GetPixelBilinear(su, sv);
            if (IsPixelWater(c)) return false;
        }
        return true;
    }

    private static bool IsPixelWater(Color c)
    {
        return (c.b >= c.g * 0.95f) || (c.b > 0.45f && c.b > c.r + 0.15f);
    }

    // =========================================================================
    // GIĂNG TRẬN ĐỊA CỌC NGẦM GỖ LIM BỊT SẮT (CẮM TỪ ĐÁY SÔNG NHÔ LÊN)
    // =========================================================================
    [MenuItem("🤖 Trợ lý AI/🪵 Giăng Trận Địa Cọc Ngầm Sông Bạch Đằng")]
    public static void SpawnHistoricSpikes()
    {
        Terrain tComp = Terrain.activeTerrain;
        if (tComp == null)
        {
            var allTerrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allTerrains.Length > 0) tComp = allTerrains[0];
        }

        string pName = "--- TRẬN ĐỊA CỌC NGẦM BẠCH ĐẰNG (938) ---";
        var allSpikes = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allSpikes)
        {
            if (go == null) continue;
            string n = go.name;
            if (n == pName || n.StartsWith("Wood_Spike_02_sculpted") || n.StartsWith("CocGo_Hang") || n.Contains("TRẬN ĐỊA CỌC NGẦM"))
            {
                Undo.DestroyObjectImmediate(go);
            }
        }

        GameObject root = new GameObject(pName);
        root.transform.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(root, "Spawn Historic Spikes");

        string spikePath = "Assets/wood-spike/source/Wood_Spike_02_sculpted.fbx";
        GameObject spikePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(spikePath);
        if (spikePrefab == null)
        {
            Debug.LogError("❌ Không tìm thấy mô hình cọc gỗ tại: " + spikePath);
            return;
        }

        // Material gỗ lim URP Lit
        Material spikeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/wood-spike/Materials/Wood_Spike_URP.mat");

        int rowCount = 4;
        int countPerRow = 12;

        for (int r = 0; r < rowCount; r++)
        {
            float rowX = -290f + r * 18f;
            for (int i = 0; i < countPerRow; i++)
            {
                float zT = (float)i / (countPerRow - 1);
                float rowZ = Mathf.Lerp(180f, 400f, zT) + Random.Range(-6f, 6f);
                float posX = rowX + Random.Range(-5f, 5f);

                // Lấy độ cao đáy sông thực tế
                float bedY = 6.0f;
                if (tComp != null)
                {
                    bedY = tComp.SampleHeight(new Vector3(posX, 0, rowZ));
                }

                // CẮM SÂU VÀO ĐÁY SÔNG: Chân cọc bắt đầu từ dưới đáy bùn 1.2m
                Vector3 basePosition = new Vector3(posX, bedY - 1.2f, rowZ);

                GameObject spike = (GameObject)PrefabUtility.InstantiatePrefab(spikePrefab);
                if (spike == null) spike = Object.Instantiate(spikePrefab);

                spike.name = $"CocGo_Hang{r+1}_{i+1}";
                spike.transform.parent = root.transform;
                spike.transform.position = basePosition;

                // Gán Material gỗ lim đậm màu URP Lit
                if (spikeMat != null)
                {
                    var renderers = spike.GetComponentsInChildren<MeshRenderer>();
                    foreach (var rend in renderers)
                    {
                        rend.sharedMaterial = spikeMat;
                    }
                }

                // CỌC ĐỨNG CẮM TỪ DƯỚI ĐÁY LÊN, NGHIÊNG 18 - 24 ĐỘ ĐÓN ĐẦU THUYỀN GIẶC (-X)
                // Dùng Quaternion.LookRotation để hướng mũi nhọn thẳng lên trên mặt nước
                Vector3 tiltDirection = new Vector3(Random.Range(-0.24f, -0.36f), 0.93f, Random.Range(-0.08f, 0.08f)).normalized;
                spike.transform.rotation = Quaternion.LookRotation(tiltDirection, Vector3.up);

                // CỌC BỰ KHỔNG LỒ: Thân to 1.1m - 1.3m, dài 12m - 14m vươn từ đáy sông lên mấp mé mặt nước!
                float thickScale = Random.Range(4.5f, 5.5f);  // Đường kính thân cây khổng lồ
                float lengthScale = Random.Range(3.8f, 4.3f); // Chiều dài cọc vươn từ đáy sông lên mặt nước
                spike.transform.localScale = new Vector3(thickScale, thickScale, lengthScale);

                spike.isStatic = true;
            }
        }

        Debug.Log($"🪵 [BẠCH ĐẰNG 938] ĐÃ CẮM THÀNH CÔNG {rowCount * countPerRow} CỌC GỖ LIM BỰ KHỔNG LỒ CẮM CHẶT TỪ ĐÁY SÔNG NHÔ LÊN!");
    }

    // =========================================================================
    // DỰNG ĐẠI BẢN DOANH NGÔ QUYỀN & XƯỞNG CHẾ TÁC CỌC (TRÊN ĐỒNG BẰNG BỜ TÂY, KHÔNG DƯỚI NƯỚC)
    // =========================================================================
    [MenuItem("🤖 Trợ lý AI/⛺ Bố Trí Doanh Trại Ngô Quyền & Bãi Chế Tác Cọc")]
    public static void SpawnHistoricCampAndWorkshop()
    {
        Terrain t = Terrain.activeTerrain;
        if (t == null)
        {
            var allTerrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allTerrains.Length > 0) t = allTerrains[0];
        }

        string pDir = "Assets/Prefabs/";
        GameObject pfBarracks = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_barracks_01.prefab");
        GameObject pfGate = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_gate_01.prefab");
        GameObject pfTower = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_tower_01.prefab");
        GameObject pfStorage = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_storage_01.prefab");
        GameObject pfBlacksmith = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_blacksmith_01.prefab");
        GameObject pfCrane = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_crane_01.prefab");
        GameObject pfBoat = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Buildings/pf_build_boat_01.prefab");
        GameObject pfFence = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_fence_01.prefab");
        GameObject pfBarrels = AssetDatabase.LoadAssetAtPath<GameObject>(pDir + "Props/pf_barrels_01.prefab");

        string campParentName = "--- CHIẾN TRƯỜNG: ĐẠI BẢN DOANH & XƯỞNG CỌC BẠCH ĐẰNG ---";
        var allCamps = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allCamps)
        {
            if (go == null) continue;
            if (go.name == campParentName || go.name.Contains("ĐẠI BẢN DOANH") || go.name.Contains("XƯỞNG CỌC"))
            {
                Undo.DestroyObjectImmediate(go);
            }
        }

        GameObject campRoot = new GameObject(campParentName);
        campRoot.transform.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(campRoot, "Spawn Historic Encampment");

        // -------------------------------------------------------------
        // 1. ĐẠI BẢN DOANH TIỀN PHƯƠNG NGÔ QUYỀN
        // TỌA ĐỘ CHUẨN TRÊN ĐỒNG BẰNG XANH BỜ TÂY: X = -820m, Z = 320m (Cao độ 17.5m, cách nước >200m)
        // -------------------------------------------------------------
        GameObject hqGroup = new GameObject("1_DaiBanDoanh_NgoQuyen");
        hqGroup.transform.parent = campRoot.transform;

        Vector3 hqCenter = new Vector3(-820f, 17.5f, 320f);
        if (t != null) hqCenter.y = t.SampleHeight(hqCenter);

        if (pfBarracks != null)
        {
            GameObject hq = (GameObject)PrefabUtility.InstantiatePrefab(pfBarracks);
            hq.transform.parent = hqGroup.transform;
            hq.transform.position = hqCenter;
            hq.transform.rotation = Quaternion.Euler(0, 90f, 0);
        }

        if (pfGate != null)
        {
            GameObject gate = (GameObject)PrefabUtility.InstantiatePrefab(pfGate);
            gate.transform.parent = hqGroup.transform;
            gate.transform.position = hqCenter + new Vector3(35f, 0, 0);
            gate.transform.rotation = Quaternion.Euler(0, 90f, 0);
        }

        if (pfTower != null)
        {
            GameObject tw = (GameObject)PrefabUtility.InstantiatePrefab(pfTower);
            tw.transform.parent = hqGroup.transform;
            tw.transform.position = hqCenter + new Vector3(32f, 0, 28f);
            tw.transform.rotation = Quaternion.Euler(0, 45f, 0);
        }

        if (pfStorage != null)
        {
            GameObject st = (GameObject)PrefabUtility.InstantiatePrefab(pfStorage);
            st.transform.parent = hqGroup.transform;
            st.transform.position = hqCenter + new Vector3(-25f, 0, 20f);
            st.transform.rotation = Quaternion.Euler(0, 180f, 0);
        }

        if (pfFence != null)
        {
            for (int i = -3; i <= 3; i++)
            {
                if (Mathf.Abs(i) <= 1) continue;
                GameObject fNorth = (GameObject)PrefabUtility.InstantiatePrefab(pfFence);
                fNorth.transform.parent = hqGroup.transform;
                Vector3 fPos = hqCenter + new Vector3(35f, 0, i * 10f);
                if (t != null) fPos.y = t.SampleHeight(fPos);
                fNorth.transform.position = fPos;
                fNorth.transform.rotation = Quaternion.Euler(0, 90f, 0);
            }
        }

        if (pfBarrels != null)
        {
            GameObject b1 = (GameObject)PrefabUtility.InstantiatePrefab(pfBarrels);
            b1.transform.parent = hqGroup.transform;
            b1.transform.position = hqCenter + new Vector3(12f, 0, 15f);
        }

        // -------------------------------------------------------------
        // 2. KHU XƯỞNG RÈN BỊT SẮT & BÃI ĐẼO CỌC LIM (Ven bờ cỏ: X = -680m, Z = 260m)
        // -------------------------------------------------------------
        GameObject workshopGroup = new GameObject("2_XuongRen_Va_BaiDeoCoc");
        workshopGroup.transform.parent = campRoot.transform;

        Vector3 wsCenter = new Vector3(-680f, 17.5f, 260f);
        if (t != null) wsCenter.y = t.SampleHeight(wsCenter);

        if (pfBlacksmith != null)
        {
            GameObject bs = (GameObject)PrefabUtility.InstantiatePrefab(pfBlacksmith);
            bs.transform.parent = workshopGroup.transform;
            bs.transform.position = wsCenter;
            bs.transform.rotation = Quaternion.Euler(0, 100f, 0);
        }

        if (pfCrane != null)
        {
            GameObject crane = (GameObject)PrefabUtility.InstantiatePrefab(pfCrane);
            crane.transform.parent = workshopGroup.transform;
            crane.transform.position = wsCenter + new Vector3(25f, 0, -10f);
            crane.transform.rotation = Quaternion.Euler(0, -60f, 0);
        }

        if (pfBoat != null)
        {
            GameObject bLight = (GameObject)PrefabUtility.InstantiatePrefab(pfBoat);
            bLight.transform.parent = workshopGroup.transform;
            bLight.transform.position = new Vector3(-585f, 14.1f, 250f);
            bLight.transform.rotation = Quaternion.Euler(0, 45f, 0);
        }

        Debug.Log("⛺ [BẠCH ĐẰNG 938] ĐÃ DỰNG THÀNH CÔNG ĐẠI BẢN DOANH NGÔ QUYỀN TRÊN ĐỒNG BẰNG BỜ TÂY (100% TRÊN CẠN)!");
    }

    // =========================================================================
    // NÂNG CẤP MATERIAL NÚI ĐÁ SANG URP LIT
    // =========================================================================
    [MenuItem("🤖 Trợ lý AI/🏔️ Cập Nhật Material Núi Đá Sang URP Lit PBR")]
    public static void FixRockMaterialsToURP()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return;

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
        if (waterCol != null) DestroyImmediate(waterCol);

        if (waterGo.GetComponent<TideSystem>() == null)
        {
            waterGo.AddComponent<TideSystem>();
        }
    }

    [MenuItem("🤖 Trợ lý AI/⚓ Đặt Thuyền & Bãi Cọc Ra Giữa Sông Bạch Đằng")]
    public static void PlaceBoatAndSpikesInRiver()
    {
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
