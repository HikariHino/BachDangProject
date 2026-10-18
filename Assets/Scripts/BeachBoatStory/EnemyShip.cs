using UnityEngine;
namespace BeachBoatStory
{
    public sealed class EnemyShip : MonoBehaviour
    {
        public float moveSpeed=18,stakeHitDistance=12;
        public Transform targetPoint;
        public Vector3 spawnPoint;
        [HideInInspector] public float ambushDelay;
        public bool isDestroyed;
        bool battleStarted;
        float ambushStartedAt=-1;
        float destroyedTime;
        Vector3 sinkStart;Quaternion sinkRotation;
        GameObject[] debris;
        Rigidbody[] debrisBodies;
        public void Activate()
        {
            isDestroyed=false;destroyedTime=0;battleStarted=true;ambushStartedAt=-1;
            var direction=targetPoint!=null?targetPoint.position-transform.position:Vector3.forward;direction.y=0;
            transform.rotation=direction.sqrMagnitude>.001f?Quaternion.LookRotation(direction):Quaternion.identity;
            EnsureDebrisPool();
            gameObject.SetActive(true);
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
            var end=targetPoint.position;end.y=pos.y;
            var next=Vector3.MoveTowards(pos,end,Mathf.Max(0,moveSpeed)*dt);
            var direction=end-pos;
            if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),1-Mathf.Exp(-2*dt));
            transform.position=next;
            var timeline=BachDangBattleTimeline.Instance;
            bool ambushActive=timeline==null||timeline.CurrentMoment>=BachDangBattleTimeline.BattleMoment.Ambush;
            if(ambushActive)
            {
                if(ambushStartedAt<0)ambushStartedAt=Time.time;
                // Let the exchange of fire and the converging Vietnamese boats remain visible
                // before each successive enemy group reaches the stakes.
                if(Time.time-ambushStartedAt>=1.5f+ambushDelay)CheckStakeHit(pos,next);
            }
        }
        void CheckStakeHit(Vector3 from,Vector3 to)
        {
            var zone=RiverStakeZone.Instance;if(zone==null)return;
            foreach(var point in zone.placements)
            {
                if(point==null||!point.planted)continue;
                foreach(int index in point.stakeIndices)
                {
                    if(index<0||index>=zone.fieldStakes.Length)continue;
                    var stake=zone.fieldStakes[index];if(stake==null||!stake.gameObject.activeInHierarchy)continue;
                    var renderer=stake.GetComponentInChildren<Renderer>();
                    if(renderer==null||renderer.bounds.max.y<=TideSystem.CurrentWaterHeight)continue;
                    var p=renderer.bounds.center;p.y=from.y;
                    var delta=to-from;
                    float t=delta.sqrMagnitude>.001f?Mathf.Clamp01(Vector3.Dot(p-from,delta)/delta.sqrMagnitude):0;
                    if(Vector3.Distance(from+delta*t,p)<=stakeHitDistance){TakeDamage(p);return;}
                }
            }
        }
        public void TakeDamage()
        { TakeDamage(transform.position+transform.forward*35); }
        public void TakeDamage(Vector3 impactPoint)
        {
            if(!battleStarted||isDestroyed)return;
            isDestroyed=true;destroyedTime=0;sinkStart=transform.position;sinkRotation=transform.rotation;
            LaunchDebris(impactPoint);
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
        void OnDestroy(){if(debris==null)return;foreach(var piece in debris)if(piece!=null)Destroy(piece);}
    }
}
