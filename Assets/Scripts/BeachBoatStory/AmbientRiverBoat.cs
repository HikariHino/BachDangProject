using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    [DisallowMultipleComponent]
    public sealed class AmbientRiverBoat : MonoBehaviour
    {
        public TideSystem tide;
        public Transform[] route=Array.Empty<Transform>();
        public Animator[] rowers=Array.Empty<Animator>();
        public float cruiseSpeed=24,turnSpeed=32,visualForwardYaw,pauseAtPoint=.6f,waterlineOffset,bobAmplitude=.12f;
        public bool IsReturningToHarbor {get;private set;}
        public bool HasReturnedToHarbor {get;private set;}
        public bool IsAtBattleStation {get;private set;}
        public Vector3 ReturnBerth=>returnBerth;
        public int PlannedDetours {get;private set;}
        public bool HasNavigableRoute {get;private set;}
        readonly List<Vector3> path=new List<Vector3>();
        BoatHullCollision hull;
        Transform battleTarget;
        Transform yieldThreat;
        float yieldUntil;
        Vector3 checkpointHome,returnBerth,lastGoal;
        Quaternion checkpointRotation;
        int target,pathIndex;
        float pause,phase,clock,nextPlan,blocked;
        bool ready;
        bool liveUpdate;
        void Start()
        {
            if(tide==null)tide=FindAnyObjectByType<TideSystem>();
            if(tide==null||route.Length<2){enabled=false;return;}
            var visual=transform.Find("DaiVietPatrolVisual_Rebuilt");
            if(visual!=null)
            {
                var renderers=visual.GetComponentsInChildren<Renderer>(true);
                if(renderers.Length>0){var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);transform.position+=Vector3.up*(tide.transform.position.y-3.2f-b.min.y);}
            }
            waterlineOffset=transform.position.y-tide.transform.position.y;
            hull=BoatHullCollision.Get(transform);hull.Initialize();
            checkpointHome=transform.position;checkpointRotation=transform.rotation;
            phase=Mathf.Repeat(transform.position.x*.013f+transform.position.z*.009f,Mathf.PI*2);
            target=NearestNextPoint();ready=true;SetRowing(true);
        }
        int NearestNextPoint()
        {
            int index=0;float distance=float.PositiveInfinity;
            for(int i=0;i<route.Length;i++)if(route[i]!=null){float d=QuestRowboat.FlatDistance(route[i].position,transform.position);if(d<distance){distance=d;index=i;}}
            return (index+1)%route.Length;
        }
        public void SetBattleTarget(Transform value)
        {
            battleTarget=value;pause=0;
            if(value!=null){IsReturningToHarbor=HasReturnedToHarbor=false;}
        }
        public void YieldTo(Transform vessel){yieldThreat=vessel;yieldUntil=clock+5;}
        bool YieldFromTraffic(float dt)
        {
            if(yieldThreat==null||clock>yieldUntil||QuestRowboat.FlatDistance(transform.position,yieldThreat.position)>420)return false;
            var away=transform.position-yieldThreat.position;away.y=0;if(away.sqrMagnitude<1)away=-transform.forward;
            for(int i=0;i<8;i++)
            {
                int turn=i==0?0:((i+1)/2)*35*(i%2==1?1:-1);var direction=Quaternion.Euler(0,turn,0)*away.normalized;
                var next=transform.position+direction*Mathf.Max(18,cruiseSpeed)*dt;
                if(QuestRowboat.FlatDistance(next,yieldThreat.position)<=away.magnitude||!hull.CanMove(transform.position,transform.rotation,next,transform.rotation))continue;
                transform.position=next;Physics.SyncTransforms();path.Clear();nextPlan=0;SetRowing(true);return true;
            }
            SetRowing(false);return true;
        }
        public void ReturnToHarbor(int slot)
        {
            battleTarget=null;IsAtBattleStation=false;HasReturnedToHarbor=false;
            var dock=QuestRowboat.Instance!=null?QuestRowboat.Instance.dockPoint.position:checkpointHome;
            var used=new List<Vector3>();if(QuestRowboat.Instance!=null)used.Add(QuestRowboat.Instance.transform.position);
            foreach(var other in RiverBoardingCombat.Instance?.friendlyBoats??Array.Empty<AmbientRiverBoat>())
                if(other!=this&&(other.IsReturningToHarbor||other.HasReturnedToHarbor))used.Add(other.ReturnBerth);
            var formation=RiverBoardingCombat.Instance.friendlyBoats;int rank=0;
            foreach(var other in formation)if(other!=this&&(other.transform.position.z<transform.position.z||(Mathf.Abs(other.transform.position.z-transform.position.z)<.01f&&string.CompareOrdinal(other.name,name)<0)))rank++;
            var requested=dock+Vector3.right*140+Vector3.forward*((rank-(formation.Length-1)*.5f)*280);
            if(!RiverWaterNavigation.NearestClear(requested,hull.Radius,out returnBerth,used,hull.Radius*4+140))returnBerth=checkpointHome;
            returnBerth.y=TideSystem.CurrentWaterHeight+waterlineOffset;
            IsReturningToHarbor=true;path.Clear();nextPlan=0;SetRowing(true);
        }
        public void RestorePosition(Vector3 position,Quaternion rotation)
        {
            if(hull==null)hull=BoatHullCollision.Get(transform);
            if(!hull.CanOccupy(position,rotation)&&RiverWaterNavigation.NearestClear(position,hull.Radius,out var repaired))
            {repaired.y=TideSystem.CurrentWaterHeight+waterlineOffset;position=repaired;}
            position.y=TideSystem.CurrentWaterHeight+waterlineOffset;transform.SetPositionAndRotation(position,rotation);Physics.SyncTransforms();path.Clear();nextPlan=0;
        }
        public void ResetForCheckpoint()
        {
            if(!ready)return;battleTarget=null;IsReturningToHarbor=HasReturnedToHarbor=IsAtBattleStation=false;
            var p=checkpointHome;p.y=TideSystem.CurrentWaterHeight+waterlineOffset;RestorePosition(p,checkpointRotation);target=NearestNextPoint();pause=blocked=0;SetRowing(true);
        }
        void Update(){liveUpdate=true;Step(Time.deltaTime);liveUpdate=false;}
        public void Step(float dt)
        {
            if(!ready||dt<=0)return;
            // Water level is the support plane, even while an order/dialogue pauses navigation.
            var afloat=transform.position;afloat.y=TideSystem.CurrentWaterHeight+waterlineOffset;transform.position=afloat;
            if(BeachBoatQuestSession.InteractionBlocked)return;
            dt=Mathf.Min(dt,.05f);clock+=dt;
            if(YieldFromTraffic(dt))return;
            var story=BachDangStoryManager.Instance;
            if(story!=null&&(story.currentState==BachDangStoryManager.StoryState.Act3_Victory||story.currentState==BachDangStoryManager.StoryState.Act3_Celebration)&&!IsReturningToHarbor&&!HasReturnedToHarbor)
            {int slot=Array.IndexOf(RiverBoardingCombat.Instance.friendlyBoats,this);if(slot>=0)ReturnToHarbor(slot);}
            if(HasReturnedToHarbor){FloatOnly();SetRowing(false);return;}
            if(IsReturningToHarbor)
            {
                if(MoveTo(returnBerth,dt,Mathf.Max(24,cruiseSpeed*1.25f)))
                {IsReturningToHarbor=false;HasReturnedToHarbor=true;SetRowing(false);}return;
            }
            if(battleTarget!=null&&battleTarget.gameObject.activeInHierarchy)
            {
                // Project the attack station onto navigable water OUTSIDE the stake field.
                IsAtBattleStation=MoveTo(battleTarget.position,dt,cruiseSpeed*1.28f);return;
            }
            IsAtBattleStation=false;
            if(pause>0){pause-=dt;FloatOnly();SetRowing(false);return;}
            var point=route[target];if(point==null){target=(target+1)%route.Length;return;}
            if(MoveTo(point.position,dt,cruiseSpeed)){target=(target+1)%route.Length;pause=pauseAtPoint;path.Clear();}
        }
        bool MoveTo(Vector3 goal,float dt,float speed)
        {
            if(path.Count==0||QuestRowboat.FlatDistance(goal,lastGoal)>80||(blocked>1&&clock>=nextPlan))
            {
                if(clock<nextPlan){FloatOnly();SetRowing(false);return false;}
                if(liveUpdate&&!RiverWaterNavigation.ReservePlanningFrame()){FloatOnly();SetRowing(false);return false;}
                nextPlan=clock+1.5f;lastGoal=goal;pathIndex=0;
                HasNavigableRoute=RiverWaterNavigation.FindPath(transform.position,goal,hull.Radius,path,transform,!IsReturningToHarbor);
                if(!HasNavigableRoute){FloatOnly();SetRowing(false);return false;}PlannedDetours++;
            }
            while(pathIndex<path.Count&&QuestRowboat.FlatDistance(transform.position,path[pathIndex])<12)pathIndex++;
            if(pathIndex>=path.Count){FloatOnly();SetRowing(false);return true;}
            var direction=path[pathIndex]-transform.position;direction.y=0;
            // Grid nodes are turn points. Slowing at corners keeps a long hull on its route.
            var desired=Quaternion.LookRotation(direction)*Quaternion.Euler(0,-visualForwardYaw,0);
            var rotation=Quaternion.RotateTowards(transform.rotation,desired,turnSpeed*dt);
            float angle=Quaternion.Angle(transform.rotation,desired);
            float travel=angle>35?0:Mathf.Min(speed*dt,direction.magnitude);
            var bow=rotation*Quaternion.Euler(0,visualForwardYaw,0)*Vector3.forward;
            var next=transform.position+bow*travel;next.y=TideSystem.CurrentWaterHeight+waterlineOffset;
            bool intendedTravel=travel>.01f;
            if(!TrafficClear(next)){next=transform.position;travel=0;}
            bool clear=hull.CanMove(transform.position,transform.rotation,next,rotation);
            if(!clear&&intendedTravel)
            {next=transform.position;travel=0;clear=hull.CanMove(transform.position,transform.rotation,next,rotation);}
            if(clear)
            {
                transform.SetPositionAndRotation(next,rotation);Physics.SyncTransforms();
                // A blocked advance must still allow a clear in-place turn. Otherwise
                // the bow keeps facing traffic and can never align with its detour.
                blocked=intendedTravel&&travel<.01f?blocked+dt:0;SetRowing(travel>.01f);return false;
            }
            // Waiting is mandatory if no complete hull sweep is clear. Never ignore a
            // stake/other vessel to make progress, including when yielding at the quay.
            blocked+=dt;FloatOnly();SetRowing(false);return false;
        }
        bool TrafficClear(Vector3 next)
        {
            if(QuestRowboat.FlatDistance(next,transform.position)<.001f)return true;
            foreach(var other in BoatHullCollision.Active)
            {
                if(other==hull||other.SolidHull==null||!other.SolidHull.enabled)continue;
                float safe=hull.Radius+other.Radius+30;
                float now=QuestRowboat.FlatDistance(transform.position,other.transform.position),after=QuestRowboat.FlatDistance(next,other.transform.position);
                if(after<safe&&after<now+.001f)return false;
            }
            return true;
        }
        void FloatOnly()
        {
            var next=transform.position;next.y=TideSystem.CurrentWaterHeight+waterlineOffset+Mathf.Sin(Time.time*1.45f+phase)*bobAmplitude;
            if(hull.CanMove(transform.position,transform.rotation,next,transform.rotation))transform.position=next;
        }
        void SetRowing(bool moving){foreach(var rower in rowers)if(rower!=null)rower.speed=1;}
    }
}
