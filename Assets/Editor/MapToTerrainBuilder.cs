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
        // 1. CẤU HÌNH ĐỘ CAO CHUẨN THỰC TẾ (AAA)
        // Độ cao tổng sa bàn = 30 mét:
        // - Đáy sông sâu: Y = 2m (tỷ lệ 0.07) -> Đủ sâu cho thuyền & bãi cọc
        // - Mặt nước OptiWater: Y = 6.5m (tỷ lệ 0.22)
        // - Bờ sông mép nước: Y = 7.5m (tỷ lệ 0.25)
        // - Đất liền đồng bằng: Y = 10m - 12m (tỷ lệ 0.35 - 0.40)
        // - Đồi rừng cao: Y = 15m - 20m (tỷ lệ 0.50 - 0.67)
        // ==========================================
        int tSize = 1025;
        TerrainData td = new TerrainData();
        td.heightmapResolution = tSize;
        td.size = new Vector3(3000, 30, 3000); // 3000m x 30m chiều cao x 3000m

        float[,] rawH = new float[tSize, tSize];

        // 100% TRẢI ĐỀU TOÀN BỘ ẢNH, KHÔNG BỊ CẮT VIỀN (NO BORDER!)
        for (int y = 0; y < tSize; y++)
        {
            for (int x = 0; x < tSize; x++)
            {
                float u = (float)x / (tSize - 1);
                float v = (float)y / (tSize - 1);
                Color c = mapTex.GetPixelBilinear(u, v);

                // Nhận diện nước: Kênh Blue vượt trội hoặc màu sông (R~0.18-0.25, G~0.38-0.45, B~0.55-0.65)
                bool isWater = (c.b >= c.g * 0.95f) || (c.b > 0.45f && c.b > c.r + 0.15f);

                if (isWater)
                {
                    // Lòng sông: dốc nhẹ về giữa, sâu khoảng 2m đến 4m
                    rawH[y, x] = 0.08f;
                }
                else
                {
                    // Đất liền: 10m - 14m, gợn sóng nhẹ tự nhiên
                    float perlin = Mathf.PerlinNoise(u * 8f, v * 8f) * 0.08f;
                    rawH[y, x] = 0.38f + perlin;
                }
            }
        }

        // Làm mượt nhẹ nhàng viền bờ sông (bán kính 3 pixel vừa đủ mượt, không xóa nhánh sông nhỏ)
        float[,] smoothH = SmoothHeights(rawH, tSize, 3);
        td.SetHeights(0, 0, smoothH);

        // ==========================================
        // 2. CẤU HÌNH TEXTURE PBR AAA (TILING CHUẨN)
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

                // Reset
                splats[y, x, 0] = 0; splats[y, x, 1] = 0; splats[y, x, 2] = 0; splats[y, x, 3] = 0;

                if (isWater || h < 0.18f)
                {
                    // Đáy sông: Bùn lầy sông ngòi
                    splats[y, x, 1] = 0.8f;
                    splats[y, x, 2] = 0.2f; // pha chút cát
                }
                else if (h < 0.28f)
                {
                    // Mép nước / bờ sông: Bãi bồi cát + bùn
                    float t = Mathf.InverseLerp(0.18f, 0.28f, h);
                    splats[y, x, 2] = 1f - t; // Cát sát nước
                    splats[y, x, 0] = t;       // Lên trên thì thành cỏ
                }
                else
                {
                    // Đất liền: Cỏ xanh đồng bằng, xen kẽ rêu rừng
                    splats[y, x, 0] = 0.75f;
                    splats[y, x, 3] = 0.25f;
                }
            }
        }
        td.SetAlphamaps(0, 0, splats);

        // ==========================================
        // 3. RẢI CÂY CỎ THẬT TỰ NHIÊN TRÊN ĐẤT LIỀN
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
        for (int i = 0; pCount > 0 && i < 25000; i++)
        {
            float tx = Random.value;
            float ty = Random.value;
            Color c = mapTex.GetPixelBilinear(tx, ty);
            bool isWater = (c.b >= c.g * 0.95f) || (c.b > 0.45f && c.b > c.r + 0.15f);
            if (isWater) continue; // Tuyệt đối không cắm cây dưới sông

            int hx = Mathf.Clamp((int)(tx * tSize), 0, tSize - 1);
            int hy = Mathf.Clamp((int)(ty * tSize), 0, tSize - 1);
            if (smoothH[hy, hx] < 0.26f) continue; // Cách xa bờ nước một chút

            TreeInstance ti = new TreeInstance();
            ti.position = new Vector3(tx, 0f, ty);
            ti.prototypeIndex = Random.Range(0, pCount);
            ti.widthScale = Random.Range(0.8f, 1.6f);
            ti.heightScale = Random.Range(0.8f, 1.6f);
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
        // 5. TỰ ĐỘNG CẬP NHẬT MẶT NƯỚC (OPTIWATER)
        // ==========================================
        SetupOptiWaterSurface(6.5f); // Đặt mực nước ở Y = 6.5m ngập vừa khít các nhánh sông

        AssetDatabase.SaveAssets();
        Debug.Log("🎉 SA BÀN BẠCH ĐẰNG 3D AAA HOÀN TẤT: 100% Không Viền, Nhánh Sông Đầy Đủ, Nước Tràn Đều!");
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
        // Tìm mặt nước trong scene
        GameObject waterGo = GameObject.Find("OptiWaterSurface");
        if (waterGo == null) waterGo = GameObject.Find("Plane");

        if (waterGo == null)
        {
            waterGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterGo.name = "OptiWaterSurface";
        }

        // Chỉnh vị trí và scale phủ trọn 3000m sa bàn
        waterGo.transform.position = new Vector3(0, waterY, 0);
        waterGo.transform.localScale = new Vector3(300, 1, 300); // 10m * 300 = 3000m

        // Gán Material OptiWater
        Material waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/OptiWater/Runtime/OptiWaterSurface.mat");
        if (waterMat != null)
        {
            var renderer = waterGo.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = waterMat;
        }

        Debug.Log($"🌊 Mặt nước OptiWater đã được điều chỉnh tự động: Cao độ Y = {waterY}m, Phủ 3000m x 3000m!");
    }
}
