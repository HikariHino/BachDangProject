using UnityEngine;

/// <summary>
/// Bảng Điều Khiển Sa Bàn Chiến Trận Bạch Đằng 938 (Interactive Battle HUD & Director)
/// Hỗ trợ chuyển đổi nhanh góc quay camera, điều khiển thủy triều và quan sát bối cảnh.
/// </summary>
public class SaBanHUD : MonoBehaviour
{
    public static SaBanHUD Instance;

    [Header("Ẩn/Hiện Bảng Điều Khiển [H]")]
    public bool showHUD = true;

    private TideSystem tideSystem;
    private BoatCrash enemyBoat;
    private BattleCamera battleCam;
    private DayNightCycle dayNightCycle;
    private bool hasSettlementTour;

    public readonly struct CamPreset
    {
        public readonly string name;
        public readonly Vector3 position;
        public readonly Vector3 lookAt;

        public CamPreset(string name, Vector3 pos, Vector3 target)
        {
            this.name = name;
            this.position = pos;
            this.lookAt = target;
        }
    }

    public CamPreset[] presets;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        tideSystem = FindAnyObjectByType<TideSystem>();
        enemyBoat = FindAnyObjectByType<BoatCrash>();
        battleCam = Camera.main != null ? Camera.main.GetComponent<BattleCamera>() : null;
        foreach (DayNightCycle cycle in FindObjectsByType<DayNightCycle>(FindObjectsSortMode.None))
        {
            if (!cycle.isActiveAndEnabled || cycle.skyboxMaterial == null || cycle.gameObject.scene != gameObject.scene) continue;
            dayNightCycle = cycle;
            break;
        }

