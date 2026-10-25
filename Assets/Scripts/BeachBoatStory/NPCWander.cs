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
        public bool followWaypointsInOrder;
        public float[] waypointWaitSeconds = new float[0];
        [Tooltip("Short activity names matching the waypoint array, shown when inspecting NPC life.")]
        public string[] activityLabels = new string[0];
        public bool autoGenerateWaypoints = true;
        public Animator animator;
        public string walkParam = "isWalking";
        public string CurrentActivity { get; private set; } = "Đang nghỉ";
        public bool IsSleeping { get; private set; }
        public int WaypointIndex => targetIndex;
        Vector3 home, target;
        Terrain terrain;
        float idleRemaining, groundOffset, scale, currentSpeed, personalPace = 1, baseAnimatorSpeed = 1;
        float idleLookRemaining;
        Quaternion idleFacing;
        int walkHash, sitHash, layHash, poseHash, restPose = 1, appliedRestPose, targetIndex = -1;
        int routeDirection = 1;
        bool hasWalk, hasSit, hasLay, hasPose, walking, sitting;
        RuntimeAnimatorController controller;
        readonly RaycastHit[] hits = new RaycastHit[12];
        static Transform[] neighbourhoodStops;
        static UnityEngine.SceneManagement.Scene neighbourhoodScene;
        public float PatrolRadius {get;private set;}
        public float DistanceTravelled {get;private set;}
        bool raisedPatrol;
        public float CurrentSpeed=>currentSpeed;
        public float NominalStrideSpeed {get;private set;}
        NPCWalkNavigation navigation;
        readonly System.Collections.Generic.List<Vector3> route=new System.Collections.Generic.List<Vector3>();
        int routePoint;
        float blockedTime,nextReplan;
        public int RoutesPlanned {get;private set;}

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
            CivilianMovementProfile.Apply(animator);
            ActorSolidBody.Ensure(gameObject,animator);
            home = transform.position; terrain = Terrain.activeTerrain;
            scale = Mathf.Max(1, BachDangWorldScale.ForScene(gameObject.scene));
            // The characters are twice the terrain's reference scale. Use the actual
            // skeleton, rather than the terrain multiplier, to set a human walking pace.
            NominalStrideSpeed=Mathf.Max(scale*1.3f,EnemyDeckGuard.RigHeight(animator)*.85f);
            walkSpeed=NominalStrideSpeed*UnityEngine.Random.Range(1.02f,1.14f);
            acceleration=Mathf.Max(acceleration,scale*2.4f);deceleration=Mathf.Max(deceleration,scale*3.2f);turnSpeed=Mathf.Max(turnSpeed,240);
            if(autoGenerateWaypoints)wanderRadius=Mathf.Clamp(wanderRadius*2.5f,scale*8,scale*25);
            minIdleTime=Mathf.Min(minIdleTime,1.5f);maxIdleTime=Mathf.Min(maxIdleTime,5);
            PatrolRadius=Mathf.Max(scale*24,NominalStrideSpeed*16);
            // Some guards patrol raised wooden platforms. Preserve that authored height instead of
            // snapping them down to terrain; routes are kept on the same platform by the setup tool.
            groundOffset = terrain != null ? Mathf.Clamp(home.y - Ground(home), 0, scale * 5f) : 0;
            raisedPatrol=groundOffset>scale*1.5f;
            navigation=new NPCWalkNavigation(this,terrain,scale,raisedPatrol);
            if(navigation.FindSpawn(home,out var spawn)){transform.position=home=spawn;}
            Physics.SyncTransforms();
            animator.applyRootMotion = false;
            baseAnimatorSpeed = 1;
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
            // Reactivation and legacy idle initializers can reset the controller boolean.
            if (hasWalk) animator.SetBool(walkHash,walk);
            if(!walk)animator.speed=baseAnimatorSpeed;
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
            IsSleeping=false;Pose(false,false); target = home;route.Clear();routePoint=0;blockedTime=0;
            // Alternate jobs with longer trips along clear village/camp lanes. Platform
            // sentries retain their authored route and never step off their guard post.
            if(!raisedPatrol&&UnityEngine.Random.value<.65f&&TryNeighbourhoodTrip())return;
            for(int attempt=0;attempt<8;attempt++)
            {
                Vector3 candidate;
                if (!autoGenerateWaypoints && waypoints != null && waypoints.Length > 0)
                {
                    if (followWaypointsInOrder)
                    {
                        if (targetIndex < 0) targetIndex = 0;
                        else
                        {
                            if (targetIndex >= waypoints.Length - 1) routeDirection = -1;
                            if (targetIndex <= 0) routeDirection = 1;
                            targetIndex = Mathf.Clamp(targetIndex + routeDirection, 0, waypoints.Length - 1);
                        }
                    }
                    else {
                        int previous=targetIndex;
                        targetIndex=UnityEngine.Random.Range(0,waypoints.Length);
                        if(targetIndex==previous&&waypoints.Length>1)targetIndex=(targetIndex+1)%waypoints.Length;
                    }
                    var wp=waypoints[targetIndex];
                    if(wp==null)continue;candidate=wp.position;
                }
                else {var offset=UnityEngine.Random.insideUnitCircle.normalized*UnityEngine.Random.Range(wanderRadius*.45f,wanderRadius);candidate=home+new Vector3(offset.x,0,offset.y);}
                if(!ValidGround(candidate)||QuestRowboat.FlatDistance(candidate,transform.position)<scale*.5f)continue;
                if(!SetDestination(candidate))continue;
                CurrentActivity="Đang đi làm";return;
            }
            Idle();
        }
        bool TryNeighbourhoodTrip()
        {
            if(neighbourhoodStops==null||neighbourhoodScene!=gameObject.scene)
            {
                neighbourhoodScene=gameObject.scene;
                var stops=new System.Collections.Generic.HashSet<Transform>();
                foreach(var walker in FindObjectsByType<NPCWander>(FindObjectsInactive.Include))
                    if(walker.gameObject.scene==gameObject.scene&&walker.waypoints!=null)
                        foreach(var stop in walker.waypoints)if(stop!=null)stops.Add(stop);
                neighbourhoodStops=new Transform[stops.Count];stops.CopyTo(neighbourhoodStops);
            }
            for(int attempt=0;attempt<24;attempt++)
            {
                Vector3 candidate;
                if(attempt<16&&neighbourhoodStops.Length>0)
                {var stop=neighbourhoodStops[UnityEngine.Random.Range(0,neighbourhoodStops.Length)];if(stop==null)continue;candidate=stop.position;}
                else {var offset=UnityEngine.Random.insideUnitCircle.normalized*UnityEngine.Random.Range(PatrolRadius*.4f,PatrolRadius);candidate=home+new Vector3(offset.x,0,offset.y);}
                float distance=QuestRowboat.FlatDistance(candidate,transform.position);
                if(distance<NominalStrideSpeed*3||QuestRowboat.FlatDistance(candidate,home)>PatrolRadius||!ValidGround(candidate))continue;
                if(terrain!=null)candidate.y=Ground(candidate)+groundOffset;
                var direction=candidate-transform.position;direction.y=0;direction.Normalize();
                bool clear=true;
                for(float d=0;d<=distance;d+=scale*2)
                {
                    var point=transform.position+direction*d;
                    if(!ValidGround(point)||(terrain!=null&&Mathf.Abs(Ground(point)-Ground(transform.position))>scale*1.25f)){clear=false;break;}
                }
                if(!clear)continue;
                int count=Physics.SphereCastNonAlloc(transform.position+Vector3.up*scale,.25f*scale,direction,hits,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)
                {
                    var hit=hits[i];if(hit.collider==null||hit.collider is TerrainCollider||hit.transform.IsChildOf(transform)||transform.IsChildOf(hit.transform)||hit.collider.GetComponentInParent<Animator>()!=null)continue;
                    clear=false;break;
                }
                if(!clear)continue;
                if(!SetDestination(candidate))continue;
                targetIndex=-1;CurrentActivity="Đang đi qua làng / tuần tra";return true;
            }
            return false;
        }
        bool SetDestination(Vector3 destination)
        {
            if(!navigation.Route(transform.position,destination,route))return false;
            target=route[route.Count-1];routePoint=0;RoutesPlanned++;return true;
        }
        void Idle()
        {
            idleRemaining=UnityEngine.Random.Range(Mathf.Max(.1f,minIdleTime),Mathf.Max(minIdleTime,maxIdleTime));
            string activity=targetIndex>=0&&activityLabels!=null&&targetIndex<activityLabels.Length
                ?activityLabels[targetIndex]:null;
            bool sleeping=!string.IsNullOrEmpty(activity)&&activity.Contains("ngủ");
            bool seated=!string.IsNullOrEmpty(activity)&&activity.Contains("ngồi");
            if(sleeping) idleRemaining=Mathf.Max(idleRemaining,UnityEngine.Random.Range(12f,24f));
            if(targetIndex>=0&&waypointWaitSeconds!=null&&targetIndex<waypointWaitSeconds.Length)
                idleRemaining=Mathf.Max(.1f,waypointWaitSeconds[targetIndex]*(sleeping?1:.55f));
            IsSleeping=sleeping&&hasLay;
            // Retargeted rest clips need an authored mat or seat. Playing them at a
            // random patrol stop makes armored NPCs float or fold into the ground.
            restPose=sleeping&&hasLay?2:1;
            Pose(false,sleeping?hasLay:seated&&hasSit);
            currentSpeed=0;
            PickIdleFacing();
            if(IsSleeping&&waypoints[targetIndex]!=null)
                transform.rotation=idleFacing=waypoints[targetIndex].rotation;
            CurrentActivity=targetIndex>=0&&activityLabels!=null&&targetIndex<activityLabels.Length&&!string.IsNullOrWhiteSpace(activityLabels[targetIndex])
                ?activityLabels[targetIndex]:"Đang nghỉ chân";
        }
        public void PauseForConversation(Transform visitor)
        {
            IsSleeping=false;target=transform.position;route.Clear();idleRemaining=Mathf.Max(idleRemaining,5);currentSpeed=0;Pose(false,false);CurrentActivity="Đang trò chuyện";
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
            float dt=Mathf.Min(Time.deltaTime,.1f);
            idleLookRemaining-=dt;
            if(idleLookRemaining<=0)PickIdleFacing();
            transform.rotation=Quaternion.RotateTowards(transform.rotation,idleFacing,turnSpeed*.22f*dt);
        }
        void Update()
        {
            if(QuestCheckpoint.Instance!=null&&QuestCheckpoint.Instance.IsDefeated)return;
            if (animator == null) return;
            float dt=Mathf.Min(Time.deltaTime,.1f);
            if(dt<=0)return;
            if(navigation.SeparateActors(walkSpeed*dt,out var separated))
            {DistanceTravelled+=Vector3.Distance(transform.position,separated);transform.position=separated;Physics.SyncTransforms();idleRemaining=Mathf.Min(idleRemaining,.2f);}
            if (controller != animator.runtimeAnimatorController) CacheParameters();
            // Ambient villagers and guards keep living in the background while a story panel is
            // open. Only the NPC being spoken to is paused by PauseForConversation; globally
            // freezing everyone made the settlement look staged and mechanical.
            if (idleRemaining > 0) { idleRemaining-=dt;if(!IsSleeping)UpdateIdleLook();if(idleRemaining<=0)PickTarget();return; }
            if(route.Count==0){PickTarget();return;}
            while(routePoint<route.Count-1&&QuestRowboat.FlatDistance(transform.position,route[routePoint])<Mathf.Max(.5f,navigation.Radius*.35f))routePoint++;
            var direction=route[routePoint]-transform.position;direction.y=0;
            float remaining=direction.magnitude;
            if(remaining<Mathf.Max(.5f,scale*.12f)&&routePoint==route.Count-1){Idle();return;}
            float desiredSpeed=Mathf.Max(0,walkSpeed)*personalPace*Mathf.Clamp01(remaining/Mathf.Max(1,scale*.35f));
            currentSpeed=Mathf.MoveTowards(currentSpeed,desiredSpeed,(desiredSpeed>currentSpeed?acceleration:deceleration)*dt);
            var heading=FindClearHeading(direction/remaining,currentSpeed*dt);
            if(heading.sqrMagnitude<.5f)
            {
                currentSpeed=0;blockedTime+=dt;Pose(false,false);CurrentActivity="Đang tránh vật cản";
                if(blockedTime>.8f&&Time.time>=nextReplan)
                {nextReplan=Time.time+UnityEngine.Random.Range(.8f,1.4f);blockedTime=0;if(!SetDestination(target))PickTarget();}
                return;
            }
            // Turn before translating. A near-180-degree avoidance heading used
            // to move the body backward while the walk animation still faced forward.
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(heading),turnSpeed*personalPace*dt);
            if(Vector3.Angle(transform.forward,heading)>38){currentSpeed=0;Pose(false,false);return;}
            var facing=transform.forward;facing.y=0;facing.Normalize();
            var next=transform.position+facing*Mathf.Min(remaining,currentSpeed*dt);
            if(!navigation.Project(next,out next)||!navigation.Clear(transform.position,next,true)){currentSpeed=0;Pose(false,false);return;}
            blockedTime=0;
            DistanceTravelled+=Vector3.Distance(transform.position,next);
            transform.position=next;
            // AutoSyncTransforms is disabled in this project. Publish the moved
            // capsule now so the next walker sees this actor's actual position.
            Physics.SyncTransforms();
            // Keep the walk cycle at normal cadence through acceleration and stopping.
            // Slowing the entire Animator to 0.15 also slowed idle and exit transitions.
            animator.speed=Mathf.Clamp(walkSpeed*personalPace/NominalStrideSpeed,.95f,1.3f);
            Pose(currentSpeed>.12f,false);
        }
        Vector3 FindClearHeading(Vector3 desired,float step)
        {
            if(AmbientPopulationBudget.Instance!=null)desired=AmbientPopulationBudget.Instance.Separate(this,desired);
            float probe=Mathf.Max(step+.12f*scale,.3f*scale);
            if(!Blocked(desired,probe))return desired;
            for(int option=0;option<8;option++)
            {
                float turn=Mathf.Min(170,avoidanceAngle*(1+option/2))*(option%2==0?1:-1);
                var candidate=Quaternion.Euler(0,turn,0)*desired;
                var test=transform.position+candidate*Mathf.Max(step,.35f*scale);
                if(navigation.Project(test,out _)&&!Blocked(candidate,probe))return candidate;
            }
            return Vector3.zero;
        }
        bool Blocked(Vector3 heading,float distance)
        {
            var end=transform.position+heading*distance;
            return !navigation.Project(end,out end)||!navigation.Clear(transform.position,end,true);
        }
        void OnDisable(){currentSpeed=0;if(animator!=null){animator.speed=baseAnimatorSpeed;Pose(false,false);}}
        void OnDrawGizmosSelected(){Gizmos.color=Color.green;Gizmos.DrawWireSphere(Application.isPlaying?home:transform.position,wanderRadius);}
    }
}
