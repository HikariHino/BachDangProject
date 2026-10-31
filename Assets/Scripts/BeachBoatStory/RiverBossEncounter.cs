using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BeachBoatStory
{
    /// <summary>Saved post-battle search and a roomy, isolated 3D flagship duel.</summary>
    [DefaultExecutionOrder(450)]
    public sealed class RiverBossEncounter : MonoBehaviour
    {
        public static RiverBossEncounter Instance {get;private set;}
        public const string BossName="Lưu Hoằng Tháo";
        public const int MaximumHealth=100;
        public int Health {get;private set;}=MaximumHealth;
        public int SearchMask {get;private set;}
        public bool InArena {get;private set;}
        public bool IntroActive {get;private set;}
        public float IntroElapsed {get;private set;}
        public const float IntroDuration=3.6f;
        public bool IsFighting=>InArena&&Health>0&&!IntroActive;
        public bool Defeated {get;private set;}
        public bool VictoryCheered {get;private set;}
        public int SearchCount=>((SearchMask&1)!=0?1:0)+((SearchMask&2)!=0?1:0)+((SearchMask&4)!=0?1:0);
        public Transform GuideTarget {get;private set;}
        public Vector3 ArenaCenter=>new Vector3(RiverStakeZone.Instance.FieldBounds.center.x,TideSystem.Instance.highTideY+180,RiverStakeZone.Instance.FieldBounds.center.z);
        public Transform Boss=>boss;
        BeachBoatQuestSession session;BachDangStoryManager story;PlayerVitals vitals;RiverFighterPose heroPose,bossPose;
        readonly GameObject[] captives=new GameObject[3];
        GameObject arena,ui;Transform boss;TMP_Text nameText,actionText;Image healthFill;AudioSource victory;
        bool previousUnderwater;Material arenaWater;
        Vector3 returnPosition,returnBoatPosition;Quaternion returnRotation,returnBoatRotation;bool returnAboard;
        bool oldMotor,oldCollider,oldKinematic;float attackAt,recoverAt,playerSwing,dodgeUntil,attackClock;int attackPhase,attackNumber;
        Vector3 attackDirection,dodgeDirection;Material timber,iron,crimson;
        Transform heroVisual;Quaternion heroVisualRest;Transform[] introBones;Quaternion[] introRotations;Vector3[] introPositions;bool introStrike,introLanding,introOrbitEnabled;float introFov;
        void Awake(){Instance=this;}
        void Start()
        {
            session=BeachBoatQuestSession.Instance;story=BachDangStoryManager.Instance;vitals=session.player.GetComponent<PlayerVitals>();
            GuideTarget=new GameObject("Vi_tri_tim_tu_binh").transform;GuideTarget.SetParent(transform,false);
            victory=gameObject.AddComponent<AudioSource>();victory.playOnAwake=false;victory.volume=.8f;victory.priority=18;
        }
        public BachDangSaveData.Boss Capture()=>new BachDangSaveData.Boss{version=1,mask=SearchMask,health=Health,defeated=Defeated,inArena=InArena,cheered=VictoryCheered,returnAboard=returnAboard,returnPosition=returnPosition,returnRotation=returnRotation,returnBoat=returnBoatPosition,boatRotation=returnBoatRotation,bossPosition=boss!=null?boss.position-ArenaCenter:Vector3.zero};
        public void Restore(BachDangSaveData.Boss data)
        {
            ResetForLoad();SearchMask=data?.mask??0;Health=data!=null?Mathf.Clamp(data.health,0,MaximumHealth):MaximumHealth;Defeated=data?.defeated??false;VictoryCheered=data?.cheered??false;
            if(story.currentState==BachDangStoryManager.StoryState.Act3_SearchPrisoners)for(int i=0;i<3;i++)if((SearchMask&(1<<i))!=0)ShowCaptive(i,EnemyFleetDirector.Instance.enemyShips[i]);
            if(data?.inArena==true&&(story.currentState==BachDangStoryManager.StoryState.Act3_CommanderDuel||story.currentState==BachDangStoryManager.StoryState.Act3_FinalReport))
            {
                returnPosition=data.returnPosition;returnRotation=data.returnRotation;returnAboard=data.returnAboard;returnBoatPosition=data.returnBoat;returnBoatRotation=data.boatRotation;
                var playerPosition=session.player.transform.position;OpenArena(false);session.player.transform.position=ClampDeck(playerPosition);boss.position=ClampDeck(ArenaCenter+data.bossPosition);if(Defeated){bossPose.SetSword(false);bossPose.Animator.enabled=false;boss.rotation=Quaternion.Euler(0,0,75);}
            }
        }
        public void StopVictoryCheer(){victory?.Stop();}
        public void ResetForLoad(){CancelIntroduction();for(int i=0;i<captives.Length;i++)if(captives[i]!=null){captives[i].SetActive(false);Destroy(captives[i]);captives[i]=null;}if(InArena)RestoreMotor();InArena=false;if(arena!=null)arena.SetActive(false);if(ui!=null)ui.SetActive(false);victory?.Stop();attackPhase=0;dodgeUntil=0;}
        void Update()
        {
            if(session==null)return;
            if(story.currentState==BachDangStoryManager.StoryState.Act3_Victory&&!VictoryCheered&&!BeachBoatQuestSession.InteractionBlocked)
            {
                var voices=EnemyFleetDirector.Instance.GetComponent<RiverBattleVoices>();if(voices.VoiceBusySeconds<=0&&!voices.SpeechSource.isPlaying)
                {var clip=Resources.Load<AudioClip>("BachDang938/BattleVoices/DaiViet_Victory");if(clip!=null){victory.clip=clip;victory.Play();}VictoryCheered=true;}
            }
            if(story.currentState==BachDangStoryManager.StoryState.Act3_SearchPrisoners&&!BeachBoatQuestSession.InteractionBlocked)
            {
                int index=NextWreck();if(index>=0)
                {
                    var ship=EnemyFleetDirector.Instance.enemyShips[index];GuideTarget.position=ship.transform.position-ship.transform.right*(ship.stakeHitDistance+45);var p=GuideTarget.position;p.y=TideSystem.CurrentWaterHeight+4;GuideTarget.position=p;
                    bool close=QuestRowboat.FlatDistance(session.player.transform.position,GuideTarget.position)<95;
                    if(close)story.SetInteractPrompt(true,SearchCount>=3?"[E] Lên soái hạm • Đối mặt tướng địch":"[E] Kiểm tra thuyền đắm • Tìm lính đầu hàng",this);
                    if(close&&Input.GetKeyDown(KeyCode.E)&&StoryDialogueUI.TryConsumeInput())InspectWreck(index);
                }
            }
            if(!InArena)return;
            if(IntroActive){if(ui!=null)ui.SetActive(false);if(Time.timeScale>0&&!BachDangFrontEnd.IsBlocking&&!(StoryDialogueUI.Instance?.IsOpen??false))AdvanceIntroduction(Time.deltaTime);return;}
            if(ui!=null)ui.SetActive(true);nameText.text=BossName+"  •  "+Health+" / "+MaximumHealth;healthFill.rectTransform.anchorMax=new Vector2(Health/(float)MaximumHealth,1);
            if(!IsFighting||BeachBoatQuestSession.InputBlocked||Time.timeScale<=0)return;
            StepPlayer(Time.deltaTime);StepBoss(Time.deltaTime);
        }
        int NextWreck()
        {
            var fleet=EnemyFleetDirector.Instance;if(fleet==null)return -1;
            for(int i=0;i<Mathf.Min(3,fleet.enemyShips.Length);i++)if((SearchMask&(1<<i))==0&&fleet.enemyShips[i].isDestroyed)return i;
            return SearchCount>=3?0:-1;
        }
        public bool InspectWreck(int index)
        {
            if(story.currentState!=BachDangStoryManager.StoryState.Act3_SearchPrisoners||index<0||index>=3)return false;
            var ship=EnemyFleetDirector.Instance.enemyShips[index];var point=ship.transform.position-ship.transform.right*(ship.stakeHitDistance+45);
            if(!ship.isDestroyed||QuestRowboat.FlatDistance(session.player.transform.position,point)>=95)return false;
            ShowCaptive(index,ship);SearchMask|=1<<index;story.UpdateUI();
            if(SearchCount<3)StoryDialogueUI.Instance.Show("Lính đầu hàng","Xin đừng giết! Chủ tướng vẫn còn trên soái hạm. Chúng tôi xin hạ vũ khí.");
            else StoryDialogueUI.Instance.Show(BossName,"Ta vẫn còn đây. Hãy lên boong soái hạm đối mặt với ta!",BeginDuel);
            return true;
        }
        void ShowCaptive(int index,EnemyShip ship)
        {
            if(captives[index]!=null)return;
            if(!ship.TryWreckBoardingPoint(ship.transform.position-ship.transform.right*(ship.stakeHitDistance+45),out var floor))return;
            var go=Instantiate(RiverBoardingCombat.Instance.enemyPrefab,transform);go.name="Linh_Nam_Han_dau_hang_"+index;go.transform.position=floor;captives[index]=go;
            foreach(var c in go.GetComponentsInChildren<Collider>()){c.enabled=false;Destroy(c);}
            var rig=go.GetComponentInChildren<Animator>();rig.runtimeAnimatorController=RiverBoardingCombat.Instance.FootController;rig.enabled=true;rig.Update(0);
            rig.transform.localScale*=EnemyDeckGuard.RigHeight(session.player.GetComponentInChildren<Animator>())/Mathf.Max(.01f,EnemyDeckGuard.RigHeight(rig));rig.Update(0);
            var face=session.player.transform.position-go.transform.position;face.y=0;if(face.sqrMagnitude>.1f)go.transform.rotation=Quaternion.LookRotation(face);
            ActorSolidBody.WalkingDimensions(rig,out _,out float sole,out _);go.transform.position+=Vector3.up*(floor.y+.15f-rig.transform.position.y-sole);rig.enabled=false;
            ActorSolidBody.Ensure(go,rig);var solid=go.GetComponent<ActorSolidBody>();if(solid!=null)solid.Carrier=ship.transform;
            float h=EnemyDeckGuard.RigHeight(rig);
            for(int side=0;side<2;side++)
            {
                var upper=rig.GetBoneTransform(side==0?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);var lower=rig.GetBoneTransform(side==0?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var hand=rig.GetBoneTransform(side==0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                QuestRowboat.SolveArm(upper,lower,hand,upper.position+Vector3.up*h*.23f+go.transform.forward*h*.12f+go.transform.right*(side==0?-1:1)*h*.1f,upper.position+go.transform.right*(side==0?-1:1)*h*.3f);
            }
        }
        public void BeginDuel()
        {
            if(SearchCount<3||Defeated)return;
            returnPosition=session.player.transform.position;returnRotation=session.player.transform.rotation;var boat=QuestRowboat.Instance;returnAboard=boat.IsAboard;returnBoatPosition=boat.transform.position;returnBoatRotation=boat.transform.rotation;
            Health=MaximumHealth;story.TransitionTo(BachDangStoryManager.StoryState.Act3_CommanderDuel,true);OpenArena(true);
            var save=BachDangSaveSystem.Capture("Trước trận đấu trên soái hạm");if(BachDangSaveSystem.SaveTemporary(save))BachDangAutoSave.Instance.AcceptSavedPoint(save);
            BeginIntroduction();
        }
        public void PrepareArena(){if(session==null||arena!=null)return;BuildArena();arena.SetActive(false);ui.SetActive(false);bossPose.WarmCombatHands();}
        void BeginIntroduction()
        {
            RiverPlayerCombat.Instance.SetAim(false);heroPose.SetSword(false);heroPose.Animator.runtimeAnimatorController=RiverBoardingCombat.Instance.FootController;heroPose.Animator.enabled=true;heroPose.Animator.Update(0);
            heroVisual=heroPose.Animator.transform;heroVisualRest=heroVisual.localRotation;
            var bones=new System.Collections.Generic.List<Transform>();for(int i=0;i<(int)HumanBodyBones.LastBone;i++){var bone=heroPose.Animator.GetBoneTransform((HumanBodyBones)i);if(bone!=null)bones.Add(bone);}
            introBones=bones.ToArray();introRotations=introBones.Select(b=>b.localRotation).ToArray();introPositions=introBones.Select(b=>b.localPosition).ToArray();heroPose.Animator.enabled=false;
            var orbit=session.storyCamera.GetComponent<Camera_Script>();introOrbitEnabled=orbit.enabled;introFov=session.storyCamera.fieldOfView;orbit.enabled=false;
            IntroActive=true;IntroElapsed=0;introStrike=introLanding=false;boss.SetPositionAndRotation(ArenaCenter+new Vector3(0,0,5),Quaternion.Euler(0,180,0));FitFeet(boss,bossPose.Animator);PoseIntroduction();
        }
        void ResetIntroBones()
        {if(introBones==null)return;for(int i=0;i<introBones.Length;i++)if(introBones[i]!=null){introBones[i].localRotation=introRotations[i];introBones[i].localPosition=introPositions[i];}}
        public void AdvanceIntroduction(float dt)
        {
            if(!IntroActive||dt<=0)return;IntroElapsed=Mathf.Min(IntroDuration,IntroElapsed+dt);
            if(IntroElapsed>=.55f&&!introStrike){introStrike=true;bossPose.Slash();BeachBoatSoundscape.Instance?.PlaySwordSwing();BeachBoatSoundscape.Instance?.PlayHit();}
            if(IntroElapsed>=2f&&!introLanding){introLanding=true;BeachBoatSoundscape.Instance?.PlayHit();}
            if(IntroElapsed>=IntroDuration)
            {
                CancelIntroduction();var actor=session.player.transform;actor.SetPositionAndRotation(ArenaCenter+new Vector3(0,0,-85),Quaternion.identity);boss.SetPositionAndRotation(ArenaCenter+new Vector3(0,0,70),Quaternion.Euler(0,180,0));
                heroPose.SetSword(true);heroPose.Animator.Update(0);FitFeet(actor,heroPose.Animator);FitFeet(boss,bossPose.Animator);session.player.GetComponent<Rigidbody>().position=actor.position;
                attackAt=Time.time+1.5f;recoverAt=Time.time+.5f;dodgeUntil=Time.time+.6f;session.storyCamera.GetComponent<Camera_Script>().SnapToTarget();BeachBoatSoundscape.Instance?.PlayWeaponHandling();return;
            }
            PoseIntroduction();
        }
        void PoseIntroduction()
        {
            if(!IntroActive||heroVisual==null)return;ResetIntroBones();var actor=session.player.transform;
            float flight=Mathf.Clamp01((IntroElapsed-.55f)/1.45f),recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.2f,IntroDuration,IntroElapsed));
            float fall=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,.9f,IntroElapsed))*(1-recover);
            actor.SetPositionAndRotation(ArenaCenter+new Vector3(0,0,Mathf.Lerp(-25,-85,Mathf.SmoothStep(0,1,flight))),Quaternion.identity);
            heroVisual.rotation=Quaternion.AngleAxis(-88*fall,actor.right)*(heroVisual.parent.rotation*heroVisualRest);
            float h=EnemyDeckGuard.RigHeight(heroPose.Animator);
            if(fall>.05f)
            {
                var chest=heroPose.Animator.GetBoneTransform(HumanBodyBones.Chest);
                for(int side=0;side<2;side++)
                {var upper=heroPose.Animator.GetBoneTransform(side==0?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);var lower=heroPose.Animator.GetBoneTransform(side==0?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var hand=heroPose.Animator.GetBoneTransform(side==0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);float sign=side==0?-1:1;QuestRowboat.SolveArm(upper,lower,hand,chest.position+Vector3.up*h*.1f+actor.forward*h*.17f+actor.right*(sign*h*.16f),upper.position+actor.right*(sign*h*.3f));}
            }
            float lowest=float.PositiveInfinity;
            foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.Head,HumanBodyBones.Chest,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg}){var joint=heroPose.Animator.GetBoneTransform(bone);if(joint!=null)lowest=Mathf.Min(lowest,joint.position.y);}
            float lift=IntroElapsed<2?Mathf.Sin(flight*Mathf.PI)*18:0;actor.position+=Vector3.up*(ArenaCenter.y+.15f+lift+h*Mathf.Lerp(.025f,.065f,fall)-lowest);session.player.GetComponent<Rigidbody>().position=actor.position;
            var heroCenter=(heroPose.Animator.GetBoneTransform(HumanBodyBones.Head).position+heroPose.Animator.GetBoneTransform(HumanBodyBones.Hips).position)*.5f;
            var focus=(heroCenter+bossPose.Animator.GetBoneTransform(HumanBodyBones.Chest).position)*.5f;
            var eye=focus+Vector3.right*Mathf.Lerp(62,75,flight)+Vector3.back*Mathf.Lerp(70,88,flight)+Vector3.up*34;
            session.storyCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye));session.storyCamera.fieldOfView=54;
        }
        void CancelIntroduction()
        {
            if(!IntroActive)return;IntroActive=false;ResetIntroBones();if(heroVisual!=null)heroVisual.localRotation=heroVisualRest;if(heroPose!=null)heroPose.Animator.enabled=true;
            if(session!=null&&session.storyCamera!=null){session.storyCamera.fieldOfView=introFov;session.storyCamera.GetComponent<Camera_Script>().enabled=introOrbitEnabled;}
        }
        void BuildArena()
        {
            if(arena!=null){arena.SetActive(true);return;}
            arena=new GameObject("Boong_soai_ham_dau_tuong_3D");arena.transform.SetParent(transform,true);arena.transform.position=ArenaCenter;
            var source=EnemyFleetDirector.Instance.enemyShips[0].transform.Find("RedSailJunkVisual");
            timber=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.25f,.13f,.065f)};iron=new Material(timber){color=new Color(.1f,.08f,.055f)};
            if(source!=null)
            {
                var shell=Instantiate(source,arena.transform,false);shell.name="Vo_soai_ham";shell.gameObject.SetActive(true);shell.localPosition=new Vector3(0,-25,0);shell.localRotation=Quaternion.identity;
                var renderers=shell.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
                shell.localScale=Vector3.Scale(shell.localScale,new Vector3(220/Mathf.Max(1,b.size.x),1,330/Mathf.Max(1,b.size.z)));
                foreach(var c in shell.GetComponentsInChildren<Collider>()){c.enabled=false;Destroy(c);}
                foreach(var f in shell.GetComponentsInChildren<MeshFilter>())if(f.name.Contains("Sail")||f.name.Contains("Mast")||f.name.Contains("Rope"))f.gameObject.SetActive(false);
                // The isolated duel has a flat walkable deck. The source ship's
                // raised stern cabins otherwise cut through the arena/cinematic camera.
                foreach(var part in shell.GetComponentsInChildren<Transform>())if(part.name.StartsWith("Raised_Command_Deck_"))part.gameObject.SetActive(false);
            }
            void Plank(string name,Vector3 at,Vector3 size,Material material){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(arena.transform,false);g.transform.localPosition=at;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;}
            Material deckMaterial=timber;
            if(source!=null){var deck=source.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name.Contains("Deck")||r.sharedMaterial!=null&&r.sharedMaterial.name.ToLowerInvariant().Contains("fedelzet"));if(deck!=null)deckMaterial=deck.sharedMaterial;}
            for(int i=0;i<22;i++)Plank("Van_boong_"+i,new Vector3(-105+i*10,-1,0),new Vector3(9.8f,2,300),deckMaterial);
            var floor=new GameObject("Solid_Duel_Deck",typeof(BoxCollider));floor.transform.SetParent(arena.transform,false);floor.transform.localPosition=Vector3.down*2;floor.GetComponent<BoxCollider>().size=new Vector3(220,4,300);
            foreach(int side in new[]{-1,1})
            {
                Plank("Lan_can_man_tau",new Vector3(side*110,6,0),new Vector3(3,12,306),iron);
                Plank("Lan_can_dau_tau",new Vector3(0,6,side*152),new Vector3(220,12,3),iron);
                for(int i=-3;i<=3;i++)Plank("Cot_lan_can",new Vector3(side*110,10,i*45),new Vector3(4,20,4),timber);
            }
            foreach(int side in new[]{-1,1})
            {
                for(int i=-4;i<=4;i++)Plank("Tru_man_thuyen",new Vector3(i*24,9,side*150),new Vector3(3,18,3),timber);
                Plank("Thanh_lan_can_dau_tau",new Vector3(0,18,side*150),new Vector3(220,2.5f,3),timber);
                for(int i=-1;i<=1;i++)
                {
                    Plank("Cot_hieu_dai_ham",new Vector3(side*106,24,i*100),new Vector3(2.5f,48,2.5f),timber);
                    Plank("Co_chien_soai_ham",new Vector3(side*96,36,i*100),new Vector3(18,16,.25f),iron);
                }
            }
            var sea=GameObject.CreatePrimitive(PrimitiveType.Plane);sea.name="Mat_nuoc_duoi_soai_ham";sea.transform.SetParent(arena.transform,false);sea.transform.localPosition=Vector3.down*30;sea.transform.localScale=Vector3.one*180;arenaWater=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.1f,.32f,.34f)};arenaWater.SetFloat("_Smoothness",.85f);sea.GetComponent<Renderer>().sharedMaterial=arenaWater;sea.GetComponent<Collider>().enabled=false;
            var enemy=new GameObject("Tuong_dich_"+BossName);enemy.transform.SetParent(arena.transform,false);boss=enemy.transform;
            var model=Instantiate(RiverBoardingCombat.Instance.enemyPrefab,boss,false);var rig=model.GetComponentInChildren<Animator>();rig.runtimeAnimatorController=RiverBoardingCombat.Instance.FootController;rig.applyRootMotion=false;rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;rig.Update(0);
            bossPose=enemy.AddComponent<RiverFighterPose>();bossPose.Initialize(rig,RiverBoardingCombat.Instance.swordPrefab,RiverBoardingCombat.Instance.swordController,19);bossPose.MatchFriendlySize();bossPose.SetSword(true);
            foreach(var c in model.GetComponentsInChildren<Collider>()){c.enabled=false;Destroy(c);}
            ActorSolidBody.Ensure(enemy,rig);enemy.AddComponent<BossDamageReceiver>().owner=this;
            crimson=new Material(timber){color=new Color(.45f,.035f,.02f)};
            var cape=GameObject.CreatePrimitive(PrimitiveType.Cube);cape.name="Dai_chien_bao_do";cape.transform.SetParent(rig.GetBoneTransform(HumanBodyBones.Chest),false);var capeScale=cape.transform.parent.lossyScale;cape.transform.localScale=new Vector3(10/Mathf.Abs(capeScale.x),13/Mathf.Abs(capeScale.y),.4f/Mathf.Abs(capeScale.z));cape.transform.SetPositionAndRotation(rig.GetBoneTransform(HumanBodyBones.Chest).position-boss.forward*2-Vector3.up*3,boss.rotation);cape.GetComponent<Collider>().enabled=false;cape.GetComponent<Renderer>().sharedMaterial=crimson;
            BuildHUD();
            if(GetComponent<BossDeckSupport>()==null)gameObject.AddComponent<BossDeckSupport>().owner=this;
        }
        void BuildHUD()
        {
            ui=new GameObject("Boss_health_and_controls",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));ui.transform.SetParent(transform,false);ui.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;ui.GetComponent<Canvas>().sortingOrder=110;
            var scaler=ui.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
            var panel=new GameObject("Boss_bar",typeof(RectTransform),typeof(Image));panel.transform.SetParent(ui.transform,false);var r=panel.GetComponent<RectTransform>();r.anchorMin=new Vector2(.25f,.84f);r.anchorMax=new Vector2(.7f,.95f);r.offsetMin=r.offsetMax=Vector2.zero;panel.GetComponent<Image>().color=new Color(.025f,.02f,.015f,.88f);
            TMP_Text Text(string name,Vector2 min,Vector2 max,float size){var g=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));g.transform.SetParent(panel.transform,false);var t=g.GetComponent<TextMeshProUGUI>();StoryFont.Apply(t);t.fontSize=size;t.alignment=TextAlignmentOptions.Center;t.color=new Color(1,.84f,.5f);t.raycastTarget=false;t.rectTransform.anchorMin=min;t.rectTransform.anchorMax=max;t.rectTransform.offsetMin=t.rectTransform.offsetMax=Vector2.zero;return t;}
            nameText=Text("Boss_name",new Vector2(0,.4f),new Vector2(1,1),20);
            var fill=new GameObject("Boss_health",typeof(RectTransform),typeof(Image));fill.transform.SetParent(panel.transform,false);var track=fill.GetComponent<RectTransform>();track.anchorMin=new Vector2(.04f,.14f);track.anchorMax=new Vector2(.96f,.32f);track.offsetMin=track.offsetMax=Vector2.zero;
            var g=new GameObject("Health_fill",typeof(RectTransform),typeof(Image));g.transform.SetParent(track,false);healthFill=g.GetComponent<Image>();healthFill.color=new Color(.7f,.12f,.08f);healthFill.rectTransform.anchorMin=Vector2.zero;healthFill.rectTransform.anchorMax=Vector2.one;healthFill.rectTransform.offsetMin=healthFill.rectTransform.offsetMax=Vector2.zero;
            actionText=Text("Boss_action",new Vector2(-.05f,-.7f),new Vector2(1.05f,-.05f),16);
        }
        void OpenArena(bool fresh)
        {
            previousUnderwater=Smartomano.OptiWater.Underwater.OptiWaterUnderwaterFeature.GlobalEnabled;Smartomano.OptiWater.Underwater.OptiWaterUnderwaterFeature.GlobalEnabled=false;
            BuildArena();arena.SetActive(true);var actor=session.player;var body=actor.GetComponent<Rigidbody>();
            oldMotor=actor.GetComponent<Character_Movement>().enabled;oldCollider=actor.GetComponent<CapsuleCollider>().enabled;oldKinematic=body.isKinematic;
            RiverBoardingCombat.Instance.PrepareRowboatBoarding();QuestRowboat.Instance.LeaveForCheckpoint();oldMotor=true;oldCollider=true;oldKinematic=false;
            actor.GetComponent<Character_Movement>().enabled=false;body.isKinematic=true;body.interpolation=RigidbodyInterpolation.None;actor.GetComponent<CapsuleCollider>().enabled=false;
            heroPose=actor.GetComponent<RiverFighterPose>();heroPose.FinishTraversal();heroPose.SetSword(true);heroPose.Animator.enabled=true;
            var camera=session.storyCamera.GetComponent<Camera_Script>();camera.distance=65;camera.heightOffset=27;camera.rightOffset=6;camera.SetElevation(12);
            bossPose.Animator.enabled=true;bossPose.SetSword(true);boss.rotation=Quaternion.Euler(0,180,0);
            InArena=true;attackPhase=0;attackAt=Time.time+1.5f;recoverAt=0;dodgeUntil=Time.time+.6f;
            if(fresh){actor.transform.SetPositionAndRotation(ArenaCenter+new Vector3(0,0,-85),Quaternion.identity);boss.SetPositionAndRotation(ArenaCenter+new Vector3(0,0,70),Quaternion.Euler(0,180,0));}
            FitFeet(actor.transform,heroPose.Animator);FitFeet(boss,bossPose.Animator);camera.SnapToTarget();
        }
        static void FitFeet(Transform root,Animator rig){ActorSolidBody.WalkingDimensions(rig,out _,out float sole,out _);root.position+=Vector3.up*(Instance.ArenaCenter.y+.15f-rig.transform.position.y-sole);}
        public void SupportAfterAnimation()
        {
            if(!InArena)return;if(IntroActive){PoseIntroduction();return;}
            FitFeet(session.player.transform,heroPose.Animator);var body=session.player.GetComponent<Rigidbody>();body.position=session.player.transform.position;
            if(!Defeated)FitFeet(boss,bossPose.Animator);
        }
        public Vector3 ClampDeck(Vector3 p){p.x=Mathf.Clamp(p.x,ArenaCenter.x-96,ArenaCenter.x+96);p.z=Mathf.Clamp(p.z,ArenaCenter.z-136,ArenaCenter.z+136);return p;}
        public bool TryDodge(Vector3 direction)
        {
            direction.y=0;if(!IsFighting||BeachBoatQuestSession.InputBlocked||direction.sqrMagnitude<.1f||vitals.Stamina<18||!vitals.TryExert(18,1))return false;
            dodgeUntil=Time.time+.35f;dodgeDirection=direction.normalized;return true;
        }
        public void StepPlayer(float dt)
        {
            var actor=session.player.transform;var camera=session.storyCamera.transform;var forward=camera.forward;forward.y=0;forward.Normalize();var right=Vector3.Cross(Vector3.up,forward);
            var input=Vector3.ClampMagnitude(forward*Input.GetAxisRaw("Vertical")+right*Input.GetAxisRaw("Horizontal"),1);
            if(Input.GetKeyDown(KeyCode.LeftShift))TryDodge(input);
            bool dodging=Time.time<dodgeUntil&&dodgeDirection.sqrMagnitude>.1f;
            var move=(dodging?dodgeDirection*100:input*28)*dt;var next=ClampDeck(actor.position+move);var separation=next-boss.position;separation.y=0;if(separation.magnitude<14)next=boss.position+separation.normalized*14+Vector3.up*(actor.position.y-boss.position.y);
            actor.position=next;if(move.sqrMagnitude>.01f&&!RiverPlayerCombat.Instance.IsAiming)actor.rotation=Quaternion.Slerp(actor.rotation,Quaternion.LookRotation(move),1-Mathf.Exp(-12*dt));
            bool aiming=RiverPlayerCombat.Instance.IsAiming;heroPose.SetSword(!aiming&&PlayerShield.Instance?.Equipped!=true);heroPose.Walk(move.sqrMagnitude>.01f);
            if(Input.GetMouseButtonDown(0)&&!aiming&&heroPose.SwordVisible)TryPlayerStrike();
        }
        public bool TryPlayerStrike()
        {
            if(!IsFighting||BeachBoatQuestSession.InputBlocked||Time.time<playerSwing||vitals.Stamina<9||!vitals.TryExert(9,1))return false;
            playerSwing=Time.time+.7f;heroPose.SetSword(true);heroPose.Slash();BeachBoatSoundscape.Instance?.PlaySwordSwing();
            var delta=boss.position-session.player.transform.position;delta.y=0;
            if(delta.magnitude<34&&Vector3.Dot(session.player.transform.forward,delta.normalized)>.2f)TakeHit(12,CombatImpactKind.Sword);return true;
        }
        public bool TakeHit(int damage,CombatImpactKind kind)
        {
            if(!IsFighting||damage<=0)return false;int actual=Mathf.Min(Health,damage);Health-=actual;CombatHitFeedback.Show(boss.position+Vector3.up*12,kind);CombatDamageNumbers.Show(boss.position+Vector3.up*18,actual);
            if(Health==0){Defeated=true;bossPose.SetSword(false);bossPose.Animator.enabled=false;boss.rotation*=Quaternion.Euler(0,0,75);story.TransitionTo(BachDangStoryManager.StoryState.Act3_FinalReport,true);StoryDialogueUI.Instance.Show("Quân Đại Việt","Tướng giặc đã bị hạ! Thu quân, đưa tù binh về doanh trại!",ReturnFromDuel);}return true;
        }
        public void StepBoss(float dt)
        {
            if(!IsFighting)return;var delta=session.player.transform.position-boss.position;delta.y=0;float distance=delta.magnitude;
            if(attackPhase==0)
            {
                actionText.text="WASD di chuyển • SHIFT né • Chuột phải ngắm cung • H hồi máu";
                if(Time.time<recoverAt){bossPose.Walk(false);return;}
                if(distance>25){boss.position=ClampDeck(boss.position+delta.normalized*Mathf.Min(distance-25,24*dt));bossPose.Walk(true);}else bossPose.Walk(false);
                if(delta.sqrMagnitude>.01f)boss.rotation=Quaternion.Slerp(boss.rotation,Quaternion.LookRotation(delta),1-Mathf.Exp(-5*dt));
                if(distance<65&&Time.time>=attackAt){attackPhase=1;attackClock=0;attackDirection=delta.normalized;attackNumber++;bossPose.Walk(false);}
            }
            else
            {
                attackClock+=dt;bool heavy=attackNumber%3==0;float windup=heavy?1.1f:.75f;actionText.text=attackClock<windup?(heavy?"TƯỚNG ĐỊCH LẤY ĐÀ • NÉ ĐÒN NẶNG!":"ĐỊCH CHUẨN BỊ CHÉM • ĐỔI HƯỚNG / NÉ!"):"ĐỊCH HỞ SƯỜN • PHẢN CÔNG!";
                if(attackPhase==1&&attackClock>=windup){attackPhase=2;bossPose.Slash();BeachBoatSoundscape.Instance?.PlaySwordSwing();}
                if(attackPhase==2&&attackClock<windup+.25f)boss.position=ClampDeck(boss.position+attackDirection*(heavy?65:35)*dt);
                if(attackPhase==2&&attackClock>=windup+.25f)
                {
                    attackPhase=3;var gap=session.player.transform.position-boss.position;gap.y=0;
                    if(gap.magnitude<(heavy?37:29)&&Vector3.Dot(attackDirection,gap.normalized)>.05f&&Time.time>=dodgeUntil)
                    {bool block=PlayerShield.Instance?.IsGuarding==true&&Vector3.Dot(session.player.transform.forward,-gap.normalized)>.4f&&vitals.Stamina>=14&&vitals.TryExert(14,1);vitals.TakeImpact(block?4:heavy?26:17,block?"Khiên đỡ kiếm":"Trúng kiếm tướng địch",CombatImpactKind.Sword);}
                }
                if(attackClock>windup+.65f){attackPhase=0;recoverAt=Time.time+(heavy?1.5f:.9f);attackAt=recoverAt+.7f;}
            }
        }
        void RestoreMotor()
        {
            Smartomano.OptiWater.Underwater.OptiWaterUnderwaterFeature.GlobalEnabled=previousUnderwater;
            var actor=session.player;actor.GetComponent<Character_Movement>().enabled=oldMotor;actor.GetComponent<CapsuleCollider>().enabled=oldCollider;var body=actor.GetComponent<Rigidbody>();body.isKinematic=oldKinematic;body.interpolation=RigidbodyInterpolation.Interpolate;
            heroPose?.Restore();if(heroPose!=null){heroPose.Animator.runtimeAnimatorController=RiverBoardingCombat.Instance.FootController;heroPose.Animator.enabled=true;}
        }
        public void ReturnFromDuel()
        {
            CancelIntroduction();
            InArena=false;RestoreMotor();arena.SetActive(false);ui.SetActive(false);var actor=session.player;actor.transform.SetPositionAndRotation(returnPosition,returnRotation);
            if(returnAboard)QuestRowboat.Instance.RestoreSave(true,returnBoatPosition,returnBoatRotation);session.storyCamera.GetComponent<Camera_Script>().SnapToTarget();
            story.UpdateUI();
        }
        void OnDestroy(){CancelIntroduction();if(InArena)Smartomano.OptiWater.Underwater.OptiWaterUnderwaterFeature.GlobalEnabled=previousUnderwater;if(Instance==this)Instance=null;if(timber!=null)Destroy(timber);if(iron!=null)Destroy(iron);if(arenaWater!=null)Destroy(arenaWater);if(crimson!=null)Destroy(crimson);}
    }
    [DefaultExecutionOrder(1100)]
    public sealed class BossDeckSupport:MonoBehaviour
    {public RiverBossEncounter owner;void LateUpdate()=>owner?.SupportAfterAnimation();}
    public sealed class BossDamageReceiver:MonoBehaviour {public RiverBossEncounter owner;}
}
