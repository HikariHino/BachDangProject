using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BoatCrash : MonoBehaviour
{
    [Header("Tốc độ bơi của thuyền")]
    public float speed = 5f;
    
    [Header("Tốc độ chìm xuống")]
    public float sinkSpeed = 1.5f;

    [Header("Tốc độ chúi mũi (Độ/giây)")]
    public float noseDiveSpeed = 60f;

    [Header("Thuyền chìm bao nhiêu giây thì dừng")]
    public float sinkDuration = 2.0f;

    [Header("Khoảng cách quét chướng ngại vật phía trước")]
    public float forwardCheckDistance = 2.0f;
    
    private bool isSinking = false;
    private bool isGroundedOnShore = false; // Mắc cạn vào bờ đất
    private float sinkTimer = 0f; 
    private Rigidbody rb;
    private Collider col;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        
        rb.useGravity = false;
        // Bật Continuous Dynamic để tuyệt đối không bao giờ xuyên thủng vật cản hay bờ đất
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
    }

    void FixedUpdate()
    {
        if (isSinking)
        {
            if (sinkTimer < sinkDuration)
            {
                // Hạ chìm xuống
                transform.Translate(Vector3.down * sinkSpeed * Time.fixedDeltaTime, Space.World);
                // Chúi mũi cắm xuống
                transform.Rotate(Vector3.back * noseDiveSpeed * Time.fixedDeltaTime);
                sinkTimer += Time.fixedDeltaTime;
            }
            return;
        }

        if (isGroundedOnShore)
        {
            // Thuyền đã đâm vào bờ đất -> Mắc cạn đứng im, không thể xuyên qua đất
            rb.linearVelocity = Vector3.zero;
            return;
        }

        // Hướng mũi thuyền di chuyển (Vector3.right theo model của thuyền)
        Vector3 moveDir = transform.right;
        Vector3 nextPos = rb.position + moveDir * speed * Time.fixedDeltaTime;

        // Quét Raycast phía trước xem có bờ đất / vật cản không
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 1f, moveDir, out hit, forwardCheckDistance))
        {
            // Nếu phía trước là Terrain (Đất liền)
            if (hit.collider.GetComponent<Terrain>() != null || hit.collider.GetComponent<TerrainCollider>() != null || hit.collider.CompareTag("Terrain"))
            {
                Debug.Log($"🛑 THUYỀN ĐÃ ĐÂM VÀO BỜ ĐẤT '{hit.collider.name}'! Mắc cạn vững chắc, không thể đi xuyên qua đất!");
                isGroundedOnShore = true;
                rb.linearVelocity = Vector3.zero;
                return;
            }
        }

        // Di chuyển bằng vật lý Rigidbody.MovePosition (Đảm bảo gặp vật cứng là dừng, không lún)
        rb.MovePosition(nextPos);
    }

    void OnCollisionEnter(Collision collision)
    {
        string hitName = collision.gameObject.name.ToLower();

        // 1. ĐÂM VÀO CỌC GỖ -> KỊCH BẢN ĐẮM THUYỀN TITANIC
        if (hitName.Contains("spike") || hitName.Contains("wood"))
        {
            if (!isSinking) 
            {
                Debug.Log("💥 RẦM! TRÚNG CỌC! THUYỀN NAM HÁN ĐANG CHÚI MŨI CHÌM!");
                isSinking = true;
                rb.isKinematic = true; 
                TaoManhGoVang(collision.contacts[0].point);
            }
        }
        // 2. ĐÂM VÀO BỜ ĐẤT / TERRAIN -> MẮC CẠN, CẤM XUYÊN QUA
        else if (collision.gameObject.GetComponent<Terrain>() != null || 
                 collision.gameObject.GetComponent<TerrainCollider>() != null ||
                 hitName.Contains("terrain") || hitName.Contains("saban"))
        {
            Debug.Log($"🛑 THUYỀN ĐÂM VÀO BỜ ĐẤT: {collision.gameObject.name}! Mắc cạn tại bờ sông!");
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
