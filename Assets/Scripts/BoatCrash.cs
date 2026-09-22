using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BoatCrash : MonoBehaviour
{
    [Header("Tốc độ bơi của thuyền")]
    public float speed = 5f;

    [Header("Cao độ nổi so với mặt nước (mét)")]
    public float waterFloatOffset = 0.2f;
    
    [Header("Kịch bản đắm tàu Titanic")]
    [Tooltip("Tốc độ chìm sâu xuống nước")]
    public float sinkSpeed = 1.0f;

    [Tooltip("Góc cắm mũi Titanic tối đa (độ)")]
    public float maxNoseDiveAngle = 35f;

    [Tooltip("Thời gian chìm trước khi dừng hẳn ở tư thế nửa chìm (giây)")]
    public float sinkDuration = 3.0f;

    [Header("Khoảng cách quét chướng ngại vật")]
    public float forwardCheckDistance = 2.0f;
    
    private bool isSinking = false;
    private bool isGroundedOnShore = false;
    private float sinkTimer = 0f;
    private Quaternion initialSinkingRot;
    private Quaternion targetSinkingRot;
    private Vector3 initialSinkingPos;
    private Rigidbody rb;
    private Collider col;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // KHÓA TOÀN BỘ TRỤC XOAY VẬT LÝ: 
        // Tuyệt đối không bao giờ bị va chạm vật lý làm quay mòng mòng như compa nữa!
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
    }

    void FixedUpdate()
    {
        // 1. KỊCH BẢN ĐẮM THUYỀN TITANIC (DÙNG QUATERNION SLERP - TUYỆT ĐỐI KHÔNG QUAY MÒNG MÒNG)
        if (isSinking)
        {
            if (sinkTimer < sinkDuration)
            {
                sinkTimer += Time.fixedDeltaTime;
                float progress = Mathf.Clamp01(sinkTimer / sinkDuration);

                // Chìm từ từ xuống đáy
                transform.position += Vector3.down * (sinkSpeed * Time.fixedDeltaTime);

                // Cắm mũi xuống nước mượt mà và dừng lại đúng góc maxNoseDiveAngle
                transform.rotation = Quaternion.Slerp(initialSinkingRot, targetSinkingRot, progress);
            }
            return;
        }

        // 2. NẾU ĐÃ MẮC CẠN VÀO BỜ ĐẤT
        if (isGroundedOnShore)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        // 3. THUYỀN NỔI BẬP BỀNH THEO MỰC NƯỚC THỦY TRIỀU
        float currentWaterY = TideSystem.CurrentWaterHeight;
        float targetY = currentWaterY + waterFloatOffset;

        Vector3 moveDir = transform.right; // Hướng bơi về phía trước (+X)
        Vector3 nextPos = rb.position + moveDir * (speed * Time.fixedDeltaTime);
        nextPos.y = Mathf.Lerp(rb.position.y, targetY, Time.fixedDeltaTime * 5f); // Nổi êm dịu theo sóng biển

        // Quét Raycast phía trước xem có bờ đất không
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

        // 1. ĐÂM TRÚNG CỌC GỖ -> KÍCH HOẠT ĐẮM THUYỀN TITANIC
        if (hitName.Contains("spike") || hitName.Contains("wood") || hitName.Contains("cocgo"))
        {
            if (!isSinking) 
            {
                Debug.Log("💥 RẦM! TRÚNG CỌC GỖ! MŨI CẮM XUỐNG NƯỚC, ĐUÔI NHỔNG LÊN TRỜI!");
                isSinking = true;
                rb.isKinematic = true;

                // Ghi nhận góc quay xuất phát
                initialSinkingRot = transform.rotation;
                initialSinkingPos = transform.position;

                // Tính toán chính xác góc cắm mũi xuống nước (quay quanh trục mạn thuyền)
                Vector3 pitchAxis = Vector3.Cross(Vector3.up, transform.right).normalized;
                targetSinkingRot = Quaternion.AngleAxis(maxNoseDiveAngle, pitchAxis) * initialSinkingRot;

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
        for (int i = 0; i < 25; i++)
        {
            GameObject manhGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            
            // Xóa ngay collider của mảnh vụn để không húc vào thân thuyền
            Collider col = manhGo.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            manhGo.transform.position = toaDoDam + new Vector3(
                Random.Range(-1f, 1f), 
                Random.Range(0f, 2f), 
                Random.Range(-1f, 1f));
            manhGo.transform.localScale = new Vector3(
                Random.Range(0.1f, 0.3f), 
                Random.Range(0.1f, 0.5f), 
                Random.Range(0.5f, 1.5f));
            
            var r = manhGo.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.4f, 0.2f, 0.1f);

            Rigidbody rbManhGo = manhGo.AddComponent<Rigidbody>();
            rbManhGo.AddExplosionForce(500f, toaDoDam, 4f, 2f); 
            Destroy(manhGo, 3f);
        }
    }
}
