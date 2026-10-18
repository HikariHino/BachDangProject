using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Phát âm thanh chân đi cho mọi nhân vật (Player, Enemy).
/// 
/// 2 chế độ hoạt động:
/// 1. Animation Event (khuyên dùng): Gọi Footstep() ở frame chân chạm đất trong clip Walk/Run.
///    -> Đồng bộ hoàn hảo, không cần config gì thêm.
/// 2. Fallback Timer: Nếu clip không có event, tự phát theo khoảng thời gian dựa trên tốc độ.
/// 
/// Tránh lặp nhàm chán: MỘT MẢNG clips duy nhất, luân phiên (round-robin).
/// - Đi bộ: volume thấp, pitch thấp, khoảng cách bước lớn hơn
/// - Chạy:  volume cao, pitch cao, khoảng cách bước nhỏ hơn (tần suất dày hơn)
/// -> Không cần tách file walk/run riêng.
/// 
/// Setup nhanh:
/// - Thêm component vào GameObject nhân vật (cùng cấp Animator).
/// - Size > 0 cho Footstep Clips → kéo các file audio vào (3-4 file là tốt).
/// - (Tuỳ chọn) Thêm Animation Event "Footstep" vào clip Walk/Run.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Animator))]
public class FootstepSound : MonoBehaviour
{
    [Header("Audio Clips (Single pool - walk/run dùng chung, khác volume/pitch)")]
    [Tooltip("Các âm thanh bước chân - sẽ luân phiên phát (clip 0, 1, 2... quay lại 0). Dùng chung cho walk & run.")]
    public AudioClip[] footstepClips = new AudioClip[0];

    [Header("Walk Settings")]
    [Range(0f, 1f)] public float walkVolume = 0.4f;
    [Range(0.5f, 1.2f)] public float walkPitch = 0.9f;
    [Tooltip("Khoảng cách (m) giữa 2 bước chân khi WALK")]
    public float walkStepDistance = 0.8f;

    [Header("Run Settings")]
    [Range(0f, 1f)] public float runVolume = 0.7f;
    [Range(0.8f, 1.5f)] public float runPitch = 1.15f;
    [Tooltip("Khoảng cách (m) giữa 2 bước chân khi RUN - nhỏ hơn = bước nhanh hơn")]
    public float runStepDistance = 0.55f;

    [Header("Variation (tránh máy móc)")]
    [Range(0f, 0.15f)] public float volumeVariation = 0.05f;
    [Range(0f, 0.15f)] public float pitchVariation = 0.08f;

    [Header("Fallback Timer (khi KHÔNG dùng Animation Event)")]
    [Tooltip("Bật để tự phát âm thanh theo timer nếu animation không có event Footstep")]
    public bool useTimerFallback = true;

    [Header("Surface Detection (Optional)")]
    [Tooltip("Layer của ground để raycast detect surface type")]
    public LayerMask groundLayer = 1;
    [Tooltip("Tag của ground object (nếu không dùng layer)")]
    public string groundTag = "Ground";

    // Components
    private AudioSource _audioSource;
    private Animator _animator;
    private NavMeshAgent _agent;
    private Rigidbody _rb;
    private CharacterController _cc;

    // State
    private float _timer = 0f;
    private bool _wasGrounded = true;

    // Round-robin index
    private int _clipIndex = 0;

    // Animator hashes
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private static readonly int IsRunningHash = Animator.StringToHash("isRunning");

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _animator = GetComponent<Animator>();
        _agent = GetComponent<NavMeshAgent>();
        _rb = GetComponent<Rigidbody>();
        _cc = GetComponent<CharacterController>();
        if (_rb == null) _rb = GetComponentInParent<Rigidbody>();
        if (_cc == null) _cc = GetComponentInParent<CharacterController>();

