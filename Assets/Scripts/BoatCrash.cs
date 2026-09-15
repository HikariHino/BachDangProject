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
    
    // Thêm bộ đếm thời gian để canh lúc dừng chìm
    private float sinkTimer = 0f; 

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
            // Tiến lên phía trước 
            transform.Translate(Vector3.right * speed * Time.deltaTime);
        }
        else
        {
            // KỊCH BẢN CHÌM NỬA TÀU: Chỉ chìm trong đúng 1.5 giây rồi dừng
            if (sinkTimer < 1.5f) 
            {
                // Hạ trục Y xuống để chìm
                transform.Translate(Vector3.down * sinkSpeed * Time.deltaTime, Space.World);
                // Xoay trục X để mũi thuyền cắm xuống đáy sông
                transform.Rotate(Vector3.right * 15f * Time.deltaTime); 
                
                // Tăng bộ đếm thời gian
                sinkTimer += Time.deltaTime;
            }
            // Hết 1.5 giây thì vòng if này không chạy nữa -> Thuyền kẹt cứng ở tư thế Nửa chìm nửa nổi!
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name.ToLower().Contains("spike") || collision.gameObject.name.ToLower().Contains("wood"))
        {
            if (!isSinking) 
            {
                Debug.Log("💥 RẦM! TRÚNG CỌC RỒI! THUYỀN NAM HÁN ĐANG CHÌM NỬA TÀU!");
                isSinking = true;
                GetComponent<Rigidbody>().isKinematic = true; 

                TaoManhGoVang(collision.contacts[0].point);
            }
        }
    }

    // --- HÀM TẠO ẢO GIÁC MẢNH VỠ ---
    void TaoManhGoVang(Vector3 toaDoDam)
    {
        for (int i = 0; i < 30; i++)
        {
            GameObject manhGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            manhGo.transform.position = toaDoDam + new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 2f), Random.Range(-1f, 1f));
            manhGo.transform.localScale = new Vector3(Random.Range(0.1f, 0.3f), Random.Range(0.1f, 0.5f), Random.Range(0.5f, 1.5f));
            try { manhGo.GetComponent<Renderer>().material.color = new Color(0.4f, 0.2f, 0.1f); } catch {}
            Rigidbody rbManhGo = manhGo.AddComponent<Rigidbody>();
            rbManhGo.AddExplosionForce(800f, toaDoDam, 5f, 3f); 
            Destroy(manhGo, 3f);
        }
    }
}
