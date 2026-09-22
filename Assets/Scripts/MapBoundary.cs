using UnityEngine;

/// <summary>
/// Gắn script này vào nhân vật (Player) để giới hạn không ra ngoài bản đồ.
/// Tự động đọc kích thước Terrain trong scene và tạo "hàng rào vô hình".
/// </summary>
public class MapBoundary : MonoBehaviour
{
    [Header("Tự động tìm Terrain trong scene")]
    public bool autoDetectTerrain = true;

    [Header("Hoặc nhập tay nếu cần (Nếu autoDetect = false)")]
    public Vector3 mapCenter = Vector3.zero;
    public float mapWidth  = 3000f; // X
    public float mapDepth  = 3000f; // Z

    [Header("Hiện viền đỏ debug trong Scene View")]
    public bool showDebugBounds = true;

    private Bounds _bounds;

    void Start()
    {
        if (autoDetectTerrain)
        {
            Terrain terrain = FindFirstObjectByType<Terrain>();
            if (terrain != null)
            {
                TerrainData td = terrain.terrainData;
                Vector3 pos    = terrain.transform.position;
                _bounds = new Bounds(
                    pos + new Vector3(td.size.x / 2f, td.size.y / 2f, td.size.z / 2f),
                    td.size
                );
                Debug.Log($"🗺️ MapBoundary: Tự động nhận Terrain {td.size.x}m x {td.size.z}m");
            }
            else
            {
                Debug.LogWarning("⚠️ Không tìm thấy Terrain! Dùng giá trị nhập tay.");
                _bounds = new Bounds(mapCenter, new Vector3(mapWidth, 9999f, mapDepth));
            }
        }
        else
        {
            _bounds = new Bounds(mapCenter, new Vector3(mapWidth, 9999f, mapDepth));
        }
    }

    void LateUpdate()
    {
        // Sau mỗi frame, kéo nhân vật về trong vùng giới hạn nếu đã bước ra ngoài
        Vector3 pos = transform.position;

        float minX = _bounds.min.x + 5f; // Thêm 5m buffer để không bị kẹt vào tường
        float maxX = _bounds.max.x - 5f;
        float minZ = _bounds.min.z + 5f;
        float maxZ = _bounds.max.z - 5f;

        bool clamped = false;
        if (pos.x < minX) { pos.x = minX; clamped = true; }
        if (pos.x > maxX) { pos.x = maxX; clamped = true; }
        if (pos.z < minZ) { pos.z = minZ; clamped = true; }
        if (pos.z > maxZ) { pos.z = maxZ; clamped = true; }

        if (clamped)
        {
            // Nếu có Rigidbody thì zero velocity để không bị lún qua tường
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 v = rb.linearVelocity;
                if (pos.x <= minX || pos.x >= maxX) v.x = 0;
                if (pos.z <= minZ || pos.z >= maxZ) v.z = 0;
                rb.linearVelocity = v;
            }
            transform.position = pos;
        }
    }

    void OnDrawGizmos()
    {
        if (!showDebugBounds) return;
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireCube(_bounds.center, _bounds.size);
    }
}
