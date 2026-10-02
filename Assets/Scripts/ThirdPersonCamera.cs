using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 5.0f;
    public float xSpeed = 120.0f;
    public float ySpeed = 120.0f;
    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    private float x = 0.0f;
    private float y = 0.0f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target)
        {
            x += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
            y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;

            y = ClampAngle(y, yMinLimit, yMaxLimit);

            Quaternion rotation = Quaternion.Euler(y, x, 0);

            // Tự động tính toán chiều cao và khoảng cách dựa trên kích thước thật của nhân vật
            float targetHeight = 1.5f;
            float targetDistance = distance;
            
            Collider col = target.GetComponent<Collider>();
            if (col != null)
            {
                targetHeight = col.bounds.extents.y; // Nửa chiều cao (Tương đương ngực/mặt)
                targetDistance = distance * (col.bounds.size.y / 2f); // Tỉ lệ thuận với độ to
            }
            
            Vector3 negDistance = new Vector3(0.0f, 0.0f, -targetDistance);
            Vector3 position = rotation * negDistance + target.position + Vector3.up * targetHeight;

            transform.rotation = rotation;
            transform.position = position;
        }
    }

    public static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F) angle += 360F;
        if (angle > 360F) angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
}
