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
        // 1. CẤU HÌNH ĐỘ CAO HÙNG VĨ (NÂNG CAO CẢ ĐẤT LẪN NƯỚC)
        // Chiều cao tổng sa bàn: 60 mét
        // - Đáy sông sâu: Y = 4.8m (tỷ lệ 0.08)
        // - Mặt nước OptiWater: Y = 14m (nước sâu ~9m, thuyền bè bơi lội bao đã)
        // - Bãi cát ven sông: Y = 13m - 18m
        // - Đất liền & đồi núi: Y = 25m - 32m (cao hơn mặt nước 11m - 18m, nhìn cực kỳ hoành tráng)
        // ==========================================
        int tSize = 1025;
        TerrainData td = new TerrainData();
        td.heightmapResolution = tSize;
        td.size = new Vector3(3000, 60, 3000); // Tăng chiều cao lên 60m

        float[,] rawH = new float[tSize, tSize];

        for (int y = 0; y < tSize; y++)
        {
            for (int x = 0; x < tSize; x++)
            {
                float u = (float)x / (tSize - 1);
                float v = (float)y / (tSize - 1);
                Color c = mapTex.GetPixelBilinear(u, v);

                // Nhận diện nước: Kênh Blue vượt trội
                bool isWater = (c.b >= c.g * 0.95f) || (c.b > 0.45f && c.b > c.r + 0.15f);

                if (isWater)
                {
                    // Lòng sông sâu: Y ~ 4.8m
                    rawH[y, x] = 0.08f;
                }
                else
                {
                    // Đất liền cao ráo: Y ~ 25m - 32m (tỷ lệ 0.42 + gợn sóng đồi núi tự nhiên)
                    float perlin = Mathf.PerlinNoise(u * 8f, v * 8f) * 0.10f;
                    rawH[y, x] = 0.42f + perlin;
                }
            }
        }

        // Làm mượt bờ sông theo 2 lớp:
        // Lớp 1: Bán kính rộng (5 pixel) tạo dải bờ thoai thoải
        // Lớp 2: Giữ vách đồi bên trong cao ráo
        float[,] smoothH = SmoothHeights(rawH, tSize, 5);
        td.SetHeights(0, 0, smoothH);

        // ==========================================
        // 2. CẤU HÌNH TEXTURE PBR AAA (BỜ CÁT RỘNG RÃI)
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

        // Sơn texture lên từng vùng: Bãi cát vàng mịn trải rộng theo mép nước
        int aRes = td.alphamapResolution;
        float[,,] splats = new float[aRes, aRes, 4];

        for (int y = 0; y < aRes; y++)
        {
            for (int x = 0; x < aRes; x++)
            {
                float u = (float)x / aRes;
                float v = (float)y / aRes;
                Color c = mapTex.GetPixelBilinear(u, v);

                int hy = Mathf.Clamp((int)(v * tSize), 0, tSize - 1);
                int hx = Mathf.Clamp((int)(u * tSize), 0, tSize - 1);
                float h = smoothH[hy, hx];

                bool isWater = (c.b >= c.g * 0.95f) || (c.b > 0.45f && c.b > c.r + 0.15f);

                splats[y, x, 0] = 0; splats[y, x, 1] = 0; splats[y, x, 2] = 0; splats[y, x, 3] = 0;

                if (isWater || h < 0.17f)
                {
                    // Lòng sông sâu: Bùn lầy sông
                    splats[y, x, 1] = 0.85f;
                    splats[y, x, 2] = 0.15f;
                }
                else if (h < 0.28f)
                {
                    // BÃI CÁT VÀNG MỊN THOAI THOẢI (Cao độ 10m - 17m quanh mép nước 14m)
                    // Cho sóng biển và thủy triều dập dềnh lên bãi cát cực đẹp
                    splats[y, x, 2] = 1.0f; // 100% Cát vàng
                }
                else if (h < 0.35f)
                {
                    // Vùng chuyển tiếp: Cát pha cỏ xanh
                    float t = Mathf.InverseLerp(0.28f, 0.35f, h);
                    splats[y, x, 2] = 1f - t; // Cát
                    splats[y, x, 0] = t;      // Cỏ
                }
                else
                {
                    // Đất liền & đồi núi trên cao: Cỏ xanh mát mắt pha rêu rừng
                    splats[y, x, 0] = 0.75f;
                    splats[y, x, 3] = 0.25f;
                }
            }
        }
        td.SetAlphamaps(0, 0, splats);

        // ==========================================
        // 3. RẢI CÂY CỎ TỰ NHIÊN
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
            Color c = mapTex.GetPixelBilinear(tx, ty);
            bool isWater = (c.b >= c.g * 0.95f) || (c.b > 0.45f && c.b > c.r + 0.15f);
            if (isWater) continue;

            int hx = Mathf.Clamp((int)(tx * tSize), 0, tSize - 1);
            int hy = Mathf.Clamp((int)(ty * tSize), 0, tSize - 1);
            if (smoothH[hy, hx] < 0.30f) continue; // Chỉ cắm cây trên phần đất cao, không cắm ở mép nước

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
        // 4. TẠO HOẶC CẬP NHẬT TERRAIN TRONG SCENE
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
        // 5. NÂNG CAO MẶT NƯỚC (OPTIWATER) LÊN Y = 14M
        // ==========================================
        float targetWaterY = 14.0f;
        SetupOptiWaterSurface(targetWaterY);

        // Nâng thuyền và cọc (nếu có trong scene) theo mặt nước mới
        AdjustBoatAndSpikes(targetWaterY);

        AssetDatabase.SaveAssets();
        Debug.Log($"🎉 ĐÃ XÂY XONG SA BÀN: Đất nâng lên 25m - 32m, Nước nâng lên {targetWaterY}m, Phủ kín 3000m!");
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

    private static void AdjustBoatAndSpikes(float waterY)
    {
        // Gọi hàm bố trí thuyền và bãi cọc
        PlaceBoatAndSpikesInRiver();
    }

    [MenuItem("🤖 Trợ lý AI/⚓ Đặt Thuyền & Bãi Cọc Ra Giữa Sông Bạch Đằng")]
    public static void PlaceBoatAndSpikesInRiver()
    {
        float waterY = 14.0f;

        // 1. TỌA ĐỘ VÀNG GIỮA LÒNG SÔNG BẠCH ĐẰNG (NƯỚC SÂU):
        // X = -200, Y = 14.2 (nổi bập bềnh trên mặt nước), Z = 150
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
            boatGo.transform.rotation = Quaternion.Euler(0, 0, 0); // Thuyền hướng mũi về bãi cọc (+X)
            Debug.Log($"⚓ Đã đưa Thuyền Nam Hán ra giữa dòng sông Bạch Đằng tại: {boatPos}");
        }

        // 2. BỐ TRÍ HÀNG RÀO CỌC GỖ NGAY TRƯỚC MŨI THUYỀN:
        // Đặt ở X = -140 (cách thuyền 60m), Y = 13.5 (đầu cọc nhọn nhô sát mặt nước 14m)
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
                spikes[i].transform.rotation = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0, 360), Random.Range(-10f, -25f)); // Cọc cắm hơi nghiêng đón thuyền
            }
            Debug.Log($"🪵 Đã giăng bãi cọc gỗ ({spikes.Count} cọc) đón đầu thuyền tại X = -140!");
        }

        // 3. DI CHUYỂN CAMERA LẠI GẦN ĐỂ XEM ĐƯỢC NGAY
        Camera cam = Camera.main;
        if (cam != null)
        {
            Undo.RecordObject(cam.transform, "Move Camera To Battle");
            cam.transform.position = new Vector3(-240f, 22f, 150f);
            cam.transform.LookAt(new Vector3(-170f, 14f, 150f));
        }

        // 4. FOCUS SCENE VIEW VÀO VỊ TRÍ CHIẾN TRƯỜNG
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.pivot = new Vector3(-170f, 15f, 150f);
            SceneView.lastActiveSceneView.size = 60f;
            SceneView.lastActiveSceneView.Repaint();
        }
    }
}
