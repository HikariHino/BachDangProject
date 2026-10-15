// Scene-local adaptation; original TestQuest implementation stays unchanged.
namespace BeachBoatStory
{
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class Character_Movement : MonoBehaviour
{
    [Header("Movement (world units; scene values match character scale)")]
    public float walkSpeed = 30f, runSpeed = 55f, rotationSpeed = 15f;
    public float acceleration = 120f, braking = 180f;
    public float jumpHeight = 5f, gravityMultiplier = 3f;
    public bool IsGrounded { get; private set; }
    Rigidbody body;
    CapsuleCollider capsule;
    Camera_Script orbit;
    Transform view;
    Vector2 input;
    bool sprint, aim, freeLook;
    bool risingFromJump;
    Vector3 groundNormal = Vector3.up;
    float jumpUntil = -1;
    readonly RaycastHit[] groundHits = new RaycastHit[24];
    readonly List<Animator> animators = new List<Animator>();
    readonly HashSet<Animator> walking = new HashSet<Animator>(), running = new HashSet<Animator>(), jumping = new HashSet<Animator>();
    static readonly int Walk = Animator.StringToHash("isWalking"), Run = Animator.StringToHash("isRunning"), Jump = Animator.StringToHash("jump");

    void Awake()
    {
        body = GetComponent<Rigidbody>(); capsule = GetComponent<CapsuleCollider>();
        body.constraints |= RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        if (!body.isKinematic) body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        foreach (var anim in GetComponentsInChildren<Animator>(true))
        {
            // Preserve valid rig/controller assignments and disabled weapon/model variants.
            if (anim.runtimeAnimatorController == null) anim.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Player_Neutral") ?? Resources.Load<RuntimeAnimatorController>("Sword_Anima");
            if (anim.runtimeAnimatorController == null) continue;
            anim.applyRootMotion = false; animators.Add(anim);
            foreach (var parameter in anim.parameters)
            {
                if (parameter.nameHash == Walk && parameter.type == AnimatorControllerParameterType.Bool) walking.Add(anim);
                if (parameter.nameHash == Run && parameter.type == AnimatorControllerParameterType.Bool) running.Add(anim);
                if (parameter.nameHash == Jump && parameter.type == AnimatorControllerParameterType.Trigger) jumping.Add(anim);
            }
        }
    }
    void Start()
    {
        var camera = Camera.main;
        if (camera != null) { view = camera.transform; orbit = camera.GetComponent<Camera_Script>(); }
    }
    void Update()
    {
        bool allowed = !BeachBoatQuestSession.InputBlocked && Cursor.lockState == CursorLockMode.Locked;
        input = allowed ? Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1) : Vector2.zero;
        aim = allowed && orbit != null && orbit.enableShoulderAim && Input.GetMouseButton(1);
        freeLook = allowed && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
        sprint = allowed && !aim && input.sqrMagnitude > .01f && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        if (allowed && Input.GetKeyDown(KeyCode.Space)) jumpUntil = Time.time + .15f;
        if (!allowed) jumpUntil = -1;
        UpdateAnimation(input.sqrMagnitude > .01f, sprint);
    }
    void FixedUpdate()
    {
        if (body == null || body.isKinematic) return;
        if (BeachBoatQuestSession.InteractionBlocked)
        {
            input = Vector2.zero; jumpUntil = -1; aim = false; freeLook = false; sprint = false;
            body.linearVelocity = new Vector3(0, body.linearVelocity.y, 0);
            UpdateAnimation(false, false);
        }
        float scale = Mathf.Max(.1f, Mathf.Abs(transform.lossyScale.y));
        IsGrounded = ProbeGround(scale);
        Vector3 forward = freeLook ? transform.forward : orbit != null ? Quaternion.Euler(0, orbit.Yaw, 0) * Vector3.forward : view != null ? view.forward : transform.forward;
        forward.y = 0; forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 direction = forward * input.y + right * input.x;
        Vector3 desired = direction * (sprint ? runSpeed : walkSpeed) * (aim ? .65f : 1f);
        Vector3 velocity = body.linearVelocity;
        float change = (input.sqrMagnitude > .01f ? acceleration : braking) * Time.fixedDeltaTime;
        if (IsGrounded)
        {
            // Drive along the support plane. Horizontal-only drive stalls against stair ramps.
            Vector3 alongGround = Vector3.ProjectOnPlane(desired, groundNormal).normalized * desired.magnitude;
            velocity = Vector3.MoveTowards(Vector3.ProjectOnPlane(velocity, groundNormal), alongGround, change);
        }
        else
        {
            Vector3 horizontal = Vector3.MoveTowards(new Vector3(velocity.x, 0, velocity.z), desired, change);
            velocity.x = horizontal.x; velocity.z = horizontal.z;
        }
        if (IsGrounded && jumpUntil >= Time.time)
        {
            velocity.y = Mathf.Sqrt(2 * Mathf.Abs(Physics.gravity.y) * Mathf.Max(1, gravityMultiplier) * Mathf.Max(.1f, jumpHeight));
            jumpUntil = -1; IsGrounded = false; risingFromJump = true;
            foreach (var anim in jumping) if (anim != null && anim.isActiveAndEnabled) anim.SetTrigger(Jump);
        }
        body.linearVelocity = velocity;
        if (body.useGravity && !IsGrounded) body.AddForce(Physics.gravity * (Mathf.Max(1, gravityMultiplier) - 1), ForceMode.Acceleration);
        if (direction.sqrMagnitude > .001f || aim)
        {
            Vector3 facing = aim ? forward : direction;
            if (facing.sqrMagnitude > .001f) body.MoveRotation(Quaternion.Slerp(body.rotation, Quaternion.LookRotation(facing), 1 - Mathf.Exp(-rotationSpeed * Time.fixedDeltaTime)));
        }
    }
    bool ProbeGround(float scale)
    {
        // Ascending a slope is still grounded; only an actual jump suppresses support checks.
        if (risingFromJump && body.linearVelocity.y > 0) return false;
        risingFromJump = false;
        var bounds = capsule.bounds;
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * .9f;
        float skin = .08f * scale;
        var center = new Vector3(bounds.center.x, bounds.min.y + radius + skin, bounds.center.z);
        int count = Physics.SphereCastNonAlloc(center, Mathf.Max(.01f, radius), Vector3.down, groundHits, skin + .12f * scale, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        groundNormal = Vector3.up;
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (Vector3.Dot(hit.normal, Vector3.up) >= .65f && hit.distance < nearest)
            { nearest = hit.distance; groundNormal = hit.normal; }
        }
        return !float.IsPositiveInfinity(nearest);
    }
    void UpdateAnimation(bool moving, bool fast)
    {
        foreach (var anim in animators)
        {
            if (anim == null || !anim.isActiveAndEnabled) continue;
            if (walking.Contains(anim)) anim.SetBool(Walk, moving);
            if (running.Contains(anim)) anim.SetBool(Run, fast);
        }
    }
    void OnDisable()
    {
        input = Vector2.zero; jumpUntil = -1; risingFromJump = false;
        if (body != null && !body.isKinematic) body.linearVelocity = new Vector3(0, body.linearVelocity.y, 0);
        UpdateAnimation(false, false);
    }
}

}
