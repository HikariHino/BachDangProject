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

    public static TideSystem Instance { get; private set; }

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
    public bool showUI = false;

    private float currentTargetY;
    private bool isRising = false;
    private float timer = 0f;
    private float worldScale = 1f;

    public float TargetWaterHeight => currentTargetY;
    public bool IsRising => isRising;
    private Object tideOwner;
    private bool heldHigh;
    private bool controlledEbb;
    private float ebbElapsed, ebbDuration, ebbStartY;
    public bool IsControlledEbb => controlledEbb;
    public bool IsLowTideLocked => tideOwner != null && !heldHigh;
    public bool IsTideLocked => tideOwner != null;

    // A mission can reserve low water without changing other scenes' normal tide cycle.
    public void HoldLowTide(Object owner)
    {
        HoldTide(owner, false);
    }

    public void HoldHighTide(Object owner, bool immediate = false)
    {
        HoldTide(owner, true, immediate);
    }

    /// <summary>Locks the water to a story controlled state until the same owner releases it.</summary>
    public void HoldTide(Object owner, bool high, bool immediate = false)
    {
        if (owner == null || (tideOwner != null && tideOwner != owner)) return;
        tideOwner = owner;
        controlledEbb = false;
        heldHigh = high;
        autoCycle = false;
        isRising = high;
        currentTargetY = high ? highTideY : lowTideY;
        if (immediate)
        {
            var position = transform.position;
            position.y = currentTargetY;
            transform.position = position;
            CurrentWaterHeight = position.y;
        }
    }

    public void ReleaseLowTide(Object owner)
    {
        ReleaseTide(owner);
    }

    public void ReleaseTide(Object owner)
    {
        if (tideOwner != owner) return;
        tideOwner = null;
        controlledEbb = false;
        timer = 0;
    }

    /// <summary>Mission-owned ebb with a visible duration, independent of the normal tide smoothing.</summary>
    public bool BeginEbb(Object owner, float duration)
    {
        if (owner == null || (tideOwner != null && tideOwner != owner)) return false;
        HoldTide(owner, false);
        ebbStartY = transform.position.y;
        ebbDuration = Mathf.Max(1f, duration); ebbElapsed = 0; controlledEbb = true;
        return true;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        currentTargetY = transform.position.y;
        CurrentWaterHeight = transform.position.y;
    }
    public BeachBoatStory.BachDangSaveData.Tide CaptureSave()=>new BeachBoatStory.BachDangSaveData.Tide {
        height=transform.position.y,target=currentTargetY,start=ebbStartY,elapsed=ebbElapsed,duration=ebbDuration,
        rising=isRising,controlled=controlledEbb,locked=IsTideLocked,high=heldHigh,
        hour=BeachBoatStory.BachDangBattleTimeline.Instance.DisplayHour,moment=(int)BeachBoatStory.BachDangBattleTimeline.Instance.CurrentMoment};
    public void RestoreSave(BeachBoatStory.BachDangSaveData.Tide data)
    {
        var p=transform.position;p.y=data.height;transform.position=p;CurrentWaterHeight=p.y;
        currentTargetY=data.target;ebbStartY=data.start;ebbElapsed=data.elapsed;ebbDuration=Mathf.Max(1,data.duration);
        isRising=data.rising;controlledEbb=data.controlled;heldHigh=data.high;
        tideOwner=data.locked?BeachBoatStory.BachDangBattleTimeline.Instance:null;autoCycle=false;timer=0;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // Preserve the legacy low-tide correction in the scene's world units.
        // The scene must be fully loaded: ForScene can return 1 during Awake,
        // which incorrectly lowered beachBoat's serialized low tide from 70 to 10.
        worldScale = BachDangWorldScale.ForScene(gameObject.scene);
        if (lowTideY > 10.1f * worldScale)
        {
            lowTideY = 10.0f * worldScale;
        }

        // XÓA NGAY Collider trên mặt nước (nếu có)! 
        // Nước là chất lỏng, tuyệt đối không được dùng MeshCollider cứng gây hất văng hay quay mòng mòng thuyền!
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }
    }

    public void ToggleTide()
    {
        SetTide(!isRising);
    }

    /// <summary>Shared command for keyboard, HUD and the scene director.</summary>
    public void SetTide(bool high, bool immediate = false)
    {
        if (IsTideLocked) return;
        controlledEbb = false;
        autoCycle = false;
        isRising = high;
        currentTargetY = high ? highTideY : lowTideY;

        if (immediate)
        {
            Vector3 position = transform.position;
            position.y = currentTargetY;
            transform.position = position;
            CurrentWaterHeight = position.y;
        }

        float targetMetres = currentTargetY / worldScale;
        Debug.Log($"🌊 LỆNH THỦY TRIỀU: {(high ? "TRIỀU DÂNG (High Tide)" : "TRIỀU RÚT (Low Tide)")} -> Mục tiêu: {targetMetres:F1}m");
    }

    void Update()
    {
        // 1. Phím tắt T để chuyển đổi nhanh Thủy Triều
        if (!BeachBoatStory.BachDangFrontEnd.IsBlocking && !IsTideLocked && Input.GetKeyDown(toggleTideKey))
        {
            ToggleTide();
        }

        // 2. Chế độ Thủy Triều tự động lên xuống nhịp nhàng
        if (IsTideLocked)
        {
            currentTargetY = heldHigh ? highTideY : lowTideY;
            isRising = heldHigh;
        }
        else if (autoCycle)
        {
            timer += Time.deltaTime;
            float phase = (timer / Mathf.Max(0.1f, cycleDuration)) * Mathf.PI * 2f - Mathf.PI / 2f;
            float t = (Mathf.Sin(phase) + 1f) / 2f;
            currentTargetY = Mathf.Lerp(lowTideY, highTideY, t);
            isRising = Mathf.Cos(phase) > 0f;
        }

        // 3. Nâng hạ mặt nước êm dịu (Lerp)
        Vector3 pos = transform.position;
        if (controlledEbb)
        {
            ebbElapsed = Mathf.Min(ebbDuration, ebbElapsed + Time.deltaTime);
            pos.y = Mathf.Lerp(ebbStartY, lowTideY, Mathf.SmoothStep(0, 1, ebbElapsed / ebbDuration));
            if (ebbElapsed >= ebbDuration) controlledEbb = false;
        }
        else pos.y = Mathf.Lerp(pos.y, currentTargetY, Time.deltaTime * 1.5f);
        transform.position = pos;

        // Cập nhật biến toàn cục
        CurrentWaterHeight = pos.y;
    }

    void OnGUI()
    {
        // Đã tắt vĩnh viễn theo yêu cầu của người chơi
        return;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 13;
        boxStyle.normal.textColor = Color.white;
        boxStyle.alignment = TextAnchor.MiddleLeft;
        boxStyle.richText = true;

        GUILayout.BeginArea(new Rect(20, 20, 320, 115), boxStyle);
        GUILayout.Label($"<b>🌊 THỦY TRIỀU BẠCH ĐẰNG 938</b>", boxStyle);
        
        string statusText = isRising ? "<color=#00e676>▲ ĐANG DÂNG CAO (Ngập cọc)</color>" : "<color=#ff5252>▼ ĐANG RÚT CẠN (Lộ bãi cọc)</color>";
        GUILayout.Label($"Trạng thái: {statusText}", boxStyle);
        float waterMetres = transform.position.y / worldScale;
        GUILayout.Label($"Cao độ mặt nước: <b>{waterMetres:F2} mét</b>", boxStyle);
        GUILayout.Label($"<i>[Bấm phím <b>T</b> để Đảo Chiều Thủy Triều]</i>", boxStyle);
        GUILayout.EndArea();
    }
}
