using UnityEngine;
namespace BeachBoatStory
{
    public sealed class EnemyShip : MonoBehaviour
    {
        public float moveSpeed=18,stakeHitDistance=12;
        [Min(1)] public float hullHalfLength=42f;
        public Transform targetPoint;
        public Transform[] routeWaypoints=new Transform[0];
        public Vector3 spawnPoint;
        [HideInInspector] public float ambushDelay;
        public bool isDestroyed;
        public bool SignalReady {get;private set;}
        public bool IsReleasedForAmbush=>attackReleased;
        public bool IsHullInsideStakeField=>RiverStakeZone.Instance!=null&&RiverStakeZone.Instance.TryHullContact(
            transform.position,transform.position,transform.forward,hullHalfLength,stakeHitDistance,false,out _);
        bool battleStarted;
        bool attackReleased;
        int routeIndex;
        float ambushStartedAt=-1;
        float destroyedTime;
        Vector3 sinkStart;Quaternion sinkRotation;
        GameObject[] debris;
        Rigidbody[] debrisBodies;
        public void Activate()
        {
            isDestroyed=false;destroyedTime=0;battleStarted=true;ambushStartedAt=-1;
            attackReleased=false;SignalReady=false;routeIndex=0;
            var first=routeWaypoints!=null&&routeWaypoints.Length>0?routeWaypoints[0]:targetPoint;
            var direction=first!=null?first.position-transform.position:Vector3.forward;direction.y=0;
            transform.rotation=direction.sqrMagnitude>.001f?Quaternion.LookRotation(direction):Quaternion.identity;
            gameObject.SetActive(true);
            // The imported junk was originally seated against the low-tide surface.
            // Re-seat its visible hull at the actual water level for every battle wave.
            var visual = transform.Find("RedSailJunkVisual");
            if (visual != null)
            {
                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    visual.position += Vector3.up * (TideSystem.CurrentWaterHeight - 4f - bounds.min.y);
                }
            }
            EnsureDebrisPool();
        }
        void Update()=>Step(Time.deltaTime);
        public void Step(float dt)
        {
            if(dt<=0||!battleStarted)return;
            if(isDestroyed)
            {
                destroyedTime+=dt;float t=Mathf.Clamp01(destroyedTime/4.2f);
                float fall=Mathf.SmoothStep(0,1,t);
                transform.SetPositionAndRotation(sinkStart-transform.forward*(3f*Mathf.Sin(t*Mathf.PI))-Vector3.up*35*fall,
                    sinkRotation*Quaternion.Euler(28*fall,0,32*fall));
                if(t>=1)
                {
                    HideDebris();
                    battleStarted=false;
                    gameObject.SetActive(false);
                    // Victory and the rising tide wait until the final sinking is visible.
                    BachDangStoryManager.Instance?.OnEnemyShipDestroyed();
                }
                return;
            }
            var pos=transform.position;pos.y=TideSystem.CurrentWaterHeight;transform.position=pos;
            if(targetPoint==null)return;
            Transform destination=targetPoint;
            if(routeWaypoints!=null)
                while(routeIndex<routeWaypoints.Length)
                {
                    var waypoint=routeWaypoints[routeIndex];
                    if(waypoint==null){routeIndex++;continue;}
                    if(QuestRowboat.FlatDistance(pos,waypoint.position)<=9){routeIndex++;continue;}
                    destination=waypoint;break;
                }
            bool finalLeg=destination==targetPoint;
            var end=destination.position;end.y=pos.y;
            float distance=Vector3.Distance(pos,end);
            // The player must give the counterattack order when the hull reaches the trap.
            // Holding at a readable distance also prevents a ship from passing its stake
            // while the tide is still high or the soldier is rowing into position.
            float holdDistance=hullHalfLength+stakeHitDistance*.5f;
            if(finalLeg&&!attackReleased&&distance<=holdDistance+.5f)
            {SignalReady=true;return;}
            float step=Mathf.Max(0,moveSpeed)*dt;
            if(finalLeg&&!attackReleased)step=Mathf.Min(step,Mathf.Max(0,distance-holdDistance));
            var next=Vector3.MoveTowards(pos,end,step);
            if(finalLeg&&!attackReleased&&Vector3.Distance(next,end)<=holdDistance+.5f)SignalReady=true;
            var direction=end-pos;
            if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),1-Mathf.Exp(-2*dt));
            transform.position=next;
            var timeline=BachDangBattleTimeline.Instance;
            bool ambushActive=timeline==null||timeline.CurrentMoment>=BachDangBattleTimeline.BattleMoment.Ambush;
            if(ambushActive&&attackReleased)
            {
                // A visible stake touching the hull is the impact; no extra wave timer delays it.
                CheckStakeHit(pos,next);
            }
        }
        public void ReleaseForAmbush()
        {
            if(!battleStarted||isDestroyed)return;
            attackReleased=true;SignalReady=false;ambushStartedAt=Time.time;
        }
        void CheckStakeHit(Vector3 from,Vector3 to)
        {
            var zone=RiverStakeZone.Instance;if(zone==null)return;
            if(zone.TryHullContact(from,to,transform.forward,hullHalfLength,stakeHitDistance,true,out var point))TakeDamage(point);
        }
        public void TakeDamage()
        { TakeDamage(transform.position+transform.forward*35); }
        public void TakeDamage(Vector3 impactPoint)
        {
            if(!battleStarted||isDestroyed)return;
            isDestroyed=true;destroyedTime=0;sinkStart=transform.position;sinkRotation=transform.rotation;
            LaunchDebris(impactPoint);
            BeachBoatSoundscape.Instance?.PlayCrash();
            FindAnyObjectByType<RiverBattleChoreography>()?.PlayShipImpact();
        }

        void EnsureDebrisPool()
        {
            if(debris!=null)return;
            const int count=12;debris=new GameObject[count];debrisBodies=new Rigidbody[count];
            var parent=transform.parent;Material material=null;
            foreach(var candidate in GetComponentsInChildren<Renderer>(true))
            {
                if(candidate.sharedMaterial==null)continue;
                material=candidate.sharedMaterial;
                string n=material.name.ToLowerInvariant();
                if(n.Contains("hajotest")||n.Contains("fedelzet"))break; // imported hull/deck wood
            }
            for(int i=0;i<count;i++)
            {
                var piece=GameObject.CreatePrimitive(PrimitiveType.Cube);piece.name=name+"_ManhGo_"+(i+1);piece.transform.SetParent(parent,true);
                piece.transform.localScale=new Vector3(Random.Range(.45f,1.1f),Random.Range(.25f,.55f),Random.Range(2.5f,6.5f));
                var renderer=piece.GetComponent<Renderer>();if(material!=null)renderer.sharedMaterial=material;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                var body=piece.AddComponent<Rigidbody>();body.mass=.35f;body.linearDamping=.12f;body.angularDamping=.08f;
                piece.SetActive(false);debris[i]=piece;debrisBodies[i]=body;
            }
        }
        void LaunchDebris(Vector3 impactPoint)
        {
            EnsureDebrisPool();
            for(int i=0;i<debris.Length;i++)
            {
                var piece=debris[i];var body=debrisBodies[i];piece.SetActive(true);
                piece.transform.position=impactPoint+transform.right*Random.Range(-12f,12f)+Vector3.up*Random.Range(1f,8f);
                piece.transform.rotation=Random.rotation;
                body.linearVelocity=transform.forward*Random.Range(8f,20f)+transform.right*Random.Range(-18f,18f)+Vector3.up*Random.Range(12f,28f);
                body.angularVelocity=Random.insideUnitSphere*Random.Range(4f,11f);
            }
        }
        void HideDebris(){if(debris==null)return;foreach(var piece in debris)if(piece!=null)piece.SetActive(false);}
        public void ResetForCheckpoint()
        {
            HideDebris();battleStarted=false;isDestroyed=false;attackReleased=false;SignalReady=false;routeIndex=0;ambushStartedAt=-1;destroyedTime=0;
            gameObject.SetActive(false);
        }
        void OnDestroy(){if(debris==null)return;foreach(var piece in debris)if(piece!=null)Destroy(piece);}
    }
}
