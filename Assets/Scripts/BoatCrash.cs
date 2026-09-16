using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BoatCrash : MonoBehaviour
{
    [Header("Tốc độ bơi của thuyền")]
    public float speed = 5f;
    
    [Header("Kịch bản đắm tàu Titanic")]
    [Tooltip("Tốc độ chìm xuống nước (mét/giây)")]
    public float sinkSpeed = 1.2f;

    [Tooltip("Tốc độ chúi mũi xuống đáy (Độ/giây)")]
    public float noseDiveSpeed = 25f;

    [Tooltip("Góc cắm mũi tối đa (độ) - 30 đến 40 độ là chuẩn Titanic")]
    public float maxNoseDiveAngle = 35f;

    [Tooltip("Thời gian chìm trước khi dừng ở tư thế nửa chìm nửa nổi (giây)")]
    public float sinkDuration = 2.5f;

    [Header("Khoảng cách quét chướng ngại vật phía trước")]
    public float forwardCheckDistance = 2.0f;
    
    private bool isSinking = false;
    private bool isGroundedOnShore = false;
    private float sinkTimer = 0f;
    private float currentPitchAngle = 0f;
    private Rigidbody rb;
    private Collider col;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void FixedUpdate()
    {
        if (isSinking)
        {
            if (sinkTimer < sinkDuration)
            {
                sinkTimer += Time.fixedDeltaTime;

                // 1. Hạ chìm nửa thân tàu xuống nước
                transform.Translate(Vector3.down * sinkSpeed * Time.fixedDeltaTime, Space.World);

                // 2. Chúi mũi thuyền xuống nước (Xoay quanh trục ngang vuông góc hướng bơi)
                // Tuyệt đối không bị lật nghiêng mạn thuyền nữa
                if (currentPitchAngle < maxNoseDiveAngle)
                {
                    float angleStep = noseDiveSpeed * Time.fixedDeltaTime;
                    // Trục ngang vuông góc với hướng bơi (transform.right) và bầu trời (Vector3.up)
                    Vector3 pitchAxis = Vector3.Cross(transform.right, Vector3.up).normalized;
                    transform.Rotate(pitchAxis, angleStep, Space.World);
                    currentPitchAngle += angleStep;
                }
            }
            return;
        }

        if (isGroundedOnShore)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        // Hướng bơi của thuyền (+X theo mô hình)
        Vector3 moveDir = transform.right;
        Vector3 nextPos = rb.position + moveDir * speed * Time.fixedDeltaTime;

        // Quét Raycast phía trước xem có chạm bờ đất không
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 1f, moveDir, out hit, forwardCheckDistance))
        {
            if (hit.collider.GetComponent<Terrain>() != null || hit.collider.GetComponent<TerrainCollider>() != null || hit.collider.CompareTag("Terrain"))
            {
                Debug.Log($"🛑 THUYỀN ĐÂM VÀO BỜ ĐẤT: Mắc cạn kiên cố tại bờ sông!");
                isGroundedOnShore = true;
                rb.linearVelocity = Vector3.zero;
                return;
            }
        }

        rb.MovePosition(nextPos);
    }

    void OnCollisionEnter(Collision collision)
    {
        string hitName = collision.gameObject.name.ToLower();

        // 1. ĐÂM TRÚNG CỌC GỖ -> KỊCH BẢN TITANIC
        if (hitName.Contains("spike") || hitName.Contains("wood"))
        {
            if (!isSinking) 
            {
                Debug.Log("💥 RẦM! THUYỀN ĐÂM TRÚNG CỌC GỖ! MŨI CẮM XUỐNG, ĐUÔI NHỔNG LÊN!");
                isSinking = true;
                rb.isKinematic = true; 
                TaoManhGoVang(collision.contacts[0].point);
            }
        }
        // 2. ĐÂM VÀO BỜ ĐẤT -> MẮC CẠN
        else if (collision.gameObject.GetComponent<Terrain>() != null || 
                 collision.gameObject.GetComponent<TerrainCollider>() != null ||
                 hitName.Contains("terrain") || hitName.Contains("saban"))
        {
            Debug.Log($"🛑 THUYỀN MẮC CẠN VÀO BỜ ĐẤT!");
            isGroundedOnShore = true;
            rb.linearVelocity = Vector3.zero;
        }
    }

    void TaoManhGoVang(Vector3 toaDoDam)
    {
        for (int i = 0; i < 30; i++)
        {
            GameObject manhGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            manhGo.transform.position = toaDoDam + new Vector3(
                Random.Range(-1f, 1f), 
                Random.Range(0f, 2f), 
                Random.Range(-1f, 1f));
            manhGo.transform.localScale = new Vector3(
                Random.Range(0.1f, 0.3f), 
                Random.Range(0.1f, 0.5f), 
                Random.Range(0.5f, 1.5f));
            try { manhGo.GetComponent<Renderer>().material.color = new Color(0.4f, 0.2f, 0.1f); } catch {}
            Rigidbody rbManhGo = manhGo.AddComponent<Rigidbody>();
            rbManhGo.AddExplosionForce(800f, toaDoDam, 5f, 3f); 
            Destroy(manhGo, 3f);
        }
    }
}
