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
        // 2. KHỞI TẠO ĐỊA HÌNH 3 TẦNG CHUẨN LỊCH SỬ BẠCH ĐẰNG 938:
        // - Tầng 1: Lòng sông sâu (4.8m - 6m)
        // - Tầng 2: Bờ cát thoai thoải 50 mét (9.5m - 16.5m, dốc phẳng ~7 độ như biển nhiệt đới)
        // - Tầng 3: Đồi rừng & Dãy núi đá vôi Karst Tràng Kênh (cao 35m - 50m)
        // ==========================================
        float[,] rawH = new float[tSize, tSize];
        float beachPixelWidth = 18f; // ~53 mét bãi cát thoai thoải

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
                        // BÃI CÁT PHẲNG THOAI THOẢI (Dốc cực nhẹ đón sóng dập dềnh)
                        float t = d / beachPixelWidth;
                        rawH[y, x] = Mathf.Lerp(0.158f, 0.275f, Mathf.Pow(t, 1.2f));
                    }
                    else
                    {
                        // VÙNG ĐẤT LIỀN, ĐỒI RỪNG & DÃY NÚI ĐÁ VÔI TRÀNG KÊNH:
                        float inlandDist = d - beachPixelWidth;
                        float inlandT = Mathf.Clamp01(inlandDist / 14f);

                        // Bình nguyên & gò đồi thoải lùi sau bãi cát
                        float hillBase = Mathf.Lerp(0.275f, 0.38f, inlandT);

                        // Dãy núi đá vôi Karst đồ sộ khi lùi sâu vào đất liền (d > 24 pixel ~ 70m)
                        float mountainBonus = 0f;
                        if (d > 24f)
                        {
                            float mFactor = Mathf.Clamp01((d - 24f) / 18f);
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
        // 3. CẤU HÌNH TEXTURE PBR AAA (6 LỚP VẬT LIỆU CHÂN THỰC)
        // 0: Cỏ xanh | 1: Bùn lòng sông | 2: Cát vàng biển | 3: Rêu rừng | 4: Đá vôi Tràng Kênh | 5: Sỏi đất chân núi
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
                    // Lòng sông sâu: Bùn lầy sông ngòi
                    splats[y, x, 1] = 0.85f;
                    splats[y, x, 2] = 0.15f;
                }
                else if (d <= beachPixelWidth)
                {
                    // BÃI CÁT VÀNG MỊN THOAI THOẢI 50 MÉT
                    splats[y, x, 2] = 1.0f;
                }
                else if (slope > 0.38f)
                {
                    // VÁCH NÚI ĐÁ VÔI TRÀNG KÊNH DỰNG ĐỨNG
                    splats[y, x, 4] = 0.80f; // Đá vôi xám
                    splats[y, x, 3] = 0.20f; // Rêu phong
                }
                else if (slope > 0.24f)
                {
                    // CHÂN NÚI SỎI ĐÁ
                    splats[y, x, 5] = 0.65f; // Đất sỏi
                    splats[y, x, 0] = 0.35f; // Cỏ
                }
                else if (d <= beachPixelWidth + 6f)
                {
                    // Chuyển tiếp cát pha cỏ
                    float t = (d - beachPixelWidth) / 6f;
                    splats[y, x, 2] = 1f - t;
                    splats[y, x, 0] = t;
                }
                else
                {
                    // Đồng cỏ xanh & rừng nguyên sinh
                    splats[y, x, 0] = 0.70f;
                    splats[y, x, 3] = 0.30f;
                }
            }
        }
        td.SetAlphamaps(0, 0, splats);

        // ==========================================
        // 4. BỐ TRÍ THỰC VẬT ĐA DẠNG:
        // - Lau sậy & cỏ hoa ven mép bãi cát
        // - Rừng dương xỉ & cây bụi rậm rạp trên đồi gò
        // ==========================================
        string prefabFolder = "Assets/TerrainSampleAssets/Prefabs/";
        var protos = new List<TreePrototype>();

        // Danh mục thực vật lịch sử: Dương xỉ, cây bụi, lau sậy khô, hoa dại
        string[] vegList = { 
            "Fern_A", "Fern_B", "Fern_C", 
            "Bush_A", "Bush_B", "BushDry_A", 
            "Grass_A", "Grass_C", "GrassDry_A",
            "Plant_A", "Plant_B", "Heather_A" 
        };

        foreach (var v in vegList)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFolder + v + ".prefab");
            if (p != null) protos.Add(new TreePrototype { prefab = p });
        }
        td.treePrototypes = protos.ToArray();

        var trees = new List<TreeInstance>();
        int pCount = protos.Count;

        for (int i = 0; pCount > 0 && i < 35000; i++)
        {
            float tx = Random.value;
            float ty = Random.value;

            int hx = Mathf.Clamp((int)(tx * tSize), 0, tSize - 1);
            int hy = Mathf.Clamp((int)(ty * tSize), 0, tSize - 1);

            if (isWaterMap[hy, hx]) continue;
            float d = distToWater[hy, hx];
            float slope = GetSlope(smoothH, hx, hy, tSize);

            // Tuyệt đối không mọc trên vách đá đứng (> 0.40)
            if (slope > 0.40f) continue;

            // VÙNG 1: Mép trên bãi cát (d từ 12 đến 18) -> Rải cụm lau sậy, cỏ lác, hoa dại
            if (d >= 12f && d <= beachPixelWidth)
            {
                if (Random.value < 0.25f) // Rải thưa thớt tự nhiên
                {
                    TreeInstance tiShore = new TreeInstance();
                    tiShore.position = new Vector3(tx, 0f, ty);
                    // Chọn cỏ dại, lau sậy hoặc hoa dại
                    tiShore.prototypeIndex = Random.Range(5, pCount);
                    tiShore.widthScale = Random.Range(0.7f, 1.3f);
                    tiShore.heightScale = Random.Range(0.7f, 1.3f);
                    tiShore.color = tiShore.lightmapColor = Color.white;
                    trees.Add(tiShore);
                }
                continue;
            }

            // VÙNG 2: Rừng rậm nguyên sinh phía trong (d > beachPixelWidth)
            if (d > beachPixelWidth + 3f)
            {
                TreeInstance tiInland = new TreeInstance();
                tiInland.position = new Vector3(tx, 0f, ty);
                tiInland.prototypeIndex = Random.Range(0, pCount);
                tiInland.widthScale = Random.Range(0.9f, 2.0f);
                tiInland.heightScale = Random.Range(0.9f, 2.2f);
                tiInland.color = tiInland.lightmapColor = Color.white;
                trees.Add(tiInland);
            }
        }
        td.SetTreeInstances(trees.ToArray(), true);

        // ==========================================
        // 5. TẠO HOẶC CẬP NHẬT TERRAIN TRONG SCENE
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
        tComp.treeBillboardDistance = 650f;
        tComp.heightmapPixelError = 3f;

        // ==========================================
        // 6. CẬP NHẬT MẶT NƯỚC & BỐ TRÍ CHIẾN TRẬN
        // ==========================================
        float targetWaterY = 14.0f;
        SetupOptiWaterSurface(targetWaterY);
        PlaceBoatAndSpikesInRiver();

        AssetDatabase.SaveAssets();
        Debug.Log($"🎉 SA BÀN BẠCH ĐẰNG 938 HOÀN THIỆN: Núi đá vôi Tràng Kênh sừng sững, bãi cát thoai thoải 50m, lau sậy & rừng nguyên sinh rậm rạp!");
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
