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
        // Tạo dải bãi cát thoai thoải phẳng lì rộng ~50m bao quanh mọi hòn đảo và bờ sông
        // (Y chang bãi biển nhiệt đới: sóng dập dềnh trên bãi cát phẳng, đồi dốc lùi sâu vào trong)
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

        // Tính khoảng cách (pixel) từ mỗi điểm đất liền tới mép nước gần nhất
        float[,] distToWater = ComputeDistanceTransform(isWaterMap, tSize);

        // ==========================================
        // 2. KHỞI TẠO ĐỘ CAO THEO TỪNG TẦNG ĐỊA HÌNH
        // - Lòng sông: Y ~ 4.8m - 6.0m
        // - Bãi cát bồi thoai thoải (Rộng 50 mét): Y dốc cực nhẹ từ 9.5m lên 16.5m (dốc ~ 7 độ)
        // - Đất liền & đồi núi: Lùi sâu sau bãi cát, cao 25m - 32m
        // ==========================================
        float[,] rawH = new float[tSize, tSize];
        float beachPixelWidth = 18f; // ~53 mét bãi cát thoai thoải phẳng lì

        for (int y = 0; y < tSize; y++)
        {
            for (int x = 0; x < tSize; x++)
            {
                float u = (float)x / (tSize - 1);
                float v = (float)y / (tSize - 1);

                if (isWaterMap[y, x])
                {
                    // Lòng sông sâu
                    rawH[y, x] = 0.08f;
                }
                else
                {
                    float d = distToWater[y, x];
                    if (d <= beachPixelWidth)
                    {
                        // BÃI BIỂN THOAI THOẢI (NHƯ HÌNH 1):
                        // Dốc cực kỳ êm từ 9.5m (dưới mực triều rút 10m) lên 16.5m (trên mực triều dâng 15.5m)
                        float t = d / beachPixelWidth;
                        rawH[y, x] = Mathf.Lerp(0.158f, 0.275f, Mathf.Pow(t, 1.2f));
                    }
                    else
                    {
                        // ĐẤT LIỀN & ĐỒI NÚI: Nằm sâu bên trong bãi cát
                        float inlandT = Mathf.Clamp01((d - beachPixelWidth) / 12f);
                        float perlin = Mathf.PerlinNoise(u * 8f, v * 8f) * 0.10f;
                        rawH[y, x] = Mathf.Lerp(0.275f, 0.42f + perlin, Mathf.SmoothStep(0f, 1f, inlandT));
                    }
                }
            }
        }

        // Làm mượt nhẹ nhàng
        float[,] smoothH = SmoothHeights(rawH, tSize, 3);
        td.SetHeights(0, 0, smoothH);

        // ==========================================
        // 3. CẤU HÌNH TEXTURE PBR AAA (PHỦ 100% CÁT VÀNG TRÊN DẢI THOẠI)
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

        td.terrainLayers = new TerrainLayer[] { layerGrass, layerMud, layerSand, layerMoss };

        // Sơn texture: Bãi cát trải rộng toàn bộ 50 mét bờ biển
        int aRes = td.alphamapResolution;
        float[,,] splats = new float[aRes, aRes, 4];

        for (int y = 0; y < aRes; y++)
        {
            for (int x = 0; x < aRes; x++)
            {
                int hy = Mathf.Clamp((int)((float)y / aRes * tSize), 0, tSize - 1);
                int hx = Mathf.Clamp((int)((float)x / aRes * tSize), 0, tSize - 1);

                bool isWater = isWaterMap[hy, hx];
                float d = distToWater[hy, hx];

                splats[y, x, 0] = 0; splats[y, x, 1] = 0; splats[y, x, 2] = 0; splats[y, x, 3] = 0;

                if (isWater)
                {
                    // Lòng sông sâu: Bùn lầy
                    splats[y, x, 1] = 0.85f;
                    splats[y, x, 2] = 0.15f;
                }
                else if (d <= beachPixelWidth)
                {
                    // 100% CÁT VÀNG MỊN THOAI THOẢI (RỘNG 50M NHƯ HÌNH 1)
                    splats[y, x, 2] = 1.0f;
                }
                else if (d <= beachPixelWidth + 6f)
                {
                    // Vùng chuyển tiếp sau bãi cát: Cát pha cỏ xanh
                    float t = (d - beachPixelWidth) / 6f;
                    splats[y, x, 2] = 1f - t;
                    splats[y, x, 0] = t;
                }
                else
                {
                    // Đồi núi & đất liền trên cao: Cỏ xanh mát mắt pha rêu rừng
                    splats[y, x, 0] = 0.75f;
                    splats[y, x, 3] = 0.25f;
                }
            }
        }
        td.SetAlphamaps(0, 0, splats);

        // ==========================================
        // 4. RẢI CÂY CỎ (CHỈ TRỒNG SAU BÃI CÁT)
        // Bãi cát được giữ sạch bóng 100% cho sóng biển vỗ vào
        // ==========================================
        string prefabFolder = "Assets/TerrainSampleAssets/Prefabs/";
        var protos = new List<TreePrototype>();
        string[] vegList = { "Fern_A", "Fern_B", "Bush_A", "Bush_B", "Plant_A", "Plant_B", "Grass_A" };
        foreach (var v in vegList)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFolder + v + ".prefab");
            if (p != null) protos.Add(new TreePrototype { prefab = p });
        }
        td.treePrototypes = protos.ToArray();

        var trees = new List<TreeInstance>();
        int pCount = protos.Count;
        for (int i = 0; pCount > 0 && i < 28000; i++)
        {
            float tx = Random.value;
            float ty = Random.value;

            int hx = Mathf.Clamp((int)(tx * tSize), 0, tSize - 1);
            int hy = Mathf.Clamp((int)(ty * tSize), 0, tSize - 1);

            if (isWaterMap[hy, hx]) continue;
            // Chỉ trồng cây lùi sâu sau bãi cát (> 50 mét tính từ mép nước)
            if (distToWater[hy, hx] < beachPixelWidth + 4f) continue;

            TreeInstance ti = new TreeInstance();
            ti.position = new Vector3(tx, 0f, ty);
            ti.prototypeIndex = Random.Range(0, pCount);
            ti.widthScale = Random.Range(0.9f, 1.8f);
            ti.heightScale = Random.Range(0.9f, 1.8f);
            ti.color = ti.lightmapColor = Color.white;
            trees.Add(ti);
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
        tComp.treeBillboardDistance = 600f;
        tComp.heightmapPixelError = 3f;

        // ==========================================
        // 6. NÂNG CAO MẶT NƯỚC (OPTIWATER) LÊN Y = 14M
        // ==========================================
        float targetWaterY = 14.0f;
        SetupOptiWaterSurface(targetWaterY);

        // Bố trí thuyền và cọc
        PlaceBoatAndSpikesInRiver();

        AssetDatabase.SaveAssets();
        Debug.Log($"🎉 ĐÃ XÂY XONG SA BÀN: Bờ biển thoai thoải 50 mét như bãi biển nhiệt đới, sóng vỗ dạt cát cực đẹp!");
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

        // XÓA BỎ HOÀN TOÀN Collider trên mặt nước (Nước là chất lỏng, không để Collider cứng hất văng thuyền)
        Collider waterCol = waterGo.GetComponent<Collider>();
        if (waterCol != null)
        {
            DestroyImmediate(waterCol);
        }

        // Tự động gắn hệ thống Thủy Triều vào mặt nước
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
