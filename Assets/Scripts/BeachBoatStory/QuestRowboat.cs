using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>One scene-owned, kinematic quest boat. Reuses the player and Thai's rowing animation.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class QuestRowboat : MonoBehaviour
    {
        public static QuestRowboat Instance { get; private set; }
        public Transform boardingPoint, dockPoint, seat;
        public RuntimeAnimatorController rowingController;
        public Transform paddle;
        public Terrain terrain;
        public float maxSpeed = 95, acceleration = 38, turnSpeed = 48;
        public float boardingRange = 35, dockingRange = 90, minimumDepth = 12;
        public bool IsAboard { get; private set; }
        public float Speed { get; private set; }
        public float RowingRate => animator != null && IsAboard ? animator.speed : 0;
        public bool AtDock => dockPoint != null && FlatDistance(transform.position, dockPoint.position) <= dockingRange;
        BeachBoatQuestSession session;
        Character_Movement motor;
        Rigidbody playerBody;
        CapsuleCollider playerCollider;
        Animator animator;
        Transform leftHand, rightHand;
        RuntimeAnimatorController originalController;
        Camera_Script orbit;
        Vector3 cameraSettings;
        bool originalRootMotion, colliderEnabled, bodyKinematic, motorEnabled;
        Transform paddleParent;
        Vector3 paddlePosition, paddleScale;
        Quaternion paddleRotation;
        readonly Collider[] overlaps = new Collider[32];

        public static float FlatDistance(Vector3 a, Vector3 b)
        { a.y = b.y = 0; return Vector3.Distance(a, b); }

        void Awake() { Instance = this; }
        void Start()
        {
            session = BeachBoatQuestSession.Instance;
            if (session == null || boardingPoint == null || dockPoint == null || seat == null || rowingController == null)
            { Debug.LogError("Quest rowboat bindings incomplete.", this); enabled = false; return; }
            motor = session.player.GetComponent<Character_Movement>();
            playerBody = session.player.GetComponent<Rigidbody>();
            playerCollider = session.player.GetComponent<CapsuleCollider>();
            animator = session.player.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            { leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand); rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand); }
            orbit = session.storyCamera.GetComponent<Camera_Script>();
            if (paddle != null)
            {
                paddleParent = paddle.parent; paddlePosition = paddle.localPosition;
                paddleRotation = paddle.localRotation; paddleScale = paddle.localScale;
            }
        }

        public bool TryBoard()
        {
            var story = BachDangStoryManager.Instance;
            if (!isActiveAndEnabled || session == null || IsAboard || BeachBoatQuestSession.InteractionBlocked ||
                story == null || story.currentState != BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver ||
                story.collectedWood < story.totalWoodRequired || !AtDock || animator == null ||
                FlatDistance(session.player.transform.position, boardingPoint.position) > boardingRange) return false;
            motorEnabled = motor.enabled; bodyKinematic = playerBody.isKinematic; colliderEnabled = playerCollider.enabled;
            originalController = animator.runtimeAnimatorController; originalRootMotion = animator.applyRootMotion;
            cameraSettings = new Vector3(orbit.distance, orbit.heightOffset, orbit.rightOffset);
            motor.enabled = false;
            if (!playerBody.isKinematic) { playerBody.linearVelocity = Vector3.zero; playerBody.angularVelocity = Vector3.zero; }
            playerBody.isKinematic = true; playerCollider.enabled = false;
            animator.applyRootMotion = false; animator.runtimeAnimatorController = rowingController;
            animator.Rebind(); animator.Update(0); animator.speed = 0;
            IsAboard = true; Speed = 0;
            orbit.distance = 115; orbit.heightOffset = 42; orbit.rightOffset = 0;
            AttachPlayer(); orbit.SnapToTarget();
            story.SetInteractPrompt(false, owner: this); story.UpdateUI();
            return true;
        }

        public bool TryDisembark()
        {
            if (!IsAboard || !AtDock || BeachBoatQuestSession.InteractionBlocked || Mathf.Abs(Speed) > 5) return false;
            RestorePlayer();
            session.player.transform.SetPositionAndRotation(boardingPoint.position, boardingPoint.rotation);
            playerBody.position = boardingPoint.position; playerBody.rotation = boardingPoint.rotation;
            Physics.SyncTransforms(); orbit.SnapToTarget();
            BachDangStoryManager.Instance?.UpdateUI(); return true;
        }

        void RestorePlayer()
        {
            IsAboard = false; Speed = 0;
            if (animator != null) { animator.runtimeAnimatorController = originalController; animator.applyRootMotion = originalRootMotion; animator.speed = 1; animator.Rebind(); }
            if (motor != null) motor.enabled = motorEnabled && session.storyMode;
            if (playerBody != null) playerBody.isKinematic = bodyKinematic || !session.storyMode;
            if (playerCollider != null) playerCollider.enabled = colliderEnabled;
            if (orbit != null) { orbit.distance = cameraSettings.x; orbit.heightOffset = cameraSettings.y; orbit.rightOffset = cameraSettings.z; }
            if (paddle != null) { paddle.SetParent(paddleParent, false); paddle.localPosition = paddlePosition; paddle.localRotation = paddleRotation; paddle.localScale = paddleScale; }
        }

        void Update()
        {
            var story = BachDangStoryManager.Instance;
            if (story == null || session == null || BeachBoatQuestSession.InteractionBlocked) return;
            bool prompt = IsAboard ? AtDock && Mathf.Abs(Speed) <= 5 :
                story.currentState == BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver && AtDock &&
                FlatDistance(session.player.transform.position, boardingPoint.position) <= boardingRange;
            if (!prompt) return;
            story.SetInteractPrompt(true, IsAboard ? "[E] Rời thuyền lên bến" : "[E] Lên thuyền ra bãi cọc", this);
            if (Input.GetKeyDown(KeyCode.E) && StoryDialogueUI.TryConsumeInput())
            { if (IsAboard) TryDisembark(); else TryBoard(); }
        }

        void FixedUpdate()
        {
            bool input = IsAboard && !BeachBoatQuestSession.InputBlocked && Cursor.lockState == CursorLockMode.Locked;
            Step(input ? Input.GetAxisRaw("Vertical") : 0, input ? Input.GetAxisRaw("Horizontal") : 0, Time.fixedDeltaTime);
        }

        // Shared by live controls and editor validation; no allocation or scene scans per tick.
        public void Step(float throttle, float steering, float dt)
        {
            if (dt <= 0 || !isActiveAndEnabled) return;
            var position = transform.position; position.y = TideSystem.CurrentWaterHeight;
            transform.position = position;
            if (!IsAboard || BeachBoatQuestSession.InteractionBlocked)
            { Speed = 0; if (IsAboard) animator.speed = 0; return; }
            dt = Mathf.Min(dt, .05f);
            float speed = Mathf.MoveTowards(Speed, Mathf.Clamp(throttle, -1, 1) * maxSpeed, acceleration * dt);
            var rotation = transform.rotation * Quaternion.Euler(0, Mathf.Clamp(steering, -1, 1) * turnSpeed * dt, 0);
            var next = position + rotation * Vector3.forward * (speed * dt);
            if (CanOccupy(next, rotation)) { transform.SetPositionAndRotation(next, rotation); Speed = speed; }
            else Speed = 0;
            // Turning the stationary boat still uses the paddle; blocked translation never plays a full-speed stroke.
            float travelRate = Mathf.Abs(Speed) / Mathf.Max(1, maxSpeed);
            float turningRate = Quaternion.Angle(transform.rotation, rotation) < .01f ? Mathf.Abs(steering) * .35f : 0;
            animator.speed = Mathf.Max(travelRate, turningRate);
            AttachPlayer();
        }

        public bool CanOccupy(Vector3 position, Quaternion rotation)
        {
            for (int i = -1; i <= 1; i++)
            {
                var sample = position + rotation * Vector3.forward * (i * 26f);
                if (terrain != null)
                {
                    var local = sample - terrain.transform.position; var size = terrain.terrainData.size;
                    if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z ||
                        terrain.SampleHeight(sample) + terrain.transform.position.y + minimumDepth > TideSystem.CurrentWaterHeight) return false;
                }
                int count = Physics.OverlapSphereNonAlloc(sample + Vector3.up * 2, 11, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count == overlaps.Length) return false;
                for (int n = 0; n < count; n++)
                {
                    var hit = overlaps[n];
                    if (hit == null || hit is TerrainCollider || hit.transform.IsChildOf(transform) ||
                        (session != null && hit.transform.IsChildOf(session.player.transform))) continue;
                    return false;
                }
            }
            return true;
        }

        void AttachPlayer()
        {
            if (!IsAboard || session == null) return;
            session.player.transform.SetPositionAndRotation(seat.position, seat.rotation);
            playerBody.position = seat.position; playerBody.rotation = seat.rotation;
        }
        void LateUpdate()
        {
            AttachPlayer();
            if (IsAboard && paddle != null && leftHand != null && rightHand != null)
            {
                // Match both grips in the imported rowing pose instead of guessing the hand's local axes.
                var axis = (rightHand.position - leftHand.position).normalized;
                if (axis.sqrMagnitude > .5f)
                    paddle.SetPositionAndRotation(rightHand.position - axis * 14, Quaternion.FromToRotation(Vector3.up, axis));
            }
        }
        void OnDisable() { if (IsAboard) RestorePlayer(); }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
