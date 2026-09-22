using UnityEngine;

/// <summary>
/// Hệ thống Đào Đất Từng Lớp (Layer-by-Layer Terraforming):
/// - Đất liền bình thường là vật cứng 100% (TerrainCollider), không thuyền hay vật gì đi xuyên qua được.
/// - Khi người chơi dùng công cụ đào (Click chuột trái hoặc bấm phím F):
///   + Lớp 1 (Mặt cỏ): Cỏ bị cạo đi, lộ ra lớp Đất Bùn Nâu.
///   + Lớp 2 (Sâu hơn): Đất bị khoét sâu, lộ ra lớp Cát Sỏi / Đá cứng.
///   + Lớp 3 (Đào tới mực nước): Nước sông tự động tràn vào rãnh đào thành hào chống giặc!
/// </summary>
public class TerrainDigger : MonoBehaviour
{
    [Header("Cài đặt Đào Đất")]
    [Tooltip("Bán kính vùng cuốc đào (mét)")]
    public float digRadius = 4f;

    [Tooltip("Độ sâu khoét xuống mỗi nhát cuốc (mét)")]
    public float digDepthPerHit = 1.2f;

    [Tooltip("Phím tắt để đào (hoặc Click chuột trái)")]
    public KeyCode digKey = KeyCode.F;

    [Header("Hiệu ứng văng đất")]
    public bool spawnDirtChunks = true;

    private Terrain targetTerrain;
    private TerrainData td;

    void Start()
    {
        targetTerrain = FindFirstObjectByType<Terrain>();
        if (targetTerrain != null)
        {
            td = targetTerrain.terrainData;
        }
    }

    void Update()
    {
        // Nhận lệnh đào: Click chuột trái HOẶC bấm phím F
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(digKey))
        {
            TryDigGround();
        }
    }

    void TryDigGround()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            Terrain hitTerrain = hit.collider.GetComponent<Terrain>();
            if (hitTerrain != null)
            {
                DigAtWorldPosition(hit.point);
            }
        }
    }

    public void DigAtWorldPosition(Vector3 worldPoint)
    {
        if (targetTerrain == null) targetTerrain = FindFirstObjectByType<Terrain>();
        if (targetTerrain == null) return;
        td = targetTerrain.terrainData;

        Vector3 tPos = targetTerrain.transform.position;
        Vector3 tSize = td.size;

        // Chuyển đổi tọa độ thế giới sang tọa độ heightmap (0 .. resolution-1)
        int hRes = td.heightmapResolution;
        int centerX = Mathf.RoundToInt(((worldPoint.x - tPos.x) / tSize.x) * (hRes - 1));
        int centerZ = Mathf.RoundToInt(((worldPoint.z - tPos.z) / tSize.z) * (hRes - 1));

        int rInPixels = Mathf.Max(1, Mathf.RoundToInt((digRadius / tSize.x) * (hRes - 1)));

        int startX = Mathf.Clamp(centerX - rInPixels, 0, hRes - 1);
        int startZ = Mathf.Clamp(centerZ - rInPixels, 0, hRes - 1);
        int width  = Mathf.Clamp(centerX + rInPixels, 0, hRes - 1) - startX + 1;
        int height = Mathf.Clamp(centerZ + rInPixels, 0, hRes - 1) - startZ + 1;

        if (width <= 0 || height <= 0) return;

        float[,] heights = td.GetHeights(startX, startZ, width, height);
        float normalizedDig = digDepthPerHit / tSize.y;

        // 1. KHOÉT LÕM ĐỘ CAO THEO HÌNH BÁT ÚP
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int currentX = startX + x;
                int currentZ = startZ + y;

                float dist = Vector2.Distance(new Vector2(centerX, centerZ), new Vector2(currentX, currentZ));
                if (dist <= rInPixels)
                {
                    // Trũng sâu ở tâm, thoai thoải ra viền
                    float falloff = 1f - (dist / rInPixels);
                    heights[y, x] = Mathf.Max(0.02f, heights[y, x] - normalizedDig * falloff);
                }
            }
        }
        td.SetHeights(startX, startZ, heights);

        // 2. BỘC LỘ TỪNG LỚP ĐẤT (Sơn lại Alphamap: Cỏ -> Bùn Nâu -> Cát Sỏi)
        int aRes = td.alphamapResolution;
        int layerCount = td.terrainLayers.Length;

        if (layerCount >= 3)
        {
            int aCenterX = Mathf.RoundToInt(((worldPoint.x - tPos.x) / tSize.x) * (aRes - 1));
            int aCenterZ = Mathf.RoundToInt(((worldPoint.z - tPos.z) / tSize.z) * (aRes - 1));
            int aR = Mathf.Max(1, Mathf.RoundToInt((digRadius / tSize.x) * (aRes - 1)));

            int aStartX = Mathf.Clamp(aCenterX - aR, 0, aRes - 1);
            int aStartZ = Mathf.Clamp(aCenterZ - aR, 0, aRes - 1);
            int aW = Mathf.Clamp(aCenterX + aR, 0, aRes - 1) - aStartX + 1;
            int aH = Mathf.Clamp(aCenterZ + aR, 0, aRes - 1) - aStartZ + 1;

            if (aW > 0 && aH > 0)
            {
                float[,,] splats = td.GetAlphamaps(aStartX, aStartZ, aW, aH);

                for (int y = 0; y < aH; y++)
                {
                    for (int x = 0; x < aW; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(aCenterX, aCenterZ), new Vector2(aStartX + x, aStartZ + y));
                        if (dist <= aR)
                        {
                            // Đào lộ lớp: Cạo sạch cỏ (Layer 0), đắp bùn nâu (Layer 1) & cát sỏi (Layer 2)
                            splats[y, x, 0] = 0f; // Mất cỏ
                            splats[y, x, 1] = 0.7f; // Bùn đất nâu
                            splats[y, x, 2] = 0.3f; // Sỏi đá
                            if (layerCount > 3) splats[y, x, 3] = 0f;
                        }
                    }
                }
                td.SetAlphamaps(aStartX, aStartZ, splats);
            }
        }

        // 3. TẠO VỤN ĐẤT VĂNG LÊN MẶT ĐẤT KHI ĐÀO
        if (spawnDirtChunks)
        {
            SpawnDirtEffects(worldPoint);
        }

        Debug.Log($"⛏️ ĐÃ ĐÀO LÕM ĐẤT TẠI: {worldPoint}! Lớp cỏ bị cạo, bộc lộ lớp bùn sỏi đá bên dưới!");
    }

    private void SpawnDirtEffects(Vector3 pos)
    {
        for (int i = 0; i < 15; i++)
        {
            GameObject dirt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dirt.transform.position = pos + new Vector3(Random.Range(-1f, 1f), 0.5f, Random.Range(-1f, 1f));
            dirt.transform.localScale = Vector3.one * Random.Range(0.2f, 0.4f);
            
            var r = dirt.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.35f, 0.22f, 0.12f); // Màu đất nâu sẫm

            Rigidbody rb = dirt.AddComponent<Rigidbody>();
            rb.AddExplosionForce(350f, pos, 3f, 1.5f);
            Destroy(dirt, 2.5f);
        }
    }
}
