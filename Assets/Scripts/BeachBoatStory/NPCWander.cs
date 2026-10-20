using System;
using UnityEngine;

namespace BeachBoatStory
{
    [DisallowMultipleComponent]
    public class NPCWander : MonoBehaviour
    {
        public float wanderRadius = 30, walkSpeed = 8, minIdleTime = 3, maxIdleTime = 10;
        [Header("Natural movement")]
        public float acceleration = 5.5f, deceleration = 8f, turnSpeed = 150f, arrivalSlowDistance = 5f;
        [Range(15,75)] public float avoidanceAngle = 38f;
        [Min(.2f)] public float obstacleProbeDistance = 2.2f;
        [Range(.75f,1.2f)] public float minimumPace = .88f;
        [Range(.8f,1.35f)] public float maximumPace = 1.12f;
        [Range(0,100)] public float sitLayChance = 30;
        public Transform[] waypoints;
        [Tooltip("Short activity names matching the waypoint array, shown when inspecting NPC life.")]
        public string[] activityLabels = new string[0];
        public bool autoGenerateWaypoints = true;
        public Animator animator;
        public string walkParam = "isWalking";
        public string CurrentActivity { get; private set; } = "Đang nghỉ";
        Vector3 home, target;
        Terrain terrain;
        float idleRemaining, groundOffset, scale, currentSpeed, personalPace = 1, baseAnimatorSpeed = 1;
        float idleLookRemaining;
        Quaternion idleFacing;
        int walkHash, sitHash, layHash, poseHash, restPose = 1, appliedRestPose, targetIndex = -1;
        bool hasWalk, hasSit, hasLay, hasPose, walking, sitting;
        RuntimeAnimatorController controller;
        readonly RaycastHit[] hits = new RaycastHit[12];

