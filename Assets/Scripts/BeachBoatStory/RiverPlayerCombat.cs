using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Player-directed archery, readable return fire and wave suppression during the river battle.</summary>
    [DefaultExecutionOrder(350)]
    public sealed class RiverPlayerCombat : MonoBehaviour
    {
        public static RiverPlayerCombat Instance {get;private set;}
        public EnemyDeckGuard[] guards=new EnemyDeckGuard[0];
        public RuntimeAnimatorController bowController;
        public Material bowMaterial;
        public float range=420,enemyRange=330;
        public int Kills {get;private set;}
        public int AlliedKills {get;private set;}
        public int ShotsFired {get;private set;}
        public int Hits {get;private set;}
        public int HostileShots {get;private set;}
        public int DodgedShots {get;private set;}
        public int Arrows {get;private set;}=6;
        public bool IsAiming {get;private set;}
        public bool IsReloading=>reloadUntil>Time.time;
        public bool PracticeActive=>ArcheryTraining.Active!=null;
        bool CanUseBow=>BattleActive||PracticeActive;
        public bool ThreatPending=>threat!=null;
        public EnemyDeckGuard ThreatGuard=>threat;
        public bool ReturnFireEnabled {get;set;}=true;
        public bool BattleActive=>EnemyFleetDirector.Instance!=null&&EnemyFleetDirector.Instance.BattleStarted&&
            QuestRowboat.Instance!=null&&QuestRowboat.Instance.IsAboard;
        public int RequiredKills=>EnemyFleetDirector.Instance!=null?EnemyFleetDirector.Instance.CommandOpportunityCount:5;
        public int RemainingEnemies {get{int count=0;foreach(var guard in guards)if(guard!=null&&guard.IsAlive)count++;return count;}}
        public EnemyDeckGuard AimedGuard {get;private set;}
        public EnemyDeckGuard PriorityGuard
        {
            get
            {
                EnemyDeckGuard best=null;float distance=float.PositiveInfinity;
                foreach(var guard in guards)if(guard!=null&&guard.IsAlive)
                {float d=(guard.transform.position-session.player.transform.position).sqrMagnitude;if(d<distance){best=guard;distance=d;}}
                return best;
            }
        }
        sealed class Arrow
        {
            public Transform visual;
            public Vector3 start,end;
            public EnemyDeckGuard target;
            public int damage;
            public float elapsed,duration;
            public bool hostile,active,practice;
            public ArcheryPracticeTarget practiceTarget;public int practiceScore,practiceIndex;
        }
        readonly Arrow[] arrows=new Arrow[16];
        readonly RaycastHit[] rayHits=new RaycastHit[64];
        readonly HashSet<int> suppressed=new HashSet<int>();
        public int SuppressedWaveCount=>suppressed.Count;
        BeachBoatQuestSession session;
        Camera camera;
        Animator animator;
        BowGripProfile gripProfile;SkinnedMeshRenderer[] gripMeshes;int leftGripIndex,rightGripIndex;
        public BowGripProfile GripProfile=>gripProfile;
        Transform bow;
        Transform leftFoot,rightFoot;
        LineRenderer bowString,bowStave;Transform nockedArrow;float releasedAt=-10,poseTension;
        GameObject hud;
        CompactRiverCombatHUD compactHUD;
        Image chargeFill,reticle;TMP_Text drawHint;
        float charge,nextShot,reloadUntil,nextThreat,threatUntil,noticeUntil,hitUntil;
        EnemyDeckGuard threat;
        bool wasBattle;
        bool savedAim;
        float savedAimDistance,savedAimFov,savedRightOffset,savedHeight;
        string message;
        void Awake(){Instance=this;session=GetComponent<BeachBoatQuestSession>();camera=session.storyCamera;animator=session.player.GetComponentInChildren<Animator>();leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);}
        void Start(){LoadHandGrips();BuildBow();BuildHUD();BuildArrows();gameObject.AddComponent<EnemyHealthHUD>();}
        void LoadHandGrips()
        {
            var profile=BowGripProfile.Load();if(profile==null)return;
            gripMeshes=System.Array.FindAll(session.player.GetComponentsInChildren<SkinnedMeshRenderer>(),r=>r.sharedMesh==profile.source);
            if(gripMeshes.Length==0)return;gripProfile=profile;
            leftGripIndex=profile.posed.GetBlendShapeIndex("LeftBowGrip");rightGripIndex=profile.posed.GetBlendShapeIndex("RightBowHook");
        }
        void UpdateHandGrips()
        {
            if(gripProfile==null)return;
            bool hammer=StakeConstruction.Active!=null&&StakeConstruction.Active.Step==StakeConstruction.WorkStep.Hammer;
            foreach(var r in gripMeshes)if(r!=null)
            {
                if(IsAiming||hammer){if(r.sharedMesh!=gripProfile.posed)r.sharedMesh=gripProfile.posed;r.SetBlendShapeWeight(leftGripIndex,100);r.SetBlendShapeWeight(rightGripIndex,hammer?100:Mathf.Clamp01(charge/.3f)*100);}
                else if(r.sharedMesh==gripProfile.posed){r.SetBlendShapeWeight(leftGripIndex,0);r.SetBlendShapeWeight(rightGripIndex,0);r.sharedMesh=gripProfile.source;}
            }
        }
        public bool HasSuppressedWave(int wave)=>suppressed.Contains(wave);
        public void FillSave(BachDangSaveData.Fleet data)
        {
            data.kills=Kills;data.alliedKills=AlliedKills;data.arrows=Arrows;data.shots=ShotsFired;data.hits=Hits;
            data.suppressed=new List<int>(suppressed).ToArray();
        }
        public void RestoreSave(BachDangSaveData.Fleet data)
        {
            Kills=data.kills;AlliedKills=data.alliedKills;Arrows=Mathf.Clamp(data.arrows,0,6);ShotsFired=data.shots;Hits=data.hits;
            suppressed.Clear();foreach(int wave in data.suppressed??System.Array.Empty<int>())suppressed.Add(wave);
            wasBattle=data.active;threat=null;reloadUntil=nextShot=0;nextThreat=Time.time+4;
        }
        public void RegisterKill(EnemyDeckGuard guard)
        {
            CampaignProgress.Instance?.RecordKill(guard);
            Kills++;suppressed.Add(guard.waveIndex);
            Tell($"ĐÃ HẠ ĐỊCH • Còn {RemainingEnemies} lính địch");
            BachDangStoryManager.Instance?.UpdateUI();
        }
        public void RegisterAlliedKill(EnemyDeckGuard guard){AlliedKills++;suppressed.Add(guard.waveIndex);Tell($"Đồng minh hạ địch • Còn {RemainingEnemies} lính địch");BachDangStoryManager.Instance?.UpdateUI();}
        public void BeginBattle()
        {
            CampaignProgress.Instance?.BeginBattle();
            ResetForCheckpoint();wasBattle=true;nextThreat=Time.time+4;
            foreach(var guard in guards)if(guard!=null)guard.ResetGuard();
            Tell("Nhử địch vào vòng vây. Giữ ngoài bãi cọc, chờ nước rút và lệnh phản công rồi mới áp sát.");
        }
        public void ResetForCheckpoint()
        {
            RiverBoardingCombat.Instance?.ResetBattle();
            SetAim(false);Kills=AlliedKills=ShotsFired=Hits=HostileShots=DodgedShots=0;Arrows=6;charge=reloadUntil=nextShot=0;
            threat=null;suppressed.Clear();wasBattle=false;
            foreach(var arrow in arrows)if(arrow!=null){arrow.active=false;arrow.visual.gameObject.SetActive(false);}
        }
        void Update()
        {
            bool active=CanUseBow&&!BeachBoatQuestSession.InputBlocked;
            if(!BattleActive&&wasBattle){SetAim(false);wasBattle=false;threat=null;}
            if(hud!=null)hud.SetActive(active);
            if(!active){SetAim(false);return;}
            if(reloadUntil>0&&Time.time>=reloadUntil){reloadUntil=0;Arrows=6;Tell("Đã nạp đủ 6 mũi tên.");}
            var boarding=RiverBoardingCombat.Instance;
            SetAim((boarding==null||!boarding.SwordDrawn&&!boarding.IsTraversing)&&Input.GetMouseButton(1)&&Cursor.lockState==CursorLockMode.Locked);
            if(Input.GetKeyDown(KeyCode.R))Reload();
            AimedGuard=FindTarget(camera.ViewportPointToRay(new Vector3(.5f,.5f)),out _);
            if(IsAiming&&!IsReloading&&Input.GetMouseButton(0))charge=Mathf.Min(1.1f,charge+Time.deltaTime);
            if(IsAiming&&Input.GetMouseButtonUp(0)&&charge>=.15f){TryFire(camera.ViewportPointToRay(new Vector3(.5f,.5f)),charge);charge=0;}
            if(!IsAiming)charge=0;
            StepArrows(Time.deltaTime);if(!PracticeActive)UpdateThreat();UpdateHUD();
        }
        public void SetAim(bool value)
        {
            if(value&&RiverBoardingCombat.Instance!=null&&(RiverBoardingCombat.Instance.SwordDrawn||RiverBoardingCombat.Instance.IsTraversing))value=false;
            if(value==IsAiming)return;
            if(camera==null){IsAiming=false;return;}
            var orbit=camera.GetComponent<Camera_Script>();
            if(value)
            {
                savedAim=orbit.enableShoulderAim;savedAimDistance=orbit.aimDistance;savedAimFov=orbit.aimFieldOfView;savedRightOffset=orbit.rightOffset;savedHeight=orbit.heightOffset;
                orbit.enableShoulderAim=true;orbit.aimDistance=32;orbit.aimFieldOfView=40;orbit.rightOffset=-10;orbit.heightOffset=17;
            }
            else
            {orbit.enableShoulderAim=savedAim;orbit.aimDistance=savedAimDistance;orbit.aimFieldOfView=savedAimFov;orbit.rightOffset=savedRightOffset;orbit.heightOffset=savedHeight;}
            IsAiming=value;if(!value)poseTension=0;
            orbit.SetCombatAim(value);UpdateHandGrips();
            if(animator!=null&&QuestRowboat.Instance!=null&&QuestRowboat.Instance.IsAboard)
            {animator.runtimeAnimatorController=value&&bowController!=null?bowController:
                RiverBoardingCombat.Instance!=null&&(RiverBoardingCombat.Instance.State==RiverBoardingCombat.PlayerState.EnemyDeck||RiverBoardingCombat.Instance.State==RiverBoardingCombat.PlayerState.FriendlyDeck||RiverBoardingCombat.Instance.State==RiverBoardingCombat.PlayerState.Land)?
                (RiverBoardingCombat.Instance.SwordDrawn?RiverBoardingCombat.Instance.swordController:RiverBoardingCombat.Instance.FootController):QuestRowboat.Instance.rowingController;animator.speed=1;}
            if(bow!=null)bow.gameObject.SetActive(value);
        }
        public bool Reload()
        {
            if(!CanUseBow||BeachBoatQuestSession.InputBlocked||IsReloading||Arrows>=6)return false;
            reloadUntil=Time.time+2;charge=0;Tell("Đang lấy tên từ bó tên trên thuyền…");return true;
        }
        public bool TryFire(Ray ray,float draw)
        {
            if(PracticeActive&&!ArcheryTraining.Active.CanFire)return false;
            if(!CanUseBow||BeachBoatQuestSession.InputBlocked||!IsAiming||IsReloading||Time.time<nextShot||!float.IsFinite(draw)||draw<0)return false;
            if(Arrows<=0){Reload();return false;}
            var target=FindTarget(ray,out var end);
            Arrows--;if(!PracticeActive)ShotsFired++;nextShot=Time.time+.6f;releasedAt=Time.time;
            BeachBoatSoundscape.Instance?.PlayBow();
            Launch(bow.position+camera.transform.right*.45f+Vector3.up*.9f+camera.transform.forward*.5f,end,target,draw>=.55f?2:1,false);
            if(PracticeActive)
            {
                var arrow=System.Array.Find(arrows,a=>a.active&&a.elapsed==0&&!a.hostile);
                if(arrow!=null){arrow.target=null;arrow.end=ArcheryTraining.Active.ResolveShot(ray,out var practice,out int score);arrow.practiceTarget=practice;arrow.practiceScore=score;}
                int index=ArcheryTraining.Active.RecordShot();if(arrow!=null)arrow.practiceIndex=index;return true;
            }
            if(target==null)Tell("Tên đã bắn. Đưa tâm ngắm vào người lính trên boong.");
            return true;
        }
        EnemyDeckGuard FindTarget(Ray ray,out Vector3 end)
        {
            end=ray.GetPoint(range);
            int count=Physics.SphereCastNonAlloc(ray,2.8f,rayHits,range,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide);
            float nearest=range;EnemyDeckGuard result=null;
            for(int i=0;i<count;i++)
            {
                var hit=rayHits[i];var guard=hit.collider.GetComponentInParent<EnemyDeckGuard>();
                if(hit.transform.IsChildOf(session.player.transform)||hit.transform.IsChildOf(QuestRowboat.Instance.transform))continue;
                if(guard!=null&&!guard.IsAlive)continue;
                if(hit.collider.isTrigger&&guard==null)continue;
                if(guard==null&&hit.collider.GetComponentInParent<EnemyShip>()!=null)continue;
                if(hit.distance>=nearest)continue;
                nearest=hit.distance;result=guard;end=guard!=null?guard.AimPoint:hit.point;
            }
            return result;
        }
        void Launch(Vector3 from,Vector3 to,EnemyDeckGuard target,int damage,bool hostile)
        {
            Arrow shot=null;foreach(var a in arrows)if(!a.active){shot=a;break;}
            if(shot==null)return;
            shot.practiceTarget=null;shot.practiceScore=0;shot.practice=PracticeActive;
            shot.start=from;shot.end=to;shot.target=target;shot.damage=damage;shot.hostile=hostile;
            shot.elapsed=0;shot.duration=Mathf.Clamp(Vector3.Distance(from,to)/560,.12f,.8f);shot.active=true;shot.visual.gameObject.SetActive(true);
        }
        public void StepArrows(float dt)
        {
            foreach(var a in arrows)
            {
                if(a==null||!a.active)continue;
                a.elapsed+=dt;float t=Mathf.Clamp01(a.elapsed/a.duration);
                // Hits are resolved at arrival, and the visible arrow follows the moving deck.
                var end=a.target!=null&&a.target.IsAlive?a.target.AimPoint:a.end;
                var p=Vector3.Lerp(a.start,end,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*3);
                a.visual.position=p;a.visual.rotation=Quaternion.FromToRotation(Vector3.up,end-p+Vector3.down*.01f);
                if(t<1)continue;
                a.active=false;a.visual.gameObject.SetActive(false);
                if(a.hostile)
                {
                    if(QuestRowboat.FlatDistance(session.player.transform.position,a.end)<22)
                    {session.player.GetComponent<PlayerVitals>().TakeImpact(a.damage,"Trúng tên địch",CombatImpactKind.Arrow);Tell("TRÚNG TÊN! Bỏ ngắm, chèo lệch hướng để né loạt kế tiếp.");}
                    else {DodgedShots++;Tell("ĐÃ NÉ LOẠT TÊN!");}
                }
                else if(a.practice){ArcheryTraining.Active?.RegisterShotResult(a.practiceIndex,a.practiceTarget,a.end,a.practiceScore);if(a.practiceScore>0)hitUntil=Time.time+.3f;}
                else if(a.target!=null&&a.target.IsAlive&&a.target.Hit(a.damage,true,CombatImpactKind.Arrow))
                {Hits++;hitUntil=Time.time+.3f;if(a.target.IsAlive)Tell("TRÚNG ĐỊCH • giữ dây cung lâu hơn để bắn mạnh.");}
            }
        }
        void UpdateThreat()
        {
            if(!ReturnFireEnabled){threat=null;return;}
            if(threat!=null)
            {
                if(!threat.IsAlive){threat=null;nextThreat=Time.time+2;return;}
                if(Time.time<threatUntil)return;
                Launch(threat.AimPoint,session.player.transform.position+Vector3.up*10,null,12,true);
                HostileShots++;
                BeachBoatSoundscape.Instance?.PlayBow(true);
                threat=null;nextThreat=Time.time+3.5f;return;
            }
            if(Time.time<nextThreat)return;
            EnemyDeckGuard nearest=null;float distance=enemyRange;
            foreach(var guard in guards)
                if(guard!=null&&guard.IsAlive&&!guard.IsInMelee)
                {float d=QuestRowboat.FlatDistance(guard.transform.position,session.player.transform.position);if(d<distance){nearest=guard;distance=d;}}
            if(nearest==null)return;
            threat=nearest;threatUntil=Time.time+1.4f;
        }
        void LateUpdate()
        {
            UpdateHandGrips();
            if(!IsAiming||!CanUseBow||bow==null)return;
            var direction=camera.transform.forward;
            var flat=direction;flat.y=0;
            poseTension=Mathf.MoveTowards(poseTension,charge/1.1f,Time.deltaTime*7);
            if(flat.sqrMagnitude>.01f)session.player.transform.rotation=Quaternion.LookRotation(Quaternion.AngleAxis(Mathf.Lerp(20,50,poseTension),Vector3.up)*flat);
            // Only the rowboat stance needs a water-relative height. On shore
            // or another deck, gravity/the boarding surface already supports us.
            var boarding=RiverBoardingCombat.Instance;
            if(!PracticeActive&&QuestRowboat.Instance.IsAboard&&leftFoot!=null&&rightFoot!=null&&(boarding==null||boarding.State==RiverBoardingCombat.PlayerState.Rowboat))
            {
                var position=session.player.transform.position;
                position.y+=TideSystem.CurrentWaterHeight+5-Mathf.Min(leftFoot.position.y,rightFoot.position.y);
                session.player.transform.position=position;session.player.GetComponent<Rigidbody>().position=position;
            }
            var shoulder=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);var head=animator.GetBoneTransform(HumanBodyBones.Head);
            var actor=session.player.transform;var chest=animator.GetBoneTransform(HumanBodyBones.Chest);
            head.rotation=Quaternion.FromToRotation(actor.forward,flat.normalized)*head.rotation;
            float reach=Vector3.Distance(shoulder.position,animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).position)+Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).position,animator.GetBoneTransform(HumanBodyBones.LeftHand).position);
            var supportHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            float palmReach=gripProfile!=null?Vector3.Dot(gripProfile.leftCenter,gripProfile.leftForward)*Mathf.Abs(supportHand.lossyScale.y):0;
            float tension=charge/1.1f;var cheek=head.position+direction*1.8f+camera.transform.right*1.1f+Vector3.up*.8f;
            float faceFront=Vector3.Dot(cheek-chest.position,actor.forward);if(faceFront<2.5f)cheek+=actor.forward*(2.5f-faceFront);
            // Put the arrow rest on the sight line and solve the support arm's
            // available forward reach; the grip includes the palm beyond the wrist.
            var across=Vector3.ProjectOnPlane(cheek-camera.transform.right*.45f-Vector3.up*.9f-shoulder.position,direction);
            float totalReach=reach*.92f+palmReach;
            var grip=shoulder.position+across+direction*Mathf.Sqrt(Mathf.Max(1,totalReach*totalReach-across.sqrMagnitude));
            bow.SetPositionAndRotation(grip,Quaternion.LookRotation(direction));
            var ready=grip+camera.transform.right*.45f+Vector3.up*.9f-direction*2.8f;
            var draw=Vector3.Lerp(ready,cheek,Mathf.SmoothStep(0,1,poseTension));
            if(Time.time-releasedAt<.25f)draw+=actor.right*(Mathf.Sin((Time.time-releasedAt)/.25f*Mathf.PI)*.6f);
            float front=Vector3.Dot(draw-chest.position,actor.forward);if(front<2.5f)draw+=actor.forward*(2.5f-front);
            bowString.SetPosition(1,bow.InverseTransformPoint(draw));
            bowString.SetPosition(0,new Vector3(0,-7,-(2.8f+tension*.7f)));bowString.SetPosition(2,new Vector3(0,7,-(2.8f+tension*.7f)));
            for(int i=0;i<13;i++){float y=(i/12f-.5f)*14;bowStave.SetPosition(i,new Vector3(0,y,-(2.8f+tension*.7f)*(y*y/49)));}
            nockedArrow.gameObject.SetActive(charge>.03f);nockedArrow.SetPositionAndRotation(draw+direction*3,Quaternion.FromToRotation(Vector3.up,direction));
            QuestRowboat.Instance.ApplyBowArmPose(grip,draw,direction);
        }
        void Tell(string text){message=text;noticeUntil=Time.time+3;}
        void UpdateHUD()
        {
            compactHUD.Cluster.gameObject.SetActive(!PracticeActive);
            compactHUD.Refresh(threat!=null?"Địch đang nhắm! Di chuyển để né tên.":Time.time<noticeUntil?message:"",threat!=null);
            reticle.gameObject.SetActive(IsAiming);reticle.color=Time.time<hitUntil?Color.white:AimedGuard!=null?new Color(1,.25f,.2f):new Color(1,.83f,.4f);
            chargeFill.rectTransform.anchorMax=new Vector2(IsAiming?charge/1.1f:0,1);
            chargeFill.transform.parent.gameObject.SetActive(IsAiming);
            drawHint.gameObject.SetActive(IsAiming);drawHint.text=charge>.03f?"THẢ CHUỘT TRÁI ĐỂ BẮN":"GIỮ CHUỘT TRÁI KÉO DÂY";
        }
        void BuildArrows()
        {
            for(int i=0;i<arrows.Length;i++)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="PlayerBattleArrow_"+i;go.transform.SetParent(transform,false);
                go.transform.localScale=new Vector3(.28f,3,.28f);Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=bowMaterial;go.SetActive(false);
                arrows[i]=new Arrow{visual=go.transform};
            }
        }
        void BuildBow()
        {
            bow=new GameObject("Cung_Go_Quan_Si").transform;bow.SetParent(transform,false);
            var stave=bow.gameObject.AddComponent<LineRenderer>();bowStave=stave;stave.useWorldSpace=false;stave.sharedMaterial=bowMaterial;stave.widthMultiplier=.65f;stave.positionCount=13;stave.generateLightingData=true;
            for(int i=0;i<13;i++){float y=(i/12f-.5f)*14;stave.SetPosition(i,new Vector3(0,y,-2.8f*(y*y/49)));}
            var line=new GameObject("Day_Cung");line.transform.SetParent(bow,false);bowString=line.AddComponent<LineRenderer>();bowString.useWorldSpace=false;bowString.sharedMaterial=bowMaterial;bowString.widthMultiplier=.12f;bowString.positionCount=3;bowString.generateLightingData=true;
            bowString.SetPositions(new[]{new Vector3(0,-7,-2.8f),new Vector3(0,0,-2.8f),new Vector3(0,7,-2.8f)});bow.gameObject.SetActive(false);
            var shaft=GameObject.CreatePrimitive(PrimitiveType.Cylinder);shaft.name="Nocked_arrow";shaft.transform.SetParent(bow,false);shaft.transform.localScale=new Vector3(.15f,3,.15f);Destroy(shaft.GetComponent<Collider>());shaft.GetComponent<Renderer>().sharedMaterial=bowMaterial;nockedArrow=shaft.transform;shaft.SetActive(false);
        }
        public void BeginPractice(){SetAim(false);Arrows=6;reloadUntil=nextShot=charge=0;}
        public void EndPractice(){SetAim(false);charge=0;foreach(var a in arrows)if(a!=null&&a.practice){a.active=false;a.visual.gameObject.SetActive(false);}}
        public void RestorePracticeAmmo(int count){Arrows=Mathf.Clamp(count,0,6);}
        void BuildHUD()
        {
            var go=new GameObject("RiverPlayerCombatHUD",typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=110;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            hud=go;
            compactHUD=go.AddComponent<CompactRiverCombatHUD>();compactHUD.Initialize(this);
            reticle=Box("AimReticle",go.transform,new Vector2(.497f,.495f),new Vector2(.503f,.505f),Color.white).GetComponent<Image>();
            var bar=Box("DrawTrack",go.transform,new Vector2(.47f,.47f),new Vector2(.53f,.474f),new Color(.13f,.14f,.14f));
            chargeFill=Box("DrawPower",bar.transform,Vector2.zero,Vector2.one,new Color(1,.8f,.32f)).GetComponent<Image>();drawHint=Text("DrawReleaseHint",go.transform,new Vector2(.36f,.425f),new Vector2(.64f,.46f),12);hud.SetActive(false);
        }
        static GameObject Box(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return go;}
        static TMP_Text Text(string name,Transform parent,Vector2 min,Vector2 max,float size)
        {var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;var t=go.GetComponent<TextMeshProUGUI>();StoryFont.Apply(t);t.fontSize=size;t.color=Color.white;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;}
        void OnDestroy(){SetAim(false);if(Instance==this)Instance=null;}
    }
}
