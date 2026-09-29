using UnityEngine;

/// <summary>
/// Bảng Điều Khiển Sa Bàn Chiến Trận Bạch Đằng 938 (Interactive Battle HUD & Director)
/// Hỗ trợ chuyển đổi nhanh góc quay camera, điều khiển thủy triều và kích hoạt diễn biến chiến trận.
/// </summary>
public class SaBanHUD : MonoBehaviour
{
    public static SaBanHUD Instance;

    [Header("Ẩn/Hiện Bảng Điều Khiển [H]")]
    public bool showHUD = true;

    private TideSystem tideSystem;
    private BoatCrash enemyBoat;
    private BattleCamera battleCam;

    // Các vị trí quan sát điện ảnh (Cinematic Camera Viewpoints)
    private readonly struct CamPreset
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

    private CamPreset[] presets;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        tideSystem = FindAnyObjectByType<TideSystem>();
        enemyBoat = FindAnyObjectByType<BoatCrash>();
        battleCam = Camera.main != null ? Camera.main.GetComponent<BattleCamera>() : null;

        presets = new CamPreset[]
        {
            // 1. Toàn cảnh Sa bàn từ trên cao (Birds-eye tactical view)
            new CamPreset("1. Toàn Cảnh Sa Bàn", new Vector3(-450f, 160f, -80f), new Vector3(-350f, 14f, 300f)),
            // 2. Cận cảnh bãi cọc & thuyền giặc Nam Hán
            new CamPreset("2. Bãi Cọc & Thuyền Giặc", new Vector3(-470f, 26f, 300f), new Vector3(-350f, 14f, 300f)),
            // 3. Đại Bản Doanh Tướng Ngô Quyền
            new CamPreset("3. Đại Bản Doanh Ngô Quyền", new Vector3(-770f, 22f, 305f), new Vector3(-808f, 18f, 320f)),
            // 4. Xưởng Rèn Cọc Lim & Bến Thuyền
            new CamPreset("4. Xưởng Rèn & Bến Thuyền", new Vector3(-640f, 22f, 290f), new Vector3(-675f, 17.5f, 255f))
        };
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
    }

    public void SwitchToPreset(int index)
    {
        if (index < 0 || index >= presets.Length) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        if (battleCam != null)
        {
            battleCam.mode = BattleCamera.CameraMode.FreeFly;
        }

        var p = presets[index];
        cam.transform.position = p.position;
        cam.transform.LookAt(p.lookAt);
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

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = 12;
        btnStyle.fontStyle = FontStyle.Bold;
        btnStyle.fixedHeight = 28;

        float panelWidth = 360f;
        float panelHeight = 240f;
        GUILayout.BeginArea(new Rect(15, 15, panelWidth, panelHeight), panelStyle);

        GUILayout.Label("⚔️ ĐIỀU KHIỂN SA BÀN BẠCH ĐẰNG 938", headerStyle);
        GUILayout.Space(4);

        // THỦY TRIỀU
        float waterY = tideSystem != null ? tideSystem.transform.position.y : 14f;
        string tideDesc = waterY > 13.5f ? "<color=#00e676>▲ TRIỀU DÂNG (Ngập đầu cọc)</color>" : "<color=#ff5252>▼ TRIỀU RÚT (Lộ bãi cọc nhọn)</color>";
        GUILayout.Label($"🌊 Mực nước: <b>{waterY:F1}m</b> - Trạng thái: {tideDesc}", bodyStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🌊 Đảo Chiều Thủy Triều [T]", btnStyle))
        {
            if (tideSystem != null)
            {
                tideSystem.autoCycle = false;
                tideSystem.transform.position = new Vector3(
                    tideSystem.transform.position.x,
                    waterY > 13.5f ? tideSystem.lowTideY : tideSystem.highTideY,
                    tideSystem.transform.position.z
                );
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // GÓC QUAY CAMERA
        GUILayout.Label("🎥 <b>Chọn Góc Quan Sát Điện Ảnh [1-4]:</b>", bodyStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[1] Sa Bàn", btnStyle)) SwitchToPreset(0);
        if (GUILayout.Button("[2] Bãi Cọc", btnStyle)) SwitchToPreset(1);
        if (GUILayout.Button("[3] Bản Doanh", btnStyle)) SwitchToPreset(2);
        if (GUILayout.Button("[4] Xưởng Rèn", btnStyle)) SwitchToPreset(3);
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("<i>[Tab/C] Bay tự do | [H] Ẩn/Hiện Menu | [Chuột phải] Xoay</i>", bodyStyle);

        GUILayout.EndArea();
    }
}
