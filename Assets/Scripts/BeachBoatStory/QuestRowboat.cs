using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>One scene-owned, kinematic quest boat. Reuses the player and Thai's rowing animation.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class QuestRowboat : MonoBehaviour
    {
        static readonly Unity.Profiling.ProfilerMarker StepMarker=new Unity.Profiling.ProfilerMarker("BachDang.Rowboat");
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
        public bool BlocksGroundInteractions=>IsAboard&&RiverBoardingCombat.Instance?.State!=RiverBoardingCombat.PlayerState.Land;
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
        Transform leftIndex,leftLittle,leftIndexMid,leftLittleMid,rightIndex,rightLittle,rightIndexMid,rightLittleMid;
        public Vector3 LeftPaddleGrip {get;private set;}
        public Vector3 RightPaddleGrip {get;private set;}
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
            // Resize only the hull and its footprint: scaling the boat root would also
            // scale attachments and visiting soldiers. The player keeps their own scale.
            const float footprint=.65f;
            var hull=transform.Find("Thuyen_Go_Song");
            if(hull!=null)hull.localScale=Vector3.Scale(hull.localScale,new Vector3(footprint,1,footprint));
            if(seat!=null)seat.localPosition=Vector3.Scale(seat.localPosition,new Vector3(footprint,1,footprint));
            foreach(var box in GetComponents<BoxCollider>())
            {box.size=Vector3.Scale(box.size,new Vector3(footprint,1,footprint));box.center=Vector3.Scale(box.center,new Vector3(footprint,1,footprint));}
            hullProbeSpacing*=footprint;hullProbeRadius*=footprint;
            BoatHullCollision.Get(transform).Initialize();
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
                leftIndex=animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);leftLittle=animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
                leftIndexMid=animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate);leftLittleMid=animator.GetBoneTransform(HumanBodyBones.LeftLittleIntermediate);
                rightIndex=animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);rightLittle=animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
                rightIndexMid=animator.GetBoneTransform(HumanBodyBones.RightIndexIntermediate);rightLittleMid=animator.GetBoneTransform(HumanBodyBones.RightLittleIntermediate);
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
            return BoardPlayer(true);
        }
        bool BoardPlayer(bool advanceStory)
        {
            var story=BachDangStoryManager.Instance;
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
            if (advanceStory && story.currentState == BachDangStoryManager.StoryState.Act3_WaitForLowTide) story.OnReadyForBattle();
            story.SetInteractPrompt(false, owner: this); story.UpdateUI();
            return true;
        }

        public void RestoreSave(bool aboard,Vector3 position,Quaternion rotation)
        {
            if(IsAboard)RestorePlayer();
            transform.SetPositionAndRotation(position,rotation);
            if(aboard)BoardPlayer(false);
            Speed=0;IsBoosting=false;
        }

        public bool TryDisembark()
        {
            if(RiverBoardingCombat.Instance!=null&&RiverBoardingCombat.Instance.PlayerAway)return false;
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
            orbit.distance = 62f;
            orbit.heightOffset = 25f;
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

        // Preserve the logical boat session while the player walks and fights on shore.
        public void SetShoreWalking(bool walking)
        {
            if(!IsAboard)return;
            motor.enabled=false;
            if(!playerBody.isKinematic){playerBody.linearVelocity=Vector3.zero;playerBody.angularVelocity=Vector3.zero;}
            playerBody.isKinematic=!walking;playerCollider.enabled=walking;
            playerBody.interpolation=walking?originalInterpolation:RigidbodyInterpolation.None;
            animator.speed=1;animator.applyRootMotion=false;
            if(walking)
            {
                animator.enabled=true;animator.runtimeAnimatorController=RiverBoardingCombat.Instance.FootController;
                // Leaving a procedural swim must discard the seated controller's
                // cached humanoid muscles before standing/weapon clips evaluate.
                animator.Rebind();animator.Update(0);
            }
            motor.enabled=walking&&session.storyMode;
        }
        public void ResumeRowing()
        {
            if(!IsAboard)return;
            SetShoreWalking(false);animator.enabled=true;animator.runtimeAnimatorController=rowingController;animator.speed=0;
            Speed=0;IsBoosting=false;AttachPlayer();Physics.SyncTransforms();
        }
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
                bool building = story != null && story.currentState == BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver;
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
            var traversal=RiverBoardingCombat.Instance;
            if(IsAboard&&traversal!=null&&traversal.PlayerAway)
            {
                if(traversal.NearRowboat)
                {
                    story.SetInteractPrompt(true,"[E / SPACE] Lên thuyền chèo",this);
                    if(Input.GetKeyDown(KeyCode.E)&&StoryDialogueUI.TryConsumeInput())traversal.TryReturnToRowboat();
                }
                return;
            }
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
            using var sample=StepMarker.Auto();
            if(dt>.05f)
            {
                // Keep elapsed time when a frame is slow. Every small slice
                // still performs the complete hull sweep before moving.
                int slices=Mathf.CeilToInt(Mathf.Min(dt,.25f)/.05f);float slice=Mathf.Min(dt,.25f)/slices;
                for(int i=0;i<slices;i++)Step(throttle,steering,slice,boostWanted);return;
            }
            var boarding=RiverBoardingCombat.Instance;
            if(IsAboard&&boarding!=null&&(boarding.PlayerAway||boarding.SwordDrawn))
            {Speed=0;IsBoosting=false;animator.speed=1;if(!boarding.PlayerAway)AttachPlayer();return;}
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
            bool clear=TryCanOccupy(next,rotation,out var obstacle)&&BoatHullCollision.Get(transform).CanMove(position,transform.rotation,next,rotation,false);
            if (clear) { transform.SetPositionAndRotation(next, rotation); Speed = speed; wedgedAtDockSeconds = 0; }
            else
            {
                // Colliding while afloat never teleports the boat through the quay.
                // Safe launch/old-save placement happens before rowing begins.
                if(obstacle==BoatObstacleKind.None)
                {var blocker=BoatHullCollision.Get(transform).LastBlocker;obstacle=blocker!=null?ClassifyObstacle(blocker):BoatObstacleKind.Structure;}
                bool knockedOut = HandleImpact(obstacle, Mathf.Max(Mathf.Abs(Speed), Mathf.Abs(speed)));
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

        public bool CanOccupy(Vector3 position, Quaternion rotation) => TryCanOccupy(position, rotation, out _)&&BoatHullCollision.Get(transform).CanOccupy(position,rotation,false);

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
            if(RiverBoardingCombat.Instance!=null&&RiverBoardingCombat.Instance.PlayerAway)return;
            bool aiming=RiverPlayerCombat.Instance!=null&&RiverPlayerCombat.Instance.IsAiming;
            aiming|=RiverBoardingCombat.Instance!=null&&RiverBoardingCombat.Instance.SwordDrawn;
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
            if(RiverBoardingCombat.Instance!=null&&(RiverBoardingCombat.Instance.PlayerAway||RiverBoardingCombat.Instance.SwordDrawn)){if(paddle!=null)paddle.gameObject.SetActive(false);return;}
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
            // The grip is in the palm between curled fingers, not at the wrist bone.
            for(int i=0;i<3;i++)
            {
                AlignHand(leftHand,leftIndex,leftLittle,direction);AlignHand(rightHand,rightIndex,rightLittle,direction);
                var leftOffset=Palm(leftHand,leftIndex,leftLittle,leftIndexMid,leftLittleMid)-leftHand.position;
                var rightOffset=Palm(rightHand,rightIndex,rightLittle,rightIndexMid,rightLittleMid)-rightHand.position;
                SolveArm(leftUpperArm,leftForearm,leftHand,grip-leftOffset,leftUpperArm.position-transform.right*5-transform.forward*2-Vector3.up*2);
                SolveArm(rightUpperArm,rightForearm,rightHand,grip+direction*4-rightOffset,rightUpperArm.position+transform.right*5-transform.forward*2-Vector3.up*2);
            }
            LeftPaddleGrip=Palm(leftHand,leftIndex,leftLittle,leftIndexMid,leftLittleMid);
            RightPaddleGrip=Palm(rightHand,rightIndex,rightLittle,rightIndexMid,rightLittleMid);
            var between=RightPaddleGrip-LeftPaddleGrip;if(between.sqrMagnitude>.1f)direction=between.normalized;
            forward=Vector3.ProjectOnPlane(transform.forward,direction).normalized;
            paddle.SetPositionAndRotation(LeftPaddleGrip,Quaternion.LookRotation(forward,-direction));
            paddle.localScale=new Vector3(1,Vector3.Distance(LeftPaddleGrip,BladeCenter)/24,1);
            BladeCenter=paddle.TransformPoint(new Vector3(0,-24,0));
        }
        static Vector3 Palm(Transform hand,Transform index,Transform little,Transform middleIndex,Transform middleLittle)
        {
            if(index==null||little==null)return hand.position;
            var knuckles=(index.position+little.position)*.5f;
            return middleIndex!=null&&middleLittle!=null?(knuckles+(middleIndex.position+middleLittle.position)*.5f)*.5f:Vector3.Lerp(hand.position,knuckles,.6f);
        }
        static void AlignHand(Transform hand,Transform index,Transform little,Vector3 shaft)
        {
            if(index==null||little==null)return;var across=index.position-little.position;
            if(Vector3.Dot(across,shaft)<0)shaft=-shaft;
            hand.rotation=Quaternion.FromToRotation(across,shaft)*hand.rotation;
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
