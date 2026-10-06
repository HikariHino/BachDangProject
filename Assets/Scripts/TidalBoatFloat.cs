using UnityEngine;

/// <summary>
/// Gentle tide following for moored and ambush boats. Never drives combat physics.
/// Attach to the boat root so its crew moves with the deck.
/// </summary>
[DisallowMultipleComponent]
public class TidalBoatFloat : MonoBehaviour
{
    public TideSystem tideSystem;

    [Tooltip("Boat root height above the water. Capture after placing the boat correctly.")]
    public float waterlineOffset = 0.15f;

    [Min(0f)] public float bobAmplitude = 0.04f;
    [Min(0.1f)] public float bobPeriod = 5.5f;
    [Range(0f, 2f)] public float rollDegrees = 0.35f;

    [SerializeField, HideInInspector] private bool waterlineCaptured;

    private Quaternion restRotation;
    private Vector3 restPosition;
    private float motionTime;
    private float phase;
    private bool initialized;

    void Reset()
    {
        tideSystem = FindAnyObjectByType<TideSystem>();
        CaptureWaterline();
    }

    [ContextMenu("Capture current waterline")]
    public void CaptureWaterline()
    {
        if (tideSystem == null) tideSystem = FindAnyObjectByType<TideSystem>();
        if (tideSystem == null) return;

        waterlineOffset = transform.position.y - tideSystem.transform.position.y;
        waterlineCaptured = true;
    }

    void Start()
    {
        // A combat boat or a dynamic body must retain its own movement system.
        if (GetComponentInParent<BoatCrash>() != null || GetComponentInChildren<BoatCrash>() != null)
        {
            enabled = false;
            return;
        }

        Rigidbody body = GetComponentInParent<Rigidbody>();
        if (body != null && !body.isKinematic)
        {
            enabled = false;
            return;
        }

        foreach (Rigidbody childBody in GetComponentsInChildren<Rigidbody>())
        {
            if (!childBody.isKinematic)
            {
                enabled = false;
                return;
            }
        }

        if (tideSystem == null) tideSystem = FindAnyObjectByType<TideSystem>();
        if (tideSystem == null)
        {
            enabled = false;
            return;
        }

        if (!waterlineCaptured) CaptureWaterline();
        restRotation = transform.rotation;
        restPosition = transform.position;
        // Keep the authored phase when the editor scales world positions. The
        // serialized waterline offset and bob amplitude are already converted.
        float worldScale = BachDangWorldScale.ForScene(gameObject.scene);
        phase = Mathf.Repeat((restPosition.x * 0.173f + restPosition.z * 0.117f) / worldScale, Mathf.PI * 2f);
        initialized = true;
    }

    void LateUpdate()
    {
        if (!initialized || tideSystem == null) return;

        motionTime += Time.deltaTime;
        float angle = motionTime * Mathf.PI * 2f / Mathf.Max(0.1f, bobPeriod) + phase;
        float fadeIn = Mathf.Clamp01(motionTime / 2f);

        Vector3 position = transform.position;
        position.y = tideSystem.transform.position.y + waterlineOffset
            + Mathf.Sin(angle) * bobAmplitude * fadeIn;
        transform.position = position;
        transform.rotation = restRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(angle * 0.8f) * rollDegrees * fadeIn);
    }

    void OnDisable()
    {
        if (!initialized) return;
        transform.rotation = restRotation;
        Vector3 position = transform.position;
        position.y = tideSystem != null ? tideSystem.transform.position.y + waterlineOffset : restPosition.y;
        transform.position = position;
    }
}
