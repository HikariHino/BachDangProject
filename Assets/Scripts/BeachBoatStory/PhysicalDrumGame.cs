using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BeachBoatStory
{
    /// <summary>Listen, aim real drumsticks, then repeat three signals with visible squad responses.</summary>
    [DefaultExecutionOrder(950)]
    public sealed class PhysicalDrumGame:MonoBehaviour
    {
        public enum LessonPhase { Demonstration, Repeat, Response }
        public const int BeatsPerCommand=4;
        static readonly string[] Commands={"TẬP HỢP","TIẾN QUÂN","GIỮ TRẬN"};
        static readonly float[][] Rhythm={new[]{0f,.7f,1.4f,2.1f},new[]{0f,.42f,.84f,1.26f},new[]{0f,.75f,1f,1.75f}};
        public static PhysicalDrumGame Active {get;private set;}
        public bool Running {get;private set;}public LessonPhase Phase {get;private set;}
        public int CommandIndex {get;private set;}public int BeatIndex {get;private set;}public int ImpactCount {get;private set;}public int CorrectHits {get;private set;}
        public int SquadCount=>squad?.Count??0;public int SquadGuarding=>squad?.GuardingCount??0;public int SquadCommands=>squad?.CommandsReceived??0;public float SquadMovement=>squad?.DistanceMoved??0;
        public float Clock {get;private set;}public float NextHitTime=>anchor<0?Clock:anchor+Rhythm[CommandIndex][BeatIndex]-anchorOffset;
        public bool HandsOnly=>hands?.BodyHidden??false;public float HandStretch=>hands?.MaxSegmentError??0;public float HandGripError=>hands?.MaxContactError??0;
        public Vector3 StrikeCenter=>ground.StrikeCenter;public Vector3 HeadNormal=>ground.SurfaceNormal;
        public Ray AimAt(Vector3 point)=>new Ray(ground.Eye,(point-ground.Eye).normalized);
        sealed class Stick {public Transform root;public bool held,swing,done,demo,valid;public float hold,age,strength;public Vector3 hit,tip,shaft;}
        readonly Stick[] sticks={new Stick(),new Stick()};
        BeachBoatMinigameStation owner;DrumTrainingGround ground;DrumDrillSquad squad;WorkbenchHands hands;Transform player;Animator rig;Rigidbody body;Character_Movement motor;CapsuleCollider capsule;Camera_Script orbit;Camera view;
        bool oldMotor,oldKinematic,oldCollider,oldRig;RuntimeAnimatorController oldController;AnimatorCullingMode oldCulling;Vector3 oldPosition;Quaternion oldRotation;float oldFov,oldElevation;CursorLockMode oldLock;bool oldCursor;
        GameObject props,hud,cross;GameObject[] signs;Material wood,wrap,mark;TMP_Text title,status,hint;Image[] ticks;MaterialPropertyBlock headProperties;
        float phaseTime,anchor=-1,anchorOffset,flash;int demoBeat;string feedback="";float feedbackUntil;Ray aim;
        public void Begin(BeachBoatMinigameStation station)
        {
            End();owner=station;ground=station.GetComponentInParent<DrumTrainingGround>();if(ground==null)throw new InvalidOperationException("Drum training ground is missing");
            var session=BeachBoatQuestSession.Instance;player=session.player.transform;rig=player.GetComponentInChildren<Animator>();body=player.GetComponent<Rigidbody>();motor=player.GetComponent<Character_Movement>();capsule=player.GetComponent<CapsuleCollider>();orbit=session.storyCamera.GetComponent<Camera_Script>();view=session.storyCamera;
            RiverPlayerCombat.Instance?.SetAim(false);oldMotor=motor.enabled;oldKinematic=body.isKinematic;oldCollider=capsule.enabled;oldRig=rig.enabled;oldController=rig.runtimeAnimatorController;oldCulling=rig.cullingMode;oldPosition=player.position;oldRotation=player.rotation;oldFov=view.fieldOfView;oldElevation=orbit.Elevation;oldLock=Cursor.lockState;oldCursor=Cursor.visible;
            motor.enabled=false;body.isKinematic=true;capsule.enabled=false;rig.enabled=true;rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;rig.runtimeAnimatorController=RiverBoardingCombat.Instance.FootController;rig.Rebind();rig.Update(0);
            player.SetPositionAndRotation(ground.Approach,Quaternion.LookRotation(ground.transform.forward));body.position=player.position;
            signs=new[]{ground.transform.Find("Training_Sign")?.gameObject,ground.transform.Find("Training_Sign_Board")?.gameObject};foreach(var sign in signs)if(sign!=null)sign.SetActive(false);
            props=new GameObject("Physical_Drumsticks");props.transform.SetParent(ground.transform,false);
            Material Mat(string name,Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};return m;}
            wood=Mat("Drumstick_wood",new Color(.43f,.25f,.10f));wrap=Mat("Drumstick_grip",new Color(.25f,.08f,.035f));mark=Mat("Drum_aim_mark",new Color(1,.8f,.36f));
            for(int i=0;i<2;i++)
            {
                sticks[i]=new Stick();var root=new GameObject(i==0?"Left_Drumstick":"Right_Drumstick").transform;root.SetParent(props.transform,false);sticks[i].root=root;
                Part("Wooden_shaft",root,PrimitiveType.Cylinder,new Vector3(0,0,3),new Vector3(.48f,5,.48f),wood,Quaternion.Euler(90,0,0));Part("Rounded_tip",root,PrimitiveType.Sphere,new Vector3(0,0,8),Vector3.one*1.1f,wood);Part("Grip_wrap",root,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.62f,1.5f,.62f),wrap,Quaternion.Euler(90,0,0));
            }
            cross=new GameObject("Physical_Drum_Aim_Cross");cross.transform.SetParent(props.transform,false);Part("Horizontal",cross.transform,PrimitiveType.Cube,Vector3.zero,new Vector3(.8f,.09f,.07f),mark);Part("Vertical",cross.transform,PrimitiveType.Cube,Vector3.zero,new Vector3(.09f,.8f,.07f),mark);
            hands=new WorkbenchHands(player,rig);hands.Show(true);squad=new DrumDrillSquad(ground);headProperties=new MaterialPropertyBlock();
            Running=true;Active=this;CommandIndex=BeatIndex=CorrectHits=ImpactCount=0;Clock=flash=0;feedbackUntil=0;aim=AimAt(ground.StrikeCenter);StartDemo();BuildHud();hud.SetActive(true);
            orbit.FocusHandwork(ground.Eye,ground.StrikeCenter,ground.transform);Cursor.lockState=CursorLockMode.Confined;Cursor.visible=true;
        }
        GameObject Part(string name,Transform parent,PrimitiveType shape,Vector3 p,Vector3 size,Material material,Quaternion rotation=default)
        {var g=GameObject.CreatePrimitive(shape);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;g.transform.localRotation=rotation==default?Quaternion.identity:rotation;var c=g.GetComponent<Collider>();c.enabled=false;Destroy(c);var r=g.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return g;}
        void StartDemo(){Phase=LessonPhase.Demonstration;phaseTime=-.45f;demoBeat=0;BeatIndex=0;anchor=-1;owner.ReplayPhysicalCommand();foreach(var s in sticks){s.held=s.swing=false;s.age=0;}}
        public void ReplayExample(){if(Running&&Phase!=LessonPhase.Response)StartDemo();}
        public bool Raise(bool left,Ray ray)
        {if(!Running||Phase!=LessonPhase.Repeat||owner.Failed||BachDangFrontEnd.IsBlocking)return false;var s=sticks[left?0:1];if(s.swing||s.held)return false;s.held=true;s.hold=0;aim=ray;return true;}
        public bool Release(bool left,Ray ray)
        {if(!Running||Phase!=LessonPhase.Repeat)return false;var s=sticks[left?0:1];if(!s.held)return false;s.held=false;BeginSwing(left?0:1,ray,false);return true;}
        void BeginSwing(int hand,Ray ray,bool demo)
        {
            var s=sticks[hand];s.swing=true;s.done=false;s.demo=demo;s.age=0;s.strength=Mathf.Clamp01(.35f+s.hold*1.8f);s.hold=0;
            var plane=new Plane(ground.SurfaceNormal,ground.StrikeCenter);s.hit=plane.Raycast(ray,out float distance)?ray.GetPoint(distance):ground.StrikeCenter+ground.transform.right*20;
            s.valid=ground.StrikeSurface.Raycast(new Ray(s.hit+ground.SurfaceNormal*2,-ground.SurfaceNormal),out var surface,4)&&Vector3.Distance(surface.point,s.hit)<.8f;
        }
        public void ReadInput()
        {
            if(!Running||owner.Failed||BachDangFrontEnd.IsBlocking||StoryDialogueUI.InputConsumedThisFrame)return;
            aim=view.ScreenPointToRay(Input.mousePosition);if(Input.GetKeyDown(KeyCode.H))ReplayExample();
            for(int i=0;i<2;i++){if(Input.GetMouseButtonDown(i))Raise(i==0,aim);if(Input.GetMouseButtonUp(i))Release(i==0,aim);}
        }
        public void Tick(float seconds)
        {
            if(!Running)return;Clock+=seconds;phaseTime+=seconds;flash=Mathf.Max(0,flash-seconds);squad?.Tick(seconds);
            if(Phase==LessonPhase.Demonstration)
            {
                if(demoBeat<4&&phaseTime>=Rhythm[CommandIndex][demoBeat]){int hand=demoBeat%2;sticks[hand].hold=.2f;BeginSwing(hand,AimAt(ground.StrikeCenter+ground.transform.right*(hand==0?-2:2)),true);demoBeat++;}
                if(demoBeat==4&&phaseTime>=Rhythm[CommandIndex][3]+.75f){Phase=LessonPhase.Repeat;phaseTime=0;anchor=-1;BeatIndex=0;feedback="Đánh lại hiệu lệnh vừa nghe";feedbackUntil=Clock+1;}
            }
            else if(Phase==LessonPhase.Response&&phaseTime>=1.1f)
            {if(CommandIndex==2){owner.CompletePhysicalDrum();return;}CommandIndex++;StartDemo();}
            else if(Phase==LessonPhase.Repeat&&anchor>=0&&BeatIndex<4&&Clock>NextHitTime+.28f)
            {owner.RecordPhysicalBeat(false);if(!Running)return;anchor=-1;feedback="Chậm nhịp • đánh tiếp từ nhịp đang sáng";feedbackUntil=Clock+1.2f;}
            foreach(var s in sticks)
            {
                if(s.held)s.hold+=seconds;if(!s.swing)continue;s.age+=seconds;
                if(s.age>=.13f&&!s.done){s.done=true;Impact(s);if(!Running)return;}
                if(s.age>=.38f)s.swing=false;
            }
            RefreshHud();
        }
        void Impact(Stick s)
        {
            if(!s.valid){if(!s.demo){owner.RecordPhysicalBeat(false);feedback="Ngắm vào mặt trống rồi thả dùi";feedbackUntil=Clock+1;}return;}
            ImpactCount++;flash=.18f;BeachBoatSoundscape.Instance?.PlayDrum(s.strength>.7f?1:2);
            if(s.demo||Phase!=LessonPhase.Repeat)return;
            bool correct=anchor<0||Mathf.Abs(Clock-NextHitTime)<=.28f;owner.RecordPhysicalBeat(correct);if(!Running||owner.Failed)return;
            if(!correct){anchor=-1;feedback="Sớm nhịp • giữ bình tĩnh, đánh tiếp";feedbackUntil=Clock+1;return;}
            if(anchor<0){anchor=Clock;anchorOffset=Rhythm[CommandIndex][BeatIndex];}BeatIndex++;CorrectHits++;feedback="Đúng nhịp";feedbackUntil=Clock+.55f;
            if(BeatIndex>=4){owner.CompletedPhysicalCommand();squad?.Receive(CommandIndex);Phase=LessonPhase.Response;phaseTime=0;anchor=-1;feedback="Quân đã nhận lệnh";feedbackUntil=Clock+1.1f;}
        }
        void LateUpdate()
        {
            if(!Running)return;squad?.FitFeet();hands.Show(true);hands.Grip(WorkbenchHands.GripPose.Rod,WorkbenchHands.GripPose.Rod);orbit.FocusHandwork(ground.Eye,ground.StrikeCenter,ground.transform);
            for(int i=0;i<2;i++)
            {
                var s=sticks[i];float side=i==0?-1:1;var rest=ground.transform.TransformPoint(new Vector3(side*4,17,-14));var raised=s.hit+ground.SurfaceNormal*9+Vector3.up*7;
                if(s.held){var plane=new Plane(ground.SurfaceNormal,ground.StrikeCenter);if(plane.Raycast(aim,out float d))s.hit=aim.GetPoint(d);raised=s.hit+ground.SurfaceNormal*9+Vector3.up*7;}
                float lift=s.held?Mathf.Clamp01(s.hold/.22f):0;var dir=Vector3.Slerp((ground.transform.forward-Vector3.up*.2f).normalized,(ground.transform.forward*.3f+Vector3.up).normalized,lift);var tip=Vector3.Lerp(rest,raised,lift);
                if(s.swing)
                {
                    float down=Mathf.SmoothStep(0,1,Mathf.Clamp01(s.age/.13f));var strike=s.hit+ground.SurfaceNormal*.55f;tip=Vector3.Lerp(raised,strike,down);dir=Vector3.Slerp((ground.transform.forward*.3f+Vector3.up).normalized,(ground.transform.forward-Vector3.up*.15f).normalized,down);
                    if(s.age>.13f){float back=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.13f,.38f,s.age));tip=Vector3.Lerp(strike,rest,back);dir=Vector3.Slerp(dir,(ground.transform.forward-Vector3.up*.2f).normalized,back);}
                }
                s.tip=tip;s.shaft=dir;s.root.SetPositionAndRotation(tip-dir*8,Quaternion.LookRotation(dir));
            }
            Vector3 Forearm(int i){var basis=view.transform.up;return Vector3.ProjectOnPlane(basis,sticks[i].shaft).normalized;}
            hands.Pose(view.transform,sticks[1].root.position,sticks[0].root.position,false,false,sticks[1].shaft,sticks[0].shaft,false,Forearm(1),Forearm(0),1);
            bool onHead=ground.StrikeSurface.Raycast(aim,out var at,120);cross.SetActive(Phase==LessonPhase.Repeat&&onHead);if(onHead)cross.transform.SetPositionAndRotation(at.point+ground.SurfaceNormal*.15f,ground.transform.rotation);
            float glow=flash>0?flash/.18f:Phase==LessonPhase.Repeat&&anchor>=0&&Mathf.Abs(Clock-NextHitTime)<.15f?.28f:0;ground.FrontHead.GetPropertyBlock(headProperties);headProperties.SetColor("_BaseColor",Color.Lerp(ground.FrontHead.sharedMaterial.color,new Color(1,.77f,.32f),glow*.4f));ground.FrontHead.SetPropertyBlock(headProperties);
        }
        void BuildHud()
        {
            if(hud!=null)return;hud=new GameObject("PhysicalDrumHUD",typeof(Canvas),typeof(CanvasScaler));var c=hud.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=95;var scale=hud.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);
            TMP_Text Text(string name,Vector2 a,Vector2 b,int size){var g=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));g.transform.SetParent(hud.transform,false);var t=g.GetComponent<TextMeshProUGUI>();StoryFont.Apply(t);t.fontSize=size;t.color=new Color(1,.86f,.58f);t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.rectTransform.anchorMin=a;t.rectTransform.anchorMax=b;t.rectTransform.offsetMin=t.rectTransform.offsetMax=Vector2.zero;return t;}
            title=Text("Order",new Vector2(.25f,.88f),new Vector2(.75f,.95f),24);status=Text("Status",new Vector2(.22f,.81f),new Vector2(.78f,.88f),15);hint=Text("HandsControl",new Vector2(.2f,.055f),new Vector2(.8f,.15f),17);
            ticks=new Image[4];for(int i=0;i<4;i++){var g=new GameObject("PhysicalBeat_"+i,typeof(RectTransform),typeof(Image));g.transform.SetParent(hud.transform,false);var r=g.GetComponent<RectTransform>();r.anchorMin=new Vector2(.42f+i*.045f,.785f);r.anchorMax=new Vector2(.455f+i*.045f,.795f);r.offsetMin=r.offsetMax=Vector2.zero;ticks[i]=g.GetComponent<Image>();ticks[i].raycastTarget=false;}
        }
        void RefreshHud()
        {
            if(hud==null)return;hud.SetActive(!BachDangFrontEnd.IsBlocking);title.text=$"HIỆU LỆNH {CommandIndex+1}/3 • {Commands[CommandIndex]}";
            status.text=(Phase==LessonPhase.Demonstration?"NGHE NHỊP MẪU • QUAN SÁT DÙI":Phase==LessonPhase.Response?"QUAN SÁT QUÂN NHẬN LỆNH":$"ĐÁNH LẠI • {BeatIndex}/4 NHỊP")+$"  •  {owner.TimeRemaining:0}s  •  Sai {owner.Mistakes}\n{CampaignProgress.Instance.TimingBands(0)}";
            hint.text=(Clock<feedbackUntil?feedback:Phase==LessonPhase.Demonstration?"Nhớ khoảng cách giữa các tiếng trống":"Ngắm mặt trống • Giữ rồi THẢ chuột trái/phải để đánh")+"\nH: nghe mẫu lại • ESC: rời thao trường";
            for(int i=0;i<4;i++)ticks[i].color=i<(Phase==LessonPhase.Demonstration?demoBeat:BeatIndex)?new Color(.3f,.85f,.52f):new Color(.3f,.3f,.27f);
        }
        public void End()
        {
            if(!Running)return;foreach(var sign in signs)if(sign!=null)sign.SetActive(true);Running=false;if(Active==this)Active=null;squad?.Release();squad=null;hands?.Dispose();hands=null;
            if(orbit!=null){orbit.ClearWorkFocus();if(player!=null){player.SetPositionAndRotation(oldPosition,oldRotation);body.position=oldPosition;}orbit.SnapToTarget();orbit.SetElevation(oldElevation);view.fieldOfView=oldFov;}
            if(motor!=null)motor.enabled=oldMotor;if(body!=null)body.isKinematic=oldKinematic;if(capsule!=null)capsule.enabled=oldCollider;if(rig!=null){rig.runtimeAnimatorController=oldController;rig.cullingMode=oldCulling;rig.enabled=oldRig;}
            Cursor.lockState=oldLock;Cursor.visible=oldCursor;if(props!=null){props.SetActive(false);Destroy(props);}if(hud!=null)hud.SetActive(false);if(ground!=null&&ground.FrontHead!=null)ground.FrontHead.SetPropertyBlock(null);
            foreach(var m in new[]{wood,wrap,mark})if(m!=null)Destroy(m);wood=wrap=mark=null;
        }
        void OnDisable()=>End();void OnDestroy(){End();if(hud!=null)Destroy(hud);}
    }
}