        presets = new CamPreset[]
        {
            // 1. Toàn cảnh Sa bàn từ trên cao (Birds-eye tactical view)
            new CamPreset("1. Toàn Cảnh Sa Bàn", new Vector3(-550f, 150f, -20f), new Vector3(-380f, 14f, 300f)),
            // 2. Cận cảnh bãi cọc & thuyền giặc Nam Hán
            new CamPreset("2. Bãi Cọc & Chiến Hạm", new Vector3(-430f, 22f, 280f), new Vector3(-350f, 14.2f, 300f)),
            // 3. Đại Bản Doanh & Tướng Ngô Quyền
            new CamPreset("3. Đại Bản Doanh Ngô Quyền", new Vector3(-798f, 21f, 310f), new Vector3(-817f, 19f, 320f)),
            // 4. Góc Nhìn Chủ Tướng Hướng Ra Sông
            new CamPreset("4. Tầm Nhìn Tướng Ngô Quyền", new Vector3(-814f, 19.4f, 320f), new Vector3(-770f, 16.5f, 320f))
        };

        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name != "BachDang_Living_Settlements") continue;
            hasSettlementTour = true;
            System.Array.Resize(ref presets, 8);
            presets[4] = new CamPreset("5. Làng Chợ", new Vector3(-805f, 60f, 0f), new Vector3(-680f, 20f, 110f));
            presets[5] = new CamPreset("6. Xóm Chài", new Vector3(20f, 82f, -20f), new Vector3(180f, 20f, 75f));
            presets[6] = new CamPreset("7. Trại Quân", new Vector3(-1040f, 74f, 120f), new Vector3(-935f, 20f, 220f));
            presets[7] = new CamPreset("8. Xóm Vườn", new Vector3(-1220f, 68f, 155f), new Vector3(-1090f, 20f, 255f));
            break;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            showHUD = !showHUD;
        }

        // Phím tắt đổi camera [1], [2], [3], [4]
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchToPreset(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchToPreset(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchToPreset(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchToPreset(3);
        if (hasSettlementTour)
        {
            if (Input.GetKeyDown(KeyCode.Alpha5)) SwitchToPreset(4);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SwitchToPreset(5);
            if (Input.GetKeyDown(KeyCode.Alpha7)) SwitchToPreset(6);
            if (Input.GetKeyDown(KeyCode.Alpha8)) SwitchToPreset(7);
        }
    }

    public void SwitchToPreset(int index)
    {
        if (presets == null || index < 0 || index >= presets.Length) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        var p = presets[index];
        if (battleCam == null || battleCam.gameObject != cam.gameObject)
            battleCam = cam.GetComponent<BattleCamera>();

        if (battleCam != null)
        {
            battleCam.SetFreeFlyPose(p.position, p.lookAt);
        }
        else
        {
            cam.transform.position = p.position;
            cam.transform.LookAt(p.lookAt);
        }
        Debug.Log($"🎥 Đã chuyển sang góc quay: {p.name}");
    }

    void OnGUI()
    {
        if (!showHUD) return;

        GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.padding = new RectOffset(12, 12, 10, 10);

        GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = 15;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

        GUIStyle bodyStyle = new GUIStyle(GUI.skin.label);
        bodyStyle.fontSize = 12;
        bodyStyle.normal.textColor = Color.white;
        bodyStyle.richText = true;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = 12;
        btnStyle.fontStyle = FontStyle.Bold;
        btnStyle.fixedHeight = 28;

        float panelWidth = 370f;
        bool hasDayNightCycle = dayNightCycle != null && dayNightCycle.isActiveAndEnabled;
        float panelHeight = hasDayNightCycle ? 325f : 230f;
        if (hasSettlementTour) panelHeight += 34f;
        GUILayout.BeginArea(new Rect(15, 15, panelWidth, panelHeight), panelStyle);

        GUILayout.Label("⚔️ ĐIỀU KHIỂN SA BÀN BẠCH ĐẰNG 938", headerStyle);
        GUILayout.Space(3);

        // THỦY TRIỀU
        float waterY = tideSystem != null ? tideSystem.transform.position.y : 14f;
        string tideDesc = waterY > 13.5f ? "<color=#00e676>▲ TRIỀU DÂNG (Ngập đầu cọc)</color>" : "<color=#ff5252>▼ TRIỀU RÚT (Lộ bãi cọc nhọn)</color>";
        GUILayout.Label($"🌊 Mực nước: <b>{waterY:F1}m</b> - Trạng thái: {tideDesc}", bodyStyle);

        if (GUILayout.Button("🌊 Đảo Chiều Thủy Triều [T]", btnStyle))
        {
            if (tideSystem != null)
            {
                tideSystem.ToggleTide();
            }
        }

        GUILayout.Space(6);

        // THỜI GIAN & BẦU TRỜI
        if (hasDayNightCycle)
        {
            int totalMinutes = Mathf.FloorToInt(Mathf.Repeat(dayNightCycle.timeOfDay, 24f) * 60f);
            GUILayout.Label($"<b>Thời gian: {totalMinutes / 60:00}:{totalMinutes % 60:00}</b>", bodyStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sáng", btnStyle)) dayNightCycle.SetTimeOfDay(9f);
            if (GUILayout.Button("Hoàng hôn", btnStyle)) dayNightCycle.SetTimeOfDay(18f);
            if (GUILayout.Button("Đêm", btnStyle)) dayNightCycle.SetTimeOfDay(0f);
            GUILayout.EndHorizontal();
            dayNightCycle.autoAdvance = GUILayout.Toggle(dayNightCycle.autoAdvance, "Tự chạy chu kỳ ngày / đêm");
            GUILayout.Space(6);
        }

        // GÓC QUAY CAMERA
        GUILayout.Label(hasSettlementTour ? "🎥 <b>Góc Quay Điện Ảnh [Phím 1 - 8]:</b>"
                                         : "🎥 <b>Góc Quay Điện Ảnh [Phím 1 - 4]:</b>", bodyStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[1] Sa Bàn", btnStyle)) SwitchToPreset(0);
        if (GUILayout.Button("[2] Bãi Cọc", btnStyle)) SwitchToPreset(1);
        if (GUILayout.Button("[3] Trại", btnStyle)) SwitchToPreset(2);
        if (GUILayout.Button("[4] Tướng", btnStyle)) SwitchToPreset(3);
        GUILayout.EndHorizontal();

        if (hasSettlementTour)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("[5] Làng", btnStyle)) SwitchToPreset(4);
            if (GUILayout.Button("[6] Bến cá", btnStyle)) SwitchToPreset(5);
            if (GUILayout.Button("[7] Trại", btnStyle)) SwitchToPreset(6);
            if (GUILayout.Button("[8] Xóm vườn", btnStyle)) SwitchToPreset(7);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4);
        GUILayout.Label("<i>[Tab/C] Bay tự do | [H] Ẩn/Hiện Menu | [Chuột phải] Xoay</i>", bodyStyle);

        GUILayout.EndArea();
    }
}
