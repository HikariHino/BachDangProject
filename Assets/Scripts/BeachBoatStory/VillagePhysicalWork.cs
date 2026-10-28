using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Carry goods in third person, then work at the bench using a hands-only view.</summary>
    [DefaultExecutionOrder(800)]
    public sealed class VillagePhysicalWork : MonoBehaviour
    {
        public enum Step { Take, CarryToBench, Saw, Tie, FillRice, LiftRice, TakeIngredient, PlaceIngredient, Wrap, Deliver }
        public Step Current {get;private set;}
        public float ActionProgress {get;private set;}
        public float RiceFill {get;private set;}
        public int Knots {get;private set;}
        public int Ingredients {get;private set;}
        public Vector2 ToolPointer {get;private set;}
        public bool ToolHeld {get;private set;}
        public bool ScoopLoaded {get;private set;}
        public Vector3 RightGripPosition {get;private set;}
        public Vector3 LeftGripPosition {get;private set;}
        public int ToolStages {get;private set;}
        public bool Finishing {get;private set;}
        public float ToolPressure {get;private set;}
        public bool AtTable=>ToolStep&&Near()&&stall.Phase==VillageWorkStall.WorkPhase.Playing;
        public bool HandsOnly=>hands!=null&&hands.BodyHidden;
        public int HandTriangles=>hands?.TriangleCount??0;
        public float HandSegmentError=>hands?.MaxSegmentError??0;
        public float HandWristAngle=>hands?.MaxWristAngle??0;
        public float HandContactError=>hands?.MaxContactError??0;
        public bool SawGuidesVisible=>sawGuides.Count>0&&sawGuides.TrueForAll(g=>g!=null&&g.activeInHierarchy);
        public Vector3 ToolWorldPosition=>ToolPoint(ToolPointer);
        public bool RiceReady=>Current==Step.FillRice&&RiceFill>=.65f&&RiceFill<=.85f;
        public bool RiceExcess=>Current==Step.FillRice&&RiceFill>.85f;
        public bool RiceOverBag=>ToolPointer.x>=.65f&&Mathf.Abs(ToolPointer.y)<=.8f;
        public bool PointerDragging {get;private set;}
        public Vector2 ToolGrabPoint=>Current==Step.Wrap&&!foldGrabbed?ToolGoal:ToolPointer;
        public VillageWorkFoley Foley {get;private set;}
        public string ToolFeedback {get;private set;}="";
        float strokeSeconds,strokeTravel;Vector2 grabOffset;
        bool foldGrabbed;int sawDirection=1;
        public bool ToolStep=>Current==Step.Saw||Current==Step.Tie||Current==Step.FillRice||Current==Step.Wrap;
        static readonly Vector2[] LoopPoints={Vector2.right,Vector2.up,Vector2.left,Vector2.down};
        static readonly Vector2[] Corners={Vector2.left,Vector2.right,Vector2.down,Vector2.up};
        public Vector2 ToolGoal=>Current==Step.Saw?Vector2.right*sawDirection:Current==Step.Tie?LoopPoints[ToolStages%4]:
            Current==Step.FillRice?(ScoopLoaded?Vector2.right:Vector2.left):foldGrabbed?Vector2.zero:Corners[Mathf.Min(3,ToolStages)];
        public bool Carrying=>held!=null&&held.activeSelf;
        public const float Reach=9;
        public static VillagePhysicalWork Active=>VillageWorkStall.Active?.Physical;
        public static bool AllowsCamera=>Active!=null&&Time.timeScale>0&&!BachDangFrontEnd.IsBlocking&&!CampaignJournalUI.IsOpen;
        public static bool AllowsMovement=>AllowsCamera&&VillageWorkStall.Active.Phase==VillageWorkStall.WorkPhase.Playing&&!Active.busy&&!Active.AtTable;
        public Vector3 RequiredApproach=>World(Current==Step.Deliver?new Vector3(25,0,-24):Current==Step.Take||Current==Step.TakeIngredient?new Vector3(-25,0,-24):
            Current==Step.Tie?new Vector3(-10+Knots*10,0,-14):new Vector3(0,0,-14));
        public Vector3 TargetPosition=>World(Current==Step.Deliver?new Vector3(25,7,-14):Current==Step.TakeIngredient?new Vector3(-28+Ingredients*3,9,-18):Current==Step.Take?new Vector3(-25,9,-14):
            Current==Step.Tie?new Vector3(-10+Knots*10,11,-8):Current==Step.FillRice?new Vector3(6,12.6f,-8):new Vector3(0,11,-8));
        public string Hint
        {
            get
            {
                switch(Current)
                {
                    case Step.Take:return "E / ENTER • "+(stall.kind==VillageWorkStall.WorkKind.Timber?"Lấy một khúc gỗ bên trái":stall.kind==VillageWorkStall.WorkKind.Rope?"Lấy cuộn dây bên trái":"Lấy bao rỗng trên thùng BÊN TRÁI");
                    case Step.CarryToBench:return "Mang tới bàn • E / ENTER đặt xuống";
                    case Step.Saw:return $"Bấm giữ cán cưa, kéo LÊN / XUỐNG dọc rãnh • {ToolStages}/8 nhịp";
                    case Step.Tie:return Finishing?$"Giữ để SIẾT, thả khi thanh XANH • Nút {Knots+1}/3":$"Giữ chuột trái, kéo dây QUANH 4 điểm sáng • Nút {Knots+1}/3";
                    case Step.FillRice:return RiceExcess?"DƯ GẠO • R bớt 1 gáo • C đổ lại bao":RiceReady?"ĐỦ GẠO • E / ENTER nhấc bao, mang tới kệ bên phải":ScoopLoaded?"Kéo gáo tới MIỆNG BAO BÊN PHẢI rồi THẢ chuột để đổ":"Bấm giữ GÁO, kéo về RỔ TRÁI để xúc • rồi sang bao và THẢ";
                    case Step.LiftRice:return "E • Nhấc bao gạo đã đủ";
                    case Step.TakeIngredient:return "E • Lấy "+new[]{"nắm gạo đã gói","cá khô","gói muối"}[Mathf.Min(2,Ingredients)];
                    case Step.PlaceIngredient:return "Mang tới khăn trên bàn • E đặt nguyên liệu";
                    case Step.Wrap:return Finishing?"GIỮ YÊN chuột trái để ép kín nếp gấp":$"BẤM GIỮ MÉP KHĂN SÁNG → KÉO VÀO GIỮA → giữ ép • {ToolStages}/4 mép";
                    default:return "Mang tới giá BÊN PHẢI • E / ENTER giao vật tư";
                }
            }
        }
        VillageWorkStall stall;Transform player;Animator rig;Transform[] arms;Quaternion[] armRest;Quaternion spineRest;Transform rightHand,leftHand,chest,spine;
        GameObject props,held,benchLog,bag,grain,blade,sawHandle,parcel,marker,foodTray,toolCursor,spoon,ladleRice;
        LineRenderer rope,pour,fillBand;TextMeshPro label;Material wood,fibre,cloth,rice,fish,salt,gold,green,red;
        readonly List<Material> owned=new List<Material>();
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        readonly List<GameObject> placed=new List<GameObject>();readonly List<GameObject> knots=new List<GameObject>();
        readonly List<Transform> flaps=new List<Transform>();
        readonly List<(Renderer renderer,bool hidden)> fabrics=new List<(Renderer,bool)>();
        readonly List<GameObject> bundleLogs=new List<GameObject>();
        readonly List<GameObject> sawGuides=new List<GameObject>();
        bool busy;float actionUntil,nextSound,pourUntil,verticalScale=1;Vector3 origin;Quaternion rotation;
        Camera_Script camera;Vector3 cameraSettings;float cameraElevation;
        WorkbenchHands hands;BowGripProfile gripProfile;VillageWorkstation station;
        bool tableWasActive;
        AnimatorCullingMode previousCulling;SkinnedMeshRenderer[] playerMeshes;bool[] previousOffscreen;
        Vector3 World(Vector3 local){local.y*=verticalScale;return origin+rotation*local;}
        Material Make(string name,Color color)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};m.SetFloat("_Smoothness",.12f);owned.Add(m);return m;}
        GameObject Shape(string name,PrimitiveType type,Vector3 p,Vector3 size,Material material,bool solid=false,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent??props.transform,false);go.transform.localPosition=p;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid){var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);}return go;
        }
        LineRenderer Line(string name,Material material,float width)
        {var go=new GameObject(name);go.transform.SetParent(props.transform,false);var l=go.AddComponent<LineRenderer>();l.sharedMaterial=material;l.widthMultiplier=width;l.useWorldSpace=true;l.generateLightingData=true;l.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return l;}
        void OpenSack(GameObject sack)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
            Vector3 P(int i,float radius,float y){float a=i/32f*Mathf.PI*2;return new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius);}
            for(int i=0;i<32;i++)
            {
                Quad(P(i,.5f,-1),P(i,.5f,1),P(i+1,.5f,1),P(i+1,.5f,-1));
                Quad(P(i+1,.44f,-1),P(i+1,.44f,1),P(i,.44f,1),P(i,.44f,-1));
                Quad(P(i,.5f,1),P(i,.44f,1),P(i+1,.44f,1),P(i+1,.5f,1));
            }
            var mesh=new Mesh{name="Open cloth sack"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();sack.GetComponent<MeshFilter>().sharedMesh=mesh;ownedMeshes.Add(mesh);
        }
        public void Begin(VillageWorkStall owner)
        {
            Finish();stall=owner;player=BeachBoatQuestSession.Instance.player.transform;rig=player.GetComponentInChildren<Animator>();
            chest=rig.GetBoneTransform(HumanBodyBones.Chest)??rig.GetBoneTransform(HumanBodyBones.Spine);
            spine=rig.GetBoneTransform(HumanBodyBones.Spine);spineRest=spine.localRotation;
            station=owner.EnsureWorkstation();verticalScale=station.HeightScale;
            previousCulling=rig.cullingMode;rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            playerMeshes=player.GetComponentsInChildren<SkinnedMeshRenderer>();previousOffscreen=playerMeshes.Select(r=>r.updateWhenOffscreen).ToArray();
            foreach(var renderer in playerMeshes)renderer.updateWhenOffscreen=true;
            origin=stall.transform.position;rotation=Quaternion.Euler(0,stall.transform.eulerAngles.y,0);
            props=new GameObject("Village_3D_Work_"+stall.kind);props.transform.SetPositionAndRotation(origin,rotation);
            Foley=new VillageWorkFoley(props.transform);
            props.transform.localScale=new Vector3(1,verticalScale,1);
            foreach(var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))if(r.name.StartsWith("Vai_Phoi_")&&QuestRowboat.FlatDistance(r.bounds.center,origin)<120)fabrics.Add((r,r.forceRenderingOff));
            wood=station.Wood;
            fibre=Make("Hemp",new Color(.57f,.40f,.21f));cloth=Make("Undyed linen",new Color(.62f,.54f,.36f));rice=Make("Rice",new Color(.91f,.84f,.61f));
            fish=Make("Dried fish",new Color(.39f,.23f,.1f));salt=Make("Salt",new Color(.88f,.9f,.85f));gold=Make("Work target",new Color(1,.65f,.12f));green=Make("Correct fill",new Color(.16f,.72f,.35f));red=Make("Excess fill",new Color(.8f,.14f,.06f));
            bundleLogs.AddRange(station.BundleLogs);station.ResetDisplay();
            camera=BeachBoatQuestSession.Instance.storyCamera.GetComponent<Camera_Script>();cameraSettings=new Vector3(camera.distance,camera.heightOffset,camera.rightOffset);
            cameraElevation=camera.Elevation;camera.distance=30;camera.heightOffset=13;camera.rightOffset=8;camera.SetElevation(20);
            arms=new[]{rig.GetBoneTransform(HumanBodyBones.RightUpperArm),rig.GetBoneTransform(HumanBodyBones.RightLowerArm),rig.GetBoneTransform(HumanBodyBones.LeftUpperArm),rig.GetBoneTransform(HumanBodyBones.LeftLowerArm)};
            rightHand=rig.GetBoneTransform(HumanBodyBones.RightHand);leftHand=rig.GetBoneTransform(HumanBodyBones.LeftHand);chest=rig.GetBoneTransform(HumanBodyBones.Chest)??rig.GetBoneTransform(HumanBodyBones.Spine);
            armRest=arms.Select(a=>a.localRotation).ToArray();
            hands=new WorkbenchHands(player,rig);gripProfile=BowGripProfile.Load();
            marker=new GameObject("Active_work_target");marker.transform.SetParent(props.transform,false);var ring=marker.AddComponent<LineRenderer>();ring.sharedMaterial=gold;ring.useWorldSpace=false;ring.widthMultiplier=.35f;ring.positionCount=33;
            for(int i=0;i<33;i++){float a=i/32f*Mathf.PI*2;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*1.2f,0,Mathf.Sin(a)*1.2f));}
            label=new GameObject("Context_prompt").AddComponent<TextMeshPro>();label.transform.SetParent(props.transform,false);StoryFont.Apply(label);label.fontSize=6.5f;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(55,10);label.color=new Color(1,.82f,.37f);
            label.transform.localScale=new Vector3(1,1/verticalScale,1);
            rope=Line("Rope_pulled_around_bundle",fibre,.5f);pour=Line("Rice_pour",rice,.3f);pour.enabled=false;
            toolCursor=Shape("Hand_tool_contact",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.8f,gold);
            spoon=new GameObject("Rice_ladle");spoon.transform.SetParent(props.transform,false);
            Shape("Ladle_bowl",PrimitiveType.Cylinder,Vector3.zero,new Vector3(3,.6f,3),wood,false,spoon.transform);
            Shape("Ladle_handle",PrimitiveType.Cube,new Vector3(0,0,-3),new Vector3(.6f,.6f,5),wood,false,spoon.transform);
            ladleRice=Shape("Rice_in_ladle",PrimitiveType.Cylinder,new Vector3(0,.5f,0),new Vector3(2.6f,.3f,2.6f),rice,false,spoon.transform);
            if(stall.kind==VillageWorkStall.WorkKind.Timber)foreach(float x in new[]{-1.6f,1.6f})
            {var guide=Shape("Saw_guide_rail",PrimitiveType.Cube,new Vector3(x,12.5f,-10.5f),new Vector3(.18f,.15f,5),green);guide.SetActive(false);sawGuides.Add(guide);}
            NextOrder();Physics.SyncTransforms();
        }
        void ClearOrder()
        {
            foreach(var go in new[]{held,benchLog,bag,grain,blade,sawHandle,parcel,foodTray})if(go!=null)Destroy(go);
            foreach(var go in placed)if(go!=null)Destroy(go);placed.Clear();foreach(var go in knots)if(go!=null)Destroy(go);knots.Clear();
            held=benchLog=bag=grain=blade=sawHandle=parcel=foodTray=null;flaps.Clear();ActionProgress=RiceFill=0;Knots=Ingredients=0;busy=false;ResetTool();
            if(rope!=null)rope.positionCount=0;if(pour!=null)pour.enabled=false;
        }
        public void NextOrder(){ClearOrder();Current=stall.kind==VillageWorkStall.WorkKind.Provisions?Step.TakeIngredient:Step.Take;}
        void ResetTool(){ToolHeld=ScoopLoaded=foldGrabbed=Finishing=PointerDragging=false;ToolStages=0;sawDirection=1;ToolPressure=strokeSeconds=strokeTravel=0;ToolFeedback="";ToolPointer=stall!=null&&stall.kind==VillageWorkStall.WorkKind.Rope?Vector2.down:Vector2.left;}
        bool RiceAtSource=>ToolPointer.x<=-.65f&&Mathf.Abs(ToolPointer.y)<=.8f;
        void LoadRiceScoop()
        {if(ScoopLoaded||!RiceAtSource)return;ScoopLoaded=true;ToolFeedback="Đã xúc gạo • kéo gáo sang miệng bao rồi THẢ chuột";Foley?.Play(VillageWorkFoley.Cue.Scoop);}
        public bool PressTool()
        {
            if(!CanInteract||!ToolStep)return false;
            ToolHeld=true;busy=true;actionUntil=Time.time+.12f;
            if(Current==Step.FillRice)LoadRiceScoop();
            if(Current==Step.Wrap&&!foldGrabbed&&Vector2.Distance(ToolPointer,ToolGoal)<.3f)foldGrabbed=true;
            if(Current==Step.Wrap&&foldGrabbed&&ToolPointer.magnitude<.2f){Finishing=true;ToolPressure=0;}
            return true;
        }
        public void ReleaseTool()
        {
            if(Current==Step.FillRice&&ToolHeld&&CanInteract&&ScoopLoaded)
            {
                if(RiceOverBag){RiceFill=Mathf.Min(1.2f,RiceFill+.24f);ActionProgress=RiceFill;ScoopLoaded=false;pourUntil=Time.time+.45f;Foley?.Play(VillageWorkFoley.Cue.Pour);ToolFeedback=RiceExcess?"Đã múc dư • R bớt một gáo hoặc C đổ lại":RiceReady?"ĐÃ ĐỦ • E / ENTER nhấc bao":"Đã đổ một gáo • xúc thêm ở rổ bên trái";}
                else ToolFeedback="Gạo vẫn trong gáo • đưa tới miệng bao bên phải rồi thả";
            }
            bool knot=Current==Step.Tie&&Finishing&&ToolHeld;
            ToolHeld=PointerDragging=false;busy=false;
            if(knot)
            {
                Finishing=false;
                if(ToolPressure>=.55f&&ToolPressure<=.85f){Foley?.Play(VillageWorkFoley.Cue.Knot);CompleteTool();}
                else {ToolStages=0;ToolPressure=ActionProgress=0;ToolPointer=Vector2.down;ToolFeedback="Dây tuột • thử nút này lại, thả trong dải xanh";}
            }
            else if(Current==Step.Wrap&&Finishing){Finishing=false;ToolPressure=0;ToolFeedback="Chưa ép kín nếp • giữ yên lâu hơn một chút";}
        }
        public void AdvanceTool(float seconds)
        {
            if(!ToolHeld||!CanInteract||!Finishing||seconds<=0||!float.IsFinite(seconds))return;
            seconds=Mathf.Min(seconds,.1f);
            if(Current==Step.Tie){ToolPressure=Mathf.Min(1,ToolPressure+seconds*.65f);Foley?.Move(VillageWorkFoley.Cue.Rope,.5f);}
            else if(Current==Step.Wrap)
            {
                if(ToolPointer.magnitude>.25f){Finishing=false;ToolPressure=0;return;}
                ToolPressure=Mathf.Min(1,ToolPressure+seconds/.45f);
                Foley?.Move(VillageWorkFoley.Cue.Cloth,.35f);
                if(ToolPressure>=1){Foley?.Play(VillageWorkFoley.Cue.Crease);Finishing=false;ToolStages++;ActionProgress=ToolStages/4f;foldGrabbed=false;ToolPressure=0;ToolFeedback="Nếp đã kín • bấm giữ mép sáng tiếp theo";if(ToolStages==4)CompleteTool();}
            }
        }
        Vector3 ToolPoint(Vector2 point)
        {
            if(Current==Step.Tie)return World(new Vector3(-10+Knots*10+point.x*1.7f+(Finishing?ToolPressure*2:0),12.5f+point.y*1.2f,-11-(Finishing?ToolPressure*2:0)));
            if(Current==Step.FillRice)return World(new Vector3(Mathf.Lerp(-5,4,(point.x+1)*.5f),13.2f,-10.2f+point.y*3));
            if(Current==Step.Wrap)return World(new Vector3(point.x*4.5f,9.7f,-10.2f+point.y*2));
            return World(new Vector3(point.y*4,13,-10.5f+point.x*1.2f));
        }
        public Vector2 ScreenToToolDelta(Vector2 delta)
        {
            if(camera==null||!ToolStep||!float.IsFinite(delta.x)||!float.IsFinite(delta.y))return Vector2.zero;
            var view=camera.GetComponent<Camera>();const float step=.05f;
            Vector2 Project(Vector2 p)=>view.WorldToScreenPoint(ToolPoint(p));
            // Keep the actual projected scale of both tool axes. Normalizing each
            // separately turns an upright mouse drag into diagonal world movement.
            var a=(Project(Vector2.right*step)-Project(Vector2.left*step))/(2*step);
            var b=(Project(Vector2.up*step)-Project(Vector2.down*step))/(2*step);
            float units=Mathf.Sqrt(a.magnitude*b.magnitude);if(units<.001f)return Vector2.zero;
            a/=units;b/=units;
            float det=a.x*b.y-a.y*b.x;if(Mathf.Abs(det)<.1f)return Vector2.zero;
            return new Vector2((delta.x*b.y-delta.y*b.x)/det,(a.x*delta.y-a.y*delta.x)/det);
        }
        public bool DragScreen(Vector2 delta,float seconds)=>DragTool(ScreenToToolDelta(delta),seconds);
        public bool PointerCoordinates(Vector2 screen,out Vector2 point)
        {
            point=default;if(camera==null||!ToolStep||!float.IsFinite(screen.x)||!float.IsFinite(screen.y))return false;
            var view=camera.GetComponent<Camera>();
            Vector3 Project(Vector2 p)
            {
                var s=view.WorldToScreenPoint(ToolPoint(p));float depth=view.orthographic?1:s.z;
                return new Vector3(s.x*depth,s.y*depth,depth);
            }
            // Invert the projected 3D tool plane in homogeneous screen coordinates.
            // Using the same projection as rendering avoids ray/projection jitter
            // disagreement and subtraction of large world-space plane distances.
            var origin=Project(Vector2.zero);var u=Project(Vector2.right)-origin;var v=Project(Vector2.up)-origin;
            if(origin.z<=0)return false;
            float a=u.x-screen.x*u.z,b=v.x-screen.x*v.z,c=screen.x*origin.z-origin.x;
            float d=u.y-screen.y*u.z,e=v.y-screen.y*v.z,f=screen.y*origin.z-origin.y;
            float det=a*e-b*d;if(Mathf.Abs(det)<.001f)return false;
            point=new Vector2((c*e-b*f)/det,(a*f-c*d)/det);return float.IsFinite(point.x)&&float.IsFinite(point.y);
        }
        public bool PressPointer(Vector2 screen)
        {
            if(!CanInteract||!ToolStep||!PointerCoordinates(screen,out var point))return false;
            var pick=ToolGrabPoint;
            var projected=(Vector2)camera.GetComponent<Camera>().WorldToScreenPoint(ToolPoint(pick));
            if(Vector2.Distance(screen,projected)>Mathf.Max(45,Screen.height*128f/720)){ToolFeedback=Current==Step.Wrap?"Bấm giữ ngay mép khăn có vòng sáng":"Bấm giữ ngay dụng cụ đang cầm";return false;}
            if(Current==Step.Wrap&&!foldGrabbed)ToolPointer=pick;
            grabOffset=ToolPointer-point;PointerDragging=PressTool();return PointerDragging;
        }
        public bool DragPointer(Vector2 screen,float seconds)
        {
            if(!PointerDragging||!ToolHeld||!PointerCoordinates(screen,out var point))return false;
            var goal=point+grabOffset;goal=new Vector2(Mathf.Clamp(goal.x,-1,1),Mathf.Clamp(goal.y,-1,1));
            return MoveTool(goal-ToolPointer,seconds,true);
        }
        public bool DragTool(Vector2 delta,float seconds)
            =>MoveTool(delta,seconds,false);
        bool MoveTool(Vector2 delta,float seconds,bool direct)
        {
            if(!ToolHeld||!CanInteract||!ToolStep||seconds<=0||!float.IsFinite(seconds)||!float.IsFinite(delta.x)||!float.IsFinite(delta.y))return false;
            var motion=direct?Vector2.ClampMagnitude(delta,3):Vector2.ClampMagnitude(delta,Mathf.Min(.4f,seconds*8));
            ToolPointer=new Vector2(Mathf.Clamp(ToolPointer.x+motion.x,-1,1),Mathf.Clamp(ToolPointer.y+motion.y,-1,1));
            busy=true;actionUntil=Time.time+.12f;
            if(Finishing&&Current==Step.Tie)return true;
            if(Current==Step.Saw)
            {
                strokeSeconds+=Mathf.Min(seconds,.1f);if(motion.x*sawDirection>0)strokeTravel+=Mathf.Abs(motion.x);
                if(motion.magnitude>.001f)Foley?.Move(VillageWorkFoley.Cue.Saw,motion.magnitude/Mathf.Max(.01f,seconds)/5);
                ToolFeedback=Mathf.Abs(ToolPointer.y)>.28f?"Lệch rãnh • đưa cưa về giữa":"Giữ cưa trong hai vạch xanh";
                if(strokeTravel>=1.2f&&Mathf.Abs(ToolPointer.x-sawDirection)<.15f&&Mathf.Abs(ToolPointer.y)<=.28f&&strokeSeconds>=.22f)
                {ToolStages++;strokeSeconds=strokeTravel=0;sawDirection=-sawDirection;ActionProgress=ToolStages/8f;if(ToolStages==8)CompleteTool();}
            }
            else if(Current==Step.Tie)
            {
                if(motion.magnitude>.001f)Foley?.Move(VillageWorkFoley.Cue.Rope,motion.magnitude/Mathf.Max(.01f,seconds)/4);
                if(motion.sqrMagnitude>0&&Vector2.Distance(ToolPointer,ToolGoal)<.25f)
                {ToolStages++;ActionProgress=ToolStages/4f;if(ToolStages==4){Finishing=true;ToolPressure=0;ToolFeedback="Siết dây rồi THẢ trong dải xanh";}}
            }
            else if(Current==Step.FillRice)
            {
                if(motion.sqrMagnitude>0)LoadRiceScoop();
            }
            else
            {
                if(motion.magnitude>.001f)Foley?.Move(VillageWorkFoley.Cue.Cloth,motion.magnitude/Mathf.Max(.01f,seconds)/4);
                if(!foldGrabbed&&Vector2.Distance(ToolPointer,ToolGoal)<.25f)foldGrabbed=true;
                else if(foldGrabbed&&!Finishing&&ToolPointer.magnitude<.2f&&motion.sqrMagnitude>0)
                {Finishing=true;ToolPressure=0;ToolFeedback="Giữ yên để ép kín nếp";}
            }
            return true;
        }
        void Carry(string name,Material material,Vector3 size)
        {
            if(held!=null)Destroy(held);
            if(name=="Carried_rope")
            {
                held=new GameObject(name);held.transform.SetParent(props.transform,false);var coil=held.AddComponent<LineRenderer>();coil.sharedMaterial=fibre;coil.useWorldSpace=false;coil.widthMultiplier=.5f;coil.positionCount=80;
                for(int i=0;i<80;i++){float a=i*.3f;coil.SetPosition(i,new Vector3(Mathf.Cos(a)*2.3f,Mathf.Sin(a)*2.3f,i*.02f));}return;
            }
            if(name=="Bound_timber_bundle")
            {
                held=new GameObject(name);held.transform.SetParent(props.transform,false);
                for(int i=0;i<3;i++){var log=Shape("Bound_log",PrimitiveType.Cylinder,new Vector3(0,i==2?1.5f:0,i==0?-1:1),new Vector3(2,6,2),wood,false,held.transform);log.transform.localRotation=Quaternion.Euler(0,0,90);}
                foreach(int x in new[]{-4,4}){var go=new GameObject("Binding");go.transform.SetParent(held.transform,false);var ring=go.AddComponent<LineRenderer>();ring.sharedMaterial=fibre;ring.useWorldSpace=false;ring.widthMultiplier=.4f;ring.positionCount=33;for(int i=0;i<33;i++){float a=i/32f*Mathf.PI*2;ring.SetPosition(i,new Vector3(x,.7f+Mathf.Cos(a)*2,Mathf.Sin(a)*2));}}return;
            }
            var type=stall.kind==VillageWorkStall.WorkKind.Timber||name=="Full_rice_sack"?PrimitiveType.Cylinder:name=="Wrapped_rations"?PrimitiveType.Sphere:PrimitiveType.Cube;
            if(name=="Full_rice_sack")size.y*=.5f;held=Shape(name,type,Vector3.zero,size,material);
        }
        bool Near()=>player!=null&&QuestRowboat.FlatDistance(player.position,RequiredApproach)<=Reach&&Mathf.Abs(player.position.y-RequiredApproach.y)<18;
        public bool CanInteract=>stall!=null&&VillageWorkStall.Active==stall&&stall.Phase==VillageWorkStall.WorkPhase.Playing&&Time.timeScale>0&&!BachDangFrontEnd.IsBlocking&&Near();
        public bool Confirm()=>Interact();
        public bool TrimRice()
        {
            if(!CanInteract||Current!=Step.FillRice||RiceFill<=0)return false;
            ToolHeld=PointerDragging=busy=ScoopLoaded=false;RiceFill=Mathf.Max(0,RiceFill-.24f);ActionProgress=RiceFill;
            ToolFeedback=RiceReady?"Đã bớt gạo • ĐỦ • E / ENTER nhấc bao":RiceExcess?"Vẫn DƯ • R bớt thêm một gáo":"Đã bớt gạo • THIẾU • xúc thêm";Foley?.Play(VillageWorkFoley.Cue.Scoop);return true;
        }
        public bool EmptyRice()
        {
            if(!CanInteract||Current!=Step.FillRice||RiceFill<=0)return false;
            ToolHeld=PointerDragging=busy=ScoopLoaded=false;RiceFill=ActionProgress=0;
            ToolFeedback="Đã đổ lại bao • lấy gạo ở rổ bên trái";Foley?.Play(VillageWorkFoley.Cue.Pour);return true;
        }
        public bool Interact()
        {
            if(!CanInteract)return false;
            switch(Current)
            {
                case Step.Take:
                    Carry(stall.kind==VillageWorkStall.WorkKind.Timber?"Carried_log":stall.kind==VillageWorkStall.WorkKind.Rope?"Carried_rope":"Empty_sack",stall.kind==VillageWorkStall.WorkKind.Timber?wood:fibre,stall.kind==VillageWorkStall.WorkKind.Timber?new Vector3(2.5f,7,2.5f):new Vector3(5,3,4));Current=Step.CarryToBench;break;
                case Step.CarryToBench:
                    Destroy(held);held=null;
                    if(stall.kind==VillageWorkStall.WorkKind.Timber)
                    {
                        benchLog=Shape("Log_on_saw_bench",PrimitiveType.Cylinder,new Vector3(0,11,-9.8f),new Vector3(3,10,3),wood);benchLog.transform.localRotation=Quaternion.Euler(0,0,90);
                        blade=Shape("Hand_saw",PrimitiveType.Cube,new Vector3(0,14,-8),new Vector3(.35f,5,7),Make("Saw iron",new Color(.34f,.37f,.37f)));Current=Step.Saw;
                        sawHandle=Shape("Saw_wooden_handle",PrimitiveType.Cube,Vector3.zero,new Vector3(4,.7f,1.2f),wood);
                    }
                    else if(stall.kind==VillageWorkStall.WorkKind.Rope)Current=Step.Tie;
                    else
                    {
                        bag=Shape("Sack_at_fill_station",PrimitiveType.Cylinder,new Vector3(4,11.5f,-10.2f),new Vector3(6,2.5f,6),cloth);
                        OpenSack(bag);
                        grain=Shape("Visible_rice_level",PrimitiveType.Cylinder,new Vector3(4,10,-10.2f),new Vector3(5.4f,.1f,5.4f),rice);
                        fillBand=Line("Sack_fill_status",gold,.5f);fillBand.positionCount=33;for(int i=0;i<33;i++){float a=i/32f*Mathf.PI*2;fillBand.SetPosition(i,World(new Vector3(4+Mathf.Cos(a)*3.2f,12.6f,-10.2f+Mathf.Sin(a)*3.2f)));}placed.Add(fillBand.gameObject);Current=Step.FillRice;
                    }
                    break;
                case Step.FillRice:
                    if(!RiceReady){ToolFeedback=RiceExcess?"DƯ GẠO • nhấn R để bớt hoặc C để đổ lại":"CHƯA ĐỦ • kéo gáo từ rổ sang bao rồi thả";return false;}
                    ToolHeld=PointerDragging=busy=ScoopLoaded=false;Carry("Full_rice_sack",cloth,new Vector3(5,6,5));bag.SetActive(false);grain.SetActive(false);foreach(var go in placed)if(go!=null)go.SetActive(false);Current=Step.Deliver;pour.enabled=false;break;
                case Step.TakeIngredient:
                    Carry("Carried_ingredient_"+Ingredients,new[]{rice,fish,salt}[Ingredients],Ingredients==1?new Vector3(6,.8f,2):new Vector3(3,2,3));Current=Step.PlaceIngredient;break;
                case Step.PlaceIngredient:
                    if(parcel==null)
                    {
                        parcel=new GameObject("Packing_cloth");parcel.transform.SetParent(props.transform,false);parcel.transform.localPosition=new Vector3(0,9.2f,-10.2f);
                        Shape("Cloth_base",PrimitiveType.Cube,Vector3.zero,new Vector3(6,.2f,4),cloth,false,parcel.transform);
                        for(int i=0;i<4;i++){var pivot=new GameObject("Fold_"+i).transform;pivot.SetParent(parcel.transform,false);pivot.localPosition=i<2?new Vector3(i==0?-3:3,0,0):new Vector3(0,0,i==2?-1.5f:1.5f);Shape("Cloth_flap",PrimitiveType.Cube,i<2?new Vector3(i==0?-1.5f:1.5f,0,0):new Vector3(0,0,i==2?-1.5f:1.5f),i<2?new Vector3(3,.2f,4):new Vector3(6,.2f,3),cloth,false,pivot);flaps.Add(pivot);}
                    }
                    Destroy(held);held=null;placed.Add(Shape("Ingredient_on_cloth_"+Ingredients,Ingredients==1?PrimitiveType.Sphere:PrimitiveType.Cube,new Vector3(-2+Ingredients*2,10,-10.2f),Ingredients==1?new Vector3(4,1,1.5f):new Vector3(2,1,2),new[]{rice,fish,salt}[Ingredients]));
                    Ingredients++;Current=Ingredients==3?Step.Wrap:Step.TakeIngredient;break;
                case Step.Deliver:
                    if(!Carrying)return false;var goods=held;held=null;goods.name="Delivered_goods_"+stall.Progress;goods.transform.SetParent(props.transform,false);goods.transform.localPosition=new Vector3(25,6+stall.Progress*4,-14);goods.transform.localRotation=stall.kind==VillageWorkStall.WorkKind.Timber?Quaternion.Euler(0,0,90):Quaternion.identity;
                    var renderers=goods.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);goods.transform.position+=Vector3.up*(World(new Vector3(25,6+stall.Progress*4,-14)).y-bounds.min.y);
                    stall.CompletePhysicalOrder();break;
                default:return false;
            }
            ResetTool();Foley?.Play(VillageWorkFoley.Cue.SetDown);return true;
        }
        // Time alone cannot finish a physical action; all progress comes from tool motion.
        public bool Work(float seconds)=>false;
        void CompleteTool()
        {
            ActionProgress=0;Finishing=false;ToolHeld=busy=false;
            if(Current==Step.Tie)
            {
                var knot=Shape("Tightened_knot_"+Knots,PrimitiveType.Sphere,new Vector3(-10+Knots*10,14.3f,-10.2f),new Vector3(1.6f,1,2),fibre);knots.Add(knot);
                var loop=Line("Tied_binding_"+Knots,fibre,.5f);loop.positionCount=33;for(int i=0;i<33;i++){float a=i/32f*Mathf.PI*2;loop.SetPosition(i,World(new Vector3(-10+Knots*10,11.3f+Mathf.Cos(a)*3,-10.2f+Mathf.Sin(a)*1.6f)));}placed.Add(loop.gameObject);Knots++;
                if(Knots==3){foreach(var go in bundleLogs)go.SetActive(false);foreach(var go in knots)go.SetActive(false);foreach(var go in placed)go.SetActive(false);Carry("Bound_timber_bundle",wood,new Vector3(12,4,4));Current=Step.Deliver;}
            }
            else if(Current==Step.Saw){if(benchLog!=null)benchLog.transform.localScale=new Vector3(3,5,3);if(blade!=null)blade.SetActive(false);if(sawHandle!=null)sawHandle.SetActive(false);Carry("Sawn_timber",wood,new Vector3(2.5f,5,2.5f));Current=Step.Deliver;}
            else {foreach(var go in placed)if(go!=null)go.SetActive(false);if(parcel!=null)parcel.SetActive(false);Carry("Wrapped_rations",cloth,new Vector3(7,4,5));Current=Step.Deliver;}
            ResetTool();
        }
        public void Tick()
        {
            if(stall==null||props==null)return;
            if(QuestRowboat.FlatDistance(player.position,origin)>130){stall.Close();return;}
            if(Input.GetKeyDown(KeyCode.E)||Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter))Confirm();
            if(Current==Step.FillRice&&Input.GetKeyDown(KeyCode.R))TrimRice();
            if(Current==Step.FillRice&&Input.GetKeyDown(KeyCode.C))EmptyRice();
            bool overUI=UnityEngine.EventSystems.EventSystem.current!=null&&UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if(Input.GetMouseButtonDown(0)&&!overUI)PressPointer(Input.mousePosition);
            if(Input.GetMouseButton(0)&&PointerDragging)DragPointer(Input.mousePosition,Time.deltaTime);
            if(Input.GetMouseButton(0))AdvanceTool(Time.deltaTime);
            if(Input.GetMouseButtonUp(0)){if(PointerDragging)DragPointer(Input.mousePosition,Time.deltaTime);ReleaseTool();}
            if(Time.time>actionUntil&&!ToolHeld)busy=false;
        }
        void LateUpdate()
        {
            if(stall==null||props==null)return;
            Foley?.Tick(Time.unscaledDeltaTime,Time.timeScale<=0||stall.Phase!=VillageWorkStall.WorkPhase.Playing||BachDangFrontEnd.IsBlocking||CampaignJournalUI.IsOpen);
            if(Camera.main!=null)
                foreach(var item in fabrics)if(item.renderer!=null)
                {var cameraPosition=Camera.main.transform.position;bool Blocks(Vector3 target){var delta=target-cameraPosition;return item.renderer.bounds.IntersectRay(new Ray(cameraPosition,delta.normalized),out float distance)&&distance<delta.magnitude;}item.renderer.forceRenderingOff=item.hidden||(item.renderer.bounds.ClosestPoint(cameraPosition)-cameraPosition).sqrMagnitude<400||Blocks(player.position+Vector3.up*10)||Blocks(TargetPosition);}
            bool table=AtTable;
            if(table&&!tableWasActive)camera.CaptureCursor(false);
            else if(!table&&tableWasActive&&stall.Phase==VillageWorkStall.WorkPhase.Playing&&!BachDangFrontEnd.IsBlocking&&!CampaignJournalUI.IsOpen)camera.CaptureCursor(true);
            if(table&&(!tableWasActive||QuestRowboat.FlatDistance(player.position,RequiredApproach)>.1f))
            {
                var dock=RequiredApproach;dock.y=player.position.y;player.position=dock;
                var body=player.GetComponent<Rigidbody>();if(body!=null){body.position=dock;if(!body.isKinematic)body.linearVelocity=Vector3.zero;}Physics.SyncTransforms();
            }
            tableWasActive=table;hands?.Show(table);
            foreach(var guide in sawGuides)guide.SetActive(table&&Current==Step.Saw&&benchLog!=null);
            hands?.Grip(false,Current==Step.Saw||Current==Step.FillRice||Current==Step.Tie);
            if(camera!=null){if(AtTable)camera.FocusTable(World(new Vector3(Current==Step.Tie?-10+Knots*10:0,Current==Step.Saw?12:Current==Step.FillRice?12:10,-9.5f)),rotation*Vector3.forward,Current==Step.Wrap?.78f:Current==Step.Tie?.9f:1);else camera.ClearWorkFocus();}
            marker.transform.position=(ToolStep?ToolPoint(ToolGoal):TargetPosition)+Vector3.up*.3f;
            toolCursor.SetActive(ToolStep);if(ToolStep)toolCursor.transform.position=ToolPoint(ToolPointer);
            spoon.SetActive(Current==Step.FillRice);if(spoon.activeSelf)spoon.transform.SetPositionAndRotation(ToolPoint(ToolPointer),rotation*Quaternion.Euler(Time.time<pourUntil?55:0,0,0));
            ladleRice.SetActive(ScoopLoaded);
            label.transform.position=TargetPosition+Vector3.up*7;label.text=Current==Step.FillRice?(RiceFill<.65f?"THIẾU":RiceFill<=.85f?"ĐỦ • E / ENTER":"DƯ • R BỚT GẠO"):Near()?(Current==Step.Saw||Current==Step.Tie||Current==Step.Wrap?"BẤM GIỮ VẬT RỒI KÉO":"[E] TƯƠNG TÁC"):"ĐẾN ĐÂY";
            label.color=Current==Step.FillRice&&RiceFill>=.65f?(RiceFill<=.85f?new Color(.3f,1,.5f):new Color(1,.3f,.2f)):new Color(1,.82f,.37f);
            if(Camera.main!=null)label.transform.rotation=Camera.main.transform.rotation;
            label.gameObject.SetActive(!AtTable);
            if(held!=null){held.transform.position=chest.position+player.forward*4.5f+player.right*(stall.kind==VillageWorkStall.WorkKind.Timber?2:0);held.transform.rotation=player.rotation*(stall.kind==VillageWorkStall.WorkKind.Timber?Quaternion.Euler(0,0,90):Quaternion.identity);}
            if(busy||AtTable){var facing=(AtTable?rotation*Vector3.forward:TargetPosition-player.position);facing.y=0;var body=player.GetComponent<Rigidbody>();if(body!=null&&facing.sqrMagnitude>.1f)body.MoveRotation(Quaternion.Slerp(body.rotation,Quaternion.LookRotation(facing),1-Mathf.Exp(-12*Time.deltaTime)));}
            if(Carrying||busy||AtTable)
            {
                if(busy&&!Carrying&&Time.timeScale>0&&spine!=null)spine.rotation=Quaternion.AngleAxis(Current==Step.Wrap?30:18,player.right)*spine.rotation;
                var grip=Carrying?held.transform.position:ToolPoint(ToolPointer);
                // Cloth can lift in 3D while the pointer keeps following the table
                // plane; baking that lift into pointer coordinates inverted drags.
                if(!Carrying&&Current==Step.Wrap&&foldGrabbed)grip+=Vector3.up*(Mathf.Max(0,1-ToolPointer.magnitude)*1.5f*verticalScale);
                var right=grip+player.right*2;var left=grip-player.right*2;
                if(!Carrying&&Current==Step.FillRice){right=spoon.transform.TransformPoint(new Vector3(0,0,-3));left=World(new Vector3(.5f,13,-11.8f));}
                else if(!Carrying&&Current==Step.Saw)left=World(new Vector3(-4,12,-10));
                else if(!Carrying&&Current==Step.Tie){right=grip;left=World(new Vector3(-13.5f+Knots*10,12.5f,-10.5f));}
                else if(!Carrying&&Current==Step.Wrap){right=grip;left=World(new Vector3(0,9.7f,-10.2f));}
                RightGripPosition=right;
                LeftGripPosition=left;
                QuestRowboat.SolveArm(arms[0],arms[1],rightHand,right,arms[0].position+player.right*4-Vector3.up*3);
                QuestRowboat.SolveArm(arms[2],arms[3],leftHand,left,arms[2].position-player.right*4-Vector3.up*3);
                if(AtTable&&gripProfile!=null)
                {
                    if(Current==Step.Saw||Current==Step.FillRice)gripProfile.Align(arms[0],arms[1],rightHand,right,arms[0].position+player.right*4-Vector3.up*3,false,true,Current==Step.Saw?player.right:player.forward);
                }
            }
            if(AtTable&&camera!=null)hands?.Pose(camera.transform,RightGripPosition,LeftGripPosition,true,Current==Step.Wrap);
            if(blade!=null&&Current==Step.Saw)blade.transform.position=ToolPoint(ToolPointer);
            if(sawHandle!=null&&Current==Step.Saw)sawHandle.transform.SetPositionAndRotation(ToolPoint(ToolPointer)+player.right*2,player.rotation);
            if(grain!=null){float level=Mathf.Clamp01(RiceFill);grain.transform.localScale=new Vector3(5.4f,Mathf.Max(.05f,level*2.5f),5.4f);grain.transform.localPosition=new Vector3(4,9+level*2.5f,-10.2f);grain.GetComponent<Renderer>().sharedMaterial=rice;}
            if(fillBand!=null)fillBand.sharedMaterial=RiceReady?green:RiceExcess?red:gold;
            if(pour!=null){pour.enabled=Current==Step.FillRice&&Time.time<pourUntil;if(pour.enabled){pour.positionCount=9;for(int i=0;i<9;i++){float t=i/8f;pour.SetPosition(i,World(Vector3.Lerp(new Vector3(4,14,-10.2f),new Vector3(4,12,-10.2f),t)));}}}
            if(rope!=null&&stall.kind==VillageWorkStall.WorkKind.Rope)
            {rope.positionCount=Current==Step.Tie?33:0;for(int i=0;i<rope.positionCount;i++){float a=i/32f*Mathf.PI*2;float radius=1.25f-ActionProgress*.25f;rope.SetPosition(i,World(new Vector3(-10+Knots*10,11.3f+Mathf.Cos(a)*3*radius,-10.2f+Mathf.Sin(a)*1.6f*radius)));}}
            if(parcel!=null&&Current==Step.Wrap)for(int i=0;i<flaps.Count;i++){float a=(i<ToolStages?1:i==ToolStages&&foldGrabbed?1-Mathf.Clamp01(ToolPointer.magnitude):0)*130;flaps[i].localRotation=i<2?Quaternion.Euler(0,0,i==0?-a:a):Quaternion.Euler(i==2?a:-a,0,0);}
        }
        public void Finish()
        {
            busy=tableWasActive=false;Foley?.Stop();Foley=null;hands?.Dispose();hands=null;if(props!=null){props.SetActive(false);Destroy(props);}props=null;
            sawGuides.Clear();
            if(station!=null)station.ResetDisplay();station=null;
            foreach(var item in fabrics)if(item.renderer!=null)item.renderer.forceRenderingOff=item.hidden;fabrics.Clear();bundleLogs.Clear();
            foreach(var m in owned)if(m!=null)Destroy(m);owned.Clear();placed.Clear();knots.Clear();
            foreach(var mesh in ownedMeshes)if(mesh!=null)Destroy(mesh);ownedMeshes.Clear();
            if(camera!=null){camera.ClearWorkFocus();camera.distance=cameraSettings.x;camera.heightOffset=cameraSettings.y;camera.rightOffset=cameraSettings.z;camera.SetElevation(cameraElevation);}camera=null;
            if(arms!=null&&armRest!=null)for(int i=0;i<arms.Length;i++)if(arms[i]!=null)arms[i].localRotation=armRest[i];
            if(spine!=null)spine.localRotation=spineRest;
            if(rig!=null&&playerMeshes!=null){rig.cullingMode=previousCulling;for(int i=0;i<playerMeshes.Length;i++)if(playerMeshes[i]!=null)playerMeshes[i].updateWhenOffscreen=previousOffscreen[i];}playerMeshes=null;
            stall=null;held=null;
        }
        void OnDisable(){Finish();}
    }
}
