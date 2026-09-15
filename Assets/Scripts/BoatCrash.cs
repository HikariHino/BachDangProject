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
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.useGravity = false; 
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous; 
    }

    void Update()
    {
        if (!isSinking)
        {
            // Tiến lên phía trước (Sửa right/left/forward tùy ý sếp)
            transform.Translate(Vector3.right * speed * Time.deltaTime);
        }
        else
        {
            // Từ từ chìm xuống và nghiêng mũi
            transform.Translate(Vector3.down * sinkSpeed * Time.deltaTime, Space.World);
            transform.Rotate(Vector3.right * 15f * Time.deltaTime); 
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name.ToLower().Contains("spike") || collision.gameObject.name.ToLower().Contains("wood"))
        {
            if (!isSinking) // Đảm bảo chỉ văng gỗ 1 lần duy nhất
            {
                Debug.Log("💥 RẦM! TRÚNG CỌC RỒI! THUYỀN NAM HÁN ĐANG CHÌM!");
                isSinking = true;
                GetComponent<Rigidbody>().isKinematic = true; 

                // Gọi tuyệt chiêu văng mảnh gỗ!
                // collision.contacts[0].point chính là "Tọa độ 2 vật chạm vào nhau"
                TaoManhGoVang(collision.contacts[0].point);
            }
        }
    }

    // --- HÀM TẠO ẢO GIÁC MẢNH VỠ ---
    void TaoManhGoVang(Vector3 toaDoDam)
    {
        // Đẻ ra 30 mảnh gỗ tung tóe
        for (int i = 0; i < 30; i++)
        {
            // 1. Nặn ra một khối lập phương (Tượng trưng cho mảnh ván)
            GameObject manhGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            
            // 2. Đặt nó ngay tại cái điểm đâm nhau (cho nó rải rác lộn xộn ra tí)
            manhGo.transform.position = toaDoDam + new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 2f), Random.Range(-1f, 1f));
            
            // 3. Bóp méo khối lập phương thành hình dăm gỗ (ốm, dài, dẹt ngẫu nhiên)
            manhGo.transform.localScale = new Vector3(Random.Range(0.1f, 0.3f), Random.Range(0.1f, 0.5f), Random.Range(0.5f, 1.5f));
            
            // 4. Sơn màu nâu gỗ cho dăm ván
            try {
                manhGo.GetComponent<Renderer>().material.color = new Color(0.4f, 0.2f, 0.1f);
            } catch {}
            
            // 5. Gắn Vật lý cho mảnh gỗ để nó biết rớt
            Rigidbody rbManhGo = manhGo.AddComponent<Rigidbody>();
            
            // 6. KÍCH NỔ! Dùng lực hất văng mảnh gỗ lộn nhào lên trời
            rbManhGo.AddExplosionForce(800f, toaDoDam, 5f, 3f); // Lực nổ 800, hất lên cao 3f
            
            // 7. Xóa sổ mảnh gỗ sau 3 giây để máy không bị giật lag
            Destroy(manhGo, 3f);
        }
    }
}
