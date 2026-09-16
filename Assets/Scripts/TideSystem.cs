using UnityEngine;

/// <summary>
/// Hệ thống Thủy Triều & Sóng Vỗ Bờ Cát Bạch Đằng:
/// - Nước dâng lên rút xuống nhịp nhàng theo thời gian (hoặc bấm phím T để chuyển đổi tức thì).
/// - Khi Triều Lên (Y ~ 15.5m): Nước biển dâng cao tràn ngập bãi cát, phủ ngập đầu cọc gỗ.
/// - Khi Triều Rút (Y ~ 12.0m): Nước rút cạn làm lộ bãi cát vàng mênh mông và hàng cọc nhọn hoắt đâm thủng thuyền giặc!
/// </summary>
public class TideSystem : MonoBehaviour
{
    // Biến toàn cục để các script khác (như BoatCrash) đọc được mực nước hiện tại
    public static float CurrentWaterHeight = 14.0f;

    [Header("Cài đặt Mực Nước Thủy Triều (mét)")]
    [Tooltip("Mực nước khi Thủy triều rút cạn (lộ bãi cọc & bãi cát)")]
    public float lowTideY = 10.0f; // Rút sâu xuống 10 mét theo yêu cầu của sếp

    [Tooltip("Mực nước khi Thủy triều dâng cao (ngập bãi cọc)")]
    public float highTideY = 15.5f;

    [Tooltip("Thời gian của 1 chu kỳ triều lên - rút (giây)")]
    public float cycleDuration = 40f;

    [Header("Chế độ Tự Động")]
    public bool autoCycle = true;

    [Header("Phím tắt chuyển đổi Thủy Triều (T)")]
    public KeyCode toggleTideKey = KeyCode.T;

    [Header("Hiển thị giao diện trạng thái")]
    public bool showUI = true;

    private float currentTargetY;
    private bool isRising = false;
    private float timer = 0f;

    void Start()
    {
        currentTargetY = transform.position.y;
        CurrentWaterHeight = transform.position.y;

        // XÓA NGAY Collider trên mặt nước (nếu có)! 
        // Nước là chất lỏng, tuyệt đối không được dùng MeshCollider cứng gây hất văng hay quay mòng mòng thuyền!
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }
    }

    void Update()
    {
        // 1. Phím tắt T để chuyển đổi nhanh Thủy Triều
        if (Input.GetKeyDown(toggleTideKey))
        {
            isRising = !isRising;
            autoCycle = false; // Tạm dừng tự động khi người chơi tự can thiệp
            currentTargetY = isRising ? highTideY : lowTideY;
            Debug.Log($"🌊 LỆNH THỦY TRIỀU: {(isRising ? "TRIỀU DÂNG (High Tide)" : "TRIỀU RÚT (Low Tide)")} -> Mục tiêu: {currentTargetY}m");
        }

        // 2. Chế độ Thủy Triều tự động lên xuống nhịp nhàng
        if (autoCycle)
        {
            timer += Time.deltaTime;
            float t = (Mathf.Sin((timer / cycleDuration) * Mathf.PI * 2f - Mathf.PI / 2f) + 1f) / 2f;
            currentTargetY = Mathf.Lerp(lowTideY, highTideY, t);
            isRising = Mathf.Cos((timer / cycleDuration) * Mathf.PI * 2f - Mathf.PI / 2f) > 0f;
        }

        // 3. Nâng hạ mặt nước êm dịu (Lerp)
        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, currentTargetY, Time.deltaTime * 1.5f);
        transform.position = pos;

        // Cập nhật biến toàn cục
        CurrentWaterHeight = pos.y;
    }

    void OnGUI()
    {
        if (!showUI) return;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 13;
        boxStyle.normal.textColor = Color.white;
        boxStyle.alignment = TextAnchor.MiddleLeft;

        GUILayout.BeginArea(new Rect(20, 20, 320, 115), boxStyle);
        GUILayout.Label($"<b>🌊 THỦY TRIỀU BẠCH ĐẰNG 938</b>", boxStyle);
        
        string statusText = isRising ? "<color=#00e676>▲ ĐANG DÂNG CAO (Ngập cọc)</color>" : "<color=#ff5252>▼ ĐANG RÚT CẠN (Lộ bãi cọc)</color>";
        GUILayout.Label($"Trạng thái: {statusText}", boxStyle);
        GUILayout.Label($"Cao độ mặt nước: <b>{transform.position.y:F2} mét</b>", boxStyle);
        GUILayout.Label($"<i>[Bấm phím <b>T</b> để Đảo Chiều Thủy Triều]</i>", boxStyle);
        GUILayout.EndArea();
    }
}
