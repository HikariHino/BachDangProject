using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;

    void Update()
    {
        // Lấy nút bấm W A S D (hoặc phím mũi tên)
        float moveX = Input.GetAxis("Horizontal"); // A và D
        float moveZ = Input.GetAxis("Vertical");   // W và S

        // Tạo hướng di chuyển
        Vector3 move = new Vector3(moveX, 0, moveZ);

        // Cập nhật vị trí của Cube
        transform.Translate(move * speed * Time.deltaTime, Space.World);
    }
}