        // Cấu hình AudioSource cho SFX 3D
        _audioSource.spatialBlend = 1f;        // 3D sound
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.maxDistance = 20f;
        _audioSource.playOnAwake = false;
    }

    void Update()
    {
        if (!useTimerFallback) return;

        // Chỉ chạy timer khi KHÔNG dùng Animation Event
        float speed = GetCurrentSpeed();
        if (speed < 0.1f) return;

        bool isRunning = IsRunning();
        float stepDist = isRunning ? runStepDistance : walkStepDistance;
        float interval = stepDist / Mathf.Max(0.1f, speed);

        _timer += Time.deltaTime;
        if (_timer >= interval)
        {
            _timer = 0f;
            PlayFootstep(isRunning);
        }
    }

    /// <summary>
    /// GỌI TỪ ANIMATION EVENT ở frame chân chạm đất (Walk/Run clip).
    /// Đây là cách chính xác nhất - không phụ thuộc timer.
    /// </summary>
    public void Footstep()
    {
        if (useTimerFallback) return; // Nếu bật timer fallback thì bỏ qua event (tránh phát 2 lần)

        bool isRunning = IsRunning();
        PlayFootstep(isRunning);
    }

    /// <summary>
    /// GỌI TỪ ANIMATION EVENT với tham số rõ ràng (nếu muốn tách event Walk/Run).
    /// </summary>
    public void FootstepWalk() => PlayFootstep(false);
    public void FootstepRun()  => PlayFootstep(true);

    private void PlayFootstep(bool isRunning)
    {
        if (footstepClips == null || footstepClips.Length == 0) return;

        // Lấy clip tiếp theo (round-robin)
        AudioClip clip = footstepClips[_clipIndex];
        _clipIndex = (_clipIndex + 1) % footstepClips.Length; // Quay vòng

        if (clip == null) return;

        // Walk vs Run: khác volume, pitch, không khác clip
        float baseVol = isRunning ? runVolume : walkVolume;
        float basePitch = isRunning ? runPitch : walkPitch;

        float vol = baseVol + Random.Range(-volumeVariation, volumeVariation);
        float pitch = basePitch + Random.Range(-pitchVariation, pitchVariation);

        vol = Mathf.Clamp01(vol);
        pitch = Mathf.Max(0.5f, pitch);

        _audioSource.pitch = pitch;
        _audioSource.PlayOneShot(clip, vol);
    }

    private float GetCurrentSpeed()
    {
        // 1. NavMeshAgent (Enemy)
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            return _agent.velocity.magnitude;

        // 2. CharacterController (Player dùng CC)
        if (_cc != null)
            return _cc.velocity.magnitude;

        // 3. Rigidbody
        if (_rb != null && !_rb.isKinematic)
            return new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z).magnitude;

        // 4. Animator parameter MoveSpeed (fallback)
        if (_animator != null && HasParameter(_animator, MoveSpeedHash))
            return _animator.GetFloat(MoveSpeedHash);

        // beachBoat's imported character controller exposes walking/running as bools.
        // The Animator lives below the Rigidbody, so use these parameters as a stable fallback.
        if (_animator != null)
        {
            if (HasParameter(_animator, IsRunningHash) && _animator.GetBool(IsRunningHash)) return 6f;
            if (HasParameter(_animator, IsWalkingHash) && _animator.GetBool(IsWalkingHash)) return 2.5f;
        }

        return 0f;
    }

    private bool IsRunning()
    {
        // Ưu tiên Animator Bool (EnemyAttack/Character_Movement đều set isRunning)
        if (_animator != null)
        {
            if (HasParameter(_animator, IsRunningHash) && _animator.GetBool(IsRunningHash)) return true;
            if (HasParameter(_animator, IsWalkingHash) && _animator.GetBool(IsWalkingHash)) return false;
            // Fallback: dùng MoveSpeed threshold
            if (HasParameter(_animator, MoveSpeedHash))
                return _animator.GetFloat(MoveSpeedHash) > 1.5f; // tùy config animator
        }

        // Fallback: tốc độ thực
        float speed = GetCurrentSpeed();
        return speed > 3.5f; // ngưỡng walk/run mặc định
    }

    /// <summary>
    /// Gọi từ code khác nếu muốn ép phát 1 bước chân (ví dụ: landing, jump)
    /// </summary>
    public void ForceFootstep(bool isRunning = false) => PlayFootstep(isRunning);

    /// <summary>
    /// Kiểm tra Animator có parameter với hash cho trước (Unity không có hasParameter method)
    /// </summary>
    private static bool HasParameter(Animator animator, int paramHash)
    {
        foreach (var p in animator.parameters)
        {
            if (p.nameHash == paramHash) return true;
        }
        return false;
    }
}
