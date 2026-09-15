using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class BoatCrash : MonoBehaviour
{
    [Header("Tốc độ bơi của thuyền")]
    public float speed = 5f;
    
    [Header("Tốc độ chìm xuống")]
    public float sinkSpeed = 1.5f;

    [Header("Tốc độ chúi mũi (Độ/giây) - Tăng lên cho giống Titanic")]
    public float noseDiveSpeed = 60f;

    [Header("Thuyền chìm bao nhiêu giây thì dừng")]
    public float sinkDuration = 2.0f;
    
    private bool isSinking = false;
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
            transform.Translate(Vector3.right * speed * Time.deltaTime);
        }
        else
        {
            if (sinkTimer < sinkDuration) 
            {
                // Chìm xuống
                transform.Translate(Vector3.down * sinkSpeed * Time.deltaTime, Space.World);
                // Chúi mũi xuống như Titanic (Thử forward/back/right nếu bị lật ngang)
                transform.Rotate(Vector3.back * noseDiveSpeed * Time.deltaTime); 
                sinkTimer += Time.deltaTime;
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name.ToLower().Contains("spike") || 
            collision.gameObject.name.ToLower().Contains("wood"))
        {
            if (!isSinking) 
            {
                Debug.Log("💥 RẦM! TRÚNG CỌC! THUYỀN NAM HÁN ĐANG CHÚI MŨI CHÌM!");
                isSinking = true;
                GetComponent<Rigidbody>().isKinematic = true; 
                TaoManhGoVang(collision.contacts[0].point);
            }
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
