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
        public enum PlayerState { Rowboat, Swimming, Climbing, EnemyDeck }
        public static RiverBoardingCombat Instance {get;private set;}
        public GameObject swordPrefab,allyPrefab,enemyPrefab;
        public RuntimeAnimatorController swordController;
        public AmbientRiverBoat[] friendlyBoats=Array.Empty<AmbientRiverBoat>();
        public PlayerState State {get;private set;}
        public bool SwordDrawn {get;private set;}
        public bool PlayerAway=>State!=PlayerState.Rowboat;
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
        sealed class Ally
        {
            public GameObject go;public RiverFighterPose pose;public AmbientRiverBoat home;public EnemyDeckGuard target;
            public int health=6,state,arrows=2;public float swing,travel,respawn,shot;public Vector3 start,local;public int side;
        }
        readonly List<Ally> allies=new List<Ally>();
        sealed class Defender
        {public Transform soldier;public RiverFighterPose pose;public int health=4;public float nextAttack;public bool fighting;}
        readonly List<Defender> defenders=new List<Defender>();
        public int DefenderHits {get;private set;}
        public int DefenderDamageTaken {get;private set;}
        sealed class Hostile
        {public GameObject go;public EnemyDeckGuard guard;public RiverFighterPose pose;public EnemyShip source;public Transform boat;public Vector3 start,local;public int state;public float time,respawn;public bool fromWreck,targetPlayer;}
        public int ShipwreckSurvivors {get;private set;}
        public int ShipwreckDeaths {get;private set;}
        public int SurvivorsTargetingPlayer {get;private set;}
        public int SurvivorsTargetingAllies {get;private set;}
        readonly List<Hostile> hostiles=new List<Hostile>();
        readonly Dictionary<EnemyDeckGuard,RiverFighterPose> enemyPoses=new Dictionary<EnemyDeckGuard,RiverFighterPose>();
        readonly Dictionary<EnemyDeckGuard,float> enemyAttacks=new Dictionary<EnemyDeckGuard,float>();
        BeachBoatQuestSession session;RiverPlayerCombat archery;RiverFighterPose playerPose;
        EnemyShip deckShip;Transform deckAnchor;Vector3 deckLocal,climbStart,climbLocalStart;float climbTime,nextSlash,nextArrow;
        TMP_Text hint;bool wasBattle;int traversalInputFrame=-1;
        public bool UsedTraversalThisFrame=>traversalInputFrame==Time.frameCount;
        public BachDangSaveData.Boarding CaptureSave()
        {
            return new BachDangSaveData.Boarding {
                state=(int)State,sword=SwordDrawn,deckShip=deckShip!=null?deckShip.name:null,climbTime=climbTime,deckLocal=deckLocal,climbLocalStart=climbLocalStart,
                allies=allies.Select(a=>new BachDangSaveData.Fighter{id=a.go.name,active=a.go.activeSelf,health=a.health,state=a.state,arrows=a.arrows,time=a.travel,start=a.start,local=a.local,target=a.target!=null?BachDangSaveSystem.GuardId(a.target):null,position=a.go.transform.position,rotation=a.go.transform.rotation}).ToArray(),
                hostiles=hostiles.Select(h=>new BachDangSaveData.Fighter{id=BachDangSaveSystem.GuardId(h.guard),active=h.go.activeSelf,source=h.source!=null?h.source.name:null,boat=h.boat!=null?h.boat.name:null,state=h.state,time=h.time,start=h.start,local=h.local,fromWreck=h.fromWreck,targetPlayer=h.targetPlayer,position=h.go.transform.position,rotation=h.go.transform.rotation}).ToArray(),
                defenders=defenders.Select(d=>new BachDangSaveData.Fighter{id=d.soldier.GetComponentInParent<AmbientRiverBoat>().name+"/"+d.soldier.name,health=d.health,active=d.soldier.gameObject.activeSelf,position=d.soldier.position,rotation=d.soldier.rotation}).ToArray()};
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
                h.source=Ship(saved.source);h.boat=Boat(saved.boat);h.state=saved.state;h.time=saved.time;h.start=saved.start;h.local=saved.local;h.fromWreck=saved.fromWreck;h.targetPlayer=saved.targetPlayer;
                h.go.SetActive(saved.active);h.go.transform.SetPositionAndRotation(saved.position,saved.rotation);
                h.pose.FinishTraversal();h.pose.SetSword(h.state==3);if(h.state==3)h.guard.FitStandingOnDeck();
                if(h.state==1)h.pose.Traversal=RiverFighterPose.Motion.Swim;else if(h.state==2)h.pose.Traversal=RiverFighterPose.Motion.Climb;
            }
            foreach(var saved in data.allies??Array.Empty<BachDangSaveData.Fighter>())
            {
                var a=allies.FirstOrDefault(x=>x.go.name==saved.id);if(a==null)continue;
                a.health=saved.health;a.state=saved.state;a.arrows=saved.arrows;a.travel=saved.time;a.start=saved.start;a.local=saved.local;a.target=Guard(saved.target);
                a.go.SetActive(saved.active);a.go.transform.SetPositionAndRotation(saved.position,saved.rotation);a.pose.FinishTraversal();a.pose.SetSword(a.state==3);
                if(a.state==1)a.pose.Traversal=RiverFighterPose.Motion.Swim;else if(a.state==2)a.pose.Traversal=RiverFighterPose.Motion.Climb;
            }
            foreach(var saved in data.defenders??Array.Empty<BachDangSaveData.Fighter>())
            {
                var d=defenders.FirstOrDefault(x=>x.soldier.GetComponentInParent<AmbientRiverBoat>().name+"/"+x.soldier.name==saved.id);if(d==null)continue;
                d.health=saved.health;d.soldier.gameObject.SetActive(saved.active);d.soldier.SetPositionAndRotation(saved.position,saved.rotation);
            }
            State=(PlayerState)data.state;SwordDrawn=data.sword;deckShip=Ship(data.deckShip);deckAnchor=deckShip!=null?deckShip.transform:null;
            deckLocal=data.deckLocal;climbLocalStart=data.climbLocalStart;climbTime=data.climbTime;
            playerPose.FinishTraversal();playerPose.SetSword(SwordDrawn);
            if(State==PlayerState.EnemyDeck&&!SwordDrawn)playerPose.Animator.runtimeAnimatorController=FootController;
            if(State==PlayerState.Swimming)playerPose.Traversal=RiverFighterPose.Motion.Swim;else if(State==PlayerState.Climbing)playerPose.Traversal=RiverFighterPose.Motion.Climb;
            wasBattle=BachDangStoryManager.Instance.currentState==BachDangStoryManager.StoryState.Act3_BattleInProgress;
            if(PlayerAway){var camera=session.storyCamera.GetComponent<Camera_Script>();camera.distance=70;camera.heightOffset=27;}
        }
        void Awake(){Instance=this;session=GetComponent<BeachBoatQuestSession>();archery=GetComponent<RiverPlayerCombat>();}
        void Start()
        {
            FootController=Resources.Load<RuntimeAnimatorController>("Player_Neutral");
            playerPose=session.player.AddComponent<RiverFighterPose>();playerPose.Initialize(session.player.GetComponentInChildren<Animator>(),swordPrefab,swordController,17);
            foreach(var guard in archery.guards)
            {guard.MatchPlayerSize();var pose=guard.gameObject.AddComponent<RiverFighterPose>();pose.Initialize(guard.visual.GetComponentInChildren<Animator>(),swordPrefab,swordController,17);enemyPoses[guard]=pose;enemyAttacks[guard]=0;}
            for(int i=0;i<friendlyBoats.Length*2;i++)
            {
                var go=Instantiate(allyPrefab,transform);go.name="Linh_Dai_Viet_Tiep_Chien_"+i;
                var pose=go.AddComponent<RiverFighterPose>();pose.Initialize(go.GetComponentInChildren<Animator>(),swordPrefab,swordController,11);
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
                var crew=friendly.GetComponentInChildren<FleetDeckCrew>();if(crew==null)continue;
                foreach(var soldier in crew.soldiers)
                {var pose=soldier.gameObject.AddComponent<RiverFighterPose>();pose.Initialize(soldier.GetComponentInChildren<Animator>(),swordPrefab,swordController,11);defenders.Add(new Defender{soldier=soldier,pose=pose});}
            }
            var canvas=new GameObject("BoardingCombatHUD",typeof(Canvas),typeof(CanvasScaler));canvas.transform.SetParent(transform,false);canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=112;
            var scale=canvas.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);
            var text=new GameObject("BoardingHint",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(canvas.transform,false);var rect=(RectTransform)text.transform;rect.anchorMin=new Vector2(.23f,.30f);rect.anchorMax=new Vector2(.79f,.36f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            hint=text.GetComponent<TMP_Text>();StoryFont.Apply(hint);hint.fontSize=18;hint.color=new Color(1,.84f,.48f);hint.alignment=TextAlignmentOptions.Center;
        }
        public void ResetBattle(bool preserveWrecks=false)
        {
            if(playerPose!=null){playerPose.Restore();if(QuestRowboat.Instance!=null&&QuestRowboat.Instance.IsAboard)playerPose.Animator.runtimeAnimatorController=QuestRowboat.Instance.rowingController;}SwordDrawn=false;State=PlayerState.Rowboat;deckShip=null;deckAnchor=null;
            foreach(var a in allies){a.go.SetActive(false);a.health=6;a.state=0;a.pose.Restore();}
            foreach(var d in defenders){d.pose.Restore();d.health=4;d.fighting=false;d.soldier.gameObject.SetActive(true);d.pose.Animator.enabled=true;d.pose.Animator.Update(0);d.pose.Animator.enabled=false;}
            DefenderHits=DefenderDamageTaken=0;
            foreach(var h in hostiles){h.pose.Restore();if(h.fromWreck&&!preserveWrecks){h.guard.IsIndependentBoarder=false;h.guard.RestoreDeck();h.guard.ResetGuard();}else h.go.SetActive(false);h.state=0;h.source=null;}
            hostiles.RemoveAll(h=>h.fromWreck);if(!preserveWrecks)ShipwreckSurvivors=ShipwreckDeaths=SurvivorsTargetingPlayer=SurvivorsTargetingAllies=0;
            foreach(var pair in enemyPoses){pair.Value.Restore();pair.Key.SetMelee(false);}
            wasBattle=false;if(hint!=null)hint.text="";
        }
        void Update()
        {
            bool battle=archery!=null&&archery.BattleActive;
            if(!battle){if(wasBattle)ResetBattle(true);return;}
            if(!wasBattle)
            {
                wasBattle=true;foreach(var a in allies){a.health=6;a.state=0;a.arrows=2;a.shot=Time.time+2;a.go.SetActive(true);a.pose.SetSword(true);}nextArrow=Time.time+3;
                PlayerSwordHits=AlliedArrowHits=AlliedSwordHits=EnemySwordHits=SwimsStarted=ClimbsStarted=HostileBoardings=0;
            }
            if(BeachBoatQuestSession.InputBlocked)return;
            if(Cursor.lockState==CursorLockMode.Locked)
            {
                if(Input.GetKeyDown(KeyCode.Q))HandleActionKey(KeyCode.Q);
                if(Input.GetKeyDown(KeyCode.F))HandleActionKey(KeyCode.F);
                if(Input.GetKeyDown(KeyCode.Space))HandleActionKey(KeyCode.Space);
                if(SwordDrawn&&Input.GetMouseButtonDown(0))TrySlash();
            }
            StepPlayer(Time.deltaTime);StepAllies(Time.deltaTime);StepHostiles(Time.deltaTime);StepDefenders();StepEnemies(Time.deltaTime);
            hint.text=State==PlayerState.Swimming?"WASD: BƠI • SHIFT: BƠI NHANH • SPACE: LEO LÊN TÀU GẦN / VỀ THUYỀN":
                State==PlayerState.Climbing?"ĐANG BÁM MẠN VÀ LEO LÊN BOONG…":
                SwordDrawn?"KIẾM • CHUỘT TRÁI: CHÉM • CHUỘT PHẢI: ĐỠ • Q: CẤT KIẾM • F: XUỐNG NƯỚC":
                "Q: RÚT KIẾM • F: XUỐNG NƯỚC • SPACE: LEO TÀU • ĐỒNG MINH ĐANG TIẾP CHIẾN";
        }
        public bool HandleActionKey(KeyCode key)
        {
            if(key==KeyCode.Q)return ToggleSword();
            if(key==KeyCode.Space&&State==PlayerState.Swimming)return TryTraverse();
            if(key==KeyCode.F&&State!=PlayerState.Swimming)return TryTraverse();
            return false;
        }
        public bool ToggleSword()
        {
            if(!archery.BattleActive||BeachBoatQuestSession.InteractionBlocked||IsTraversing)return false;
            archery.SetAim(false);SwordDrawn=!SwordDrawn;playerPose.SetSword(SwordDrawn);
            if(!SwordDrawn&&State==PlayerState.EnemyDeck)playerPose.Animator.runtimeAnimatorController=FootController;return true;
        }
        public bool TrySlash()
        {
            if(!SwordDrawn||IsTraversing||!archery.BattleActive||BeachBoatQuestSession.InteractionBlocked||Time.time<nextSlash)return false;
            var vitals=session.player.GetComponent<PlayerVitals>();if(!vitals.TryExert(9,1))return false;
            nextSlash=Time.time+.72f;playerPose.Slash();PlaySword(false);
            var player=session.player.transform;
            foreach(var guard in archery.guards)
            {
                if(!guard.IsAlive)continue;var delta=guard.transform.position-player.position;
                if(Mathf.Abs(delta.y)>19)continue;delta.y=0;
                if(delta.magnitude<=32&&Vector3.Dot(player.forward,delta.normalized)>.15f&&guard.Hit(1)){PlayerSwordHits++;PlaySword(true);}
            }
            return true;
        }
        public bool TryTraverse()
        {
            if(!archery.BattleActive||BeachBoatQuestSession.InteractionBlocked||State==PlayerState.Climbing)return false;
            if(State==PlayerState.Swimming)
            {
                traversalInputFrame=Time.frameCount;
                var nearest=NearestBoard(session.player.transform.position,out var ship,out var anchor,out var local);
                float ownDistance=QuestRowboat.FlatDistance(session.player.transform.position,QuestRowboat.Instance.transform.position);
                if(ownDistance<48&&ownDistance<nearest)
                {playerPose.FinishTraversal();playerPose.Animator.runtimeAnimatorController=QuestRowboat.Instance.rowingController;State=PlayerState.Rowboat;deckShip=null;deckAnchor=null;QuestRowboat.Instance.UseSoldierBattleCamera();return true;}
                if(nearest>(ship!=null&&ship.isDestroyed?52:24))return false;deckShip=ship;deckAnchor=anchor;deckLocal=local;climbStart=session.player.transform.position;climbLocalStart=anchor.InverseTransformPoint(climbStart);climbTime=0;State=PlayerState.Climbing;playerPose.Traversal=RiverFighterPose.Motion.Climb;ClimbsStarted++;BeachBoatSoundscape.Instance?.PlayClimbGrip();return true;
            }
            BeginSwim(session.player.transform.position+session.player.transform.right*22);return true;
        }
        void BeginSwim(Vector3 p)
        {
            archery.SetAim(false);playerPose.SetSword(false);SwordDrawn=false;
            p.y=TideSystem.CurrentWaterHeight-5;MovePlayer(p,session.player.transform.rotation);State=PlayerState.Swimming;playerPose.Traversal=RiverFighterPose.Motion.Swim;SwimsStarted++;
            BeachBoatSoundscape.Instance?.PlayWaterEntry();
            var camera=session.storyCamera.GetComponent<Camera_Script>();camera.distance=70;camera.heightOffset=27;camera.SnapToTarget();
        }
        void MovePlayer(Vector3 p,Quaternion rotation)
        {session.player.transform.SetPositionAndRotation(p,rotation);var body=session.player.GetComponent<Rigidbody>();body.position=p;body.rotation=rotation;}
        public void StepPlayer(float dt)
        {
            if(State==PlayerState.Rowboat)return;
            if(State==PlayerState.EnemyDeck&&(deckAnchor==null||deckShip==null||!deckShip.gameObject.activeInHierarchy)){BeginSwim(session.player.transform.position);return;}
            if(State==PlayerState.Climbing)
            {
                if(deckAnchor==null||deckShip==null||!deckShip.gameObject.activeInHierarchy){BeginSwim(session.player.transform.position);return;}
                climbTime+=dt;var destination=deckAnchor.TransformPoint(deckLocal);float t=Mathf.Clamp01(climbTime/1.8f);
                if(destination.y<TideSystem.CurrentWaterHeight-2){BeginSwim(session.player.transform.position);return;}
                var facing=deckAnchor.position-session.player.transform.position;facing.y=0;
                var from=deckAnchor.TransformPoint(climbLocalStart);var above=from;above.y=destination.y+2;
                var climb=t<.72f?Vector3.Lerp(from,above,Mathf.SmoothStep(0,1,t/.72f)):Vector3.Lerp(above,destination,Mathf.SmoothStep(0,1,(t-.72f)/.28f));
                var rail=from;rail.y=destination.y+1;rail+=deckAnchor.right*(Mathf.Sign(deckLocal.x)*-5);
                playerPose.ClimbRail=rail;playerPose.ClimbProgress=t;
                MovePlayer(climb,facing.sqrMagnitude>.01f?Quaternion.LookRotation(facing):session.player.transform.rotation);
                if(t>=1){State=PlayerState.EnemyDeck;playerPose.FinishTraversal();SwordDrawn=true;playerPose.SetSword(true);}return;
            }
            var camera=session.storyCamera.transform;var forward=camera.forward;forward.y=0;forward.Normalize();var right=Vector3.Cross(Vector3.up,forward);
            var direction=Vector3.ClampMagnitude(forward*Input.GetAxisRaw("Vertical")+right*Input.GetAxisRaw("Horizontal"),1);
            bool fast=Input.GetKey(KeyCode.LeftShift)&&session.player.GetComponent<PlayerVitals>().TryExert(15,dt);
            var p=session.player.transform.position+direction*(State==PlayerState.Swimming?(fast?38:23):26)*dt;
            if(State==PlayerState.Swimming)
            {
                playerPose.SwimMoving=direction.sqrMagnitude>.01f;playerPose.SwimFast=fast;
                p=RiverHullCollision.Move(session.player.transform.position+Vector3.up*3,p+Vector3.up*3,4)-Vector3.up*3;
                p.y=TideSystem.CurrentWaterHeight-5+Mathf.Sin(Time.time*3)*.3f;
            }
            else
            {
                var carried=deckAnchor.TransformPoint(deckLocal);var proposed=carried+direction*26*dt;
                if(deckShip.isDestroyed)
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
                playerPose.Walk(direction.sqrMagnitude>.01f);
            }
            MovePlayer(p,direction.sqrMagnitude>.01f?Quaternion.LookRotation(direction):session.player.transform.rotation);
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
                    var boarding=candidate.transform.Find(side<0?"Boarding_Port":"Boarding_Starboard");
                    var point=candidate.isDestroyed?new Vector3(side*12,2,0):boarding!=null?boarding.localPosition:new Vector3(side*candidate.stakeHitDistance*.5f,20,0);
                    var world=target.TransformPoint(point);
                    if(!candidate.isDestroyed)world=target.TransformPoint(new Vector3(side*(candidate.stakeHitDistance+7),point.y,point.z));
                    float d=QuestRowboat.FlatDistance(p,world);if(d>=best)continue;best=d;ship=candidate;anchor=target;local=point;
                }
            }
            return best;
        }
        EnemyDeckGuard TargetFor(Ally ally)
        {
            EnemyDeckGuard best=null;float d=float.PositiveInfinity;
            foreach(var guard in archery.guards)if(guard.IsAlive){float distance=(guard.transform.position-ally.go.transform.position).sqrMagnitude;if(distance<d){d=distance;best=guard;}}
            return best;
        }
        public void StepAllies(float dt)
        {
            if(!AlliedAIEnabled){foreach(var a in allies)a.go.SetActive(false);return;}
            foreach(var a in allies)
            {
                if(a.health<=0){a.go.SetActive(false);a.respawn+=dt;if(a.respawn>10){a.health=6;a.state=0;a.respawn=0;a.pose.FinishTraversal();a.go.SetActive(true);}continue;}
                if(a.target==null||!a.target.IsAlive){a.target=TargetFor(a);if(a.target==null){a.state=0;a.pose.FinishTraversal();}}
                var home=a.home.transform.position+Vector3.up*20+a.home.transform.forward*20+a.home.transform.right*(a.side*12);
                if(a.state==0)
                {
                    a.go.transform.position=home;
                    a.pose.SetSword(false);a.pose.SetRanged(true);
                    if(a.target!=null){a.home.SetBattleTarget(a.target.transform);var aim=a.target.transform.position-home;aim.y=0;if(aim.sqrMagnitude>.01f)a.go.transform.rotation=Quaternion.LookRotation(aim);}
                    if(a.target!=null&&a.side<0&&a.arrows>0&&Time.time>=a.shot&&Vector3.Distance(home,a.target.transform.position)<340)
                    {a.shot=Time.time+8;a.arrows--;if(a.target.Hit(1,false))AlliedArrowHits++;BeachBoatSoundscape.Instance?.PlayBow();}
                    if(a.target!=null&&QuestRowboat.FlatDistance(home,a.target.transform.position)<230){a.state=1;a.pose.SetRanged(false);a.pose.Traversal=RiverFighterPose.Motion.Swim;SwimsStarted++;}
                    continue;
                }
                if(a.target==null)continue;
                if(a.state==1)
                {
                    if(a.target.IsIndependentBoarder&&enemyPoses.TryGetValue(a.target,out var swimmer)&&swimmer.Traversal==RiverFighterPose.Motion.Swim&&Vector3.Distance(a.go.transform.position,a.target.transform.position)<24)
                    {
                        a.pose.SwimMoving=false;if(Time.time>a.swing){a.swing=Time.time+2.4f;if(a.target.Hit(1,false))AlliedSwordHits++;}continue;
                    }
                    a.pose.SwimMoving=true;
                    var target=a.target.transform.position;
                    if(!a.target.IsOnWreck&&!a.target.IsIndependentBoarder)
                    {
                        var source=a.target.ship;float side=source.transform.InverseTransformPoint(a.go.transform.position).x<0?-1:1;
                        target=source.transform.TransformPoint(new Vector3(side*(source.stakeHitDistance*1.25f+6),0,a.target.transform.localPosition.z));
                    }
                    target.y=TideSystem.CurrentWaterHeight-4;
                    a.go.transform.position=RiverHullCollision.Move(a.go.transform.position,Vector3.MoveTowards(a.go.transform.position,target,26*dt),3.5f,true);
                    if(QuestRowboat.FlatDistance(a.go.transform.position,target)<15){a.state=2;a.travel=0;a.start=a.go.transform.position;a.pose.Traversal=RiverFighterPose.Motion.Climb;ClimbsStarted++;}
                }
                else if(a.state==2)
                {
                    a.travel+=dt;var target=a.target.transform.position+a.target.transform.right*12;float t=Mathf.Clamp01(a.travel/1.7f);
                    var above=a.start;above.y=target.y+2;a.go.transform.position=t<.72f?Vector3.Lerp(a.start,above,Mathf.SmoothStep(0,1,t/.72f)):Vector3.Lerp(above,target,Mathf.SmoothStep(0,1,(t-.72f)/.28f));
                    a.pose.ClimbProgress=t;a.pose.ClimbRail=new Vector3(a.start.x,target.y+1,a.start.z);
                    if(a.travel>=1.7f){a.state=3;a.pose.FinishTraversal();a.pose.SetSword(true);}
                }
                else
                {
                    var delta=a.target.transform.position-a.go.transform.position;
                    if(Mathf.Abs(delta.y)>18){a.state=1;a.pose.SetSword(false);a.pose.Traversal=RiverFighterPose.Motion.Swim;continue;}
                    a.go.transform.position=Vector3.MoveTowards(a.go.transform.position,a.target.transform.position-a.target.transform.forward*12,15*dt);
                    a.pose.Walk(delta.magnitude>16);
                    if(delta.magnitude<24&&Time.time>a.swing){a.swing=Time.time+2.4f;a.pose.Slash();if(a.target.Hit(1,false))AlliedSwordHits++;PlaySword(true);}
                }
                var facing=a.target.transform.position-a.go.transform.position;facing.y=0;if(facing.sqrMagnitude>.1f)a.go.transform.rotation=Quaternion.LookRotation(facing);
            }
        }
        public void StepEnemies(float dt)
        {
            foreach(var guard in archery.guards)
            {
                if(!guard.IsAlive){if(enemyPoses.TryGetValue(guard,out var deadPose))deadPose.SetSword(false);continue;}
                if(guard.IsIndependentBoarder&&hostiles.Exists(h=>h.guard==guard&&h.state!=3)){guard.SetMelee(hostiles.Exists(h=>h.guard==guard&&h.state!=0));continue;}
                Transform target=null;Ally opponent=null;Defender defender=null;float nearest=(PlayerAway||guard.IsIndependentBoarder)&&!IsTraversing?Vector3.Distance(session.player.transform.position,guard.transform.position):float.PositiveInfinity;
                if(nearest<50)target=session.player.transform;
                foreach(var a in allies)if(a.health>0&&a.state==3&&a.go.activeSelf)
                {float d=Vector3.Distance(a.go.transform.position,guard.transform.position);if(d<Mathf.Min(nearest,50)){nearest=d;target=a.go.transform;opponent=a;}}
                foreach(var d in defenders)if(d.health>0&&d.soldier.gameObject.activeInHierarchy)
                {float distance=Vector3.Distance(d.soldier.position,guard.transform.position);if(distance<Mathf.Min(nearest,50)){nearest=distance;target=d.soldier;defender=d;opponent=null;}}
                bool melee=target!=null;guard.SetMelee(melee);var pose=enemyPoses[guard];pose.SetSword(melee);
                if(!melee)continue;
                var direction=target.position-guard.transform.position;direction.y=0;
                if(direction.sqrMagnitude>.01f)guard.transform.rotation=Quaternion.LookRotation(direction);
                if(nearest>18)guard.transform.position=Vector3.MoveTowards(guard.transform.position,target.position-guard.transform.forward*13,9*dt);
                pose.Walk(nearest>20);
                if(nearest<27&&Time.time>enemyAttacks[guard])
                {
                    enemyAttacks[guard]=Time.time+1.65f;pose.Slash();EnemySwordHits++;PlaySword(true);
                    if(defender!=null){defender.health--;DefenderDamageTaken++;if(defender.health<=0)defender.soldier.gameObject.SetActive(false);}
                    else if(opponent!=null)opponent.health--;
                    else session.player.GetComponent<PlayerVitals>().TakeImpact(IsBlocking?2:9,"Kiếm địch");
                }
            }
            if(Time.time<nextArrow)return;nextArrow=Time.time+3;
            foreach(var a in allies)if(a.health>0&&a.state==1&&a.target!=null&&a.target.IsAlive&&Vector3.Distance(a.go.transform.position,a.target.transform.position)<240){a.health--;BeachBoatSoundscape.Instance?.PlayBow(true);break;}
        }
        public void StepHostiles(float dt)
        {
            if(!AlliedAIEnabled){foreach(var h in hostiles)h.go.SetActive(false);return;}
            foreach(var h in hostiles)
            {
                if(h.source==null)
                {
                    foreach(var source in EnemyFleetDirector.Instance.enemyShips)if(source.gameObject.activeInHierarchy&&!source.isDestroyed){h.source=source;break;}
                    if(h.source==null)continue;
                    h.guard.ship=h.source;h.guard.waveIndex=Array.IndexOf(EnemyFleetDirector.Instance.enemyShips,h.source)/Mathf.Max(1,EnemyFleetDirector.Instance.shipsPerWave);h.pose.FinishTraversal();h.go.SetActive(true);h.guard.ResetGuard();h.state=0;
                }
                if(!h.guard.IsAlive){h.go.SetActive(false);if(!h.fromWreck){h.respawn+=dt;if(h.respawn>12){h.source=null;h.respawn=0;}}continue;}
                if(h.state==0)
                {
                    h.go.transform.position=h.source.transform.position+Vector3.up*22;
                    h.boat=QuestRowboat.Instance.transform;float nearest=QuestRowboat.FlatDistance(h.go.transform.position,h.boat.position);
                    foreach(var friendly in friendlyBoats){float d=QuestRowboat.FlatDistance(h.go.transform.position,friendly.transform.position);if(d<nearest){nearest=d;h.boat=friendly.transform;}}
                    if(nearest>310&&!h.source.isDestroyed)continue;
                    float side=h.source.transform.InverseTransformPoint(h.boat.position).x<0?-1:1;
                    var entry=h.source.transform.TransformPoint(new Vector3(side*(h.source.stakeHitDistance+16),0,0));
                    entry.y=TideSystem.CurrentWaterHeight-4;h.go.transform.position=entry;
                    h.state=1;h.pose.SetSword(false);h.pose.Traversal=RiverFighterPose.Motion.Swim;h.guard.SetMelee(true);SwimsStarted++;
                }
                if(h.targetPlayer)h.boat=State==PlayerState.EnemyDeck&&deckAnchor!=null?deckAnchor:QuestRowboat.Instance.transform;
                if(h.boat==null){h.source=null;continue;}
                var end=h.targetPlayer&&PlayerAway?session.player.transform.position:h.boat.position+h.boat.right*(h.boat==QuestRowboat.Instance.transform?4:10)+Vector3.up*(h.boat==QuestRowboat.Instance.transform?10:24);
                if(h.boat!=QuestRowboat.Instance.transform&&h.boat.GetComponent<AmbientRiverBoat>()!=null)
                {var crew=h.boat.GetComponentInChildren<FleetDeckCrew>();if(crew!=null&&crew.soldiers.Length>0)end=crew.soldiers[0].position+h.boat.forward*8;}
                if(h.state==1)
                {
                    var water=end;
                    var targetShip=h.boat.GetComponent<EnemyShip>();
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
                    water.y=TideSystem.CurrentWaterHeight-4;h.go.transform.position=RiverHullCollision.Move(h.go.transform.position,Vector3.MoveTowards(h.go.transform.position,water,24*dt),4,true);
                    if(h.targetPlayer&&State==PlayerState.Swimming)
                    {
                        h.pose.SwimMoving=QuestRowboat.FlatDistance(h.go.transform.position,water)>18;
                        if(!h.pose.SwimMoving&&Time.time>enemyAttacks[h.guard]){enemyAttacks[h.guard]=Time.time+2.8f;session.player.GetComponent<PlayerVitals>().TakeImpact(5,"Địch tập kích dưới nước");if(QuestCheckpoint.Instance!=null&&QuestCheckpoint.Instance.IsDefeated)return;}
                        var aim=water-h.go.transform.position;aim.y=0;if(aim.sqrMagnitude>.1f)h.go.transform.rotation=Quaternion.LookRotation(aim);continue;
                    }
                    if(QuestRowboat.FlatDistance(h.go.transform.position,water)<17){h.state=2;h.start=h.go.transform.position;h.time=0;h.pose.Traversal=RiverFighterPose.Motion.Climb;ClimbsStarted++;}
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
            if(!AlliedAIEnabled)return;
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
                if(nearest<25&&Time.time>=defender.nextAttack)
                {defender.nextAttack=Time.time+1.8f;defender.pose.Slash();if(target.Hit(1,false)){DefenderHits++;AlliedSwordHits++;PlaySword(true);}}
            }
        }
        public void ReleaseShipwreckSurvivors(EnemyShip ship,EnemyDeckGuard[] crew)
        {
            int survived=0;
            for(int i=0;i<crew.Length;i++)
            {
                var guard=crew[i];if(guard.Health<=0)continue;
                if(i%3==0){guard.DefeatFromVolley();ShipwreckDeaths++;continue;}
                var pose=enemyPoses[guard];pose.SetSword(false);guard.IsIndependentBoarder=true;guard.SetMelee(true);
                guard.transform.SetParent(transform,true);var p=ship.transform.position+ship.transform.right*(i%2==0?-58:58)+ship.transform.forward*((i/2-1)*22);p.y=TideSystem.CurrentWaterHeight-4;guard.transform.position=p;
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
        void PlaySword(bool hit)
        {var scene=GetComponent<RiverBattleChoreography>();if(scene==null)return;var source=GetComponent<AudioSource>();if(source!=null)source.PlayOneShot(hit?scene.swordClash:scene.swordSwing,.25f);}
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
