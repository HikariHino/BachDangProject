using UnityEngine;

/// <summary>
/// Camera góc nhìn thứ 3 (Third-Person):
/// - Giữ chuột phải để xoay camera xung quanh nhân vật
/// - Camera bám mượt theo nhân vật bằng SmoothDamp
/// - Không bị NullRef khi player chưa spawn
/// </summary>
public class Camera_Script : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Offset & Distance")]
    [SerializeField] private float distance = 5f;
    [SerializeField] private float heightOffset = 1.8f;   // độ cao nhìn vào nhân vật

    [Header("Mouse Sensitivity")]
    [SerializeField] private float mouseXSpeed = 4f;
    [SerializeField] private float mouseYSpeed = 3f;

    [Header("Vertical Clamp")]
    [SerializeField] private float yMinAngle = -10f;
    [SerializeField] private float yMaxAngle = 70f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.08f;

    // --- internal ---
    private float yaw;    // xoay ngang (Y world axis)
    private float pitch;  // góc ngẩng/cúi
    private Vector3 posVelocity;

    void Awake()
    {
        TryFindTarget();
        // Khóa con trỏ ngay khi vào game
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Start()
    {
        if (target == null) TryFindTarget();
        // Khởi tạo góc từ góc hiện tại của camera
        yaw   = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    private void TryFindTarget()
    {
        if (target != null) return;
        var p = OpeningEvent.FindPlayerInScene();
        if (p != null) target = p.transform;
    }

    void LateUpdate()
    {
        // Toggle cursor: giữ Escape để mở/khoá con trỏ
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible   = locked;
        }

        if (target == null)
        {
            TryFindTarget();
            if (target == null) return;
        }

        // Chỉ xoay camera khi con trỏ đang bị khóa
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw   += Input.GetAxis("Mouse X") * mouseXSpeed;
            pitch -= Input.GetAxis("Mouse Y") * mouseYSpeed;
            pitch  = Mathf.Clamp(pitch, yMinAngle, yMaxAngle);
        }

        // Tính vị trí camera từ góc + distance + height offset
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 lookAtPoint = target.position + Vector3.up * heightOffset;
        Vector3 desiredPos  = lookAtPoint - rotation * Vector3.forward * distance;

        // Smooth follow
        Vector3 smoothedPos = Vector3.SmoothDamp(transform.position, desiredPos, ref posVelocity, positionSmoothTime);
        transform.position  = smoothedPos;
        transform.LookAt(lookAtPoint);
    }
}