        public static bool IsEligible(Animator a)
        {
            if (a == null || !a.isHuman || a.runtimeAnimatorController == null) return false;
            if (a.GetComponent<NPCCampPose>() != null || a.name.Contains("Lookout")) return false;
            bool walk = false;
            foreach (var p in a.parameters) if (p.type == AnimatorControllerParameterType.Bool && string.Equals(p.name,"isWalking",StringComparison.OrdinalIgnoreCase)) walk = true;
            if (!walk) return false; // Seated boat crews and idle-only actors must stay at their posts.
            if (a.CompareTag("Player") || a.GetComponentInParent<Character_Movement>() != null ||
                a.GetComponentInParent<NPCQuestGiver>() != null) return false;
            var session = a.GetComponentInParent<BeachBoatQuestSession>();
            return session == null || session.player == null || !a.transform.IsChildOf(session.player.transform);
        }
        void Start()
        {
            if (animator == null) animator = GetComponent<Animator>();
            // Reject group roots/buildings accidentally selected by the old name-based tool.
            if (!IsEligible(animator) || animator.transform != transform) { enabled = false; return; }
            home = transform.position; terrain = Terrain.activeTerrain;
            scale = Mathf.Max(1, BachDangWorldScale.ForScene(gameObject.scene));
            // Some guards patrol raised wooden platforms. Preserve that authored height instead of
            // snapping them down to terrain; routes are kept on the same platform by the setup tool.
            groundOffset = terrain != null ? Mathf.Clamp(home.y - Ground(home), 0, scale * 5f) : 0;
            animator.applyRootMotion = false;
            baseAnimatorSpeed = animator.speed;
            personalPace = UnityEngine.Random.Range(Mathf.Min(minimumPace,maximumPace),Mathf.Max(minimumPace,maximumPace));
            // Stagger the first action so a group never starts walking in lockstep.
            CacheParameters(); idleRemaining = UnityEngine.Random.Range(0f,Mathf.Max(1f,maxIdleTime*.65f));
            animator.speed=baseAnimatorSpeed*UnityEngine.Random.Range(.92f,1.04f);
            idleFacing = transform.rotation;
            if(idleRemaining<=.1f)PickTarget();else Pose(false,false);
        }
        void CacheParameters()
        {
            controller = animator.runtimeAnimatorController; hasWalk = hasSit = hasLay = hasPose = false;
            foreach (var p in animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Int && p.name == "Pose") { hasPose = true; poseHash = p.nameHash; }
                if (p.type != AnimatorControllerParameterType.Bool) continue;
                if (string.Equals(p.name, walkParam, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.name,"isWalking",StringComparison.OrdinalIgnoreCase))
                { hasWalk = true; walkHash = p.nameHash; }
                if (string.Equals(p.name,"isSitting",StringComparison.OrdinalIgnoreCase))
                { hasSit = true; sitHash = p.nameHash; }
                if (string.Equals(p.name,"isLaying",StringComparison.OrdinalIgnoreCase))
                { hasLay = true; layHash = p.nameHash; }
            }
            walking = sitting = false; appliedRestPose = 0;
            if (hasWalk) animator.SetBool(walkHash,false);
            if (hasSit) animator.SetBool(sitHash,false);
            if (hasLay) animator.SetBool(layHash,false);
            if (hasPose) animator.SetInteger(poseHash,0);
        }
        void Pose(bool walk, bool sit)
        {
            if (hasWalk && walking != walk) animator.SetBool(walkHash,walk);
            // A soldier can sit at one stop and lie down at the next. Keep the pose
            // in sync without sending animator parameters on every idle frame.
            int nextPose = sit ? restPose : 0;
            if (sitting != sit || appliedRestPose != nextPose)
            {
                if (hasSit) animator.SetBool(sitHash,sit && restPose == 1);
                if (hasLay) animator.SetBool(layHash,sit && restPose == 2);
                if (hasPose) animator.SetInteger(poseHash,nextPose);
                appliedRestPose = nextPose;
            }
            walking = walk; sitting = sit;
        }
        float Ground(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
        bool ValidGround(Vector3 p)
        {
            if (terrain == null) return true;
            var local = p - terrain.transform.position; var size = terrain.terrainData.size;
            return local.x >= 0 && local.z >= 0 && local.x <= size.x && local.z <= size.z && Ground(p) > TideSystem.CurrentWaterHeight + 1;
        }
        void PickTarget()
        {
            Pose(false,false); target = home;
            for(int attempt=0;attempt<8;attempt++)
            {
                Vector3 candidate;
                if (!autoGenerateWaypoints && waypoints != null && waypoints.Length > 0)
                {
                    targetIndex=UnityEngine.Random.Range(0,waypoints.Length);var wp=waypoints[targetIndex];
                    if(wp==null)continue;candidate=wp.position;
                }
                else {var offset=UnityEngine.Random.insideUnitCircle*Mathf.Max(0,wanderRadius);candidate=home+new Vector3(offset.x,0,offset.y);}
                if(!ValidGround(candidate))continue;
                target=candidate;CurrentActivity="Đang đi làm";return;
            }
            Idle();
        }
        void Idle()
        {
            idleRemaining=UnityEngine.Random.Range(Mathf.Max(.1f,minIdleTime),Mathf.Max(minIdleTime,maxIdleTime));
            string activity=targetIndex>=0&&activityLabels!=null&&targetIndex<activityLabels.Length
                ?activityLabels[targetIndex]:null;
            bool sleeping=!string.IsNullOrEmpty(activity)&&activity.Contains("ngủ");
            bool seated=!string.IsNullOrEmpty(activity)&&activity.Contains("ngồi");
            if(sleeping) idleRemaining=Mathf.Max(idleRemaining,UnityEngine.Random.Range(12f,24f));
            // Retargeted rest clips need an authored mat or seat. Playing them at a
            // random patrol stop makes armored NPCs float or fold into the ground.
            restPose=sleeping&&hasLay?2:1;
            Pose(false,sleeping?hasLay:seated&&hasSit);
            currentSpeed=0;
            PickIdleFacing();
            CurrentActivity=targetIndex>=0&&activityLabels!=null&&targetIndex<activityLabels.Length&&!string.IsNullOrWhiteSpace(activityLabels[targetIndex])
                ?activityLabels[targetIndex]:"Đang nghỉ chân";
        }
        public void PauseForConversation(Transform visitor)
        {
            target=transform.position;idleRemaining=Mathf.Max(idleRemaining,5);currentSpeed=0;Pose(false,false);CurrentActivity="Đang trò chuyện";
            if(visitor!=null){var direction=visitor.position-transform.position;direction.y=0;if(direction.sqrMagnitude>.1f)idleFacing=Quaternion.LookRotation(direction);}
        }
        void PickIdleFacing()
        {
            idleLookRemaining=UnityEngine.Random.Range(2.5f,6.5f);
            // Authored route markers face the next workplace. Keeping the idle pose close to
            // that direction makes villagers look at their task instead of staring randomly.
            var basis=targetIndex>=0&&waypoints!=null&&targetIndex<waypoints.Length&&waypoints[targetIndex]!=null
                ?waypoints[targetIndex].rotation:transform.rotation;
            idleFacing=basis*Quaternion.Euler(0,UnityEngine.Random.Range(-18f,18f),0);
        }
        void UpdateIdleLook()
        {
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            idleLookRemaining-=dt;
            if(idleLookRemaining<=0)PickIdleFacing();
            transform.rotation=Quaternion.RotateTowards(transform.rotation,idleFacing,turnSpeed*.22f*dt);
        }
        void Update()
        {
            if (animator == null) return;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            if(dt<=0)return;
            if (controller != animator.runtimeAnimatorController) CacheParameters();
            // Ambient villagers and guards keep living in the background while a story panel is
            // open. Only the NPC being spoken to is paused by PauseForConversation; globally
            // freezing everyone made the settlement look staged and mechanical.
            if (idleRemaining > 0) { idleRemaining-=dt;UpdateIdleLook();if(idleRemaining<=0)PickTarget();return; }
            var direction=target-transform.position;direction.y=0;
            float remaining=direction.magnitude;
            if(remaining<.75f){Idle();return;}
            float desiredSpeed=Mathf.Max(0,walkSpeed)*personalPace*Mathf.Clamp01(remaining/Mathf.Max(1,arrivalSlowDistance));
            currentSpeed=Mathf.MoveTowards(currentSpeed,desiredSpeed,(desiredSpeed>currentSpeed?acceleration:deceleration)*dt);
            var heading=FindClearHeading(direction/remaining,currentSpeed*dt);
            if(heading.sqrMagnitude<.5f)
            {
                currentSpeed=0;idleRemaining=UnityEngine.Random.Range(.5f,1.4f);Pose(false,false);
                CurrentActivity="Đang tránh vật cản";PickIdleFacing();return;
            }
            var next=transform.position+heading*currentSpeed*dt;
            if(!ValidGround(next)){Idle();return;}
            if(terrain!=null){next.y=Ground(next)+groundOffset;if(Mathf.Abs(next.y-transform.position.y)>scale){Idle();return;}}
            transform.position=next;
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(heading),turnSpeed*personalPace*dt);
            animator.speed=baseAnimatorSpeed*Mathf.Lerp(.88f,1.08f,Mathf.InverseLerp(0,Mathf.Max(.01f,walkSpeed),currentSpeed));
            Pose(currentSpeed>.12f,false);
        }
        Vector3 FindClearHeading(Vector3 desired,float step)
        {
            float probe=Mathf.Max(step+.3f*scale,obstacleProbeDistance*scale);
            if(!Blocked(desired,probe))return desired;
            float[] turns={avoidanceAngle,-avoidanceAngle,avoidanceAngle*1.8f,-avoidanceAngle*1.8f};
            foreach(float turn in turns)
            {
                var candidate=Quaternion.Euler(0,turn,0)*desired;
                var test=transform.position+candidate*Mathf.Max(step,.35f*scale);
                if(ValidGround(test)&&!Blocked(candidate,probe))return candidate;
            }
            return Vector3.zero;
        }
        bool Blocked(Vector3 heading,float distance)
        {
            int count=Physics.SphereCastNonAlloc(transform.position+Vector3.up*scale,.25f*scale,heading,hits,
                distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];if(hit.collider==null||hit.collider is TerrainCollider)continue;
                // Animator often lives below the character's capsule. Ignore both descendants and
                // ancestors of this NPC; otherwise it detects its own parent collider every frame.
                if(hit.transform.IsChildOf(transform)||transform.IsChildOf(hit.transform))continue;
                return true;
            }
            return false;
        }
        void OnDisable(){currentSpeed=0;if(animator!=null){animator.speed=baseAnimatorSpeed;Pose(false,false);}}
        void OnDrawGizmosSelected(){Gizmos.color=Color.green;Gizmos.DrawWireSphere(Application.isPlaying?home:transform.position,wanderRadius);}
    }
}
