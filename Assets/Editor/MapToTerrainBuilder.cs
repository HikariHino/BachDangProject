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

        // Làm mượt viền bờ sông nhẹ nhàng
        float[,] smoothH = SmoothHeights(rawH, tSize, 3);
        td.SetHeights(0, 0, smoothH);

        // ==========================================
        // 2. CẤU HÌNH TEXTURE PBR AAA
        // ==========================================
        string texFolder = "Assets/TerrainSampleAssets/Textures/Terrain/";

        var layerGrass = CreateOrGetLayer("Assets/TL_Grass_A.terrainlayer", 
            texFolder + "Grass_A_BaseColor.tif", texFolder + "Grass_A_Normal.tif", texFolder + "Grass_A_MaskMap.tif", new Vector2(15, 15));
        
        var layerMud = CreateOrGetLayer("Assets/TL_Muddy.terrainlayer", 
            texFolder + "Muddy_BaseColor.tif", texFolder + "Muddy_Normal.tif", texFolder + "Muddy_MaskMap.tif", new Vector2(12, 12));

        var layerSand = CreateOrGetLayer("Assets/TL_Sand.terrainlayer", 
            texFolder + "Sand_BaseColor.tif", texFolder + "Sand_Normal.tif", texFolder + "Sand_MaskMap.tif", new Vector2(10, 10));

        var layerMoss = CreateOrGetLayer("Assets/TL_Grass_Moss.terrainlayer", 
            texFolder + "Grass_Moss_BaseColor.tif", texFolder + "Grass_Moss_Normal.tif", texFolder + "Grass_Moss_MaskMap.tif", new Vector2(16, 16));

        td.terrainLayers = new TerrainLayer[] { layerGrass, layerMud, layerSand, layerMoss };

        // Sơn texture lên từng vùng
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

                if (isWater || h < 0.20f)
                {
                    // Lòng sông chìm dưới nước: Bùn lầy sông ngòi
                    splats[y, x, 1] = 0.8f;
                    splats[y, x, 2] = 0.2f;
                }
                else if (h < 0.32f)
                {
                    // Mép nước lên bờ: Bãi cát bồi
                    float t = Mathf.InverseLerp(0.20f, 0.32f, h);
                    splats[y, x, 2] = 1f - t;
                    splats[y, x, 0] = t;
                }
                else
                {
                    // Đất liền trên cao: Cỏ xanh mướt pha rêu rừng
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
    }

    private static void AdjustBoatAndSpikes(float waterY)
    {
        // Tự động nâng thuyền lên ngang mặt nước nếu đang bị chìm bên dưới
        var boats = Object.FindObjectsByType<BoatCrash>(FindObjectsSortMode.None);
        foreach (var b in boats)
        {
            Vector3 pos = b.transform.position;
            if (pos.y < waterY)
            {
                pos.y = waterY + 0.2f;
                b.transform.position = pos;
            }
        }

        // Tự động nâng các cọc gỗ
        var allGos = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allGos)
        {
            if (go.name.ToLower().Contains("spike") || go.name.ToLower().Contains("wood_spike"))
            {
                Vector3 p = go.transform.position;
                if (p.y < waterY - 5f)
                {
                    p.y = waterY - 1.5f; // Đầu cọc nhú sát mặt nước
                    go.transform.position = p;
                }
            }
        }
    }
}
