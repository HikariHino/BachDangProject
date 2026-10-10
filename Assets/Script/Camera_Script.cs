using UnityEngine;

/// <summary>
/// Camera góc nhìn thứ 3 (Style PUBG / Over-the-shoulder)
/// Đã được cân chỉnh tỷ lệ cực lớn cho nhân vật Scale 7x7x7.
/// </summary>
public class Camera_Script : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("PUBG Camera Settings (For Scale 7x)")]
    [Tooltip("Khoảng cách từ camera đến nhân vật")]
    public float distance = 22f; 
    [Tooltip("Độ cao của camera so với chân nhân vật (ngang vai/đầu)")]
    public float heightOffset = 11f; 
    [Tooltip("Lệch sang phải bao nhiêu (Over-the-shoulder)")]
    public float rightOffset = 4.5f; 

    [Header("Mouse Sensitivity")]
    public float mouseXSpeed = 5f;
    public float mouseYSpeed = 3f;

    [Header("Vertical Clamp")]
    public float yMinAngle = -15f;
    public float yMaxAngle = 70f;

    [Header("Smoothing")]
    public float positionSmoothTime = 0.05f;

    // --- internal ---
    private float yaw;
    private float pitch;
    private Vector3 posVelocity;

    void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Start()
    {
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    void LateUpdate()
    {
        // Nhấn ESC để hiện/ẩn chuột
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }

        if (target == null)
        {
            var p = OpeningEvent.FindPlayerInScene();
            if (p != null) target = p.transform;
            if (target == null) return;
        }

        // Quay camera bằng chuột
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += Input.GetAxis("Mouse X") * mouseXSpeed;
            pitch -= Input.GetAxis("Mouse Y") * mouseYSpeed;
            pitch = Mathf.Clamp(pitch, yMinAngle, yMaxAngle);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        
        // Vị trí mỏ neo (ngang vai nhân vật)
        Vector3 anchorPoint = target.position + Vector3.up * heightOffset;
        
        // Lùi lại (distance) và sang phải (rightOffset) tạo góc PUBG
        Vector3 desiredPos = anchorPoint - (rotation * Vector3.forward * distance) + (rotation * Vector3.right * rightOffset);

        // Bám theo mượt mà
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref posVelocity, positionSmoothTime);
        
        // Nhìn thẳng về phía trước của anchor point
        transform.LookAt(anchorPoint + rotation * Vector3.forward * distance + rotation * Vector3.right * rightOffset);
    }
}
