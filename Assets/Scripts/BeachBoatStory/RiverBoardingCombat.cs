using System;
using System.Collections.Generic;
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
        sealed class Hostile
        {public GameObject go;public EnemyDeckGuard guard;public RiverFighterPose pose;public EnemyShip source;public Transform boat;public Vector3 start,local;public int state;public float time,respawn;}
        readonly List<Hostile> hostiles=new List<Hostile>();
        readonly Dictionary<EnemyDeckGuard,RiverFighterPose> enemyPoses=new Dictionary<EnemyDeckGuard,RiverFighterPose>();
        readonly Dictionary<EnemyDeckGuard,float> enemyAttacks=new Dictionary<EnemyDeckGuard,float>();
        BeachBoatQuestSession session;RiverPlayerCombat archery;RiverFighterPose playerPose;
        EnemyShip deckShip;Transform deckAnchor;Vector3 deckLocal,climbStart,climbLocalStart;float climbTime,nextSlash,nextArrow;
        TMP_Text hint;bool wasBattle;
        void Awake(){Instance=this;session=GetComponent<BeachBoatQuestSession>();archery=GetComponent<RiverPlayerCombat>();}
        void Start()
        {
            FootController=Resources.Load<RuntimeAnimatorController>("Player_Neutral");
            playerPose=session.player.AddComponent<RiverFighterPose>();playerPose.Initialize(session.player.GetComponentInChildren<Animator>(),swordPrefab,swordController,17);
            foreach(var guard in archery.guards)
            {var pose=guard.gameObject.AddComponent<RiverFighterPose>();pose.Initialize(guard.visual.GetComponentInChildren<Animator>(),swordPrefab,swordController,11);enemyPoses[guard]=pose;enemyAttacks[guard]=0;}
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
                var pose=go.AddComponent<RiverFighterPose>();pose.Initialize(model.GetComponentInChildren<Animator>(),swordPrefab,swordController,11);
                hostiles.Add(new Hostile{go=go,guard=guard,pose=pose});enemyPoses[guard]=pose;enemyAttacks[guard]=0;allGuards.Add(guard);
            }
            archery.guards=allGuards.ToArray();
            var canvas=new GameObject("BoardingCombatHUD",typeof(Canvas),typeof(CanvasScaler));canvas.transform.SetParent(transform,false);canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=112;
            var scale=canvas.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);
            var text=new GameObject("BoardingHint",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(canvas.transform,false);var rect=(RectTransform)text.transform;rect.anchorMin=new Vector2(.23f,.30f);rect.anchorMax=new Vector2(.79f,.36f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            hint=text.GetComponent<TMP_Text>();StoryFont.Apply(hint);hint.fontSize=18;hint.color=new Color(1,.84f,.48f);hint.alignment=TextAlignmentOptions.Center;
        }
        public void ResetBattle()
        {
            if(playerPose!=null){playerPose.Restore();if(QuestRowboat.Instance!=null&&QuestRowboat.Instance.IsAboard)playerPose.Animator.runtimeAnimatorController=QuestRowboat.Instance.rowingController;}SwordDrawn=false;State=PlayerState.Rowboat;deckShip=null;deckAnchor=null;
            foreach(var a in allies){a.go.SetActive(false);a.health=6;a.state=0;a.pose.Restore();}
            foreach(var h in hostiles){h.go.SetActive(false);h.state=0;h.source=null;h.pose.Restore();}
            foreach(var pair in enemyPoses){pair.Value.Restore();pair.Key.SetMelee(false);}
            wasBattle=false;if(hint!=null)hint.text="";
        }
        void Update()
        {
            bool battle=archery!=null&&archery.BattleActive;
            if(!battle){if(wasBattle)ResetBattle();return;}
            if(!wasBattle)
            {
                wasBattle=true;foreach(var a in allies){a.health=6;a.state=0;a.arrows=2;a.shot=Time.time+2;a.go.SetActive(true);a.pose.SetSword(true);}nextArrow=Time.time+3;
                PlayerSwordHits=AlliedArrowHits=AlliedSwordHits=EnemySwordHits=SwimsStarted=ClimbsStarted=HostileBoardings=0;
            }
            if(BeachBoatQuestSession.InputBlocked)return;
            if(Cursor.lockState==CursorLockMode.Locked)
            {
                if(Input.GetKeyDown(KeyCode.Q))ToggleSword();
                if(Input.GetKeyDown(KeyCode.F))TryTraverse();
                if(SwordDrawn&&Input.GetMouseButtonDown(0))TrySlash();
            }
            StepPlayer(Time.deltaTime);StepAllies(Time.deltaTime);StepHostiles(Time.deltaTime);StepEnemies(Time.deltaTime);
            hint.text=State==PlayerState.Swimming?"WASD: BƠI • SHIFT: BƠI NHANH • F: LEO LÊN TÀU GẦN / VỀ THUYỀN":
                State==PlayerState.Climbing?"ĐANG BÁM MẠN VÀ LEO LÊN BOONG…":
                SwordDrawn?"KIẾM • CHUỘT TRÁI: CHÉM • CHUỘT PHẢI: ĐỠ • Q: CẤT KIẾM • F: XUỐNG NƯỚC":
                "Q: RÚT KIẾM • F: XUỐNG NƯỚC / LEO TÀU • ĐỒNG MINH ĐANG TIẾP CHIẾN";
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
                if(QuestRowboat.FlatDistance(session.player.transform.position,QuestRowboat.Instance.transform.position)<48)
                {playerPose.FinishTraversal();playerPose.Animator.runtimeAnimatorController=QuestRowboat.Instance.rowingController;State=PlayerState.Rowboat;deckShip=null;deckAnchor=null;QuestRowboat.Instance.UseSoldierBattleCamera();return true;}
                var nearest=NearestBoard(session.player.transform.position,out var ship,out var anchor,out var local);
                if(nearest>24)return false;deckShip=ship;deckAnchor=anchor;deckLocal=local;climbStart=session.player.transform.position;climbLocalStart=anchor.InverseTransformPoint(climbStart);climbTime=0;State=PlayerState.Climbing;playerPose.Traversal=RiverFighterPose.Motion.Climb;ClimbsStarted++;return true;
            }
            BeginSwim(session.player.transform.position+session.player.transform.right*22);return true;
        }
        void BeginSwim(Vector3 p)
        {
            archery.SetAim(false);playerPose.SetSword(false);SwordDrawn=false;
            p.y=TideSystem.CurrentWaterHeight-5;MovePlayer(p,session.player.transform.rotation);State=PlayerState.Swimming;playerPose.Traversal=RiverFighterPose.Motion.Swim;SwimsStarted++;
            var camera=session.storyCamera.GetComponent<Camera_Script>();camera.distance=70;camera.heightOffset=27;camera.SnapToTarget();
        }
        void MovePlayer(Vector3 p,Quaternion rotation)
        {session.player.transform.SetPositionAndRotation(p,rotation);var body=session.player.GetComponent<Rigidbody>();body.position=p;body.rotation=rotation;}
        public void StepPlayer(float dt)
        {
            if(State==PlayerState.Rowboat)return;
            if(State==PlayerState.EnemyDeck&&(deckAnchor==null||deckShip.isDestroyed&&deckShip.WreckTarget!=deckAnchor)){BeginSwim(session.player.transform.position);return;}
            if(State==PlayerState.Climbing)
            {
                if(deckAnchor==null||deckShip.isDestroyed&&deckShip.WreckTarget!=deckAnchor){BeginSwim(session.player.transform.position);return;}
                climbTime+=dt;var destination=deckAnchor.TransformPoint(deckLocal);float t=Mathf.Clamp01(climbTime/1.8f);
                var facing=deckAnchor.position-session.player.transform.position;facing.y=0;
                var from=deckAnchor.TransformPoint(climbLocalStart);var above=from;above.y=destination.y+3;
                var climb=t<.65f?Vector3.Lerp(from,above,t/.65f):Vector3.Lerp(above,destination,(t-.65f)/.35f);
                MovePlayer(climb,facing.sqrMagnitude>.01f?Quaternion.LookRotation(facing):session.player.transform.rotation);
                if(t>=1){State=PlayerState.EnemyDeck;playerPose.FinishTraversal();SwordDrawn=true;playerPose.SetSword(true);}return;
            }
            var camera=session.storyCamera.transform;var forward=camera.forward;forward.y=0;forward.Normalize();var right=Vector3.Cross(Vector3.up,forward);
            var direction=Vector3.ClampMagnitude(forward*Input.GetAxisRaw("Vertical")+right*Input.GetAxisRaw("Horizontal"),1);
            bool fast=Input.GetKey(KeyCode.LeftShift)&&session.player.GetComponent<PlayerVitals>().TryExert(15,dt);
            var p=session.player.transform.position+direction*(State==PlayerState.Swimming?(fast?38:23):26)*dt;
            if(State==PlayerState.Swimming)
            {
                var travel=p-session.player.transform.position;
                if(travel.sqrMagnitude>.0001f&&Physics.SphereCast(session.player.transform.position+Vector3.up*9,4,travel.normalized,out var hit,travel.magnitude+1,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<EnemyShip>()!=null)p=session.player.transform.position;
                p.y=TideSystem.CurrentWaterHeight-5+Mathf.Sin(Time.time*3)*.3f;
            }
            else {deckLocal+=deckAnchor.InverseTransformDirection(direction)*26*dt;deckLocal.x=Mathf.Clamp(deckLocal.x,-deckShip.stakeHitDistance*.65f,deckShip.stakeHitDistance*.65f);deckLocal.z=Mathf.Clamp(deckLocal.z,-deckShip.hullHalfLength*.65f,deckShip.hullHalfLength*.65f);p=deckAnchor.TransformPoint(deckLocal);playerPose.Walk(direction.sqrMagnitude>.01f);}
            MovePlayer(p,direction.sqrMagnitude>.01f?Quaternion.LookRotation(direction):session.player.transform.rotation);
        }
        float NearestBoard(Vector3 p,out EnemyShip ship,out Transform anchor,out Vector3 local)
        {
            ship=null;anchor=null;local=Vector3.zero;float best=float.PositiveInfinity;
            foreach(var candidate in EnemyFleetDirector.Instance.enemyShips)
            {
                var target=candidate.isDestroyed?candidate.WreckTarget:candidate.gameObject.activeInHierarchy?candidate.transform:null;if(target==null)continue;
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
                    var target=a.target.transform.position;
                    if(!a.target.IsOnWreck&&!a.target.IsIndependentBoarder)
                    {
                        var source=a.target.ship;float side=source.transform.InverseTransformPoint(a.go.transform.position).x<0?-1:1;
                        target=source.transform.TransformPoint(new Vector3(side*(source.stakeHitDistance*1.25f+6),0,a.target.transform.localPosition.z));
                    }
                    target.y=TideSystem.CurrentWaterHeight-4;
                    a.go.transform.position=Vector3.MoveTowards(a.go.transform.position,target,26*dt);
                    if(QuestRowboat.FlatDistance(a.go.transform.position,target)<15){a.state=2;a.travel=0;a.start=a.go.transform.position;a.pose.Traversal=RiverFighterPose.Motion.Climb;ClimbsStarted++;}
                }
                else if(a.state==2)
                {
                    a.travel+=dt;var target=a.target.transform.position+a.target.transform.right*12;a.go.transform.position=Vector3.Lerp(a.start,target,Mathf.Clamp01(a.travel/1.7f));
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
                Transform target=null;Ally opponent=null;float nearest=(PlayerAway||guard.IsIndependentBoarder)&&!IsTraversing?Vector3.Distance(session.player.transform.position,guard.transform.position):float.PositiveInfinity;
                if(nearest<50)target=session.player.transform;
                foreach(var a in allies)if(a.health>0&&a.state==3&&a.go.activeSelf)
                {float d=Vector3.Distance(a.go.transform.position,guard.transform.position);if(d<Mathf.Min(nearest,50)){nearest=d;target=a.go.transform;opponent=a;}}
                bool melee=target!=null;guard.SetMelee(melee);var pose=enemyPoses[guard];pose.SetSword(melee);
                if(!melee)continue;
                var direction=target.position-guard.transform.position;direction.y=0;
                if(direction.sqrMagnitude>.01f)guard.transform.rotation=Quaternion.LookRotation(direction);
                if(nearest>18)guard.transform.position=Vector3.MoveTowards(guard.transform.position,target.position-guard.transform.forward*13,9*dt);
                pose.Walk(nearest>20);
                if(nearest<27&&Time.time>enemyAttacks[guard])
                {
                    enemyAttacks[guard]=Time.time+1.65f;pose.Slash();EnemySwordHits++;PlaySword(true);
                    if(opponent!=null)opponent.health--;
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
                if(!h.guard.IsAlive){h.go.SetActive(false);h.respawn+=dt;if(h.respawn>12){h.source=null;h.respawn=0;}continue;}
                if(h.state==0)
                {
                    h.go.transform.position=h.source.transform.position+Vector3.up*22;
                    h.boat=QuestRowboat.Instance.transform;float nearest=QuestRowboat.FlatDistance(h.go.transform.position,h.boat.position);
                    foreach(var friendly in friendlyBoats){float d=QuestRowboat.FlatDistance(h.go.transform.position,friendly.transform.position);if(d<nearest){nearest=d;h.boat=friendly.transform;}}
                    if(nearest>310&&!h.source.isDestroyed)continue;
                    h.state=1;h.pose.SetSword(false);h.pose.Traversal=RiverFighterPose.Motion.Swim;h.guard.SetMelee(true);SwimsStarted++;
                }
                if(h.boat==null){h.source=null;continue;}
                var end=h.boat.position+h.boat.right*10+Vector3.up*(h.boat==QuestRowboat.Instance.transform?10:24);
                if(h.state==1)
                {
                    var water=end;water.y=TideSystem.CurrentWaterHeight-4;h.go.transform.position=Vector3.MoveTowards(h.go.transform.position,water,24*dt);
                    if(QuestRowboat.FlatDistance(h.go.transform.position,water)<17){h.state=2;h.start=h.go.transform.position;h.time=0;h.pose.Traversal=RiverFighterPose.Motion.Climb;ClimbsStarted++;}
                }
                else if(h.state==2)
                {
                    h.time+=dt;h.go.transform.position=Vector3.Lerp(h.start,end,Mathf.Clamp01(h.time/1.9f));
                    if(h.time>=1.9f){h.state=3;h.local=h.boat.InverseTransformPoint(end);h.pose.FinishTraversal();h.pose.SetSword(true);HostileBoardings++;}
                }
                else h.go.transform.position=h.boat.TransformPoint(h.local);
                var forward=end-h.go.transform.position;forward.y=0;if(forward.sqrMagnitude>.01f)h.go.transform.rotation=Quaternion.LookRotation(forward);
            }
        }
        void PlaySword(bool hit)
        {var scene=GetComponent<RiverBattleChoreography>();if(scene==null)return;var source=GetComponent<AudioSource>();if(source!=null)source.PlayOneShot(hit?scene.swordClash:scene.swordSwing,.25f);}
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
