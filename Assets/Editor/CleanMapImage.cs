using UnityEngine;
using UnityEditor;
using System.IO;

public class CleanMapImage : EditorWindow
{
    [MenuItem("🤖 Trợ lý AI/🧹 Xóa Mũi Tên Đỏ Khỏi Ảnh Bản Đồ")]
    public static void CleanMap()
    {
        string inputPath  = "Assets/BachDangMap.png";
        string outputPath = "Assets/BachDangMap_Clean.png";

        TextureImporter imp = AssetImporter.GetAtPath(inputPath) as TextureImporter;
        if (imp != null && !imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }

        Texture2D src = AssetDatabase.LoadAssetAtPath<Texture2D>(inputPath);
        if (src == null) { Debug.LogError("Không tìm thấy BachDangMap.png!"); return; }

        int w = src.width, h = src.height;
        Color[] pixels = src.GetPixels();
        Color[] output  = new Color[pixels.Length];

        // ===== PASS 1: Đánh dấu pixel đỏ / vòng tròn đỏ =====
        bool[] isRed = new bool[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            // Mũi tên đỏ: R cao, G thấp, B thấp
            if (c.r > 0.55f && c.g < 0.35f && c.b < 0.35f)
                isRed[i] = true;
        }

        // ===== PASS 2: Lấp màu đỏ bằng điểm lân cận không đỏ =====
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int idx = y * w + x;
            if (!isRed[idx]) { output[idx] = pixels[idx]; continue; }

            // Tìm điểm lân cận không đỏ gần nhất trong vùng 12x12
            Color best = Color.clear;
            float bestDist = float.MaxValue;
            for (int r = 1; r <= 12; r++)
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // Chỉ lấy vòng ngoài
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                int ni = ny * w + nx;
                if (!isRed[ni])
                {
                    float d = dx*dx + dy*dy;
                    if (d < bestDist) { bestDist = d; best = pixels[ni]; }
                }
            }
            output[idx] = best == Color.clear ? pixels[idx] : best;
        }

        // ===== PASS 3: Xóa chữ đen (Mũi tên đen) =====
        // Chỉ xóa pixel đen NẰM TRÊN VÙNG NƯỚC (xung quanh có màu xanh dương)
        for (int y = 1; y < h-1; y++)
        for (int x = 1; x < w-1; x++)
        {
            int idx = y * w + x;
            Color c = output[idx];
            bool isBlack = c.r < 0.2f && c.g < 0.2f && c.b < 0.2f;
            if (!isBlack) continue;

            // Kiểm tra xung quanh có nhiều màu xanh dương không (= đang trên sông)
            int blueNeighbors = 0;
            for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
            {
                int nx=x+dx, ny=y+dy;
                if (nx<0||nx>=w||ny<0||ny>=h) continue;
                Color n = output[ny*w+nx];
                if (n.b > 0.4f && n.b > n.r + 0.1f) blueNeighbors++;
            }
            if (blueNeighbors > 15) // Đang trên mặt nước → xóa chữ đen
            {
                // Lấy màu xanh của sông xung quanh
                Color avg = Color.clear; int cnt2 = 0;
                for (int dy=-3; dy<=3; dy++)
                for (int dx=-3; dx<=3; dx++)
                {
                    int nx=x+dx, ny=y+dy;
                    if (nx<0||nx>=w||ny<0||ny>=h) continue;
                    Color n = output[ny*w+nx];
                    if (n.b > 0.4f) { avg += n; cnt2++; }
                }
                if (cnt2 > 0) output[idx] = avg / cnt2;
            }
        }

        // ===== XUẤT FILE =====
        Texture2D result = new Texture2D(w, h, TextureFormat.RGB24, false);
        result.SetPixels(output);
        result.Apply();

        byte[] pngData = result.EncodeToPNG();
        string absPath = Application.dataPath + "/BachDangMap_Clean.png";
        File.WriteAllBytes(absPath, pngData);
        AssetDatabase.ImportAsset(outputPath);

        Debug.Log($"✅ Ảnh đã được làm sạch! Lưu tại: {outputPath}");
        EditorUtility.DisplayDialog("Xong!", "Ảnh sạch đã được lưu tại:\nAssets/BachDangMap_Clean.png\n\nSếp vào Project → Assets để xem!", "OK ngầu!");
    }
}
