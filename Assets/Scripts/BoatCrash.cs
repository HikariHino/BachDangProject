using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class BoatCrash : MonoBehaviour
{
    [Header("Tốc độ bơi của thuyền")]
    public float speed = 5f;
    
    [Header("Tốc độ chìm")]
    public float sinkSpeed = 2f;
    
    private bool isSinking = false;

    void Start()
    {
        // Tự động cài đặt Vật lý cho thuyền (để sếp đỡ phải bấm bằng tay)
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.useGravity = false; // Tắt trọng lực để thuyền không rớt xuyên mặt nước
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // Bắt va chạm cực nhạy
    }

    void Update()
    {
        if (!isSinking)
        {
            // Cho thuyền chạy thẳng về phía trước
            // (Nếu thuyền chạy ngang hoặc lùi, sếp có thể sửa Vector3.forward thành Vector3.back hoặc Vector3.left)
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }
        else
        {
            // Bị lủng bụng! Từ từ chìm xuống đáy sông và nghiêng mũi thuyền
            transform.Translate(Vector3.down * sinkSpeed * Time.deltaTime, Space.World);
            transform.Rotate(Vector3.right * 15f * Time.deltaTime); 
        }
    }

    // Hàm này sẽ tự kích hoạt khi thuyền đụng trúng vật cản
    void OnCollisionEnter(Collision collision)
    {
        // Kiểm tra xem cái vật bị đụng tên có chứa chữ "spike" hoặc "wood" không
        if (collision.gameObject.name.ToLower().Contains("spike") || collision.gameObject.name.ToLower().Contains("wood"))
        {
            Debug.Log("💥 RẦM! TRÚNG CỌC RỒI! THUYỀN NAM HÁN ĐANG CHÌM!");
            isSinking = true;
            
            // Khóa cứng vật lý để thuyền không bị bật văng ra sau
            GetComponent<Rigidbody>().isKinematic = true; 
        }
    }
}
