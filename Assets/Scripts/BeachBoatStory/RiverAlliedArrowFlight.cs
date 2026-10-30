using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>Bounded pooled deck arrows: draw, release, flight, then damage.</summary>
    public sealed class RiverAlliedArrowFlight
    {
        sealed class Shot {public Transform visual,carrier;public RiverFighterPose shooter;public EnemyDeckGuard target;public Vector3 start,end,previous;public float release,elapsed,duration;public bool active,launched;}
        readonly Shot[] shots=new Shot[48];readonly RaycastHit[] hits=new RaycastHit[32];
        readonly RiverBoardingCombat owner;
        public int Released {get;private set;}
        public RiverAlliedArrowFlight(RiverBoardingCombat combat){owner=combat;}
        public bool Begin(RiverFighterPose pose,Transform carrier,EnemyDeckGuard target)
        {
            Shot shot=null;for(int i=0;i<shots.Length;i++)
            {
                if(shots[i]==null)
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Ten_quan_ta_tren_thuyen_"+i;go.transform.SetParent(owner.transform,false);go.transform.localScale=new Vector3(.15f,3.8f,.15f);
                    var c=go.GetComponent<Collider>();c.enabled=false;Object.Destroy(c);go.GetComponent<Renderer>().sharedMaterial=pose.Weapon.GetComponentInChildren<Renderer>().sharedMaterial;go.SetActive(false);
                    shots[i]=new Shot{visual=go.transform};
                }
                if(!shots[i].active){shot=shots[i];break;}
            }
            if(shot==null)return false;
            shot.active=true;shot.launched=false;shot.shooter=pose;shot.carrier=carrier;shot.target=target;shot.release=Time.time+.73f;shot.elapsed=0;pose.BeginBowShot(target.AimPoint);return true;
        }
        public void Step(float dt)
        {
            foreach(var shot in shots)
            {
                if(shot==null||!shot.active)continue;
                if(!shot.launched)
                {
                    if(Time.time<shot.release)continue;
                    if(shot.shooter==null||!shot.shooter.gameObject.activeInHierarchy||shot.shooter.Traversal!=RiverFighterPose.Motion.Idle||shot.shooter.SwordVisible||shot.target==null||!shot.target.IsAlive){Stop(shot);continue;}
                    shot.start=shot.previous=shot.shooter.BowMuzzle;shot.end=shot.target.AimPoint;shot.duration=Mathf.Clamp(Vector3.Distance(shot.start,shot.end)/560,.25f,3.5f);shot.launched=true;shot.visual.gameObject.SetActive(true);Released++;BeachBoatSoundscape.Instance?.PlayBow(true);
                }
                shot.elapsed+=dt;float t=Mathf.Clamp01(shot.elapsed/shot.duration);
                var p=Vector3.Lerp(shot.start,shot.end,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*Mathf.Min(90,Vector3.Distance(shot.start,shot.end)*.09f));
                var movement=p-shot.previous;float nearest=movement.magnitude;Collider contact=null;
                int n=Physics.SphereCastNonAlloc(shot.previous,.25f,movement.normalized,hits,nearest,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide);
                for(int i=0;i<n;i++)
                {
                    var h=hits[i];if(h.transform.IsChildOf(shot.shooter.transform)||shot.carrier!=null&&h.transform.IsChildOf(shot.carrier))continue;
                    var guard=h.collider.GetComponentInParent<EnemyDeckGuard>();if(h.collider.isTrigger&&guard==null||guard!=null&&!guard.IsAlive||h.distance>nearest)continue;
                    nearest=h.distance;contact=h.collider;p=h.point;
                }
                if(movement.sqrMagnitude>.001f)shot.visual.rotation=Quaternion.FromToRotation(Vector3.up,movement);shot.visual.position=p;shot.previous=p;
                if(contact!=null)
                {var guard=contact.GetComponentInParent<EnemyDeckGuard>();if(guard!=null&&guard.IsAlive&&guard.Hit(1,false,CombatImpactKind.Arrow))owner.RecordAlliedArrowHit();Stop(shot);}
                else if(t>=1)Stop(shot);
            }
        }
        static void Stop(Shot shot){shot.active=false;shot.visual.gameObject.SetActive(false);shot.target=null;shot.shooter=null;}
        public void Reset(){foreach(var shot in shots)if(shot!=null)Stop(shot);}
    }
}
