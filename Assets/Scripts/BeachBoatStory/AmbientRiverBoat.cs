using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Low-cost authored patrol for background river boats.</summary>
    [DisallowMultipleComponent]
    public sealed class AmbientRiverBoat : MonoBehaviour
    {
        public TideSystem tide;
        public Transform[] route = System.Array.Empty<Transform>();
        public Animator[] rowers = System.Array.Empty<Animator>();
        [Min(.1f)] public float cruiseSpeed = 12f;
        [Min(1f)] public float turnSpeed = 22f;
        [Tooltip("Yaw from the root's blue Z axis to the model's bow. Use 90 when the hull is authored along local X.")]
        public float visualForwardYaw;
        [Min(0f)] public float pauseAtPoint = 2f;
        public float waterlineOffset;
        [Min(0f)] public float bobAmplitude = .18f;
        int target;
        float pause, phase;
        bool ready;
        Transform battleTarget;
        Vector3 checkpointHome;
        Quaternion checkpointRotation;
        Terrain terrain;
        Vector3 yieldDirection;
        readonly Collider[] yieldHits = new Collider[24];

        public void SetBattleTarget(Transform value) { battleTarget = value; pause = 0; }

        void Start()
        {
            if (tide == null) tide = FindAnyObjectByType<TideSystem>();
            if (tide == null || route == null || route.Length < 2) { enabled = false; return; }
            // The authored patrol roots can sit below the tide plane. Calibrate from the
            // visible hull once, then keep that depth as the tide moves. Using the root's
            // original Y made only the sails visible on the water.
            var hull = transform.Find("DaiVietPatrolVisual_Rebuilt");
            if (hull != null)
            {
                var renderers = hull.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    transform.position += Vector3.up * (tide.transform.position.y - 3.2f - bounds.min.y);
                }
            }
            waterlineOffset = transform.position.y - tide.transform.position.y;
            phase = Mathf.Repeat(transform.position.x * .013f + transform.position.z * .009f, Mathf.PI * 2f);
            target = NearestNextPoint(); ready = true;
            checkpointHome=transform.position;checkpointRotation=transform.rotation;
            terrain=Terrain.activeTerrain;
            SetRowing(true);
        }
        public void ResetForCheckpoint()
        {
            if(!ready)return;
            battleTarget=null;pause=0;var position=checkpointHome;position.y=TideSystem.CurrentWaterHeight+waterlineOffset;
            transform.SetPositionAndRotation(position,checkpointRotation);target=NearestNextPoint();SetRowing(true);
        }

        int NearestNextPoint()
        {
            int nearest = 0; float best = float.PositiveInfinity;
            for (int i = 0; i < route.Length; i++)
            {
                if (route[i] == null) continue;
                Vector3 d = route[i].position - transform.position; d.y = 0;
                if (d.sqrMagnitude < best) { best = d.sqrMagnitude; nearest = i; }
            }
            return (nearest + 1) % route.Length;
        }

        void Update() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            if (!ready || dt <= 0 || BeachBoatQuestSession.InteractionBlocked) return;
            dt=Mathf.Min(dt,.05f);
            if (YieldToPlayer(dt)) return;
            if (battleTarget != null && battleTarget.gameObject.activeInHierarchy)
            {
                Vector3 battleDirection = battleTarget.position - transform.position; battleDirection.y = 0;
                if (battleDirection.sqrMagnitude > 155f * 155f) MoveAlongBow(battleDirection, dt, cruiseSpeed * 1.28f);
                else
                {
                    Quaternion broadside = Quaternion.LookRotation(battleDirection.normalized, Vector3.up) * Quaternion.Euler(0, -visualForwardYaw - 18f, 0);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, broadside, turnSpeed * dt);
                    FloatOnly(); SetRowing(false);
                }
                return;
            }
            if (pause > 0) { pause -= dt; SetRowing(false); FloatOnly(); return; }
            var point = route[target];
            if (point == null) { target = (target + 1) % route.Length; return; }
            Vector3 direction = point.position - transform.position; direction.y = 0;
            if (direction.sqrMagnitude < 36f)
            {
                target = (target + 1) % route.Length; pause = pauseAtPoint; SetRowing(false); FloatOnly(); return;
            }
            MoveAlongBow(direction, dt, cruiseSpeed);
        }

        // Reserve a corridor around the small quest boat, including while it is at the pier.
        // A lateral current is intentional here: turning a long hull in place can sweep its
        // stern through the player even when its bow is steering away.
        bool YieldToPlayer(float dt)
        {
            var player=QuestRowboat.Instance;
            if(player==null || !player.gameObject.activeInHierarchy)return false;
            var away=transform.position-player.transform.position;away.y=0;
            var bow=transform.rotation*Quaternion.Euler(0,visualForwardYaw,0)*Vector3.forward;
            var relative=away+(bow*cruiseSpeed-player.transform.forward*player.Speed)*2;
            float distance=away.magnitude;
            if(distance>185 && relative.magnitude>160){yieldDirection=Vector3.zero;return false;}
            if(yieldDirection==Vector3.zero)
            {
                var tangent=Vector3.Cross(Vector3.up,bow).normalized;
                if(Vector3.Dot(tangent,away)<0)tangent=-tangent;
                yieldDirection=(tangent+away.normalized*.6f).normalized;
            }
            Vector3 best=Vector3.zero;float score=float.NegativeInfinity;
            for(int i=0;i<8;i++)
            {
                var direction=Quaternion.Euler(0,i*45,0)*yieldDirection;
                var candidate=transform.position+direction*30;
                if(!ClearWater(candidate,bow))continue;
                float separation=QuestRowboat.FlatDistance(candidate,player.transform.position);
                if(separation<distance+.5f)continue;
                float value=separation+Vector3.Dot(direction,yieldDirection)*15;
                if(value>score){score=value;best=direction;}
            }
            if(best!=Vector3.zero)
            {
                yieldDirection=best;
                var next=transform.position+best*Mathf.Max(24,cruiseSpeed*1.6f)*dt;
                if(ClearWater(next,bow))transform.position=next;
                SetRowing(true);
            }
            else SetRowing(false);
            FloatOnly();return true;
        }

        bool ClearWater(Vector3 position,Vector3 bow)
        {
            if(terrain==null)return false;
            for(int i=-1;i<=1;i++)
            {
                var p=position+bow*(i*46);var local=p-terrain.transform.position;
                if(local.x<0||local.z<0||local.x>terrain.terrainData.size.x||local.z>terrain.terrainData.size.z||
                    terrain.SampleHeight(p)+terrain.transform.position.y+15>TideSystem.CurrentWaterHeight)return false;
                int count=Physics.OverlapSphereNonAlloc(p+Vector3.up*2,16,yieldHits,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                if(count==yieldHits.Length)return false;
                for(int n=0;n<count;n++)
                {
                    var hit=yieldHits[n];
                    if(hit is TerrainCollider||hit.transform.IsChildOf(transform)||
                        hit.GetComponentInParent<QuestRowboat>()!=null||hit.GetComponentInParent<Animator>()!=null)continue;
                    return false;
                }
            }
            return true;
        }

        void MoveAlongBow(Vector3 direction, float dt, float speed)
        {
            Quaternion facing = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0, -visualForwardYaw, 0);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * dt);
            Vector3 bow = transform.rotation * Quaternion.Euler(0, visualForwardYaw, 0) * Vector3.forward;
            Vector3 position = transform.position + bow * speed * dt;
            position.y = tide.transform.position.y + waterlineOffset + Mathf.Sin(Time.time * 1.45f + phase) * bobAmplitude;
            transform.position = position; SetRowing(true);
        }

        void FloatOnly()
        {
            Vector3 p = transform.position;
            p.y = tide.transform.position.y + waterlineOffset + Mathf.Sin(Time.time * 1.45f + phase) * bobAmplitude;
            transform.position = p;
        }

        void SetRowing(bool moving)
        {
            float speed = moving ? 1f : .35f;
            foreach (var animator in rowers) if (animator != null && animator.speed != speed) animator.speed = speed;
        }
    }
}
