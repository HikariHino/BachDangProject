using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MapToTerrainBuilder : EditorWindow
{
    // Viền đất trống xung quanh bản đồ (0.0~0.5), để xây doanh trại/làng mạc
    const float BORDER = 0.18f; // 18% mỗi bên = ~540 mét đất trống quanh bản đồ

    [MenuItem("🤖 Trợ lý AI/🗺️ Xây Sa Bàn Bạch Đằng (Texture Xịn)")]
    public static void BuildTerrainFromImage()
    {
        string imagePath = "Assets/BachDangMap.png";
        TextureImporter importer = AssetImporter.GetAtPath(imagePath) as TextureImporter;
        if (importer != null && !importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }

        Texture2D mapTex = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
        if (mapTex == null) { Debug.LogError("❌ Không tìm thấy BachDangMap.png!"); return; }

        int tSize = 1025;
        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = tSize;
        // TĂNG LÊN 3000m + viền 540m mỗi phía để xây doanh trại
        terrainData.size = new Vector3(3000, 60, 3000);

        // ========== PASS 1: Phân loại từng pixel ==========
        // 0 = Nước | 1 = Đất | -1 = Không rõ (Mũi tên đỏ / Chữ trắng / Chữ đen)
        int[,] pixelType = new int[tSize, tSize];
        float[,] heights  = new float[tSize, tSize];

        for (int y = 0; y < tSize; y++)
        for (int x = 0; x < tSize; x++)
        {
            // Tính UV trong bản đồ (chỉ lấy vùng trung tâm, bỏ qua viền BORDER)
            float u = BORDER + ((float)x / (tSize - 1)) * (1f - 2f * BORDER);
            float v = BORDER + ((float)y / (tSize - 1)) * (1f - 2f * BORDER);

            // Kiểm tra có nằm trong viền đất trống không
            bool inBorder = x < tSize * BORDER || x > tSize * (1f - BORDER) ||
                            y < tSize * BORDER || y > tSize * (1f - BORDER);

            if (inBorder)
            {
                pixelType[y, x] = 1; // Viền xung quanh = Đất liền (để xây làng/doanh trại)
                heights[y, x] = 0.38f;
                continue;
            }

            Color c = mapTex.GetPixelBilinear(u, v);

            bool isLand  = c.g > 0.35f && c.g > c.r && (c.g - c.b) > 0.05f;
            bool isWater = (c.b > c.g && c.b > c.r) || (!isLand && c.b > 0.3f && (c.b + c.g) > c.r * 1.5f);
            bool isRed   = c.r > c.g + 0.2f && c.r > c.b + 0.2f; // Mũi tên đỏ
            bool isWhite = c.r > 0.82f && c.g > 0.82f && c.b > 0.82f; // Chữ trắng
            bool isBlack = c.r < 0.22f && c.g < 0.22f && c.b < 0.22f; // Chữ đen

            if (isRed || isWhite || isBlack)
            {
                pixelType[y, x] = -1; // Không rõ — sẽ lấp sau
                heights[y, x] = -1f;
            }
            else if (isWater)
            {
                pixelType[y, x] = 0;
                heights[y, x] = 0.08f;
            }
            else
            {
                pixelType[y, x] = 1;
                heights[y, x] = 0.4f;
            }
        }

        // ========== PASS 2: Lấp hố (Không rõ → Nội suy từ điểm lân cận) ==========
        // Quét nhiều lần để lan dần ra cho tới khi không còn điểm -1
        int maxIter = 30;
        for (int iter = 0; iter < maxIter; iter++)
        {
            bool anyUnknown = false;
            for (int y = 0; y < tSize; y++)
            for (int x = 0; x < tSize; x++)
            {
                if (pixelType[y, x] != -1) continue;
                anyUnknown = true;

                // Lấy trung bình từ 8 điểm xung quanh đã biết
                float sum = 0; int cnt = 0;
                for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    if (dy == 0 && dx == 0) continue;
                    int ny = Mathf.Clamp(y+dy, 0, tSize-1);
                    int nx = Mathf.Clamp(x+dx, 0, tSize-1);
                    if (pixelType[ny, nx] >= 0) { sum += heights[ny, nx]; cnt++; }
                }
                if (cnt > 0) { heights[y, x] = sum / cnt; pixelType[y, x] = heights[y,x] > 0.2f ? 1 : 0; }
            }
            if (!anyUnknown) break;
        }

        // ========== PASS 3: Làm mượt ==========
        float[,] smooth = new float[tSize, tSize];
        int sr = 5;
        for (int y = 0; y < tSize; y++)
        for (int x = 0; x < tSize; x++)
        {
            float sum = 0; int cnt = 0;
            for (int dy=-sr; dy<=sr; dy++)
            for (int dx=-sr; dx<=sr; dx++)
            {
                int ny=Mathf.Clamp(y+dy,0,tSize-1); int nx=Mathf.Clamp(x+dx,0,tSize-1);
                sum+=heights[ny,nx]; cnt++;
            }
            smooth[y,x]=sum/cnt;
        }
        terrainData.SetHeights(0, 0, smooth);

        // ========== SƠN TEXTURE THẬT ==========
        string lBase = "Assets/TerrainSampleAssets/TerrainLayers/";
        TerrainLayer grass  = AssetDatabase.LoadAssetAtPath<TerrainLayer>(lBase + "Grass_A_TerrainLayer.terrainlayer");
        TerrainLayer muddy  = AssetDatabase.LoadAssetAtPath<TerrainLayer>(lBase + "Muddy_TerrainLayer.terrainlayer");
        TerrainLayer sand   = AssetDatabase.LoadAssetAtPath<TerrainLayer>(lBase + "Sand_TerrainLayer.terrainlayer");
        TerrainLayer tidal  = AssetDatabase.LoadAssetAtPath<TerrainLayer>(lBase + "Tidal_Pools_TerrainLayer.terrainlayer");
        TerrainLayer mossy  = AssetDatabase.LoadAssetAtPath<TerrainLayer>(lBase + "Grass_Moss_TerrainLayer.terrainlayer");

        var layers = new List<TerrainLayer>();
        if (grass != null) layers.Add(grass);
        if (muddy != null) layers.Add(muddy);
        if (sand  != null) layers.Add(sand);
        if (tidal != null) layers.Add(tidal);
        if (mossy != null) layers.Add(mossy);
        terrainData.terrainLayers = layers.ToArray();

        int alphaRes = terrainData.alphamapResolution;
        int lc = layers.Count;
        float[,,] splatmaps = new float[alphaRes, alphaRes, lc];

        for (int y = 0; y < alphaRes; y++)
        for (int x = 0; x < alphaRes; x++)
        {
            float u0 = BORDER + ((float)x / alphaRes) * (1f - 2f * BORDER);
            float v0 = BORDER + ((float)y / alphaRes) * (1f - 2f * BORDER);
            bool inBorder2 = x < alphaRes * BORDER || x > alphaRes * (1f - BORDER) ||
                             y < alphaRes * BORDER || y > alphaRes * (1f - BORDER);

            Color c = inBorder2 ? new Color(0, 0.5f, 0) : mapTex.GetPixelBilinear(u0, v0);
            bool isLand   = inBorder2 || (c.g > 0.35f && c.g > c.r && (c.g - c.b) > 0.05f);
            bool isForest = isLand && c.g < 0.55f && c.r < 0.4f;
            float h = smooth[Mathf.Clamp((int)((float)y/alphaRes*tSize),0,tSize-1), Mathf.Clamp((int)((float)x/alphaRes*tSize),0,tSize-1)];
            bool isShore = isLand && h < 0.22f;

            for (int l=0; l<lc; l++) splatmaps[y,x,l] = 0f;

            if (!isLand && lc > 3)       { splatmaps[y,x,3] = 1f; }                                     // Tidal/Biển
            else if (isShore && lc > 1)  { splatmaps[y,x,1] = 0.65f; if(lc>2) splatmaps[y,x,2]=0.35f; } // Bùn + Cát bờ sông
            else if (isForest && lc > 4) { splatmaps[y,x,0] = 0.3f; splatmaps[y,x,4] = 0.7f; }          // Rêu rừng
            else                         { splatmaps[y,x,0] = 1f; }                                      // Cỏ xanh
        }
        terrainData.SetAlphamaps(0, 0, splatmaps);

        // ========== CẮM CÂY CỎ ==========
        string pBase = "Assets/TerrainSampleAssets/Prefabs/";
        var protos = new List<TreePrototype>();
        foreach (var vn in new[]{"Fern_A","Fern_B","Fern_C","Bush_A","Bush_B","Plant_A","Plant_B","Grass_A","Grass_B"})
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(pBase + vn + ".prefab");
            if (pf != null) protos.Add(new TreePrototype { prefab = pf });
        }
        terrainData.treePrototypes = protos.ToArray();

        var trees = new List<TreeInstance>();
        int pc = protos.Count;
        for (int i = 0; pc > 0 && i < 35000; i++)
        {
            float tx = Random.value, ty = Random.value;
            float u2 = BORDER + tx * (1f - 2f * BORDER);
            float v2 = BORDER + ty * (1f - 2f * BORDER);
            bool inBorder3 = tx < BORDER || tx > 1f-BORDER || ty < BORDER || ty > 1f-BORDER;

            Color c = inBorder3 ? new Color(0, 0.5f, 0) : mapTex.GetPixelBilinear(u2, v2);
            bool isLand = inBorder3 || (c.g > 0.35f && c.g > c.r && (c.g - c.b) > 0.05f);
            if (!isLand) continue;

            bool isForest = !inBorder3 && c.g < 0.6f && c.r < 0.4f;
            var ti = new TreeInstance();
            ti.position = new Vector3(tx, 0f, ty);
            ti.prototypeIndex = isForest ? Random.Range(0, Mathf.Min(5, pc)) : Random.Range(Mathf.Min(5,pc), pc);
            ti.widthScale = Random.Range(0.8f, 2.5f);
            ti.heightScale = Random.Range(0.8f, 2.5f);
            ti.color = ti.lightmapColor = Color.white;
            trees.Add(ti);
        }
        terrainData.SetTreeInstances(trees.ToArray(), true);

        // ========== SINH TERRAIN ==========
        GameObject go = Terrain.CreateTerrainGameObject(terrainData);
        go.name = "SaBan_BachDang_Final";
        go.transform.position = new Vector3(-1500, 0, -1500);
        Terrain t = go.GetComponent<Terrain>();
        t.drawTreesAndFoliage = true;
        t.treeBillboardDistance = 800f;
        t.treeDistance = 1200f;

        AssetDatabase.SaveAssets();
        Debug.Log($"🎉 SA BÀN HOÀN THÀNH! 3000m, {trees.Count} cây, hố lấp xong, viền đất rộng 540m!");
    }
}
