using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [Header("Tốc độ quay của Mặt Trời (Độ/giây)")]
    [Tooltip("10 = chậm thực tế | 50 = xem nhanh | 200 = chóng mặt")]
    public float timeSpeed = 20f;

    void Update()
    {
        transform.Rotate(Vector3.right * timeSpeed * Time.deltaTime);
    }
}
