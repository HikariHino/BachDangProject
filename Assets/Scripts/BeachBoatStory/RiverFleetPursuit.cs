using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>The fleet follows the bait as spaced parallel lanes, then commits to its trap berths.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class RiverFleetPursuit : MonoBehaviour
    {
        public enum LureStage { Approach, Pursuit, EnteringTrap }
        public LureStage Stage {get;private set;}
        public bool IsPursuing=>Stage!=LureStage.Approach;
        public bool HasEnteredTrap=>Stage==LureStage.EnteringTrap;
        public Vector3 FormationAnchor {get;private set;}
        public Vector3 GuidePosition
        {
            get
            {
                Initialize();
                var zone=RiverStakeZone.Instance;
                if(IsPursuing&&zone!=null)
                    return new Vector3(Mathf.Clamp(origin.x,zone.FieldBounds.min.x+90,zone.FieldBounds.max.x-90),
                        TideSystem.CurrentWaterHeight,zone.FieldBounds.min.z-180);
                var boat=QuestRowboat.Instance;var best=FormationAnchor;float distance=float.PositiveInfinity;
                foreach(var ship in Fleet.enemyShips)
                {
                    if(ship==null||!ship.gameObject.activeInHierarchy||ship.isDestroyed)continue;
                    float d=QuestRowboat.FlatDistance(ship.transform.position,boat!=null?boat.transform.position:best);
                    if(d>=distance)continue;distance=d;
                    // Aim ahead of its bow, without steering the rower into the hull.
                    best=ship.transform.position+Vector3.back*(ship.hullHalfLength+260);
                }
                best.y=TideSystem.CurrentWaterHeight;return best;
            }
        }
        EnemyFleetDirector Fleet=>GetComponent<EnemyFleetDirector>();
        public bool TryEscortGoal(AmbientRiverBoat escort,out Vector3 goal)
        {
            goal=default;
            var player=QuestRowboat.Instance;var zone=RiverStakeZone.Instance;
            if(!Fleet.BattleStarted||RiverBattleObjective.Counterattack||!escort.lightPatrol||player==null||zone==null)return false;
            var boats=RiverBoardingCombat.Instance?.friendlyBoats;
            if(boats==null)return false;
            int slot=0;bool registered=false;
            foreach(var boat in boats)
            {if(boat==escort){registered=true;break;}if(boat!=null&&boat.lightPatrol)slot++;}
            if(!registered)return false;
            // Keep the lure flotilla on the western flank, leaving both the player's
            // bow and every incoming enemy lane clear. Rear ranks have separate stations.
            float edge=float.PositiveInfinity;
            foreach(var ship in Fleet.enemyShips)if(ship!=null)edge=Mathf.Min(edge,ship.spawnPoint.x-ship.stakeHitDistance-120);
            float radius=BoatHullCollision.Get(escort.transform).Radius;
            float x=Mathf.Min(player.transform.position.x-260,edge-radius-escort.trafficPadding-180)-(slot%2)*240;
            float z=Mathf.Max(player.transform.position.z-220,zone.FieldBounds.max.z+320)+(slot/2)*280;
            goal=new Vector3(x,TideSystem.CurrentWaterHeight,z);return true;
        }
        Vector3 origin;
        bool initialized;
        public void ResetLure()
        {Stage=LureStage.Approach;initialized=false;FormationAnchor=origin=Vector3.zero;}
        void Initialize()
        {
            if(initialized)return;
            float minZ=float.PositiveInfinity,minX=float.PositiveInfinity,maxX=float.NegativeInfinity;
            foreach(var ship in Fleet.enemyShips)if(ship!=null)
            {minZ=Mathf.Min(minZ,ship.spawnPoint.z);minX=Mathf.Min(minX,ship.spawnPoint.x);maxX=Mathf.Max(maxX,ship.spawnPoint.x);}
            origin=new Vector3((minX+maxX)*.5f,TideSystem.CurrentWaterHeight,minZ);
            FormationAnchor=origin;initialized=true;
        }
        void Update()=>Step(Time.deltaTime);
        public void Step(float dt)
        {
            if(dt<=0||!Fleet.BattleStarted||Fleet.EbbStarted||RiverInvasionIntro.IsRunning)return;
            Initialize();var boat=QuestRowboat.Instance;var zone=RiverStakeZone.Instance;
            if(boat==null||zone==null)return;
            bool rowing=boat.IsAboard&&!boat.IsBroken&&!(RiverBoardingCombat.Instance?.PlayerAway??false);
            if(Stage==LureStage.Approach)
            {
                if(rowing)foreach(var ship in Fleet.enemyShips)
                {
                    if(ship==null||!ship.gameObject.activeInHierarchy||ship.isDestroyed)continue;
                    if(QuestRowboat.FlatDistance(ship.transform.position,boat.transform.position)>ship.hullHalfLength+340)continue;
                    Stage=LureStage.Pursuit;BachDangStoryManager.Instance?.UpdateUI();break;
                }
                if(Stage==LureStage.Approach)
                {
                    var hold=origin;hold.z=zone.FieldBounds.max.z+900;
                    // Visible approach, but never enter the trap without the player's bait.
                    if(FormationAnchor.z>hold.z)FormationAnchor=Vector3.MoveTowards(FormationAnchor,hold,12*dt);
                    return;
                }
            }
            if(Stage!=LureStage.Pursuit||!rowing)return;
            var p=boat.transform.position;var field=zone.FieldBounds;
            // The rower must actually cross the field after meeting the enemy fleet.
            if(p.z<=field.center.z&&p.x>field.min.x+40&&p.x<field.max.x-40)
            {Stage=LureStage.EnteringTrap;BachDangStoryManager.Instance?.UpdateUI();return;}
            float longest=0;foreach(var ship in Fleet.enemyShips)if(ship!=null)longest=Mathf.Max(longest,ship.hullHalfLength);
            var follow=new Vector3(origin.x+Mathf.Clamp(p.x-origin.x,-120,120),TideSystem.CurrentWaterHeight,p.z+longest+180);
            // Do not reverse the rear ranks through the leading ranks when the bait circles.
            follow.z=Mathf.Min(follow.z,FormationAnchor.z);
            var anchor=FormationAnchor;
            float advance=Mathf.Min(Mathf.Max(0,anchor.z-follow.z),26*dt);
            // Course changes require forward progress, so circling bait cannot
            // make the entire formation crab sideways at a stationary marker.
            if(Mathf.Abs(follow.x-anchor.x)>12)anchor.x=Mathf.MoveTowards(anchor.x,follow.x,advance*.22f);
            anchor.z-=advance;anchor.y=TideSystem.CurrentWaterHeight;FormationAnchor=anchor;
        }
        public bool TryDestination(EnemyShip ship,out Vector3 destination,out bool finalLeg)
        {
            destination=default;finalLeg=false;
            if(!Fleet.BattleStarted)return false;
            Initialize();
            if(HasEnteredTrap){destination=ship.targetPoint.position;finalLeg=true;return true;}
            destination=FormationAnchor+(ship.spawnPoint-origin);destination.y=TideSystem.CurrentWaterHeight;return true;
        }
        public void FillSave(BachDangSaveData.Fleet data)
        {Initialize();data.lureVersion=1;data.lureStage=(int)Stage;data.lureAnchor=FormationAnchor;}
        public void RestoreSave(BachDangSaveData.Fleet data)
        {
            initialized=false;Initialize();
            // Old saves already have ships sailing to their berths; retain that progress.
            Stage=data.lureVersion==0||data.ebb?LureStage.EnteringTrap:(LureStage)Mathf.Clamp(data.lureStage,0,2);
            if(data.lureVersion>0)FormationAnchor=data.lureAnchor;
        }
    }
}
