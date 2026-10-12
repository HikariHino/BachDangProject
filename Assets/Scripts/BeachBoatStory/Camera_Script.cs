// Scene-local adaptation; original TestQuest implementation stays unchanged.
namespace BeachBoatStory
{
using UnityEngine;

/// <summary>One orbit camera, independent of character rotation, with shoulder view and obstruction handling.</summary>
[RequireComponent(typeof(Camera))]
public class Camera_Script : MonoBehaviour
{
    public Transform target;
    public float distance = 22f, heightOffset = 11f, rightOffset = 4.5f;
    public float mouseXSpeed = 2f, mouseYSpeed = 1.5f;
    public float yMinAngle = -25f, yMaxAngle = 70f;
    public float positionSmoothTime = .06f;
    public LayerMask obstructionLayers = Physics.DefaultRaycastLayers;
    public float collisionRadius = 1.2f;
    public float aimDistance = 14f, aimFieldOfView = 48f;
    public float Yaw => yaw;
    public bool IsAiming { get; private set; }
    public bool IsFreeLooking { get; private set; }
    public bool InputCaptured => Cursor.lockState == CursorLockMode.Locked && !BeachBoatQuestSession.InputBlocked;
    float yaw, pitch = 12f, normalFieldOfView, nextTargetSearch;
    bool initialized;
    Vector3 velocity, previousTarget;
    Camera view;
    readonly RaycastHit[] hits = new RaycastHit[32];

    void Awake() { view = GetComponent<Camera>(); normalFieldOfView = view.fieldOfView; }
    void Start()
    {
        if (target == null) { var p = OpeningEvent.FindPlayerInScene(); if (p != null) target = p.transform; }
        if (!BeachBoatQuestSession.InputBlocked) CaptureCursor(true);
        SnapToTarget();
    }
    public void CaptureCursor(bool capture)
    { Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !capture; }
    public void SnapToTarget()
    {
        if (target == null) return;
        yaw = target.eulerAngles.y; pitch = 12f; velocity = Vector3.zero;
        previousTarget = target.position; initialized = true;
        ApplyPose(true);
    }
    void LateUpdate()
    {
        if (target == null)
        {
            if (Time.unscaledTime < nextTargetSearch) return;
            nextTargetSearch = Time.unscaledTime + .5f;
            var p = OpeningEvent.FindPlayerInScene(); if (p != null) target = p.transform;
            if (target == null) return;
        }
        if (!initialized) SnapToTarget();
        if (!BeachBoatQuestSession.InputBlocked && Input.GetKeyDown(KeyCode.Escape)) CaptureCursor(!InputCaptured);
        // Click to resume after unlocking; the opening itself never skips on mouse input.
        if (!BeachBoatQuestSession.InputBlocked && !InputCaptured && Input.GetMouseButtonDown(0) &&
            (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())) CaptureCursor(true);
        IsAiming = InputCaptured && Input.GetMouseButton(1);
        IsFreeLooking = InputCaptured && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
        if (InputCaptured)
        {
            yaw += Input.GetAxisRaw("Mouse X") * mouseXSpeed;
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * mouseYSpeed, yMinAngle, yMaxAngle);
        }
        bool teleported = (target.position - previousTarget).sqrMagnitude > distance * distance * 4;
        ApplyPose(teleported); previousTarget = target.position;
    }
    void ApplyPose(bool snap)
    {
        var rotation = Quaternion.Euler(pitch, yaw, 0);
        var anchor = target.position + Vector3.up * heightOffset;
        float back = IsAiming ? aimDistance : distance;
        var desired = anchor + rotation * new Vector3(rightOffset, 0, -back);
        desired = ResolveObstruction(anchor, desired);
        var smoothed = snap ? desired : Vector3.SmoothDamp(transform.position, desired, ref velocity, Mathf.Max(.01f, positionSmoothTime));
        // Recheck after smoothing so easing away from a wall cannot cut through it.
        transform.SetPositionAndRotation(ResolveObstruction(anchor, smoothed), rotation);
        if (snap) velocity = Vector3.zero;
        if (view != null) view.fieldOfView = Mathf.Lerp(view.fieldOfView, IsAiming ? aimFieldOfView : normalFieldOfView, 1 - Mathf.Exp(-12 * Time.deltaTime));
    }
    Vector3 ResolveObstruction(Vector3 anchor, Vector3 destination)
    {
        var offset = destination - anchor; float length = offset.magnitude;
        if (length < .001f) return destination;
        int count = Physics.SphereCastNonAlloc(anchor, Mathf.Max(.1f, collisionRadius), offset / length, hits, length, obstructionLayers, QueryTriggerInteraction.Ignore);
        float allowed = length;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider == null || hits[i].transform == target || hits[i].transform.IsChildOf(target)) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(.1f, hits[i].distance - .2f));
        }
        return anchor + offset / length * allowed;
    }
    void OnDisable() { if (Application.isPlaying) CaptureCursor(false); }
}

}
