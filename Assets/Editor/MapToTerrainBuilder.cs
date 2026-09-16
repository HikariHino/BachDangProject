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
        float beachPixelWidth = 18f; // ~53 mét bãi cát thoai thoải phẳng lì

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
                        // VÙNG ĐẤT LIỀN & NÚI ĐÁ VÔI TRÀNG KÊNH
                        float inlandDist = d - beachPixelWidth;
                        float inlandT = Mathf.Clamp01(inlandDist / 14f);
                        float hillBase = Mathf.Lerp(0.275f, 0.38f, inlandT);

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
                    // 100% Cát vàng bãi biển
                    splats[y, x, 2] = 1.0f;
                }
                else if (slope > 0.38f)
                {
                    // Vách đá vôi xám Tràng Kênh
                    splats[y, x, 4] = 0.80f;
                    splats[y, x, 3] = 0.20f;
                }
                else if (slope > 0.24f)
                {
                    // Chân núi sỏi đá
                    splats[y, x, 5] = 0.65f;
                    splats[y, x, 0] = 0.35f;
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
                    // Đồng cỏ xanh & rêu rừng
                    splats[y, x, 0] = 0.70f;
                    splats[y, x, 3] = 0.30f;
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

            if (slope > 0.40f) continue; // Không mọc trên vách đá dựng đứng
            float normH = smoothH[hy, hx];

            // VÙNG 1: Mép trên bãi cát (d từ 12 đến 18) -> Cỏ lau sậy, hoa dại
            if (d >= 12f && d <= beachPixelWidth)
            {
                if (Random.value < 0.35f)
                {
                    TreeInstance tiShore = new TreeInstance();
                    tiShore.position = new Vector3(tx, normH, ty);
                    tiShore.prototypeIndex = Random.Range(6, pCount);
                    tiShore.widthScale = Random.Range(1.8f, 3.2f);
                    tiShore.heightScale = Random.Range(1.8f, 3.5f);
                    tiShore.color = tiShore.lightmapColor = Color.white;
                    trees.Add(tiShore);
                }
                continue;
            }

            // VÙNG 2: Rừng rậm nguyên sinh & Cây đại thụ trên đồi (d > beachPixelWidth)
            if (d > beachPixelWidth + 1.5f)
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
        // 5. THẢM CỎ 3D DÀY ĐẶC (DETAIL PROTOTYPES)
        // Phủ bạt ngàn cỏ 3D rậm rạp đung đưa theo gió
        // ==========================================
        int dRes = 512;
        td.SetDetailResolution(dRes, 16);

        var dProtos = new List<DetailPrototype>();
        string[] grassPrefabs = { "Grass_A", "Grass_B", "Fern_A", "Heather_A" };
        foreach (var g in grassPrefabs)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFolder + g + ".prefab");
            if (p != null)
            {
                DetailPrototype dp = new DetailPrototype();
                dp.prototype = p;
                dp.usePrototypeMesh = true;
                dp.renderMode = DetailRenderMode.VertexLit;
                dp.minWidth = 1.4f; dp.maxWidth = 2.4f;
                dp.minHeight = 1.4f; dp.maxHeight = 2.5f;
                dProtos.Add(dp);
            }
        }
        td.detailPrototypes = dProtos.ToArray();

        if (dProtos.Count > 0)
        {
            for (int layerIdx = 0; layerIdx < Mathf.Min(2, dProtos.Count); layerIdx++)
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

                        // Cỏ 3D mọc dày đặc từ sau bãi cát
                        if (d > 14f)
                        {
                            grassMap[y, x] = Random.Range(5, 12); // Rậm rạp cỏ 3D!
                        }
                        else
                        {
                            grassMap[y, x] = 0;
                        }
                    }
                }
                td.SetDetailLayer(0, 0, layerIdx, grassMap);
            }
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
        tComp.treeBillboardDistance = 1800f;
        tComp.treeCrossFadeLength = 50f;
        tComp.treeMaximumFullLODCount = 4000;
        tComp.detailObjectDistance = 350f; // Bán kính nhìn thấy thảm cỏ 3D dày đặc 350 mét!
        tComp.detailObjectDensity = 1.0f;
        tComp.heightmapPixelError = 3f;

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
        Debug.Log("✅ Đã tạo mới hoàn toàn 2 Prefab Cây Cổ Thụ khổng lồ (AncientTree_A & B)!");
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

        // Lấy primitive cylinder mesh làm thân & cành cây
        GameObject tempCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Mesh cylMesh = tempCyl.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tempCyl);

        // Lấy mesh & material tán lá từ Bush_A
        GameObject bushPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TerrainSampleAssets/Prefabs/Bush_A.prefab");
        if (bushPrefab == null) bushPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TerrainSampleAssets/Prefabs/Bush_B.prefab");
        Mesh bushMesh = bushPrefab != null ? bushPrefab.GetComponent<MeshFilter>().sharedMesh : cylMesh;
        Material bushMat = bushPrefab != null ? bushPrefab.GetComponent<MeshRenderer>().sharedMaterial : null;

        // Tạo Material vỏ cây gỗ sần sùi với Diffuse & Normal Map thực thụ
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

        List<CombineInstance> woodParts = new List<CombineInstance>();
        List<CombineInstance> leafParts = new List<CombineInstance>();

        if (!isBanyan)
        {
            // ==========================================
            // ANCIENT TREE A: CÂY GỖ CỔ THỤ THẲNG VƯƠN CAO 18M
            // ==========================================
            float trunkH = 14f;
            float trunkR = 0.95f; // Đường kính thân ~1.9m
            woodParts.Add(new CombineInstance {
                mesh = cylMesh,
                transform = Matrix4x4.TRS(new Vector3(0, trunkH / 2f, 0), Quaternion.identity, new Vector3(trunkR * 2f, trunkH / 2f, trunkR * 2f))
            });
            // Cành choãng trái
            woodParts.Add(new CombineInstance {
                mesh = cylMesh,
                transform = Matrix4x4.TRS(new Vector3(-1.6f, 12f, 0.4f), Quaternion.Euler(0, 0, 32f), new Vector3(0.9f, 3.2f, 0.9f))
            });
            // Cành choãng phải
            woodParts.Add(new CombineInstance {
                mesh = cylMesh,
                transform = Matrix4x4.TRS(new Vector3(1.6f, 11.5f, -0.4f), Quaternion.Euler(0, 0, -28f), new Vector3(0.85f, 2.8f, 0.85f))
            });

            // Các vòm lá xum xuê sum sê
            leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(new Vector3(0, 16.5f, 0), Quaternion.identity, Vector3.one * 11.5f) });
            leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(new Vector3(-3.8f, 14f, 1f), Quaternion.Euler(15, 30, -10), Vector3.one * 9.5f) });
            leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(new Vector3(3.8f, 13.5f, -1f), Quaternion.Euler(-10, 60, 15), Vector3.one * 9.0f) });
            leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(new Vector3(0.5f, 12.5f, 3.5f), Quaternion.Euler(5, 120, -15), Vector3.one * 8.5f) });
            leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(new Vector3(-0.5f, 12.5f, -3.0f), Quaternion.Euler(-15, -45, 10), Vector3.one * 8.0f) });
        }
        else
        {
            // ==========================================
            // ANCIENT TREE B: CÂY ĐA CỔ THỤ ĐẠI NGÀN KHỔNG LỒ 24M
            // ==========================================
            float trunkH = 16f;
            float trunkR = 1.45f; // Thân cây khổng lồ đường kính ~2.9m
            woodParts.Add(new CombineInstance {
                mesh = cylMesh,
                transform = Matrix4x4.TRS(new Vector3(0, trunkH / 2f, 0), Quaternion.identity, new Vector3(trunkR * 2f, trunkH / 2f, trunkR * 2f))
            });
            // Cành lớn vươn xa 1
            woodParts.Add(new CombineInstance {
                mesh = cylMesh,
                transform = Matrix4x4.TRS(new Vector3(-2.8f, 14.5f, 1f), Quaternion.Euler(15, 0, 38f), new Vector3(1.3f, 4.5f, 1.3f))
            });
            // Cành lớn vươn xa 2
            woodParts.Add(new CombineInstance {
                mesh = cylMesh,
                transform = Matrix4x4.TRS(new Vector3(2.8f, 15f, -1.2f), Quaternion.Euler(-15, 45, -35f), new Vector3(1.2f, 4.2f, 1.2f))
            });
            // Cành lớn vươn xa 3
            woodParts.Add(new CombineInstance {
                mesh = cylMesh,
                transform = Matrix4x4.TRS(new Vector3(0.8f, 13.5f, 2.8f), Quaternion.Euler(35, 90, 0), new Vector3(1.1f, 4.0f, 1.1f))
            });

            // Tán lá khổng lồ sum suê che rợp bóng
            leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(new Vector3(0, 22.5f, 0), Quaternion.identity, Vector3.one * 15.5f) });
            leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(new Vector3(-5.5f, 18f, 1.5f), Quaternion.Euler(20, 25, -15), Vector3.one * 13.5f) });
            leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(new Vector3(5.5f, 18.5f, -2f), Quaternion.Euler(-15, 60, 20), Vector3.one * 13.0f) });
            leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(new Vector3(1.5f, 17f, 5f), Quaternion.Euler(10, 110, -10), Vector3.one * 12.5f) });
            leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(new Vector3(-1.5f, 16f, -4.5f), Quaternion.Euler(-20, -50, 15), Vector3.one * 12.0f) });
            leafParts.Add(new CombineInstance { mesh = bushMesh, transform = Matrix4x4.TRS(new Vector3(0, 14f, 0), Quaternion.identity, Vector3.one * 14.0f) });
        }

        // Ghép thân gỗ thành Submesh 0
        Mesh subWood = new Mesh();
        subWood.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        subWood.CombineMeshes(woodParts.ToArray(), true, true);

        // Ghép tán lá thành Submesh 1
        Mesh subLeaves = new Mesh();
        subLeaves.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        subLeaves.CombineMeshes(leafParts.ToArray(), true, true);

        // Hợp nhất thành 1 Mesh đa submesh duy nhất (Chuẩn 100% Unity Terrain Tree Engine)
        CombineInstance[] finalCombines = new CombineInstance[2];
        finalCombines[0].mesh = subWood;
        finalCombines[0].transform = Matrix4x4.identity;
        finalCombines[1].mesh = subLeaves;
        finalCombines[1].transform = Matrix4x4.identity;

        Mesh treeMesh = new Mesh();
        treeMesh.name = System.IO.Path.GetFileNameWithoutExtension(prefabPath) + "_Mesh";
        treeMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        treeMesh.CombineMeshes(finalCombines, false, false); // false = Giữ 2 submesh riêng biệt!

        string meshAssetPath = prefabPath.Replace(".prefab", "_Mesh.asset");
        AssetDatabase.DeleteAsset(meshAssetPath);
        AssetDatabase.CreateAsset(treeMesh, meshAssetPath);

        // Gắn MeshFilter và MeshRenderer TRỰC TIẾP LÊN ROOT GAMEOBJECT (Bắt buộc cho Unity Terrain Tree Prototype)
        GameObject treeGO = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
        MeshFilter mf = treeGO.AddComponent<MeshFilter>();
        mf.sharedMesh = treeMesh;

        MeshRenderer mr = treeGO.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new Material[] { woodMat, bushMat };

        AssetDatabase.DeleteAsset(prefabPath);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(treeGO, prefabPath);
        Object.DestroyImmediate(treeGO);
        return saved;
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
