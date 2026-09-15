using UnityEngine;

public class Camera_Script : MonoBehaviour
{
    [Header("Attributes Camera")]
    public Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0, 1.7f, -4f);
    private Quaternion rotation;

    //attributes for mouse movement
    private float x;
    private float y;
    [SerializeField] private float xSpeed = 5f;
    [SerializeField] private float ySpeed = 4f;

    [SerializeField] private float xMinRotation = -360f;
    [SerializeField] private float yMinRotation = 10f;
    [SerializeField] private float xMaxRotation = 360f;
    [SerializeField] private float yMaxRotation = 80f;




    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Vector3 angles = this.transform.eulerAngles;
        x= angles.y;
        y = angles.x;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void LateUpdate()
    {
        if (Input.GetKey(KeyCode.Q))
        {
            Cursor.visible = !Cursor.visible;
            Cursor.lockState = Cursor.visible ? CursorLockMode.None : CursorLockMode.Locked;
        }

            CameraMovement();
        rotation = Quaternion.Euler(y, x, 0);
        Vector3 distancevector = offset;
        Vector3 position = rotation * distancevector + target.position;
        transform.rotation = rotation;
        transform.position = position;
    }

    public void CameraMovement()
    {
        x += Input.GetAxis("Mouse X") * xSpeed;
        y -= Input.GetAxis("Mouse Y") * ySpeed;
        
        x = ClammAngle(x, xMinRotation, xMaxRotation);
        y = ClammAngle(y, yMinRotation, yMaxRotation);
    }

    public float ClammAngle(float angle, float min, float max)
    {
        if (angle < -360f)
        {
            angle += 360f;
        }
        if (angle > 360f)
        {
            angle -= 360f;
        }
        return Mathf.Clamp(angle, min, max);
    }
}
