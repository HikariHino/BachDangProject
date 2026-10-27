using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    [DefaultExecutionOrder(400)]
    public sealed class RiverBoardingCombat : MonoBehaviour
    {
        public enum PlayerState { Rowboat, Swimming, Climbing, EnemyDeck, Land, FriendlyDeck }
        public static RiverBoardingCombat Instance {get;private set;}
        public GameObject swordPrefab,allyPrefab,enemyPrefab;
        public RuntimeAnimatorController swordController;
        public AmbientRiverBoat[] friendlyBoats=Array.Empty<AmbientRiverBoat>();
        public PlayerState State {get;private set;}
        public bool SwordDrawn {get;private set;}
        public bool PlayerAway=>State!=PlayerState.Rowboat;
        public bool TraversalActive=>QuestRowboat.Instance!=null&&QuestRowboat.Instance.IsAboard;
        public Transform PlayerDeckCarrier=>(State==PlayerState.EnemyDeck||State==PlayerState.FriendlyDeck)?deckAnchor:null;
        bool OnSolidDeck=>State==PlayerState.FriendlyDeck||(State==PlayerState.EnemyDeck&&deckShip!=null&&!deckShip.isDestroyed);
        public bool IsTraversing=>State==PlayerState.Swimming||State==PlayerState.Climbing;
        public bool IsBlocking=>SwordDrawn&&!IsTraversing&&Input.GetMouseButton(1);
        public int PlayerSwordHits {get;private set;}
        public int AlliedSwordHits {get;private set;}
        public int AlliedArrowHits {get;private set;}
        public int EnemySwordHits {get;private set;}
        public int SwimsStarted {get;private set;}
        public int ClimbsStarted {get;private set;}
        public int HostileBoardings {get;private set;}
        public bool AlliedAIEnabled {get;set;}=true;
        public RuntimeAnimatorController FootController {get;private set;}
        public int ActiveAllies {get{int n=0;foreach(var a in allies)if(a.health>0&&a.go.activeSelf)n++;return n;}}
        public int AlliedCasualties=>allies.Count(a=>a.health<=0)+defenders.Count(d=>d.health<=0);
        public int AlliedStrength=>allies.Count+defenders.Count;
        sealed class Ally
        {
            public readonly RiverActorNavigation navigation=new RiverActorNavigation();
            public GameObject go;public RiverFighterPose pose;public AmbientRiverBoat home;public EnemyDeckGuard target;public Transform carrier;
            public int health=6,state,arrows=2;public float swing,travel,respawn,shot;public Vector3 start,local;public int side;
        }
        readonly List<Ally> allies=new List<Ally>();
        sealed class Defender
        {public Transform soldier;public RiverFighterPose pose;public Vector3 station;public Quaternion stationRotation;public int health=4;public float nextAttack;public bool fighting;}
        readonly List<Defender> defenders=new List<Defender>();
        public int DefenderHits {get;private set;}
        public int DefenderDamageTaken {get;private set;}
        sealed class Hostile
        {public readonly RiverActorNavigation navigation=new RiverActorNavigation();public GameObject go;public EnemyDeckGuard guard;public RiverFighterPose pose;public EnemyShip source;public Transform boat;public Vector3 start,local;public int state;public float time,respawn;public bool fromWreck,targetPlayer;}
        public int ShipwreckSurvivors {get;private set;}
        public int ShipwreckDeaths {get;private set;}
        public int SurvivorsTargetingPlayer {get;private set;}
        public int SurvivorsTargetingAllies {get;private set;}
        readonly List<Hostile> hostiles=new List<Hostile>();
        readonly Dictionary<EnemyDeckGuard,RiverFighterPose> enemyPoses=new Dictionary<EnemyDeckGuard,RiverFighterPose>();
        readonly Dictionary<EnemyDeckGuard,float> enemyAttacks=new Dictionary<EnemyDeckGuard,float>();
        BeachBoatQuestSession session;RiverPlayerCombat archery;RiverFighterPose playerPose;
        EnemyShip deckShip;Transform deckAnchor;Vector3 deckLocal,climbStart,climbLocalStart;float climbTime,nextSlash;
        readonly RaycastHit[] meleeHits=new RaycastHit[32];
        bool wasBattle;int traversalInputFrame=-1;
        public bool UsedTraversalThisFrame=>traversalInputFrame==Time.frameCount;
        public BachDangSaveData.Boarding CaptureSave()
        {
            return new BachDangSaveData.Boarding {
                state=(int)State,sword=SwordDrawn,deckShip=deckAnchor!=null?deckAnchor.name:null,climbTime=climbTime,deckLocal=deckLocal,climbLocalStart=climbLocalStart,
                allies=allies.Select(a=>new BachDangSaveData.Fighter{id=a.go.name,active=a.go.activeSelf,boat=a.carrier!=null?a.carrier.name:null,health=a.health,state=a.state,arrows=a.arrows,time=a.travel,start=a.start,local=a.local,target=a.target!=null?BachDangSaveSystem.GuardId(a.target):null,position=a.go.transform.position,rotation=a.go.transform.rotation}).ToArray(),
                hostiles=hostiles.Select(h=>new BachDangSaveData.Fighter{id=BachDangSaveSystem.GuardId(h.guard),active=h.go.activeSelf,source=h.source!=null?h.source.name:null,boat=h.boat!=null?h.boat.name:null,state=h.state,time=h.time,start=h.start,local=h.local,fromWreck=h.fromWreck,targetPlayer=h.targetPlayer,position=h.go.transform.position,rotation=h.go.transform.rotation}).ToArray(),
                defenders=defenders.Select(d=>new BachDangSaveData.Fighter{id=d.soldier.GetComponentInParent<AmbientRiverBoat>(true).name+"/"+d.soldier.name,boat=d.soldier.GetComponentInParent<AmbientRiverBoat>(true).name,local=d.station,health=d.health,active=d.soldier.gameObject.activeSelf,position=d.soldier.position,rotation=d.soldier.rotation}).ToArray()};
        }
        public void RestoreSave(BachDangSaveData.Boarding data)
        {
            if(data==null)return;
            EnemyDeckGuard Guard(string id)=>archery.guards.FirstOrDefault(g=>BachDangSaveSystem.GuardId(g)==id);
            EnemyShip Ship(string id)=>EnemyFleetDirector.Instance.enemyShips.FirstOrDefault(s=>s.name==id);
            Transform Boat(string id)
            {
                if(QuestRowboat.Instance.name==id)return QuestRowboat.Instance.transform;
                var friendly=friendlyBoats.FirstOrDefault(b=>b.name==id);if(friendly!=null)return friendly.transform;
                return Ship(id)?.transform;
            }
            foreach(var saved in data.hostiles??Array.Empty<BachDangSaveData.Fighter>())
            {
                var guard=Guard(saved.id);if(guard==null)continue;
                var h=hostiles.FirstOrDefault(x=>x.guard==guard);
                if(h==null){h=new Hostile{go=guard.gameObject,guard=guard,pose=enemyPoses[guard]};hostiles.Add(h);}
                h.source=Ship(saved.source);h.boat=Boat(saved.boat);h.state=saved.state;h.time=saved.time;h.start=saved.start;h.local=saved.local;h.fromWreck=saved.fromWreck;h.targetPlayer=saved.targetPlayer;h.go.GetComponent<ActorSolidBody>().Carrier=h.state==3?h.boat:h.state==0?h.source?.transform:null;
                h.go.SetActive(saved.active);h.go.transform.SetPositionAndRotation(saved.position,saved.rotation);
                h.pose.FinishTraversal();h.pose.SetSword(h.state==3||h.state==4);if(h.state==3||h.state==4)h.guard.FitStandingOnDeck();
                if(h.state==1)h.pose.Traversal=RiverFighterPose.Motion.Swim;else if(h.state==2)h.pose.Traversal=RiverFighterPose.Motion.Climb;
            }
            foreach(var saved in data.allies??Array.Empty<BachDangSaveData.Fighter>())
            {
                var a=allies.FirstOrDefault(x=>x.go.name==saved.id);if(a==null)continue;
                a.health=saved.health;a.state=saved.state;a.arrows=saved.arrows;a.travel=saved.time;a.start=saved.start;a.local=saved.local;a.target=Guard(saved.target);a.carrier=Boat(saved.boat);if(a.carrier==null&&(a.state==2||a.state==3)&&a.target!=null)a.carrier=CarrierOf(a.target.transform);a.go.GetComponent<ActorSolidBody>().Carrier=a.state==3?a.carrier:a.state==0?a.home.transform:null;
                a.go.SetActive(saved.active);a.go.transform.SetPositionAndRotation(saved.position,saved.rotation);a.pose.FinishTraversal();a.pose.SetSword(a.state==3);
                if(a.state==1)a.pose.Traversal=RiverFighterPose.Motion.Swim;else if(a.state==2||a.state==4)a.pose.Traversal=RiverFighterPose.Motion.Climb;
            }
            foreach(var saved in data.defenders??Array.Empty<BachDangSaveData.Fighter>())
            {
                var d=defenders.FirstOrDefault(x=>x.soldier.GetComponentInParent<AmbientRiverBoat>(true).name+"/"+x.soldier.name==saved.id);if(d==null)continue;
                d.health=saved.health;d.soldier.gameObject.SetActive(saved.active);RestoreDefenderStation(d);
            }
            State=(PlayerState)data.state;SwordDrawn=data.sword;deckShip=Ship(data.deckShip);deckAnchor=deckShip!=null?deckShip.transform:Boat(data.deckShip);
            deckLocal=data.deckLocal;climbLocalStart=data.climbLocalStart;climbTime=data.climbTime;
            playerPose.FinishTraversal();playerPose.SetSword(SwordDrawn);
            if((State==PlayerState.EnemyDeck||State==PlayerState.FriendlyDeck||State==PlayerState.Land)&&!SwordDrawn)playerPose.Animator.runtimeAnimatorController=FootController;
            if(State==PlayerState.Swimming)playerPose.Traversal=RiverFighterPose.Motion.Swim;else if(State==PlayerState.Climbing)playerPose.Traversal=RiverFighterPose.Motion.Climb;
            QuestRowboat.Instance.SetShoreWalking(State==PlayerState.Land);
            if(State==PlayerState.Land&&SwordDrawn)playerPose.Animator.runtimeAnimatorController=swordController;
            wasBattle=BachDangStoryManager.Instance.currentState==BachDangStoryManager.StoryState.Act3_BattleInProgress;
            if(PlayerAway){var camera=session.storyCamera.GetComponent<Camera_Script>();camera.distance=70;camera.heightOffset=27;}
            if(OnSolidDeck){playerPose.Animator.Update(0);FitPlayerToDeck();}
        }
        void RestoreDefenderStation(Defender d)
        {
            var carrier=d.soldier.GetComponentInParent<AmbientRiverBoat>(true).transform;
            d.pose.Restore();d.pose.FinishTraversal();d.fighting=false;d.nextAttack=0;
            d.soldier.SetPositionAndRotation(carrier.TransformPoint(d.station),carrier.rotation*d.stationRotation);
            d.soldier.GetComponent<ActorSolidBody>().Carrier=carrier;
            var rig=d.pose.Animator;rig.enabled=true;rig.Update(0);
            if(BoatHullCollision.Get(carrier).TryDeckSurface(d.soldier.position,out var floor))
            {ActorSolidBody.WalkingDimensions(rig,out _,out float sole,out _);d.soldier.position+=Vector3.up*(floor.y+.15f-rig.transform.position.y-sole);}
            rig.enabled=false;
        }
        Vector3 HomeStation(Ally a)=>a.home.lightPatrol?a.home.transform.position+a.home.transform.forward*(a.side*17):a.home.transform.position+a.home.transform.forward*20+a.home.transform.right*(a.side*12);
        bool SnapToDeck(Transform actor,Transform carrier,Vector3 near,float rootOffset,out Vector3 local)
        {
            var hull=BoatHullCollision.Get(carrier);var p=carrier.InverseTransformPoint(near);var box=hull.SolidHull;var scale=carrier.lossyScale;
            p.x=Mathf.Clamp(p.x,box.center.x-box.size.x*.5f+4/Mathf.Abs(scale.x),box.center.x+box.size.x*.5f-4/Mathf.Abs(scale.x));
            p.z=Mathf.Clamp(p.z,box.center.z-box.size.z*.5f+7/Mathf.Abs(scale.z),box.center.z+box.size.z*.5f-7/Mathf.Abs(scale.z));
            near=carrier.TransformPoint(p);
            if(!hull.TryDeckSurface(near,out var floor)&&!hull.TryDeckSurface(carrier.TransformPoint(box.center),out floor)){local=default;return false;}
            actor.position=floor+Vector3.up*rootOffset;local=carrier.InverseTransformPoint(actor.position);return true;
        }
        public void ReconcileAfterLoad(BachDangSaveData data)
        {
            var migrated=new HashSet<string>(friendlyBoats.Where(b=>!(data.patrols??Array.Empty<BachDangSaveData.Patrol>()).Any(p=>p.name==b.name&&p.layoutVersion==AmbientRiverBoat.PatrolLayoutVersion)).Select(b=>b.name));
            foreach(var d in defenders)RestoreDefenderStation(d);
            foreach(var a in allies)
            {
                if(a.health<=0||!a.go.activeSelf)continue;
                if(a.state==0)
                {a.carrier=a.home.transform;SnapToDeck(a.go.transform,a.carrier,HomeStation(a),AllyRootOffset(a)+.15f,out a.local);}
                else if(a.state==3&&LiveDeck(a.carrier))
                {
                    var near=migrated.Contains(a.carrier.name)?a.carrier.position+a.carrier.forward*(a.side*17):a.carrier.TransformPoint(a.local);
                    if(!SnapToDeck(a.go.transform,a.carrier,near,AllyRootOffset(a)+.15f,out a.local))AllySwim(a);
                }
                else if((a.state==2||a.state==4)&&a.carrier!=null&&migrated.Contains(a.carrier.name))
                {a.go.transform.position=a.carrier.position;a.state=3;AllySwim(a);}
                a.go.GetComponent<ActorSolidBody>().Carrier=a.state==0?a.home.transform:a.state==3?a.carrier:null;
            }
            foreach(var h in hostiles)
            {
                if(!h.go.activeSelf||h.guard.Health<=0)continue;
                if(h.state==3&&LiveDeck(h.boat))
                {
                    var near=migrated.Contains(h.boat.name)?h.boat.position+h.boat.forward*((hostiles.IndexOf(h)%2==0?-1:1)*20):h.boat.TransformPoint(h.local);
                    if(SnapToDeck(h.go.transform,h.boat,near,.2f,out h.local)){h.go.GetComponent<ActorSolidBody>().Carrier=h.boat;h.guard.FitStandingOnDeck();}
                }
                else if(h.state==2&&h.boat!=null&&migrated.Contains(h.boat.name))
                {
                    var hull=BoatHullCollision.Get(h.boat);var at=h.boat.position;
                    if(hull.TryWaterExit(at,4,h.go.transform,out var exit))at=exit;
                    at.y=TideSystem.CurrentWaterHeight-4;h.go.transform.position=at;h.state=1;h.time=0;h.pose.Traversal=RiverFighterPose.Motion.Swim;h.go.GetComponent<ActorSolidBody>().Carrier=null;
                }
            }
            if(OnSolidDeck)
            {
                var near=migrated.Contains(deckAnchor.name)?deckAnchor.position-deckAnchor.forward*22:deckAnchor.TransformPoint(deckLocal);
                if(SnapToDeck(session.player.transform,deckAnchor,near,StandingRootOffset()+.15f,out deckLocal))MovePlayer(session.player.transform.position,session.player.transform.rotation);
                FitPlayerToDeck();
            }
        }
        // Evaluate after the animator/weapon poses. A capsule bottom is not the
        // animated boot sole, and a navigation hull top is not the plank deck.
        void LateUpdate()
        {
            if(OnSolidDeck)FitPlayerToDeck();
            foreach(var d in defenders)
            {
                if(d.health<=0||!d.soldier.gameObject.activeInHierarchy)continue;
                var carrier=d.soldier.GetComponentInParent<AmbientRiverBoat>(true);
                if(carrier!=null&&BoatHullCollision.Get(carrier.transform).TryDeckSurface(d.soldier.position,out var floor))
                {ActorSolidBody.WalkingDimensions(d.pose.Animator,out _,out float sole,out _);d.soldier.position+=Vector3.up*(floor.y+.15f-d.pose.Animator.transform.position.y-sole);}
            }
            foreach(var h in hostiles)
            {
                if(h.state!=3||!h.guard.IsAlive||h.boat==null||h.boat.GetComponent<AmbientRiverBoat>()==null)continue;
                var p=h.go.transform.position;
                if(BoatHullCollision.Get(h.boat).TryDeckSurface(p,out var floor))
                {p.y=floor.y+.2f;h.go.transform.position=p;h.local=h.boat.InverseTransformPoint(p);h.guard.FitStandingOnDeck();}
            }
        }
        void FitPlayerToDeck()
        {
            if(deckAnchor==null||playerPose==null||!BoatHullCollision.Get(deckAnchor).TryDeckSurface(session.player.transform.position,out var floor))return;
            var p=session.player.transform.position;p.y=floor.y+StandingRootOffset()+.15f;
            MovePlayer(p,session.player.transform.rotation);deckLocal=deckAnchor.InverseTransformPoint(p);
        }
        float StandingRootOffset()
        {
            ActorSolidBody.WalkingDimensions(playerPose.Animator,out _,out float sole,out _);
            return session.player.transform.position.y-playerPose.Animator.transform.position.y-sole;
        }
        void Awake(){Instance=this;session=GetComponent<BeachBoatQuestSession>();archery=GetComponent<RiverPlayerCombat>();}
        void Start()
        {
            FootController=Resources.Load<RuntimeAnimatorController>("Player_Neutral");
            swordController=Resources.Load<RuntimeAnimatorController>("River_Sword_Combat")??swordController;
            playerPose=session.player.AddComponent<RiverFighterPose>();playerPose.Initialize(session.player.GetComponentInChildren<Animator>(),swordPrefab,swordController,17);
            foreach(var guard in archery.guards)
            {guard.MatchPlayerSize();var pose=guard.gameObject.AddComponent<RiverFighterPose>();pose.Initialize(guard.visual.GetComponentInChildren<Animator>(),swordPrefab,swordController,17);enemyPoses[guard]=pose;enemyAttacks[guard]=0;}
            for(int i=0;i<friendlyBoats.Length*2;i++)
            {
                var go=Instantiate(allyPrefab,transform);go.name="Linh_Dai_Viet_Tiep_Chien_"+i;ActorSolidBody.Ensure(go,go.GetComponentInChildren<Animator>());
                var pose=go.AddComponent<RiverFighterPose>();pose.Initialize(go.GetComponentInChildren<Animator>(),swordPrefab,swordController,17);pose.Animator.Update(0);pose.MatchFriendlySize();
                go.GetComponent<ActorSolidBody>().Carrier=friendlyBoats[i/2].transform;
                allies.Add(new Ally{go=go,pose=pose,home=friendlyBoats[i/2],side=i%2==0?-1:1});go.SetActive(false);
            }
            var allGuards=new List<EnemyDeckGuard>(archery.guards);
            for(int i=0;i<4;i++)
            {
                var go=new GameObject("Linh_Nam_Han_Nhay_Tau_"+i);go.transform.SetParent(transform,false);go.SetActive(false);
                var model=Instantiate(enemyPrefab,go.transform,false);var guard=go.AddComponent<EnemyDeckGuard>();guard.visual=model.transform;guard.ship=EnemyFleetDirector.Instance.enemyShips[i];guard.waveIndex=i/2;guard.IsIndependentBoarder=true;
                var collider=go.AddComponent<CapsuleCollider>();collider.isTrigger=true;collider.radius=3;collider.height=16;collider.center=Vector3.up*8;
                guard.MatchPlayerSize();var pose=go.AddComponent<RiverFighterPose>();pose.Initialize(model.GetComponentInChildren<Animator>(),swordPrefab,swordController,17);
                hostiles.Add(new Hostile{go=go,guard=guard,pose=pose});enemyPoses[guard]=pose;enemyAttacks[guard]=0;allGuards.Add(guard);
            }
            archery.guards=allGuards.ToArray();
            foreach(var friendly in friendlyBoats)
            {
                var crew=friendly.GetComponentInChildren<FleetDeckCrew>();if(crew==null)continue;crew.EnsureRoster();
                foreach(var soldier in crew.soldiers)
                {ActorSolidBody.Ensure(soldier.gameObject,soldier.GetComponentInChildren<Animator>());var pose=soldier.gameObject.AddComponent<RiverFighterPose>();pose.Initialize(soldier.GetComponentInChildren<Animator>(),swordPrefab,swordController,17);pose.MatchFriendlySize();defenders.Add(new Defender{soldier=soldier,pose=pose,station=friendly.transform.InverseTransformPoint(soldier.position),stationRotation=Quaternion.Inverse(friendly.transform.rotation)*soldier.rotation});}
            }
        }
        public void ResetBattle(bool preserveWrecks=false)
        {
            bool keepTraversal=preserveWrecks&&TraversalActive;
            bool keepLand=keepTraversal&&State==PlayerState.Land;
            if(TraversalActive)QuestRowboat.Instance.SetShoreWalking(keepLand);
            if(playerPose!=null)
            {
                playerPose.Restore();
                if(TraversalActive)playerPose.Animator.runtimeAnimatorController=keepTraversal&&(keepLand||State==PlayerState.EnemyDeck||State==PlayerState.FriendlyDeck)?FootController:QuestRowboat.Instance.rowingController;
                if(keepTraversal&&State==PlayerState.Swimming)playerPose.Traversal=RiverFighterPose.Motion.Swim;
                if(keepTraversal&&State==PlayerState.Climbing)playerPose.Traversal=RiverFighterPose.Motion.Climb;
            }
            SwordDrawn=false;if(!keepTraversal){State=PlayerState.Rowboat;deckShip=null;deckAnchor=null;}
            foreach(var a in allies){a.go.SetActive(false);if(!preserveWrecks){a.health=6;a.state=0;}a.pose.Restore();}
            foreach(var d in defenders){d.pose.Restore();if(!preserveWrecks)d.health=4;d.fighting=false;d.soldier.gameObject.SetActive(d.health>0);if(d.health>0){d.pose.Animator.enabled=true;d.pose.Animator.Update(0);d.pose.Animator.enabled=false;}}
            DefenderHits=DefenderDamageTaken=0;
            foreach(var h in hostiles){h.pose.Restore();if(h.fromWreck&&!preserveWrecks){h.guard.IsIndependentBoarder=false;h.guard.RestoreDeck();h.guard.ResetGuard();}else h.go.SetActive(false);h.state=0;h.source=null;}
            hostiles.RemoveAll(h=>h.fromWreck);if(!preserveWrecks)ShipwreckSurvivors=ShipwreckDeaths=SurvivorsTargetingPlayer=SurvivorsTargetingAllies=0;
            foreach(var pair in enemyPoses){pair.Value.Restore();pair.Key.SetMelee(false);}
            wasBattle=false;
        }
        void Update()
        {
            bool battle=archery!=null&&archery.BattleActive;
            if(!battle)
            {
                if(wasBattle)ResetBattle(true);
                if(!TraversalActive||BeachBoatQuestSession.InputBlocked)return;
                ReadTraversalInput();StepPlayer(Time.deltaTime);return;
            }
            if(!wasBattle)
            {
                wasBattle=true;foreach(var a in allies){a.health=6;a.state=0;a.arrows=2;a.shot=Time.time+2;a.go.SetActive(true);a.pose.SetSword(true);}
                PlayerSwordHits=AlliedArrowHits=AlliedSwordHits=EnemySwordHits=SwimsStarted=ClimbsStarted=HostileBoardings=0;
            }
            if(BeachBoatQuestSession.InputBlocked)return;
            if(Cursor.lockState==CursorLockMode.Locked)
            {
                if(Input.GetKeyDown(KeyCode.Q))HandleActionKey(KeyCode.Q);
                ReadTraversalInput();
                if(SwordDrawn&&Input.GetMouseButtonDown(0))TrySlash();
            }
            StepPlayer(Time.deltaTime);StepAllies(Time.deltaTime);StepHostiles(Time.deltaTime);StepDefenders();StepEnemies(Time.deltaTime);
        }
        void ReadTraversalInput()
        {
            if(Cursor.lockState!=CursorLockMode.Locked)return;
            if(Input.GetKeyDown(KeyCode.F))HandleActionKey(KeyCode.F);
            if(Input.GetKeyDown(KeyCode.Space))HandleActionKey(KeyCode.Space);
        }
        public bool HandleActionKey(KeyCode key)
        {
            if(key==KeyCode.Q)return ToggleSword();
            if(key==KeyCode.Space&&(State==PlayerState.Swimming||State==PlayerState.Land))return TryTraverse();
            if(key==KeyCode.F&&State!=PlayerState.Swimming)return TryTraverse();
            return false;
        }
        public bool ToggleSword()
        {
            if(!archery.BattleActive||BeachBoatQuestSession.InteractionBlocked||IsTraversing)return false;
            archery.SetAim(false);SwordDrawn=!SwordDrawn;playerPose.SetSword(SwordDrawn);
            if(!SwordDrawn&&(State==PlayerState.EnemyDeck||State==PlayerState.FriendlyDeck||State==PlayerState.Land))playerPose.Animator.runtimeAnimatorController=FootController;return true;
        }
        public bool TrySlash()
        {
            if(!SwordDrawn||IsTraversing||!archery.BattleActive||BeachBoatQuestSession.InteractionBlocked||Time.time<nextSlash)return false;
            var vitals=session.player.GetComponent<PlayerVitals>();if(!vitals.TryExert(9,1))return false;
            nextSlash=Time.time+.72f;playerPose.Slash();PlaySwordSwing();
            var player=session.player.transform;
            foreach(var guard in archery.guards)
            {
                if(!guard.IsAlive)continue;var delta=guard.transform.position-player.position;
                if(Mathf.Abs(delta.y)>19)continue;delta.y=0;
                if(delta.magnitude<=32&&Vector3.Dot(player.forward,delta.normalized)>.15f&&CanMeleeReach(player,guard.transform,36)&&guard.Hit(1)){PlayerSwordHits++;}
            }
            return true;
        }
        public bool TryTraverse()
        {
            if(!TraversalActive||BeachBoatQuestSession.InteractionBlocked||State==PlayerState.Climbing)return false;
            if((State==PlayerState.Swimming||State==PlayerState.Land)&&TryReturnToRowboat())return true;
            if(State==PlayerState.Swimming)
            {
                traversalInputFrame=Time.frameCount;
                var nearest=NearestBoard(session.player.transform.position,out var ship,out var anchor,out var local);
                if(nearest>(ship!=null&&ship.isDestroyed?52:24))return false;deckShip=ship;deckAnchor=anchor;deckLocal=local;climbStart=session.player.transform.position;climbLocalStart=anchor.InverseTransformPoint(climbStart);climbTime=0;State=PlayerState.Climbing;playerPose.Traversal=RiverFighterPose.Motion.Climb;ClimbsStarted++;BeachBoatSoundscape.Instance?.PlayClimbGrip();return true;
            }
            if(State==PlayerState.Land)return false;
            var carrier=deckAnchor!=null?deckAnchor:QuestRowboat.Instance.transform;
            if(!BoatHullCollision.Get(carrier).TryWaterExit(session.player.transform.position,4,session.player.transform,out var exit))return false;
            traversalInputFrame=Time.frameCount;BeginSwim(exit);
            if(TryWalkAshore(exit))session.storyCamera.GetComponent<Camera_Script>().SnapToTarget();
            return true;
        }
        public bool NearRowboat
        {
            get
            {
                var boat=QuestRowboat.Instance;
                if(boat==null||!boat.IsAboard||!(State==PlayerState.Swimming||State==PlayerState.Land))return false;
                var box=BoatHullCollision.Get(boat.transform).SolidHull;
                var point=box.ClosestPoint(session.player.transform.position);
                return QuestRowboat.FlatDistance(point,session.player.transform.position)<=20&&Mathf.Abs(session.player.transform.position.y-TideSystem.CurrentWaterHeight)<45;
            }
        }
        public bool TryReturnToRowboat()
        {
            if(!NearRowboat||BeachBoatQuestSession.InteractionBlocked)return false;
            traversalInputFrame=Time.frameCount;archery.SetAim(false);playerPose.FinishTraversal();playerPose.SetSword(false);SwordDrawn=false;
            QuestRowboat.Instance.SetShoreWalking(false);playerPose.Animator.enabled=true;playerPose.Animator.runtimeAnimatorController=QuestRowboat.Instance.rowingController;
            State=PlayerState.Rowboat;deckShip=null;deckAnchor=null;
            QuestRowboat.Instance.ResumeRowing();QuestRowboat.Instance.UseSoldierBattleCamera();return true;
        }
        void BeginSwim(Vector3 p)
        {
            archery.SetAim(false);playerPose.SetSword(false);SwordDrawn=false;QuestRowboat.Instance.SetShoreWalking(false);
            deckShip=null;deckAnchor=null;p.y=TideSystem.CurrentWaterHeight-5;MovePlayer(p,session.player.transform.rotation);State=PlayerState.Swimming;playerPose.Traversal=RiverFighterPose.Motion.Swim;SwimsStarted++;
            BeachBoatSoundscape.Instance?.PlayWaterEntry();
            var camera=session.storyCamera.GetComponent<Camera_Script>();camera.distance=70;camera.heightOffset=27;camera.SnapToTarget();
        }
        static bool GroundAt(Vector3 point,out float height)=>GroundAt(point,out height,out _);
        static bool GroundAt(Vector3 point,out float height,out Vector3 normal)
        {
            foreach(var terrain in Terrain.activeTerrains)
            {
                var p=point-terrain.transform.position;var size=terrain.terrainData.size;
                if(p.x>=0&&p.z>=0&&p.x<=size.x&&p.z<=size.z)
                {height=terrain.SampleHeight(point)+terrain.transform.position.y;normal=terrain.terrainData.GetInterpolatedNormal(p.x/size.x,p.z/size.z);return true;}
            }
            height=0;normal=Vector3.up;return false;
        }
        float FeetOffset()
        {
            var c=session.player.GetComponent<CapsuleCollider>();
            return (c.center.y-c.height*.5f)*session.player.transform.lossyScale.y;
        }
        float PlayerBodyHeight
        {
            get{var c=session.player.GetComponent<CapsuleCollider>();return c.height*Mathf.Abs(session.player.transform.lossyScale.y);}
        }
        public float WadeExitDepth=>PlayerBodyHeight*.40f;
        public float SwimEntryDepth=>PlayerBodyHeight*.65f;
        public bool TryEnterWaterFromShore()
        {
            if(State!=PlayerState.Land)return false;
            var at=session.player.transform.position;
            if(!GroundAt(at,out float floor)||floor>=TideSystem.CurrentWaterHeight-SwimEntryDepth||
                at.y+FeetOffset()>=TideSystem.CurrentWaterHeight-SwimEntryDepth+1)return false;
            BeginSwim(at);return true;
        }
        public bool TryWalkAshore(Vector3 point)
        {
            // Low tide can leave the beach well above the swimming root. Judge
            // the actual support slope, not a fixed height band above the water.
            if(State!=PlayerState.Swimming||!GroundAt(point,out float floor,out var normal)||floor<TideSystem.CurrentWaterHeight-WadeExitDepth||Vector3.Angle(normal,Vector3.up)>55)return false;
            var capsule=session.player.GetComponent<CapsuleCollider>();
            var landing=point;landing.y=floor-FeetOffset()+.5f;
            float radius=capsule.radius*Mathf.Max(session.player.transform.lossyScale.x,session.player.transform.lossyScale.z)*.88f;
            float height=capsule.height*session.player.transform.lossyScale.y;
            var hits=Physics.OverlapCapsule(new Vector3(point.x,floor+radius+1,point.z),new Vector3(point.x,floor+height-radius,point.z),radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            foreach(var hit in hits)if(!(hit is TerrainCollider)&&!hit.transform.IsChildOf(session.player.transform))return false;
            playerPose.FinishTraversal();State=PlayerState.Land;deckShip=null;deckAnchor=null;
            MovePlayer(landing,Quaternion.Euler(0,session.player.transform.eulerAngles.y,0));
            QuestRowboat.Instance.SetShoreWalking(true);Physics.SyncTransforms();return true;
        }
        void MovePlayer(Vector3 p,Quaternion rotation)
        {session.player.transform.SetPositionAndRotation(p,rotation);var body=session.player.GetComponent<Rigidbody>();body.position=p;body.rotation=rotation;}
        public void StepPlayer(float dt)=>StepPlayer(dt,new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical")),Input.GetKey(KeyCode.LeftShift));
        public void StepPlayer(float dt,Vector2 movement,bool sprint=false)
        {
            if(dt<=0||State==PlayerState.Rowboat)return;
            if(State==PlayerState.Land)
            {
                TryEnterWaterFromShore();
                return;
            }
            // Falling water can expose the bank beneath a stationary swimmer.
            // Restore standing before applying the swim pose or water-height clamp.
            if(State==PlayerState.Swimming&&TryWalkAshore(session.player.transform.position))return;
            if((State==PlayerState.EnemyDeck||State==PlayerState.FriendlyDeck)&&(deckAnchor==null||!deckAnchor.gameObject.activeInHierarchy)){BeginSwim(session.player.transform.position);return;}
            if(State==PlayerState.Climbing)
            {
                if(deckAnchor==null||!deckAnchor.gameObject.activeInHierarchy){BeginSwim(session.player.transform.position);return;}
                climbTime+=dt;var destination=deckAnchor.TransformPoint(deckLocal);float t=Mathf.Clamp01(climbTime/1.8f);
                if(destination.y<TideSystem.CurrentWaterHeight-2){BeginSwim(session.player.transform.position);return;}
                var facing=deckAnchor.position-session.player.transform.position;facing.y=0;
                var from=deckAnchor.TransformPoint(climbLocalStart);var above=from;above.y=destination.y+2;
                var climb=t<.72f?Vector3.Lerp(from,above,Mathf.SmoothStep(0,1,t/.72f)):Vector3.Lerp(above,destination,Mathf.SmoothStep(0,1,(t-.72f)/.28f));
                var rail=from;rail.y=destination.y+1;rail+=deckAnchor.right*(Mathf.Sign(deckLocal.x)*-5);
                playerPose.ClimbRail=rail;playerPose.ClimbProgress=t;
                MovePlayer(climb,facing.sqrMagnitude>.01f?Quaternion.LookRotation(facing):session.player.transform.rotation);
                if(t>=1){State=deckShip!=null?PlayerState.EnemyDeck:PlayerState.FriendlyDeck;playerPose.FinishTraversal();SwordDrawn=true;playerPose.SetSword(true);if(OnSolidDeck){playerPose.Animator.Update(0);FitPlayerToDeck();}if(State==PlayerState.FriendlyDeck)CampaignProgress.Instance?.RecordFriendlyBoarding();}return;
            }
            var camera=session.storyCamera.transform;var forward=camera.forward;forward.y=0;forward.Normalize();var right=Vector3.Cross(Vector3.up,forward);
            var direction=Vector3.ClampMagnitude(forward*movement.y+right*movement.x,1);
            bool fast=sprint&&session.player.GetComponent<PlayerVitals>().TryExert(15,dt);
            var p=session.player.transform.position+direction*(State==PlayerState.Swimming?(fast?38:23):26)*dt;
            if(State==PlayerState.Swimming)
            {
                playerPose.SwimMoving=direction.sqrMagnitude>.01f;playerPose.SwimFast=fast;
                p=RiverHullCollision.Move(session.player.transform.position+Vector3.up*3,p+Vector3.up*3,4,false,session.player.transform)-Vector3.up*3;
                p.y=TideSystem.CurrentWaterHeight-5+Mathf.Sin(Time.time*3)*.3f;
                if(TryWalkAshore(p))return;
            }
            else
            {
                var carried=deckAnchor.TransformPoint(deckLocal);var proposed=carried+direction*26*dt;
                if(OnSolidDeck)
                {
                    var box=BoatHullCollision.Get(deckAnchor).SolidHull;var scale=deckAnchor.lossyScale;
                    deckLocal=deckAnchor.InverseTransformPoint(proposed);float margin=16;
                    deckLocal.x=Mathf.Clamp(deckLocal.x,box.center.x-box.size.x*.5f+margin/Mathf.Abs(scale.x),box.center.x+box.size.x*.5f-margin/Mathf.Abs(scale.x));
                    deckLocal.z=Mathf.Clamp(deckLocal.z,box.center.z-box.size.z*.5f+margin/Mathf.Abs(scale.z),box.center.z+box.size.z*.5f-margin/Mathf.Abs(scale.z));
                    p=deckAnchor.TransformPoint(deckLocal);
                    if(BoatHullCollision.Get(deckAnchor).TryDeckSurface(p,out var floor))p.y=floor.y+StandingRootOffset()+.15f;
                    else p=carried;
                }
                else if(deckShip.isDestroyed)
                {
                    if(deckShip.TryWreckSurfaceAt(proposed,out var surface)&&Mathf.Abs(surface.y-carried.y)<16)p=surface;
                    else if(deckShip.TryWreckSurfaceAt(carried,out surface))p=surface;
                    else {BeginSwim(carried);return;}
                    deckLocal=deckAnchor.InverseTransformPoint(p);
                }
                else
                {
                    deckLocal+=deckAnchor.InverseTransformDirection(direction)*26*dt;
                    deckLocal.x=Mathf.Clamp(deckLocal.x,-deckShip.stakeHitDistance*.65f,deckShip.stakeHitDistance*.65f);deckLocal.z=Mathf.Clamp(deckLocal.z,-deckShip.hullHalfLength*.65f,deckShip.hullHalfLength*.65f);p=deckAnchor.TransformPoint(deckLocal);
                }
                if(OnSolidDeck)
                {
                    var hull=BoatHullCollision.Get(deckAnchor);var floor=carried;hull.TryDeckSurface(carried,out floor);
                    float clearance=floor.y+6-carried.y;
                    p=RiverHullCollision.Move(carried+Vector3.up*clearance,p+Vector3.up*clearance,4,false,session.player.transform,hull)-Vector3.up*clearance;
                }
                else p=RiverHullCollision.Move(carried+Vector3.up*8,p+Vector3.up*8,4,false,session.player.transform)-Vector3.up*8;
                deckLocal=deckAnchor.InverseTransformPoint(p);
                playerPose.Walk(direction.sqrMagnitude>.01f);
            }
            MovePlayer(p,direction.sqrMagnitude>.01f?Quaternion.LookRotation(direction):session.player.transform.rotation);
            if(OnSolidDeck)FitPlayerToDeck();
        }
        float NearestBoard(Vector3 p,out EnemyShip ship,out Transform anchor,out Vector3 local)
        {
            ship=null;anchor=null;local=Vector3.zero;float best=float.PositiveInfinity;
            foreach(var candidate in EnemyFleetDirector.Instance.enemyShips)
            {
                if(candidate==null||!candidate.gameObject.activeInHierarchy)continue;
                var target=candidate.transform;
                if(candidate.isDestroyed)
                {
                    if(candidate.TryWreckBoardingPoint(p,out var surface))
                    {float distance=QuestRowboat.FlatDistance(p,surface);if(distance<best){best=distance;ship=candidate;anchor=target;local=target.InverseTransformPoint(surface);}}
                    continue;
                }
                foreach(int side in new[]{-1,1})
                {
                    var hull=BoatHullCollision.Get(target);var box=hull.SolidHull;var scale=target.lossyScale;
                    var point=box.center+new Vector3(side*(box.size.x*.5f-16/Mathf.Abs(scale.x)),0,0);
                    if(!hull.TryDeckSurface(target.TransformPoint(point),out var floor))continue;
                    var landing=floor+Vector3.up*(.5f-FeetOffset());point=target.InverseTransformPoint(landing);
                    var edge=point;edge.x=box.center.x+side*(box.size.x*.5f+6/Mathf.Abs(scale.x));var world=target.TransformPoint(edge);
                    float d=QuestRowboat.FlatDistance(p,world);if(d>=best)continue;best=d;ship=candidate;anchor=target;local=point;
                }
            }
            foreach(var friendly in friendlyBoats)
            {
                if(friendly==null||!friendly.gameObject.activeInHierarchy)continue;
                var hull=BoatHullCollision.Get(friendly.transform).SolidHull;var at=friendly.transform.InverseTransformPoint(p);var scale=friendly.transform.lossyScale;
                foreach(int side in new[]{-1,1})
                {
                    var point=hull.center+new Vector3(side*(hull.size.x*.5f-16/Mathf.Abs(scale.x)),hull.size.y*.5f+(.5f-FeetOffset())/Mathf.Abs(scale.y),Mathf.Clamp(at.z-hull.center.z,-hull.size.z*.35f,hull.size.z*.35f));
                    var world=friendly.transform.TransformPoint(point);
                    if(!BoatHullCollision.Get(friendly.transform).TryClearDeckLanding(world,5,session.player.transform,out var floor))continue;
                    world.y=floor.y-FeetOffset()+.5f;point=friendly.transform.InverseTransformPoint(world);
                    var edge=point;edge.x=hull.center.x+side*(hull.size.x*.5f+6/Mathf.Abs(scale.x));float d=QuestRowboat.FlatDistance(p,friendly.transform.TransformPoint(edge));
                    if(d>=best)continue;best=d;ship=null;anchor=friendly.transform;local=point;
                }
            }
            return best;
        }
        EnemyDeckGuard TargetFor(Ally ally)
        {
            EnemyDeckGuard best=null;float score=float.PositiveInfinity;
            foreach(var guard in archery.guards)if(guard.IsAlive)
            {
                float distance=(guard.transform.position-ally.go.transform.position).sqrMagnitude;
                // A nearby boarder is more urgent than a distant assigned enemy hull.
                if(CarrierOf(guard.transform)==ally.home.transform)distance*=.15f;
                if(distance<score){score=distance;best=guard;}
            }
            return best;
        }
        void AllySwim(Ally a)
        {
            var p=a.go.transform.position;
            if((a.state==2||a.state==3)&&a.carrier!=null&&BoatHullCollision.Get(a.carrier).TryWaterExit(p,4,a.go.transform,out var exit))p=exit;
            a.carrier=null;a.state=1;a.pose.SetSword(false);a.pose.SetRanged(false);a.pose.Traversal=RiverFighterPose.Motion.Swim;
            p.y=TideSystem.CurrentWaterHeight-4;a.go.transform.position=RiverHullCollision.Move(p,p,3.5f,true,a.go.transform);
            a.go.GetComponent<ActorSolidBody>().Carrier=null;
        }
        static float AllyRootOffset(Ally a)
        {
            ActorSolidBody.WalkingDimensions(a.pose.Animator,out _,out float sole,out _);
            return a.go.transform.position.y-a.pose.Animator.transform.position.y-sole;
        }
        bool LiveDeck(Transform carrier)=>carrier!=null&&(carrier.GetComponent<EnemyShip>()==null||!carrier.GetComponent<EnemyShip>().isDestroyed);
        public void StepAllies(float dt)
        {
            if(!AlliedAIEnabled){foreach(var a in allies)a.go.SetActive(false);return;}
            foreach(var a in allies)
            {
                if(a.health<=0){a.go.SetActive(false);continue;}
                var body=a.go.GetComponent<ActorSolidBody>();body.Carrier=a.state==0?a.home.transform:a.state==3||a.state==4?a.carrier:null;
                if(!RiverBattleObjective.Counterattack)
                {
                    // During the lure, crew stay aboard and never order their helmsman
                    // to chase a guard, shoot, or jump overboard. Repair old premature assaults too.
                    if(a.state!=0){a.pose.FinishTraversal();a.state=0;a.travel=0;}
                    a.target=null;a.carrier=a.home.transform;body.Carrier=a.carrier;
                    var station=HomeStation(a);var hull=BoatHullCollision.Get(a.carrier);
                    if(hull.TryDeckSurface(station,out var floor))station.y=floor.y+AllyRootOffset(a)+.15f;
                    a.local=a.carrier.InverseTransformPoint(station);a.go.transform.position=station;
                    a.pose.SetSword(false);a.pose.SetRanged(false);a.pose.Walk(false);a.home.SetBattleTarget(null);continue;
                }
                var candidate=TargetFor(a);
                if(a.target==null||!a.target.IsAlive||candidate!=null&&(candidate.transform.position-a.go.transform.position).sqrMagnitude<(a.target.transform.position-a.go.transform.position).sqrMagnitude*.5f)a.target=candidate;
                var home=HomeStation(a);
                var homeHull=BoatHullCollision.Get(a.home.transform);
                if(homeHull.TryDeckSurface(home,out var homeFloor))home.y=homeFloor.y+AllyRootOffset(a)+.15f;
                if(a.state==0)
                {
                    a.carrier=a.home.transform;a.local=a.carrier.InverseTransformPoint(home);a.go.transform.position=home;
                    a.pose.SetSword(false);a.pose.SetRanged(true);
                    if(a.target==null)continue;
                    a.home.SetBattleTarget(a.target.transform);var aim=a.target.transform.position-home;aim.y=0;
                    if(aim.sqrMagnitude>.01f)a.go.transform.rotation=Quaternion.LookRotation(aim);
                    if(CarrierOf(a.target.transform)==a.home.transform)
                    {a.state=3;a.pose.SetRanged(false);a.pose.SetSword(true);continue;}
                    if(a.side<0&&a.arrows>0&&Time.time>=a.shot&&Vector3.Distance(home,a.target.transform.position)<340)
                    {a.shot=Time.time+8;a.arrows--;if(a.target.Hit(1,false,CombatImpactKind.Arrow))AlliedArrowHits++;BeachBoatSoundscape.Instance?.PlayBow();}
                    if(EnemyFleetDirector.Instance.AmbushStarted&&(QuestRowboat.FlatDistance(home,a.target.transform.position)<230||a.home.IsAtBattleStation&&QuestRowboat.FlatDistance(home,a.target.transform.position)<1400)&&homeHull.TryWaterExit(home,4,a.go.transform,out var exit))
                    {a.state=4;a.travel=0;a.start=exit;a.local=a.home.transform.InverseTransformPoint(home);a.pose.SetRanged(false);a.pose.Traversal=RiverFighterPose.Motion.Climb;}
                    continue;
                }
                if(a.state==4)
                {
                    if(!LiveDeck(a.carrier)){AllySwim(a);continue;}var exitHull=BoatHullCollision.Get(a.carrier);
                    a.travel+=dt;float t=Mathf.Clamp01(a.travel/1.1f);var from=a.carrier.TransformPoint(a.local);
                    var above=from;above.y=Mathf.Max(from.y+30,exitHull.SolidHull.bounds.max.y+12);var outside=a.start;outside.y=above.y;
                    var p=t<.4f?Vector3.Lerp(from,above,t/.4f):t<.75f?Vector3.Lerp(above,outside,(t-.4f)/.35f):Vector3.Lerp(outside,a.start,(t-.75f)/.25f);
                    a.go.transform.position=RiverHullCollision.Move(a.go.transform.position+Vector3.up*8,p+Vector3.up*8,3.5f,false,a.go.transform,exitHull)-Vector3.up*8;
                    a.pose.ClimbProgress=1-t;a.pose.ClimbRail=above;
                    if(t>=1)AllySwim(a);continue;
                }
                if(a.state==2)
                {
                    if(!LiveDeck(a.carrier)){AllySwim(a);continue;}
                    a.travel+=dt;var target=a.carrier.TransformPoint(a.local);float t=Mathf.Clamp01(a.travel/1.7f);
                    var above=a.start;above.y=target.y+2;
                    a.go.transform.position=t<.72f?Vector3.Lerp(a.start,above,Mathf.SmoothStep(0,1,t/.72f)):Vector3.Lerp(above,target,Mathf.SmoothStep(0,1,(t-.72f)/.28f));
                    a.pose.ClimbProgress=t;a.pose.ClimbRail=new Vector3(a.start.x,target.y+1,a.start.z);
                    if(t>=1){a.state=3;body.Carrier=a.carrier;a.pose.FinishTraversal();a.pose.SetSword(true);}continue;
                }
                if(a.target==null){a.pose.Walk(false);a.pose.SwimMoving=false;continue;}
                var targetCarrier=CarrierOf(a.target.transform);
                if(a.state==1)
                {
                    var target=a.target.transform.position;bool board=LiveDeck(targetCarrier);Vector3 landing=default;
                    if(board)
                    {
                        var hull=BoatHullCollision.Get(targetCarrier);
                        if(!hull.TryClearDeckLanding(target-a.target.transform.forward*14,4,a.go.transform,out landing)||!hull.TryWaterExit(landing,4,a.go.transform,out target))continue;
                        landing.y+=AllyRootOffset(a)+.2f;
                    }
                    target.y=TideSystem.CurrentWaterHeight-4;
                    float distance=Vector3.Distance(a.go.transform.position,a.target.transform.position);
                    a.pose.SwimMoving=board||distance>20;
                    if(a.pose.SwimMoving)a.go.transform.position=a.navigation.Move(a.go.transform,a.go.transform.position+Vector3.up*4,target+Vector3.up*4,26*dt,3.5f)-Vector3.up*4;
                    // A swimming body is never a boarding surface.
                    if(!board&&distance<24&&Time.time>a.swing&&CanMeleeReach(a.go.transform,a.target.transform,24))
                    {a.swing=Time.time+2.4f;if(a.target.Hit(1,false))AlliedSwordHits++;}
                    if(board&&QuestRowboat.FlatDistance(a.go.transform.position,target)<15)
                    {a.carrier=targetCarrier;a.local=targetCarrier.InverseTransformPoint(landing);a.state=2;a.travel=0;a.start=a.go.transform.position;a.pose.Traversal=RiverFighterPose.Motion.Climb;ClimbsStarted++;}
                }
                else
                {
                    if(!LiveDeck(a.carrier)){AllySwim(a);continue;}
                    var hull=BoatHullCollision.Get(a.carrier);var carried=a.carrier.TransformPoint(a.local);
                    if(targetCarrier!=a.carrier)
                    {
                        if(hull.TryWaterExit(carried,4,a.go.transform,out var exit))
                        {a.start=exit;a.local=a.carrier.InverseTransformPoint(carried);a.state=4;a.travel=0;a.pose.SetSword(false);a.pose.Traversal=RiverFighterPose.Motion.Climb;}continue;
                    }
                    var delta=a.target.transform.position-carried;float distance=delta.magnitude;
                    var destination=a.target.transform.position-delta.normalized*15;destination.y=carried.y;
                    var moved=distance>19?a.navigation.Move(a.go.transform,carried+Vector3.up*8,destination+Vector3.up*8,18*dt,3.5f,hull)-Vector3.up*8:carried;
                    if(hull.TryDeckSurface(moved,out var floor)){moved.y=floor.y+AllyRootOffset(a)+.2f;a.go.transform.position=moved;a.local=a.carrier.InverseTransformPoint(moved);}
                    a.pose.Walk(distance>20);
                    if(distance<25&&Time.time>a.swing&&CanMeleeReach(a.go.transform,a.target.transform,25))
                    {a.swing=Time.time+2.4f;a.pose.Slash();if(a.target.Hit(1,false))AlliedSwordHits++;}
                }
                var facing=a.target.transform.position-a.go.transform.position;facing.y=0;if(facing.sqrMagnitude>.1f)a.go.transform.rotation=Quaternion.LookRotation(facing);
            }
        }
        public void StepEnemies(float dt)
        {
            foreach(var guard in archery.guards)
            {
                if(!guard.IsAlive){if(enemyPoses.TryGetValue(guard,out var deadPose))deadPose.SetSword(false);continue;}
                if(guard.IsIndependentBoarder&&hostiles.Exists(h=>h.guard==guard&&h.state!=3&&h.state!=4)){guard.SetMelee(hostiles.Exists(h=>h.guard==guard&&h.state!=0));continue;}
                Transform target=null;Ally opponent=null;Defender defender=null;float nearest=(PlayerAway||guard.IsIndependentBoarder)&&!IsTraversing&&CarrierOf(guard.transform)==CarrierOf(session.player.transform)?Vector3.Distance(session.player.transform.position,guard.transform.position):float.PositiveInfinity;
                if(nearest<50)target=session.player.transform;
                foreach(var a in allies)if(a.health>0&&a.state==3&&a.go.activeSelf&&CarrierOf(guard.transform)==a.carrier)
                {float d=Vector3.Distance(a.go.transform.position,guard.transform.position);if(d<Mathf.Min(nearest,50)){nearest=d;target=a.go.transform;opponent=a;}}
                foreach(var d in defenders)if(d.health>0&&d.soldier.gameObject.activeInHierarchy&&CarrierOf(guard.transform)==CarrierOf(d.soldier))
                {float distance=Vector3.Distance(d.soldier.position,guard.transform.position);if(distance<Mathf.Min(nearest,50)){nearest=distance;target=d.soldier;defender=d;opponent=null;}}
                bool melee=target!=null;guard.SetMelee(melee);var pose=enemyPoses[guard];pose.SetSword(melee);
                if(!melee)continue;
                var direction=target.position-guard.transform.position;direction.y=0;
                if(direction.sqrMagnitude>.01f)guard.transform.rotation=Quaternion.LookRotation(direction);
                var boardingHostile=hostiles.FirstOrDefault(h=>h.guard==guard&&h.state==3);
                var carrier=boardingHostile?.boat??CarrierOf(guard.transform);
                var deckHull=LiveDeck(carrier)?BoatHullCollision.Get(carrier):null;
                if(nearest>18)
                {
                    var from=guard.transform.position+Vector3.up*8;var destination=target.position-guard.transform.forward*13+Vector3.up*8;destination.y=from.y;
                    var moved=(boardingHostile!=null?boardingHostile.navigation.Move(guard.transform,from,destination,18*dt,3.5f,deckHull):RiverHullCollision.Move(from,Vector3.MoveTowards(from,destination,18*dt),3.5f,true,guard.transform,deckHull))-Vector3.up*8;
                    if(deckHull==null||deckHull.TryDeckSurface(moved,out _))guard.transform.position=moved;
                }
                if(deckHull!=null&&deckHull.TryDeckSurface(guard.transform.position,out var support)){var p=guard.transform.position;p.y=support.y+.2f;guard.transform.position=p;guard.FitStandingOnDeck();if(boardingHostile!=null)boardingHostile.local=carrier.InverseTransformPoint(p);}
                pose.Walk(nearest>20);
                if(nearest<27&&Time.time>enemyAttacks[guard]&&CanMeleeReach(guard.transform,target,27))
                {
                    enemyAttacks[guard]=Time.time+1.65f;pose.Slash();EnemySwordHits++;
                    if(defender!=null){defender.health--;DefenderDamageTaken++;CombatHitFeedback.Show(defender.soldier.position+Vector3.up*9,CombatImpactKind.Sword);if(defender.health<=0){defender.soldier.gameObject.SetActive(false);CampaignProgress.Instance?.RecordCasualty();}}
                    else if(opponent!=null)DamageAlly(opponent);
                    else session.player.GetComponent<PlayerVitals>().TakeImpact(IsBlocking?2:9,"Kiếm địch",CombatImpactKind.Sword);
                }
            }
        }
        void DamageAlly(Ally ally){if(ally.health<=0)return;ally.health--;CombatHitFeedback.Show(ally.go.transform.position+Vector3.up*9,CombatImpactKind.Sword);if(ally.health<=0){ally.go.SetActive(false);CampaignProgress.Instance?.RecordCasualty();}}
        bool CanMeleeReach(Transform attacker,Transform target,float reach)
        {
            if(Vector3.Distance(attacker.position,target.position)>reach)return false;
            var from=attacker.position+Vector3.up*8;var delta=target.position+Vector3.up*8-from;
            if(delta.sqrMagnitude<.001f)return true;
            Physics.SyncTransforms();
            int count=Physics.RaycastNonAlloc(from,delta.normalized,meleeHits,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==meleeHits.Length)return false;
            for(int i=0;i<count;i++)
            {
                var c=meleeHits[i].collider;
                if(c.transform.IsChildOf(attacker)||c.transform.IsChildOf(target)||c.GetComponent<TideSystem>()!=null)continue;
                // The broad navigation box spans raised decks. It must not act as
                // an invisible wall between fighters standing on the same planks.
                var hull=c.GetComponentInParent<BoatHullCollision>();
                if(hull!=null&&c==hull.SolidHull&&CarrierOf(attacker)==hull.transform&&CarrierOf(target)==hull.transform)continue;
                return false;
            }
            return true;
        }
        Transform CarrierOf(Transform actor)
        {
            if(actor==session.player.transform)return PlayerDeckCarrier??(!PlayerAway?QuestRowboat.Instance.transform:null);
            var body=actor.GetComponent<ActorSolidBody>();if(body!=null&&body.Carrier!=null)return body.Carrier;
            var boat=actor.GetComponentInParent<AmbientRiverBoat>();if(boat!=null)return boat.transform;
            return actor.GetComponentInParent<EnemyShip>()?.transform;
        }
        public void StepHostiles(float dt)
        {
            if(!AlliedAIEnabled){foreach(var h in hostiles)h.go.SetActive(false);return;}
            if(!RiverBattleObjective.Counterattack)
            {
                foreach(var h in hostiles)if(h.source!=null&&h.state!=0&&!h.fromWreck)
                {h.state=0;h.boat=null;h.pose.FinishTraversal();h.pose.SetSword(false);h.guard.IsIndependentBoarder=false;h.go.GetComponent<ActorSolidBody>().Carrier=h.source.transform;h.go.transform.position=h.source.transform.position+Vector3.up*22;}
                return;
            }
            foreach(var h in hostiles)
            {
                if(h.source==null)
                {
                    foreach(var source in EnemyFleetDirector.Instance.enemyShips)if(source.gameObject.activeInHierarchy&&!source.isDestroyed){h.source=source;break;}
                    if(h.source==null)continue;
                    h.guard.ship=h.source;h.guard.waveIndex=Array.IndexOf(EnemyFleetDirector.Instance.enemyShips,h.source)/Mathf.Max(1,EnemyFleetDirector.Instance.shipsPerWave);h.pose.FinishTraversal();h.go.SetActive(true);h.guard.ResetGuard();h.state=0;
                }
                if(!h.guard.IsAlive){h.go.SetActive(false);if(!h.fromWreck){h.respawn+=dt;if(h.respawn>12){h.source=null;h.respawn=0;}}continue;}
                h.go.GetComponent<ActorSolidBody>().Carrier=h.state==0?h.source.transform:h.state==3?h.boat:null;
                if(h.state==0)
                {
                    int launchSlot=hostiles.IndexOf(h)%4;h.go.transform.position=h.source.transform.position+Vector3.up*22+h.source.transform.forward*((launchSlot-1.5f)*20);
                    h.boat=QuestRowboat.Instance.transform;float nearest=QuestRowboat.FlatDistance(h.go.transform.position,h.boat.position);
                    foreach(var friendly in friendlyBoats){float d=QuestRowboat.FlatDistance(h.go.transform.position,friendly.transform.position);if(d<nearest){nearest=d;h.boat=friendly.transform;}}
                    if(nearest>310&&!h.source.isDestroyed)continue;
                    float side=h.source.transform.InverseTransformPoint(h.boat.position).x<0?-1:1;
                    var entry=h.source.transform.TransformPoint(new Vector3(side*(h.source.stakeHitDistance+16),0,(launchSlot-1.5f)*20));
                    if(BoatHullCollision.Get(h.source.transform).TryWaterExit(entry,4,h.go.transform,out var clearEntry))entry=clearEntry;
                    entry.y=TideSystem.CurrentWaterHeight-4;h.go.transform.position=entry;
                    h.state=1;h.pose.SetSword(false);h.pose.Traversal=RiverFighterPose.Motion.Swim;h.guard.SetMelee(true);SwimsStarted++;
                }
                if(h.targetPlayer&&(h.state==1||h.state==4))h.boat=(State==PlayerState.EnemyDeck||State==PlayerState.FriendlyDeck)&&deckAnchor!=null?deckAnchor:QuestRowboat.Instance.transform;
                if(h.boat==null){h.source=null;continue;}
                bool chasePlayer=h.targetPlayer&&PlayerAway&&(h.state==1||h.state==4);
                var end=chasePlayer?session.player.transform.position:h.boat.position+h.boat.right*(h.boat==QuestRowboat.Instance.transform?4:10)+Vector3.up*(h.boat==QuestRowboat.Instance.transform?10:24);
                if(h.state==2)end=h.boat.TransformPoint(h.local);
                else if(h.state==1&&h.boat!=QuestRowboat.Instance.transform&&h.boat.GetComponent<AmbientRiverBoat>()!=null)
                {var crew=h.boat.GetComponentInChildren<FleetDeckCrew>();if(crew!=null&&crew.soldiers.Length>0)end=crew.soldiers[0].position+h.boat.forward*8;
                    int slot=hostiles.IndexOf(h)%6;end+=h.boat.forward*((slot/2-1)*20)+h.boat.right*(slot%2==0?-10:10);
                    if(BoatHullCollision.Get(h.boat).TryClearDeckLanding(end,4,h.go.transform,out var support))end=support+Vector3.up*.2f;}
                if(h.state==4)
                {
                    if(chasePlayer)
                    {
                        var direction=end-h.go.transform.position;direction.y=0;
                        if(direction.sqrMagnitude>18*18)
                        {
                            var proposed=h.go.transform.position+direction.normalized*(18*dt);
                            if(GroundAt(proposed,out float support,out var slope)&&Vector3.Angle(slope,Vector3.up)<=55)
                            {proposed.y=support+.4f;h.go.transform.position=RiverHullCollision.Move(h.go.transform.position+Vector3.up*8,proposed+Vector3.up*8,3.5f,true,h.go.transform)-Vector3.up*8;}
                        }
                        h.pose.Walk(direction.sqrMagnitude>20*20);
                        if(direction.sqrMagnitude>.1f)h.go.transform.rotation=Quaternion.LookRotation(direction);
                    }
                    if(GroundAt(h.go.transform.position,out float ground))
                    {
                        var position=h.go.transform.position;position.y=ground+.4f;h.go.transform.position=position;
                        if(ground<TideSystem.CurrentWaterHeight-3){h.state=1;h.pose.SetSword(false);h.pose.Traversal=RiverFighterPose.Motion.Swim;}
                    }
                    continue;
                }
                if(h.state==1)
                {
                    // Fight nearby swimmers instead of ignoring them to reach a boat.
                    Ally opponent=null;float nearest=38;
                    foreach(var a in allies)if(a.health>0&&a.state==1&&a.go.activeInHierarchy)
                    {float d=Vector3.Distance(a.go.transform.position,h.go.transform.position);if(d<nearest){nearest=d;opponent=a;}}
                    if(opponent!=null)
                    {
                        h.pose.SwimMoving=nearest>20;
                        var direction=opponent.go.transform.position-h.go.transform.position;direction.y=0;
                        if(direction.sqrMagnitude>.01f)h.go.transform.rotation=Quaternion.LookRotation(direction);
                        if(nearest>20)h.go.transform.position=h.navigation.Move(h.go.transform,h.go.transform.position,opponent.go.transform.position,24*dt,4);
                        if(nearest<24&&Time.time>enemyAttacks[h.guard]&&CanMeleeReach(h.go.transform,opponent.go.transform,24))
                        {enemyAttacks[h.guard]=Time.time+2.8f;DamageAlly(opponent);EnemySwordHits++;}continue;
                    }
                    h.pose.SwimMoving=true;
                    var water=end;
                    bool directPursuit=chasePlayer&&(State==PlayerState.Swimming||State==PlayerState.Land);
                    var targetShip=directPursuit?null:h.boat.GetComponent<EnemyShip>();
                    if(targetShip!=null&&targetShip.isDestroyed&&targetShip.TryWreckBoardingPoint(h.go.transform.position,out var wreckSurface))
                    {
                        end=wreckSurface;var away=h.go.transform.position-wreckSurface;away.y=0;
                        water=wreckSurface+away.normalized*10;
                    }
                    else if(targetShip!=null&&!targetShip.isDestroyed)
                    {
                        float side=targetShip.transform.InverseTransformPoint(h.go.transform.position).x<0?-1:1;
                        water=targetShip.transform.TransformPoint(new Vector3(side*(targetShip.stakeHitDistance+12),0,0));
                    }
                    if(targetShip==null&&!directPursuit)
                    {
                        var box=BoatHullCollision.Get(h.boat).SolidHull;var local=h.boat.InverseTransformPoint(h.go.transform.position)-box.center;
                        float side=local.x<0?-1:1;var edge=box.center+new Vector3(side*(box.size.x*.5f+12),0,Mathf.Clamp((hostiles.IndexOf(h)%4-1.5f)*(h.boat==QuestRowboat.Instance.transform?7:24),-box.size.z*.35f,box.size.z*.35f));
                        water=h.boat.TransformPoint(edge);
                    }
                    water.y=TideSystem.CurrentWaterHeight-4;h.go.transform.position=h.navigation.Move(h.go.transform,h.go.transform.position,water,24*dt,4);
                    if(h.targetPlayer&&State==PlayerState.Land)
                    {
                        if(GroundAt(h.go.transform.position,out float ground)&&ground>=TideSystem.CurrentWaterHeight-1)
                        {var pos=h.go.transform.position;pos.y=ground+.4f;h.go.transform.position=pos;h.state=4;h.pose.FinishTraversal();h.pose.SetSword(true);h.pose.Animator.Update(0);h.guard.FitStandingOnDeck();}
                        var face=end-h.go.transform.position;face.y=0;if(face.sqrMagnitude>.1f)h.go.transform.rotation=Quaternion.LookRotation(face);
                        continue;
                    }
                    if(h.targetPlayer&&State==PlayerState.Swimming)
                    {
                        h.pose.SwimMoving=QuestRowboat.FlatDistance(h.go.transform.position,end)>18;
                        if(!h.pose.SwimMoving&&Time.time>enemyAttacks[h.guard]&&CanMeleeReach(h.go.transform,session.player.transform,18)){enemyAttacks[h.guard]=Time.time+2.8f;session.player.GetComponent<PlayerVitals>().TakeImpact(5,"Địch tập kích dưới nước",CombatImpactKind.Sword);if(QuestCheckpoint.Instance!=null&&QuestCheckpoint.Instance.IsDefeated)return;}
                        var aim=water-h.go.transform.position;aim.y=0;if(aim.sqrMagnitude>.1f)h.go.transform.rotation=Quaternion.LookRotation(aim);continue;
                    }
                    if(QuestRowboat.FlatDistance(h.go.transform.position,water)<17){h.state=2;h.start=h.go.transform.position;h.local=h.boat.InverseTransformPoint(end);h.time=0;h.pose.Traversal=RiverFighterPose.Motion.Climb;ClimbsStarted++;}
                }
                else if(h.state==2)
                {
                    h.time+=dt;float t=Mathf.Clamp01(h.time/1.9f);var above=h.start;above.y=end.y+2;
                    h.go.transform.position=t<.72f?Vector3.Lerp(h.start,above,Mathf.SmoothStep(0,1,t/.72f)):Vector3.Lerp(above,end,Mathf.SmoothStep(0,1,(t-.72f)/.28f));
                    h.pose.ClimbProgress=t;h.pose.ClimbRail=new Vector3(h.start.x,end.y+1,h.start.z);
                    if(h.time>=1.9f){h.state=3;h.local=h.boat.InverseTransformPoint(end);h.pose.FinishTraversal();h.pose.SetSword(true);h.pose.Animator.Update(0);h.guard.FitStandingOnDeck();HostileBoardings++;}
                }
                else h.go.transform.position=h.boat.TransformPoint(h.local);
                var forward=end-h.go.transform.position;forward.y=0;if(forward.sqrMagnitude>.01f)h.go.transform.rotation=Quaternion.LookRotation(forward);
            }
        }
        public void StepDefenders()
        {
            if(!AlliedAIEnabled||!RiverBattleObjective.Counterattack)return;
            foreach(var defender in defenders)
            {
                if(defender.health<=0||!defender.soldier.gameObject.activeInHierarchy)continue;
                EnemyDeckGuard target=null;float nearest=45;
                foreach(var enemy in archery.guards)if(enemy.IsAlive&&enemy.IsIndependentBoarder&&Mathf.Abs(enemy.transform.position.y-defender.soldier.position.y)<16)
                {float distance=Vector3.Distance(enemy.transform.position,defender.soldier.position);if(distance<nearest){target=enemy;nearest=distance;}}
                if(target==null)
                {
                    if(defender.fighting){defender.pose.SetSword(false);defender.pose.Animator.Update(0);defender.pose.Animator.enabled=false;defender.fighting=false;}continue;
                }
                defender.pose.Animator.enabled=true;defender.pose.SetSword(true);defender.fighting=true;
                var forward=target.transform.position-defender.soldier.position;forward.y=0;if(forward.sqrMagnitude>.1f)defender.soldier.rotation=Quaternion.LookRotation(forward);
                if(nearest<25&&Time.time>=defender.nextAttack&&CanMeleeReach(defender.soldier,target.transform,25))
                {defender.nextAttack=Time.time+1.8f;defender.pose.Slash();if(target.Hit(1,false)){DefenderHits++;AlliedSwordHits++;}}
            }
        }
        public void ReleaseShipwreckSurvivors(EnemyShip ship,EnemyDeckGuard[] crew)
        {
            int survived=0;
            for(int i=0;i<crew.Length;i++)
            {
                var guard=crew[i];if(guard.Health<=0)continue;
                if(i%3==0)
                {
                    guard.IsIndependentBoarder=true;guard.DefeatFromVolley();
                    var at=ship.transform.position+ship.transform.forward*((i-crew.Length*.5f)*14)+ship.transform.right*(i%2==0?-60:60);
                    if(BoatHullCollision.Get(ship.transform).TryWaterExit(at,4,guard.transform,out var outside))at=outside;
                    guard.transform.SetParent(transform,true);at.y=TideSystem.CurrentWaterHeight+.4f;guard.transform.position=at;RiverCorpse.Ensure(guard);ShipwreckDeaths++;continue;
                }
                var pose=enemyPoses[guard];pose.SetSword(false);guard.IsIndependentBoarder=true;guard.SetMelee(true);
                var from=ship.transform.position+ship.transform.right*(i%2==0?-58:58)+ship.transform.forward*((i/2-1)*22);
                var p=from;if(BoatHullCollision.Get(ship.transform).TryWaterExit(from,4,guard.transform,out var clear))p=clear;
                guard.transform.SetParent(transform,true);p.y=TideSystem.CurrentWaterHeight-4;guard.transform.position=RiverHullCollision.Move(p,p,4,true,guard.transform);
                pose.Traversal=RiverFighterPose.Motion.Swim;bool targetPlayer=survived++%2==0;
                Transform boat=QuestRowboat.Instance.transform;
                if(!targetPlayer&&friendlyBoats.Length>0)
                {
                    float closest=float.PositiveInfinity;
                    foreach(var friendly in friendlyBoats){float d=QuestRowboat.FlatDistance(p,friendly.transform.position);if(d<closest){closest=d;boat=friendly.transform;}}
                }
                hostiles.Add(new Hostile{go=guard.gameObject,guard=guard,pose=pose,source=ship,boat=boat,state=1,fromWreck=true,targetPlayer=targetPlayer});
                ShipwreckSurvivors++;if(targetPlayer)SurvivorsTargetingPlayer++;else SurvivorsTargetingAllies++;SwimsStarted++;
            }
        }
        void PlaySwordSwing()
        {BeachBoatSoundscape.Instance?.PlaySwordSwing();}
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
