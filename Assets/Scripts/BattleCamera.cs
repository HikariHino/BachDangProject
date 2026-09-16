using UnityEngine;

/// <summary>
/// Camera đa năng cho chiến trường Bạch Đằng:
/// 1. MẶC ĐỊNH: Tự động bám theo con Thuyền (Third-person Cinema View)
///    - Chuột phải: Xoay 360 độ quanh thuyền
///    - Con lăn chuột (Scroll): Phóng to / Thu nhỏ (Zoom In/Out)
/// 2. CHẾ ĐỘ BAY TỰ DO (Free-Fly Mode): Bấm phím [C] hoặc [Tab] để bật/tắt
///    - W/A/S/D: Bay tới, lùi, trái, phải
///    - Q/E hoặc Space: Nâng độ cao / Hạ độ cao
///    - Giữ Chuột phải: Xoay hướng nhìn tự do
///    - Giữ Shift: Bay tăng tốc độ
/// </summary>
public class BattleCamera : MonoBehaviour
{
    public enum CameraMode { FollowBoat, FreeFly }

    [Header("Chế độ Camera")]
    public CameraMode mode = CameraMode.FollowBoat;

    [Header("Mục tiêu theo dõi (Tự động tìm nếu để trống)")]
    public Transform boatTarget;

    [Header("Cài đặt Bám theo Thuyền")]
    public float distance = 25f;
    public float height = 8f;
    public float smoothSpeed = 5f;
    public float rotationSpeed = 3f;
    public float zoomSpeed = 5f;
    public float minDistance = 8f;
    public float maxDistance = 60f;

    [Header("Cài đặt Bay tự do (Free-Fly)")]
    public float flySpeed = 30f;
    public float fastFlyMultiplier = 2.5f;
    public float freeLookSensitivity = 3f;

    private float currentYaw = 0f;
    private float currentPitch = 15f;
    private float freeYaw = 0f;
    private float freePitch = 20f;

    void Start()
    {
        FindTargetIfNull();

        Vector3 angles = transform.eulerAngles;
        freeYaw = angles.y;
        freePitch = angles.x;
        currentYaw = angles.y;
    }

    void FindTargetIfNull()
    {
        if (boatTarget == null)
        {
            var boat = FindFirstObjectByType<BoatCrash>();
            if (boat != null)
            {
                boatTarget = boat.transform;
            }
            else
            {
                var all = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var go in all)
                {
                    string n = go.name.ToLower();
                    if (n.Contains("sketchfab") || n.Contains("junk") || n.Contains("boat"))
                    {
                        boatTarget = go.transform;
                        break;
                    }
                }
            }
        }
    }

    void Update()
    {
        // Phím tắt đổi chế độ: C hoặc Tab
        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.Tab))
        {
            mode = (mode == CameraMode.FollowBoat) ? CameraMode.FreeFly : CameraMode.FollowBoat;
            Debug.Log($"🎥 Đổi chế độ Camera sang: {mode} (Bấm C hoặc Tab để chuyển đổi)");
        }

        if (mode == CameraMode.FreeFly)
        {
            UpdateFreeFly();
        }
    }

    void LateUpdate()
    {
        if (mode == CameraMode.FollowBoat)
        {
            UpdateFollowBoat();
        }
    }

    // --- 1. CHẾ ĐỘ BÁM THEO THUYỀN ---
    void UpdateFollowBoat()
    {
        if (boatTarget == null)
        {
            FindTargetIfNull();
            if (boatTarget == null) return;
        }

        // Giữ chuột phải để xoay camera quanh thuyền
        if (Input.GetMouseButton(1))
        {
            currentYaw += Input.GetAxis("Mouse X") * rotationSpeed;
            currentPitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
            currentPitch = Mathf.Clamp(currentPitch, 2f, 80f);
        }

        // Con lăn chuột để Zoom xa gần
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance = Mathf.Clamp(distance - scroll * zoomSpeed * 10f, minDistance, maxDistance);
        }

        // Tính toán vị trí camera phía sau thuyền
        Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 targetPos = boatTarget.position;
        Vector3 desiredPos = targetPos - (rot * Vector3.forward * distance) + Vector3.up * height;

        // Di chuyển mượt mà
        transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * smoothSpeed);
        transform.LookAt(targetPos + Vector3.up * 2f);
    }

    // --- 2. CHẾ ĐỘ BAY TỰ DO (SPECTATOR) ---
    void UpdateFreeFly()
    {
        // Xoay góc nhìn khi giữ chuột phải
        if (Input.GetMouseButton(1))
        {
            freeYaw += Input.GetAxis("Mouse X") * freeLookSensitivity;
            freePitch -= Input.GetAxis("Mouse Y") * freeLookSensitivity;
            freePitch = Mathf.Clamp(freePitch, -85f, 85f);
            transform.rotation = Quaternion.Euler(freePitch, freeYaw, 0f);
        }

        // Di chuyển W/A/S/D
        float speed = flySpeed * (Input.GetKey(KeyCode.LeftShift) ? fastFlyMultiplier : 1f);
        Vector3 move = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) move += transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= transform.forward;
        if (Input.GetKey(KeyCode.A)) move -= transform.right;
        if (Input.GetKey(KeyCode.D)) move += transform.right;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) move += Vector3.up;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) move -= Vector3.up;

        transform.position += move * speed * Time.deltaTime;
    }
}
