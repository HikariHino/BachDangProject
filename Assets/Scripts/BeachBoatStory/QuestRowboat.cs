using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>One scene-owned, kinematic quest boat. Reuses the player and Thai's rowing animation.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class QuestRowboat : MonoBehaviour
    {
        public enum BoatObstacleKind { None, Shore, Tree, Rock, Ship, Structure }
        public static QuestRowboat Instance { get; private set; }
        public Transform boardingPoint, dockPoint, seat;
        public RuntimeAnimatorController rowingController;
        public Transform paddle;
        public Terrain terrain;
        public TideSystem tide;
        public float maxSpeed = 95, acceleration = 38, turnSpeed = 48;
        public float sprintMultiplier = 1.55f;
        public float sprintStaminaPerSecond = 12f;
        public float boardingRange = 35, dockingRange = 90, minimumDepth = 12;
        [Header("Hull clearance")]
        public float hullProbeSpacing = 26, hullProbeRadius = 11;
        public bool IsAboard { get; private set; }
        public float Speed { get; private set; }
        public bool IsBoosting { get; private set; }
        public float RowingRate => animator != null && IsAboard ? animator.speed : 0;
        public bool AtDock => dockPoint != null && FlatDistance(transform.position, dockPoint.position) <= dockingRange;
        BeachBoatQuestSession session;
        Character_Movement motor;
        PlayerVitals vitals;
        AmbientRiverBoat[] nearbyFriendlyBoats;
        EnemyShip[] enemyShips;
        Vector3 homePosition;
        Quaternion homeRotation;
        float nextImpactTime;
        bool battleLaunchPrepared, pendingBattleLaunch;
        float nextLaunchAttempt, wedgedAtDockSeconds;
        Rigidbody playerBody;
        CapsuleCollider playerCollider;
        Animator animator;
        Transform leftHand, rightHand;
        Transform leftUpperArm, leftForearm, rightUpperArm, rightForearm;
        float strokePhase;
        public Vector3 BladeCenter { get; private set; }
        RuntimeAnimatorController originalController;
        Camera_Script orbit;
        Vector3 cameraSettings;
        bool originalRootMotion, colliderEnabled, bodyKinematic, motorEnabled;
        Transform paddleParent;
        Vector3 paddlePosition, paddleScale;
        Quaternion paddleRotation;
        RigidbodyInterpolation originalInterpolation;
        AnimatorUpdateMode originalUpdateMode;
        AnimatorCullingMode originalCullingMode;
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
            vitals = session.player.GetComponent<PlayerVitals>();
            nearbyFriendlyBoats = FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            enemyShips = EnemyFleetDirector.Instance != null ? EnemyFleetDirector.Instance.enemyShips : System.Array.Empty<EnemyShip>();
            homePosition = transform.position; homeRotation = transform.rotation;
            playerBody = session.player.GetComponent<Rigidbody>();
            playerCollider = session.player.GetComponent<CapsuleCollider>();
            animator = session.player.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand); rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm); leftForearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm); rightForearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            }
            orbit = session.storyCamera.GetComponent<Camera_Script>();
            if (tide == null) tide = FindAnyObjectByType<TideSystem>();
            if (paddle != null)
            {
                paddleParent = paddle.parent; paddlePosition = paddle.localPosition;
                paddleRotation = paddle.localRotation; paddleScale = paddle.localScale;
            }
            if (BachDangStoryManager.Instance != null && BachDangStoryManager.Instance.currentState == BachDangStoryManager.StoryState.Act3_WaitForLowTide)
                PrepareForBattle();
        }

        /// <summary>Discard Act 2 parking and find a dock pose with a clear, deep-water exit.</summary>
        public bool PrepareForBattle()
        {
            if (session == null || dockPoint == null) return false;
            battleLaunchPrepared = false;
            Physics.SyncTransforms();
            Vector3 origin = homePosition; origin.y = TideSystem.CurrentWaterHeight;
            for (int ring = 0; ring <= 6; ring++)
            for (int direction = 0; direction < (ring == 0 ? 1 : 16); direction++)
            {
                var candidate = origin + Quaternion.Euler(0, direction * 22.5f, 0) * Vector3.forward * (ring * 14f);
                if (FlatDistance(candidate, dockPoint.position) > dockingRange - 1) continue;
                for (int heading = 0; heading < 7; heading++)
                {
                    float angle = heading == 0 ? 0 : ((heading + 1) / 2) * 45 * (heading % 2 == 0 ? -1 : 1);
                    var rotation = homeRotation * Quaternion.Euler(0, angle, 0);
                    if (!HasClearLaunch(candidate, rotation)) continue;
                    transform.SetPositionAndRotation(candidate, rotation);
                    var body = GetComponent<Rigidbody>();
                    if (body != null) { body.position = candidate; body.rotation = rotation; }
                    Speed = 0; IsBoosting = false; strokePhase = 0; wedgedAtDockSeconds = 0;
                    battleLaunchPrepared = true; pendingBattleLaunch = false;
                    Physics.SyncTransforms();
                    if (IsAboard) { AttachPlayer(); orbit.SnapToTarget(); }
                    return true;
                }
            }
            pendingBattleLaunch = true; nextLaunchAttempt = Time.unscaledTime + .5f;
            return false;
        }

        bool HasClearLaunch(Vector3 position, Quaternion rotation)
        {
            for (int level = 0; level < (tide != null ? 3 : 1); level++)
            {
                var surface = position;
                if (level == 1) surface.y = tide.lowTideY;
                if (level == 2) surface.y = tide.highTideY;
                for (float distance = 0; distance <= 120; distance += 8)
                    if (!CanOccupy(surface + rotation * Vector3.forward * distance, rotation)) return false;
            }
            return true;
        }

        public bool TryBoard()
        {
            var story = BachDangStoryManager.Instance;
            if (story != null && story.currentState == BachDangStoryManager.StoryState.Act3_WaitForLowTide && !IsAboard &&
                (!battleLaunchPrepared || !HasClearLaunch(transform.position, transform.rotation)))
                if (!PrepareForBattle()) return false;
            if (!isActiveAndEnabled || session == null || IsAboard || BeachBoatQuestSession.InteractionBlocked ||
                story == null || !CanBoardForState(story.currentState) ||
                story.collectedWood < story.totalWoodRequired || !AtDock || animator == null ||
                FlatDistance(session.player.transform.position, boardingPoint.position) > boardingRange) return false;
            motorEnabled = motor.enabled; bodyKinematic = playerBody.isKinematic; colliderEnabled = playerCollider.enabled;
            originalController = animator.runtimeAnimatorController; originalRootMotion = animator.applyRootMotion;
            originalInterpolation = playerBody.interpolation;
            originalUpdateMode = animator.updateMode; originalCullingMode = animator.cullingMode;
            cameraSettings = new Vector3(orbit.distance, orbit.heightOffset, orbit.rightOffset);
            motor.enabled = false;
            if (!playerBody.isKinematic) { playerBody.linearVelocity = Vector3.zero; playerBody.angularVelocity = Vector3.zero; }
            playerBody.isKinematic = true; playerCollider.enabled = false;
            playerBody.interpolation = RigidbodyInterpolation.None;
            animator.updateMode = AnimatorUpdateMode.Normal; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            // Changing the controller is enough here: the character hierarchy did not change.
            // Forcing Rebind plus a synchronous Animator.Update during the boarding input frame
            // unnecessarily rebuilds native animation bindings just as the battle starts.
            animator.runtimeAnimatorController = rowingController;
            animator.speed = .1f;
            IsAboard = true; Speed = 0; strokePhase = 0;
            BeachBoatSoundscape.Instance?.PlayInteraction();
            orbit.distance = 115; orbit.heightOffset = 42; orbit.rightOffset = 0;
            AttachPlayer(); orbit.SnapToTarget();
            if (story.currentState == BachDangStoryManager.StoryState.Act3_WaitForLowTide) story.OnReadyForBattle();
            story.SetInteractPrompt(false, owner: this); story.UpdateUI();
            return true;
        }

        public bool TryDisembark()
        {
            if (!IsAboard || !AtDock || BeachBoatQuestSession.InteractionBlocked || Mathf.Abs(Speed) > 5) return false;
            BeachBoatSoundscape.Instance?.PlayInteraction();
            RestorePlayer();
            session.player.transform.SetPositionAndRotation(boardingPoint.position, boardingPoint.rotation);
            playerBody.position = boardingPoint.position; playerBody.rotation = boardingPoint.rotation;
            Physics.SyncTransforms(); orbit.SnapToTarget();
            BachDangStoryManager.Instance?.UpdateUI(); return true;
        }

        /// <summary>Bring the camera down behind the rowing soldier while keeping full mouse look.</summary>
        public void UseSoldierBattleCamera()
        {
            if (!IsAboard || orbit == null) return;
            // Keep the boat's docked heading. Rotating an 80 m hull instantly toward
            // the fleet while it still overlaps the pier swings it through the quay.
            // The player can steer out normally with A/D and look around with the mouse.
            orbit.distance = 48f;
            orbit.heightOffset = 20f;
            orbit.rightOffset = 7f;
            orbit.enableShoulderAim = false;
            orbit.enabled = true;
            orbit.CaptureCursor(true);
            orbit.SnapToTarget();
        }

        bool CanBoardForState(BachDangStoryManager.StoryState state) =>
            state == BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver ||
            (state == BachDangStoryManager.StoryState.Act3_WaitForLowTide &&
             EnemyFleetDirector.Instance != null && EnemyFleetDirector.Instance.IsConfigured &&
             RiverStakeZone.Instance != null && RiverStakeZone.Instance.IsComplete) ||
            state == BachDangStoryManager.StoryState.Act3_BattleInProgress || state == BachDangStoryManager.StoryState.Act3_Victory;

        void RestorePlayer()
        {
            IsAboard = false; Speed = 0; IsBoosting = false;
            if (animator != null) { animator.runtimeAnimatorController = originalController; animator.applyRootMotion = originalRootMotion; animator.speed = 1; }
            if (animator != null) { animator.updateMode = originalUpdateMode; animator.cullingMode = originalCullingMode; }
            if (motor != null) motor.enabled = motorEnabled && session.storyMode;
            if (playerBody != null) { playerBody.isKinematic = bodyKinematic || !session.storyMode; playerBody.interpolation = originalInterpolation; }
            if (playerCollider != null) playerCollider.enabled = colliderEnabled;
            if (orbit != null) { orbit.distance = cameraSettings.x; orbit.heightOffset = cameraSettings.y; orbit.rightOffset = cameraSettings.z; }
            if (paddle != null) { paddle.SetParent(paddleParent, false); paddle.localPosition = paddlePosition; paddle.localRotation = paddleRotation; paddle.localScale = paddleScale; }
        }

        void Update()
        {
            var story = BachDangStoryManager.Instance;
            if (pendingBattleLaunch && Time.unscaledTime >= nextLaunchAttempt && story != null &&
                (story.currentState == BachDangStoryManager.StoryState.Act3_WaitForLowTide || story.currentState == BachDangStoryManager.StoryState.Act3_BattleInProgress))
                PrepareForBattle();
            if (tide != null)
            {
                bool building = story != null && (story.currentState == BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver ||
                    story.currentState == BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp);
                if (building) tide.HoldLowTide(this); else tide.ReleaseLowTide(this);
            }
            // Kinematic navigation and the seated player advance once per rendered frame,
            // before Animator evaluation and the orbit camera, instead of stepping at 50 Hz.
            // Cursor lock only controls mouse look. Losing it (Esc, a UI overlay, or
            // Editor focus) must not strand the player aboard during the battle.
            bool input = IsAboard && !BeachBoatQuestSession.InputBlocked;
            bool boost = input && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            Step(input ? Input.GetAxisRaw("Vertical") : 0, input ? Input.GetAxisRaw("Horizontal") : 0, Time.deltaTime, boost);
            if (story == null || session == null || BeachBoatQuestSession.InteractionBlocked) return;
            bool prompt = IsAboard ? AtDock && Mathf.Abs(Speed) <= 5 :
                CanBoardForState(story.currentState) && AtDock &&
                FlatDistance(session.player.transform.position, boardingPoint.position) <= boardingRange;
            if (!prompt) return;
            story.SetInteractPrompt(true, IsAboard ? "[E] Rời thuyền lên bến" : "[E] Lên thuyền ra bãi cọc", this);
            if (Input.GetKeyDown(KeyCode.E) && StoryDialogueUI.TryConsumeInput())
            { if (IsAboard) TryDisembark(); else TryBoard(); }
        }

        // Shared by live controls and editor validation; no allocation or scene scans per tick.
        public void Step(float throttle, float steering, float dt, bool boostWanted = false)
        {
            if(RiverPlayerCombat.Instance!=null&&RiverPlayerCombat.Instance.IsAiming){throttle=0;steering=0;boostWanted=false;}
            if (dt <= 0 || !isActiveAndEnabled) return;
            var position = transform.position; position.y = TideSystem.CurrentWaterHeight;
            transform.position = position;
            if (!IsAboard || BeachBoatQuestSession.InteractionBlocked)
            { Speed = 0; IsBoosting = false; if (IsAboard) { animator.speed = 0; AttachPlayer(); } return; }
            dt = Mathf.Min(dt, .05f);
            IsBoosting = boostWanted && Mathf.Abs(throttle) > .1f &&
                (vitals == null || vitals.TryExert(sprintStaminaPerSecond, dt));
            float topSpeed = maxSpeed * (IsBoosting ? sprintMultiplier : 1f);
            float speed = Mathf.MoveTowards(Speed, Mathf.Clamp(throttle, -1, 1) * topSpeed,
                acceleration * (IsBoosting ? 1.55f : 1f) * dt);
            var rotation = transform.rotation * Quaternion.Euler(0, Mathf.Clamp(steering, -1, 1) * turnSpeed * dt, 0);
            var next = position + rotation * Vector3.forward * (speed * dt);
            if (TryCanOccupy(next, rotation, out var obstacle)) { transform.SetPositionAndRotation(next, rotation); Speed = speed; wedgedAtDockSeconds = 0; }
            else
            {
                var state = BachDangStoryManager.Instance != null ? BachDangStoryManager.Instance.currentState : BachDangStoryManager.StoryState.Act1_ExploreCamp;
                bool nearQuay = AtDock || (boardingPoint != null && FlatDistance(position, boardingPoint.position) <= dockingRange + hullProbeRadius * 2);
                bool wedged = nearQuay && !CanOccupy(position, transform.rotation) &&
                    (state == BachDangStoryManager.StoryState.Act3_WaitForLowTide || state == BachDangStoryManager.StoryState.Act3_BattleInProgress);
                if (wedged && Mathf.Abs(throttle) > .1f)
                {
                    wedgedAtDockSeconds += dt;
                    if (wedgedAtDockSeconds >= .75f) PrepareForBattle();
                }
                bool knockedOut = !wedged && HandleImpact(obstacle, Mathf.Max(Mathf.Abs(Speed), Mathf.Abs(speed)));
                Speed = 0; IsBoosting = false;
                if (knockedOut) return;
            }
            // Turning the stationary boat still uses the paddle; blocked translation never plays a full-speed stroke.
            float travelRate = Mathf.Abs(Speed) / Mathf.Max(1, maxSpeed);
            float turningRate = Quaternion.Angle(transform.rotation, rotation) < .01f ? Mathf.Abs(steering) * .35f : 0;
            animator.speed = Mathf.MoveTowards(animator.speed, Mathf.Max(travelRate, turningRate), dt * 3);
            strokePhase = Mathf.Repeat(strokePhase + animator.speed * dt / 1.8f, 1);
            AttachPlayer();
        }

        public bool CanOccupy(Vector3 position, Quaternion rotation) => TryCanOccupy(position, rotation, out _);

        float ShipClearance(Vector3 position, Quaternion rotation, Vector3 ship, float radius)
        {
            var axis=rotation*Vector3.forward;axis.y=0;axis.Normalize();
            var offset=ship-position;offset.y=0;
            var closest=position+axis*Mathf.Clamp(Vector3.Dot(offset,axis),-hullProbeSpacing,hullProbeSpacing);
            return FlatDistance(closest,ship)-radius;
        }

        bool EscapingShip(Vector3 position, Quaternion rotation, Transform ship, float radius)
        {
            float current=ShipClearance(transform.position,transform.rotation,ship.position,radius);
            if(current>=0)return false;
            float next=ShipClearance(position,rotation,ship.position,radius);
            // When a ship's center lies within our keel segment, the capsule depth is
            // constant until the stern clears it. Increasing center separation must still
            // count as escape; three discrete probes incorrectly created another trap.
            return next>current+.0001f || (next>=current-.0001f &&
                FlatDistance(position,ship.position)>FlatDistance(transform.position,ship.position)+.0001f);
        }

        bool TryCanOccupy(Vector3 position, Quaternion rotation, out BoatObstacleKind obstacle)
        {
            obstacle = BoatObstacleKind.None;
            for (int i = -1; i <= 1; i++)
            {
                var sample = position + rotation * Vector3.forward * (i * hullProbeSpacing);
                if (nearbyFriendlyBoats != null)
                    foreach (var other in nearbyFriendlyBoats)
                        if (other != null && other.gameObject.activeInHierarchy &&
                            FlatDistance(sample, other.transform.position) < 48f &&
                            !EscapingShip(position,rotation,other.transform,48))
                        { obstacle = BoatObstacleKind.Ship; return false; }
                if (enemyShips != null)
                    foreach (var other in enemyShips)
                        if (other != null && other.gameObject.activeInHierarchy && !other.isDestroyed &&
                            FlatDistance(sample, other.transform.position) < 53f &&
                            !EscapingShip(position,rotation,other.transform,53))
                        { obstacle = BoatObstacleKind.Ship; return false; }
                int count = Physics.OverlapSphereNonAlloc(sample + Vector3.up * 2, hullProbeRadius, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count == overlaps.Length) { obstacle = BoatObstacleKind.Structure; return false; }
                for (int n = 0; n < count; n++)
                {
                    var hit = overlaps[n];
                    if (hit == null || hit is TerrainCollider || hit.transform.IsChildOf(transform) ||
                        (session != null && hit.transform.IsChildOf(session.player.transform))) continue;
                    var friendly=hit.GetComponentInParent<AmbientRiverBoat>();
                    var enemy=hit.GetComponentInParent<EnemyShip>();
                    if(friendly!=null && EscapingShip(position,rotation,friendly.transform,48))continue;
                    if(enemy!=null && EscapingShip(position,rotation,enemy.transform,53))continue;
                    obstacle = ClassifyObstacle(hit);
                    return false;
                }
                if (terrain != null)
                {
                    var local = sample - terrain.transform.position; var size = terrain.terrainData.size;
                    if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z ||
                        terrain.SampleHeight(sample) + terrain.transform.position.y + minimumDepth > position.y)
                    { obstacle = BoatObstacleKind.Shore; return false; }
                }
            }
            return true;
        }

        static BoatObstacleKind ClassifyObstacle(Collider hit)
        {
            if (hit.GetComponentInParent<AmbientRiverBoat>() != null || hit.GetComponentInParent<EnemyShip>() != null)
                return BoatObstacleKind.Ship;
            string name = hit.name.ToLowerInvariant();
            if (name.Contains("rock") || name.Contains("stone") || name.Contains("boulder") || name.Contains("đá"))
                return BoatObstacleKind.Rock;
            if (name.Contains("tree") || name.Contains("pine") || name.Contains("mangrove") || name.Contains("cây") || name.Contains("cay"))
                return BoatObstacleKind.Tree;
            return BoatObstacleKind.Structure;
        }

        public static float ImpactDamage(BoatObstacleKind obstacle, float impactSpeed, float rowingSpeed)
        {
            if (impactSpeed < 12f) return 0;
            float baseDamage = obstacle switch
            {
                BoatObstacleKind.Ship => 26f,
                BoatObstacleKind.Rock => 21f,
                BoatObstacleKind.Tree => 15f,
                BoatObstacleKind.Shore => 12f,
                BoatObstacleKind.Structure => 17f,
                _ => 0f
            };
            return Mathf.Round(baseDamage * Mathf.Clamp(impactSpeed / Mathf.Max(1, rowingSpeed), .25f, 1.4f));
        }

        bool HandleImpact(BoatObstacleKind obstacle, float impactSpeed)
        {
            if (vitals == null || Time.unscaledTime < nextImpactTime) return false;
            float damage = ImpactDamage(obstacle, impactSpeed, maxSpeed);
            if (damage <= 0) return false;
            nextImpactTime = Time.unscaledTime + 1.2f;
            string label = obstacle switch
            {
                BoatObstacleKind.Ship => "Va vào thuyền khác",
                BoatObstacleKind.Rock => "Va vào đá",
                BoatObstacleKind.Tree => "Va vào cây",
                BoatObstacleKind.Shore => "Mắc cạn ở bờ sông",
                _ => "Va vào vật cản"
            };
            vitals.TakeImpact(damage, label);
            if (vitals.Health > 0) return false;
            if (QuestCheckpoint.Instance != null) return true;
            RestorePlayer();
            transform.SetPositionAndRotation(new Vector3(homePosition.x, TideSystem.CurrentWaterHeight, homePosition.z), homeRotation);
            session.player.transform.SetPositionAndRotation(boardingPoint.position, boardingPoint.rotation);
            playerBody.position = boardingPoint.position; playerBody.rotation = boardingPoint.rotation;
            Physics.SyncTransforms(); orbit.SnapToTarget();
            vitals.RecoverAtDock();
            BachDangStoryManager.Instance?.UpdateUI();
            return true;
        }

        void AttachPlayer()
        {
            if (!IsAboard || session == null) return;
            bool aiming=RiverPlayerCombat.Instance!=null&&RiverPlayerCombat.Instance.IsAiming;
            var position=aiming?transform.position+Vector3.up*10-transform.forward*6:seat.position;
            session.player.transform.SetPositionAndRotation(position, seat.rotation);
            playerBody.position = position; playerBody.rotation = seat.rotation;
        }
        public void LeaveForCheckpoint()
        {
            if(IsAboard)RestorePlayer();
            Speed=0;IsBoosting=false;pendingBattleLaunch=false;wedgedAtDockSeconds=0;nextImpactTime=0;
            if(tide!=null)tide.ReleaseLowTide(this);
        }
        void LateUpdate()
        {
            bool aiming=IsAboard&&RiverPlayerCombat.Instance!=null&&RiverPlayerCombat.Instance.IsAiming;
            if(paddle!=null)paddle.gameObject.SetActive(!aiming);
            if(aiming)return;
            if (IsAboard && paddle != null && leftHand != null && rightHand != null)
            {
                ApplyRowingPose(strokePhase);
            }
        }
        // The blade follows a smooth catch / underwater pull / lifted recovery cycle.
        // Arms follow two grips on that shaft; imported hand axes can never flip it overhead.
        public void ApplyRowingPose(float phase)
        {
            if (!IsAboard || paddle == null || leftUpperArm == null || rightUpperArm == null) return;
            float angle = Mathf.Repeat(phase, 1) * Mathf.PI * 2;
            float sweep = Mathf.Cos(angle), lift = Mathf.Sin(angle);
            var grip = seat.position - transform.right * .5f + Vector3.up * 11.5f + transform.forward * (.8f * sweep);
            BladeCenter = seat.position + transform.right * 19 + transform.forward * (2 + 7 * sweep);
            BladeCenter = new Vector3(BladeCenter.x, TideSystem.CurrentWaterHeight - 1.5f - (lift >= 0 ? 1.5f : 5.5f) * lift, BladeCenter.z);
            var direction = (BladeCenter - grip).normalized;
            var forward = Vector3.ProjectOnPlane(transform.forward, direction).normalized;
            paddle.SetPositionAndRotation(grip, Quaternion.LookRotation(forward, -direction));
            paddle.localScale = new Vector3(1, Vector3.Distance(grip, BladeCenter) / 24, 1);
            SolveArm(leftUpperArm, leftForearm, leftHand, grip,
                leftUpperArm.position - transform.right * 5 - transform.forward * 2 - Vector3.up * 2);
            SolveArm(rightUpperArm, rightForearm, rightHand, grip + direction * 4f,
                rightUpperArm.position + transform.right * 5 - transform.forward * 2 - Vector3.up * 2);
        }

        internal static void SolveArm(Transform upper, Transform forearm, Transform hand, Vector3 target, Vector3 hint)
        {
            float a = Vector3.Distance(upper.position, forearm.position), b = Vector3.Distance(forearm.position, hand.position);
            var offset = target - upper.position; float d = Mathf.Clamp(offset.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
            if (a < .001f || b < .001f || offset.sqrMagnitude < .0001f) return;
            var axis = offset.normalized; var bend = Vector3.ProjectOnPlane(hint - upper.position, axis).normalized;
            float along = (a * a - b * b + d * d) / (2 * d);
            var elbow = upper.position + axis * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(forearm.position - upper.position, elbow - upper.position) * upper.rotation;
            forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position, target - forearm.position) * forearm.rotation;
        }
        public void ApplyBowArmPose(Vector3 grip,Vector3 draw,Vector3 forward)
        {
            if(leftUpperArm==null||rightUpperArm==null)return;
            SolveArm(leftUpperArm,leftForearm,leftHand,grip,leftUpperArm.position-transform.right*4-Vector3.up*3);
            SolveArm(rightUpperArm,rightForearm,rightHand,draw,rightUpperArm.position+transform.right*5-forward*4);
        }
        void OnDisable() { if (IsAboard) RestorePlayer(); if (tide != null) tide.ReleaseLowTide(this); }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
