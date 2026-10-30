using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BeachBoatStory
{
    /// <summary>Real friendly-fire hits and distinct commander-aim episodes, independent of quest stage.</summary>
    [DefaultExecutionOrder(550)]
    public sealed class CampDiscipline:MonoBehaviour
    {
        public static CampDiscipline Instance {get;private set;}
        public static bool WeaponsAvailable=>BeachBoatQuestSession.Instance?.storyMode==true&&!BeachBoatQuestSession.InputBlocked&&RiverBoardingCombat.Instance?.IsTraversing!=true;
        public const string WarningLine="Hạ cung xuống! Không được chĩa vũ khí vào chủ tướng. Ta chỉ nhắc ngươi lần này.";
        public const string ExecutionLine="Quân đâu! Kẻ phản nghịch dám phạm thượng. Giết hắn!";
        public const string RetaliationLine="Có kẻ phản quân! Đừng để hắn chạy thoát!";
        public readonly List<ProtectedCampNPC> Actors=new List<ProtectedCampNPC>();
        public int CommanderWarnings {get;private set;}
        public bool ArmyHostile {get;private set;}
        public int RetaliatingSoldiers=>Actors.Count(a=>a!=null&&a.Retaliating);
        BeachBoatQuestSession session;DialogueVoice voice;TMP_Text feedback;float feedbackUntil,nextDiscover,awayTime;bool aimLatched;
        readonly HashSet<string> savedAttackers=new HashSet<string>();readonly RaycastHit[] hits=new RaycastHit[64];
        void Awake(){Instance=this;}
        void Start()
        {
            session=BeachBoatQuestSession.Instance;var audio=new GameObject("Camp_Discipline_Voice");audio.transform.SetParent(transform,false);voice=audio.AddComponent<DialogueVoice>();
            var canvas=new GameObject("Camp_Discipline_Caption",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));canvas.transform.SetParent(transform,false);canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=115;
            var scale=canvas.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);
            var text=new GameObject("Discipline_feedback",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(canvas.transform,false);feedback=text.GetComponent<TextMeshProUGUI>();StoryFont.Apply(feedback);feedback.fontSize=18;feedback.color=new Color(1,.78f,.4f);feedback.alignment=TextAlignmentOptions.Center;feedback.raycastTarget=false;feedback.rectTransform.anchorMin=new Vector2(.24f,.23f);feedback.rectTransform.anchorMax=new Vector2(.73f,.34f);feedback.rectTransform.offsetMin=feedback.rectTransform.offsetMax=Vector2.zero;feedback.gameObject.SetActive(false);
            Discover();
        }
        public void Discover()
        {
            foreach(var rig in FindObjectsByType<Animator>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(!rig.isHuman||rig.transform.IsChildOf(session.player.transform)||rig.GetComponentInParent<EnemyDeckGuard>()!=null||rig.GetComponentInParent<BossDamageReceiver>()!=null||rig.GetComponentInParent<ProtectedCampNPC>()!=null)continue;
                var commander=rig.GetComponentInParent<NPCQuestGiver>();var dialogue=rig.GetComponentInParent<NPCDialogue>();var ambient=rig.GetComponentInParent<NPCAmbientDialogue>();var pose=rig.GetComponentInParent<RiverFighterPose>();var body=rig.GetComponentInParent<ActorSolidBody>();
                var stall=rig.GetComponentInParent<VillageWorkStall>();bool work=body!=null&&body.name.StartsWith("Cong_binh_",StringComparison.Ordinal);
                if(commander==null&&dialogue==null&&ambient==null&&pose==null&&rig.GetComponent<NPCCampPose>()==null&&stall==null&&!work)continue;
                var root=commander!=null?commander.transform:pose!=null?pose.transform:work?body.transform:dialogue!=null?dialogue.transform:ambient!=null?ambient.transform:rig.transform;
                if(root.GetComponent<ProtectedCampNPC>()!=null)continue;
                string label=dialogue!=null?dialogue.speakerName:ambient!=null?ambient.displayName:root.name;
                bool soldier=commander==null&&(work||pose!=null||rig.GetComponent<NPCCampPose>()!=null||root.GetComponentInParent<AmbientRiverBoat>()!=null||ambient?.role==NPCAmbientDialogue.Role.Guard||label.StartsWith("Lính",StringComparison.OrdinalIgnoreCase)||label.StartsWith("Quân",StringComparison.OrdinalIgnoreCase)||label.StartsWith("Quản quân",StringComparison.OrdinalIgnoreCase));
                var actor=root.gameObject.AddComponent<ProtectedCampNPC>();actor.Initialize(this,rig,commander!=null?ProtectedCampNPC.Kind.Commander:soldier?ProtectedCampNPC.Kind.Soldier:ProtectedCampNPC.Kind.Civilian);
                Actors.Add(actor);if(actor.Type==ProtectedCampNPC.Kind.Soldier&&(ArmyHostile||savedAttackers.Contains(actor.Id)))actor.Retaliate();
            }
        }
        void Update()
        {
            if(session==null)return;if(Time.unscaledTime>=nextDiscover){nextDiscover=Time.unscaledTime+2;Discover();Actors.RemoveAll(a=>a==null);}
            if(feedback!=null)feedback.gameObject.SetActive(Time.unscaledTime<feedbackUntil);
            if(WeaponsAvailable)ObserveCommanderAim(RiverPlayerCombat.Instance.IsAiming,session.storyCamera.ViewportPointToRay(new Vector3(.5f,.5f)),Time.deltaTime);
            if(!session.storyMode||BeachBoatQuestSession.InputBlocked||Time.timeScale<=0)return;
            foreach(var actor in Actors)if(actor!=null&&actor.Retaliating&&actor.gameObject.activeInHierarchy)actor.StepRetaliation(Time.deltaTime);
        }
        public ProtectedCampNPC AimTarget(Ray ray,float range=600)
        {
            Physics.SyncTransforms();int count=Physics.SphereCastNonAlloc(ray,.8f,hits,range,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);float nearest=range;ProtectedCampNPC actor=null;
            for(int i=0;i<count;i++){var h=hits[i];if(h.transform.IsChildOf(session.player.transform)||QuestRowboat.Instance.IsAboard&&h.transform.IsChildOf(QuestRowboat.Instance.transform)||h.distance>=nearest)continue;nearest=h.distance;actor=h.collider.GetComponentInParent<ProtectedCampNPC>();}return actor;
        }
        public void ObserveCommanderAim(bool aiming,Ray ray,float dt)
        {
            if(!aiming){aimLatched=false;awayTime=0;return;}
            if(AimTarget(ray)?.Type!=ProtectedCampNPC.Kind.Commander){awayTime+=Mathf.Max(0,dt);if(awayTime>=.6f)aimLatched=false;return;}
            awayTime=0;if(aimLatched||QuestCheckpoint.Instance.IsDefeated)return;aimLatched=true;CommanderWarnings++;
            if(CommanderWarnings==1)Speak("Ngô Quyền",WarningLine);
            else Execute("Chĩa cung vào Ngô Quyền lần thứ hai sau khi đã bị cảnh cáo.");
        }
        public void Speak(string speaker,string line)
        {feedback.text=speaker+": "+line;feedbackUntil=Time.unscaledTime+6;voice.Play(speaker,line,"camp_discipline",false,speaker=="Ngô Quyền"?"commander_brief":"soldier");}
        public bool SwordHit(Transform player)
        {
            ProtectedCampNPC best=null;float distance=33;
            foreach(var a in Actors)
            {
                if(a==null||a.Dead||!a.gameObject.activeInHierarchy)continue;var d=a.transform.position-player.position;if(Mathf.Abs(d.y)>19)continue;d.y=0;
                if(d.magnitude>=distance||Vector3.Dot(player.forward,d.normalized)<=.15f||!RiverBoardingCombat.Instance.CanMeleeReach(player,a.transform,36))continue;best=a;distance=d.magnitude;
            }
            return best!=null&&Hit(best,CombatImpactKind.Sword);
        }
        public bool Hit(ProtectedCampNPC actor,CombatImpactKind kind)
        {
            if(actor==null||actor.Dead||QuestCheckpoint.Instance.IsDefeated)return false;
            if(actor.Type==ProtectedCampNPC.Kind.Civilian)
            {actor.Die();CombatHitFeedback.Show(actor.AimPoint,kind);QuestCheckpoint.Instance.Defeat("BẠN ĐÃ SÁT HẠI DÂN THƯỜNG","Giữ kỷ luật: không được chém hoặc bắn thường dân.");}
            else if(actor.Type==ProtectedCampNPC.Kind.Commander)Execute("Tấn công Chủ tướng Ngô Quyền.");
            else{if(!actor.Retaliating)Speak("Quân sĩ",RetaliationLine);actor.Retaliate();savedAttackers.Add(actor.Id);BeachBoatSoundscape.Instance?.PlayShieldBlock();}
            return true;
        }
        public void Execute(string reason)
        {ArmyHostile=true;foreach(var a in Actors)if(a!=null&&a.Type==ProtectedCampNPC.Kind.Soldier)a.Retaliate();Speak("Ngô Quyền",ExecutionLine);QuestCheckpoint.Instance.Defeat("BỊ XỬ VÌ PHẢN NGHỊCH",reason+"\nNgô Quyền đã ra lệnh toàn quân bắt giết bạn.");}
        public BachDangSaveData.Discipline Capture()=>new BachDangSaveData.Discipline{warnings=CommanderWarnings,armyHostile=ArmyHostile,attackers=Actors.Where(a=>a!=null&&a.Retaliating).Select(a=>a.Id).ToArray(),pursuers=Actors.Where(a=>a!=null&&a.Retaliating).Select(a=>a.CapturePursuit()).ToArray()};
        public void Restore(BachDangSaveData.Discipline data)
        {ResetAll();CommanderWarnings=data?.warnings??0;ArmyHostile=data?.armyHostile??false;foreach(var id in data?.attackers??Array.Empty<string>())savedAttackers.Add(id);Discover();foreach(var a in Actors)if(a!=null&&a.Type==ProtectedCampNPC.Kind.Soldier&&(ArmyHostile||savedAttackers.Contains(a.Id))){a.Retaliate();var saved=(data?.pursuers??Array.Empty<BachDangSaveData.DisciplineActor>()).FirstOrDefault(p=>p.id==a.Id);if(saved!=null)a.RestorePursuit(saved);}}
        public void ResetAll(){foreach(var a in Actors)if(a!=null)a.ResetActor();savedAttackers.Clear();CommanderWarnings=0;ArmyHostile=aimLatched=false;awayTime=0;feedbackUntil=0;voice?.Stop();}
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
    [DefaultExecutionOrder(560)]
    public sealed class ProtectedCampNPC:MonoBehaviour
    {
        public enum Kind {Civilian,Soldier,Commander}
        public Kind Type {get;private set;}public bool Retaliating {get;private set;}public bool Dead {get;private set;}
        public string Id {get;private set;}public Vector3 AimPoint=>rig.GetBoneTransform(HumanBodyBones.Chest).position;
        CampDiscipline owner;Animator rig;RiverFighterPose pose;NPCWander walker;NPCDailyActivity daily;NPCCampPose campPose;
        Behaviour[] talkers;bool[] talkerEnabled;bool walkerEnabled,dailyEnabled,campEnabled,rigEnabled;Quaternion restRotation;RuntimeAnimatorController controller;Transform homeParent;int homeSibling;float nextAttack,nextPlan,fireAt;Vector3 arrowTarget;int point;
        readonly List<Vector3> route=new List<Vector3>();readonly RiverActorNavigation swimNavigation=new RiverActorNavigation();Vector3 originalPosition;Quaternion originalRotation;
        public void Initialize(CampDiscipline owner,Animator animator,Kind kind)
        {
            this.owner=owner;rig=animator;Type=kind;Id=Path(transform);homeParent=transform.parent;homeSibling=transform.GetSiblingIndex();walker=GetComponent<NPCWander>();daily=GetComponent<NPCDailyActivity>();campPose=rig.GetComponent<NPCCampPose>();
            walkerEnabled=walker!=null&&walker.enabled;dailyEnabled=daily!=null&&daily.enabled;campEnabled=campPose!=null&&campPose.enabled;rigEnabled=rig.enabled;restRotation=rig.transform.localRotation;controller=rig.runtimeAnimatorController;
            talkers=GetComponentsInChildren<NPCDialogue>(true).Cast<Behaviour>().Concat(GetComponentsInChildren<NPCAmbientDialogue>(true)).ToArray();talkerEnabled=talkers.Select(t=>t.enabled).ToArray();
            ActorSolidBody.Ensure(gameObject,rig);
        }
        static string Path(Transform t){var result=t.name;while(t.parent!=null){t=t.parent;result=t.name+"/"+result;}return result;}
        void StopDaily(){foreach(var talker in talkers)talker.enabled=false;if(walker!=null)walker.enabled=false;if(daily!=null)daily.enabled=false;if(campPose!=null)campPose.enabled=false;}
        public void Retaliate()
        {
            if(Type!=Kind.Soldier||Retaliating)return;originalPosition=transform.position;originalRotation=transform.rotation;Retaliating=true;StopDaily();
            if(GetComponent<ActorSolidBody>()?.Carrier==null)transform.SetParent(BeachBoatQuestSession.Instance.transform,true);
            pose=GetComponent<RiverFighterPose>();if(pose==null){pose=gameObject.AddComponent<RiverFighterPose>();pose.Initialize(rig,RiverBoardingCombat.Instance.swordPrefab,RiverBoardingCombat.Instance.swordController,17);}
            pose.FinishTraversal();rig.enabled=true;rig.applyRootMotion=false;pose.SetSword(true);nextAttack=Time.time+.25f;nextPlan=0;
        }
        public BachDangSaveData.DisciplineActor CapturePursuit()=>new BachDangSaveData.DisciplineActor{id=Id,position=transform.position,rotation=transform.rotation,home=originalPosition,homeRotation=originalRotation};
        public void RestorePursuit(BachDangSaveData.DisciplineActor data){transform.SetPositionAndRotation(data.position,data.rotation);originalPosition=data.home;originalRotation=data.homeRotation;route.Clear();nextPlan=0;}
        public void Die(){Dead=true;originalPosition=transform.position;originalRotation=transform.rotation;StopDaily();rig.enabled=false;rig.transform.localRotation=restRotation*Quaternion.Euler(85,0,10);}
        public void ResetActor()
        {
            if(!Dead&&!Retaliating)return;if(pose!=null)pose.Restore();Retaliating=Dead=false;rig.transform.localRotation=restRotation;rig.runtimeAnimatorController=controller;rig.enabled=rigEnabled;
            if(transform.parent!=homeParent){transform.SetParent(homeParent,true);transform.SetSiblingIndex(homeSibling);}transform.SetPositionAndRotation(originalPosition,originalRotation);if(walker!=null)walker.enabled=walkerEnabled;if(daily!=null)daily.enabled=dailyEnabled;if(campPose!=null)campPose.enabled=campEnabled;for(int i=0;i<talkers.Length;i++)talkers[i].enabled=talkerEnabled[i];route.Clear();swimNavigation.Reset();
        }
        public void StepRetaliation(float dt)
        {
            if(!Retaliating||dt<=0||QuestCheckpoint.Instance.IsDefeated)return;var player=BeachBoatQuestSession.Instance.player.transform;var delta=player.position-transform.position;var flat=delta;flat.y=0;float distance=flat.magnitude;
            var carrier=GetComponent<ActorSolidBody>()?.Carrier;bool ranged=carrier!=null&&carrier!=RiverBoardingCombat.Instance.PlayerDeckCarrier||Mathf.Abs(delta.y)>20;
            if(!ranged&&distance>24)
            {
                var nav=walker?.Navigation;var next=transform.position;
                if(nav!=null)
                {
                    if(Time.time>=nextPlan){nextPlan=Time.time+1;route.Clear();point=0;nav.Route(transform.position,player.position,route);}
                    while(point<route.Count&&QuestRowboat.FlatDistance(transform.position,route[point])<5)point++;
                    if(route.Count==0)ranged=true;
                    if(point<route.Count){var wanted=Vector3.MoveTowards(transform.position,route[point],34*dt);if(nav.Project(wanted,out var ground)&&nav.Clear(transform.position,ground,true))next=ground;}
                }
                else
                {
                    var terrain=Terrain.activeTerrain;bool raised=terrain!=null&&transform.position.y-terrain.SampleHeight(transform.position)-terrain.transform.position.y>12;
                    if(raised)ranged=true;
                    else{next=swimNavigation.Move(transform,transform.position+Vector3.up*8,player.position+Vector3.up*8,32*dt,4)-Vector3.up*8;if(terrain!=null){var ground=terrain.SampleHeight(next)+terrain.transform.position.y;if(ground>TideSystem.CurrentWaterHeight){ActorSolidBody.WalkingDimensions(rig,out _,out float sole,out _);next.y=ground+transform.position.y-rig.transform.position.y-sole+.15f;}else next=transform.position;}}
                }
                pose.SetSword(!ranged);pose.Walk((next-transform.position).sqrMagnitude>.001f);transform.position=next;
            }
            if(flat.sqrMagnitude>.01f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(flat),300*dt);
            if(ranged)
            {
                pose.SetSword(false);pose.SetRanged(true);
                if(fireAt>0&&Time.time>=fireAt){fireAt=0;RiverPlayerCombat.Instance.LaunchRetaliationArrow(pose.BowMuzzle,arrowTarget,12,transform);}
                if(distance<650&&Time.time>=nextAttack){nextAttack=Time.time+1.6f;pose.BeginBowShot(player.position+Vector3.up*12);arrowTarget=player.position+Vector3.up*10;fireAt=Time.time+.73f;}
            }
            else if(distance<=30&&Time.time>=nextAttack&&RiverBoardingCombat.Instance.CanMeleeReach(transform,player,34))
            {nextAttack=Time.time+1.05f;pose.SetSword(true);pose.Walk(false);pose.Slash();BeachBoatSoundscape.Instance?.PlaySwordSwing();player.GetComponent<PlayerVitals>().TakeImpact(18,"Đồng đội trừng phạt phản quân",CombatImpactKind.Sword);}
        }
    }
}
