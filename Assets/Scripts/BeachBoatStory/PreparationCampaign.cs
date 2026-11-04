using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using StoryState=BeachBoatStory.BachDangStoryManager.StoryState;

namespace BeachBoatStory
{
    /// <summary>One-time preparation rewards, visible freight and an optional dock rescue with a real equipment choice.</summary>
    [DefaultExecutionOrder(680)]
    public sealed class PreparationCampaign:MonoBehaviour
    {
        public static PreparationCampaign Instance {get;private set;}
        public const float RescueLimit=65;
        public BachDangSaveData.Preparation Data {get;private set;}=new BachDangSaveData.Preparation();
        public int ArrowBonus=>Count(Data.qualityMask&3)+(Data.choice==1?2:0);
        public bool Carrying=>held>=0;
        public int Recovered=>Count(Data.recoveredMask);
        public bool RescueActive=>Data.incidentStarted&&!Data.incidentResolved&&!Data.incidentSkipped&&BeforeBattle;
        public bool BeforeBattle=>BachDangStoryManager.Instance!=null&&Array.IndexOf(CampaignProgress.StoryOrder,(int)BachDangStoryManager.Instance.currentState)>=Array.IndexOf(CampaignProgress.StoryOrder,(int)StoryState.Act2_ReturnToCampGate)&&Array.IndexOf(CampaignProgress.StoryOrder,(int)BachDangStoryManager.Instance.currentState)<Array.IndexOf(CampaignProgress.StoryOrder,(int)StoryState.Act3_BattleInProgress);
        public IReadOnlyList<SupplyCourier> Couriers=>couriers;public int ActiveCouriers=>couriers.Count;
        public float CourierDistance {get;private set;}
        public Transform GuideTarget {get;private set;}
        public Vector3 DockPosition=>dock!=null?dock.position:Vector3.zero;
        public Vector3 WarehousePosition=>warehouse!=null?warehouse.position:Vector3.zero;
        public float RescueRemaining=>Mathf.Max(0,RescueLimit-Data.rescueSeconds);
        public string Notice {get;private set;}="";
        public float NoticeUntil {get;private set;}
        public string JournalText=>Data.incidentResolved?(Data.choice==0?"Đã cứu hàng • Quá giờ tiếp tế thưởng":Data.choice==1?"Đã cứu hàng • Bao tên +2 sức chứa":"Đã cứu hàng • Nhận 2 thuốc"):
            Data.incidentSkipped?"Đã nhường chuyến hàng cho dân binh • Không nhận thưởng":RescueActive?$"TÙY CHỌN • Gom 3 kiện tại bến ({Recovered}/3), chọn tiếp tế cung thủ hoặc cứu thương.":"TÙY CHỌN • Kiểm tra chuyến hàng tại bến trước khi nhử địch. Không khóa nhiệm vụ hoặc hạng.";
        readonly List<SupplyCourier> couriers=new List<SupplyCourier>();
        Transform root,warehouse,dock;GameObject[] freight=new GameObject[4],sourceFreight=new GameObject[4],recoveredFreight=new GameObject[3],fallen=new GameObject[3];GameObject arrowsChoice,medicineChoice;TMP_Text depotBoard,hint,toast;GameObject hud;
        readonly List<TMP_Text> labels=new List<TMP_Text>();readonly List<Material> materials=new List<Material>();Material wood,fibre,green;int lastDelivered=-1;
        Transform player;Animator rig;HumanoidHandGrip leftGrip,rightGrip;int held=-1;Vector3 pickup;float nextPlan;bool built,prompt;
        static int Count(int mask){int n=0;for(;mask!=0;mask>>=1)n+=mask&1;return n;}
        void Awake(){Instance=this;}
        void Start(){player=BeachBoatQuestSession.Instance.player.transform;rig=player.GetComponentInChildren<Animator>();leftGrip=new HumanoidHandGrip(rig,true,.35f);rightGrip=new HumanoidHandGrip(rig,false,.35f);}
        public static string RewardGoal(VillageWorkStall.WorkKind kind)=>((int)kind<2?"Thưởng một lần: +1 sức chứa bao tên":"Thưởng một lần: +1 thuốc")+$" • Xong ≤ {CampaignProgress.Clock(CampaignProgress.MiniTarget((int)kind+1,2))}, tối đa 2 lỗi";
        public string WorkResult(VillageWorkStall stall)
        {
            int bit=1<<(int)stall.kind;
            if((Data.qualityMask&bit)!=0)return "Thưởng chuẩn bị đã nhận • Không nhận lặp";
            return Qualified(stall)?((int)stall.kind<2?"ĐẠT • Nhận +1 sức chứa bao tên":"ĐẠT • Nhận +1 thuốc"):RewardGoal(stall.kind)+" • Có thể chơi lại để cải thiện";
        }
        bool Qualified(VillageWorkStall stall)=>stall.Phase==VillageWorkStall.WorkPhase.Won&&stall.Elapsed<=CampaignProgress.MiniTarget((int)stall.kind+1,2)&&stall.Mistakes<=2;
        public void RegisterWork(VillageWorkStall stall)
        {
            if(stall==null||stall.Phase!=VillageWorkStall.WorkPhase.Won)return;
            int bit=1<<(int)stall.kind;bool first=(Data.preparedMask&bit)==0;Data.preparedMask|=bit;
            if((Data.qualityMask&bit)==0&&Qualified(stall))
            {
                Data.qualityMask|=bit;
                if((int)stall.kind<2)RiverPlayerCombat.Instance?.AddPreparedArrow();else player.GetComponent<PlayerVitals>().GrantHealingItems(1);
                Tell((int)stall.kind<2?"CHUẨN BỊ TỐT • Bao tên +1 sức chứa":"CHUẨN BỊ TỐT • +1 thuốc hồi máu");
            }
            else Tell(first?"Vật tư đã giao cho đội vận lương • Theo dõi tại kho và bến":"Đã nhận vật tư • Thưởng chất lượng không cộng lặp");
            if(first)Data.transit[(int)stall.kind]=-1;

        }
        public BachDangSaveData.Preparation Capture()=>JsonUtility.FromJson<BachDangSaveData.Preparation>(JsonUtility.ToJson(Data));
        public void Restore(BachDangSaveData save)
        {
            ReleaseCouriers();DropHeld();nextPlan=0;lastDelivered=-1;Data=save.preparation!=null&&save.preparation.version==1?JsonUtility.FromJson<BachDangSaveData.Preparation>(JsonUtility.ToJson(save.preparation)):new BachDangSaveData.Preparation();
            Data.qualityMask&=15;Data.preparedMask&=15;Data.deliveredMask&=Data.preparedMask;Data.recoveredMask&=7;Data.choice=Mathf.Clamp(Data.choice,0,2);Data.rescueSeconds=float.IsFinite(Data.rescueSeconds)?Mathf.Clamp(Data.rescueSeconds,0,RescueLimit):0;
            if(Data.transit==null||Data.transit.Length!=4)Data.transit=new[]{-1f,-1f,-1f,-1f};
            for(int i=0;i<4;i++)Data.transit[i]=float.IsFinite(Data.transit[i])?Mathf.Clamp(Data.transit[i],-1,20):-1;
            if(save.preparation==null)for(int i=0;i<Mathf.Min(4,save.stallLevels?.Length??0);i++)if(save.stallLevels[i]>=3){Data.preparedMask|=1<<i;Data.deliveredMask|=1<<i;}
            if(built)RefreshWorld();Notice="";NoticeUntil=0;
        }
        void Tell(string message){Notice=message;NoticeUntil=Time.unscaledTime+5;BeachBoatSoundscape.Instance?.PlayInteraction();}
        void Update()
        {
            if(!built){if(Time.frameCount%30==0&&VillagePreparationQuest.Instance?.stalls?.Length==4&&QuestRowboat.Instance!=null)Build();return;}
            if(!BeforeBattle&&Data.incidentStarted&&!Data.incidentResolved&&!Data.incidentSkipped){DropHeld();Data.incidentSkipped=true;ClearPrompt();RefreshWorld();}
            bool playable=BeachBoatQuestSession.Instance.storyMode&&!BeachBoatQuestSession.InputBlocked&&Time.timeScale>0&&!CampaignJournalUI.IsOpen;
            if(BeachBoatQuestSession.Instance.storyMode&&Time.timeScale>0&&!BachDangFrontEnd.IsBlocking&&!CampaignJournalUI.IsOpen)Advance(Time.unscaledDeltaTime);
            if(!playable){ClearPrompt();return;}
            if(Carrying&&QuestRowboat.Instance.IsAboard){DropHeld();ClearPrompt();return;}
            if(!Data.incidentStarted&&Data.preparedMask==15&&BeforeBattle&&QuestRowboat.Instance.IsAboard==false&&QuestRowboat.FlatDistance(player.position,dock.position)<85)StartRescue();
            RefreshInteraction();
            if(Input.GetKeyDown(KeyCode.E)&&StoryDialogueUI.TryConsumeInput())Interact();
            if(Input.GetKeyDown(KeyCode.X)){if(Carrying)DropHeld();else if(RescueActive&&QuestRowboat.FlatDistance(player.position,dock.position)<180)SkipRescue();}
        }
        public void Advance(float dt)
        {
            if(!built||dt<=0||!float.IsFinite(dt)||Time.timeScale<=0||BachDangFrontEnd.IsBlocking||CampaignJournalUI.IsOpen)return;
            dt=Mathf.Min(dt,.2f);
            for(int i=0;i<couriers.Count;i++)CourierDistance+=couriers[i].Tick(dt);
            for(int i=couriers.Count-1;i>=0;i--)if(couriers[i].Done){if(!couriers[i].Failed)Data.transit[couriers[i].Work]=8;couriers[i].Release();couriers.RemoveAt(i);}
            for(int work=0;work<4;work++)
            {
                int bit=1<<work;if((Data.preparedMask&bit)==0||(Data.deliveredMask&bit)!=0)continue;
                if(Data.transit[work]>=0){Data.transit[work]-=dt;if(Data.transit[work]<=0){Data.deliveredMask|=bit;Data.transit[work]=0;}}
                else if(QuestRowboat.FlatDistance(player.position,VillagePreparationQuest.Instance.stalls.First(s=>(int)s.kind==work).InteractionPosition)>800){Data.transit[work]=8;}
                else if(couriers.Count<2&&Time.unscaledTime>=nextPlan&&!couriers.Any(c=>c.Work==work))
                {
                    nextPlan=Time.unscaledTime+1;var courier=SupplyCourier.TryCreate(work,VillagePreparationQuest.Instance.stalls.First(s=>(int)s.kind==work),warehouse.position,couriers.Select(c=>c.Actor));if(courier!=null)couriers.Add(courier);
                }
            }
            if(RescueActive&&!BeachBoatQuestSession.InteractionBlocked&&VillageWorkStall.Active==null&&BeachBoatMinigameStation.Active==null&&ArcheryTraining.Active==null)Data.rescueSeconds=Mathf.Min(RescueLimit,Data.rescueSeconds+dt);
            if(Carrying&&!BeforeBattle)DropHeld();
            RefreshWorld();
        }
        public bool StartRescue()
        {
            if(!built||Data.incidentStarted||Data.preparedMask!=15||!BeforeBattle||QuestRowboat.Instance.IsAboard||BeachBoatQuestSession.InputBlocked||QuestRowboat.FlatDistance(player.position,dock.position)>85)return false;
            Data.incidentStarted=true;Data.rescueSeconds=0;RefreshWorld();Tell("TUỘT DÂY CHẰNG! • Cứu 3 kiện hàng trong 65 giây để chọn phần tiếp tế");BeachBoatSoundscape.Instance?.PlayResult(false);return true;
        }
        public bool Interact()
        {
            if(!RescueActive||BeachBoatQuestSession.InteractionBlocked||BeachBoatMinigameStation.Active!=null||ArcheryTraining.Active!=null||CampaignJournalUI.IsOpen||Time.timeScale<=0||QuestRowboat.Instance.IsAboard)return false;
            if(Carrying)
            {
                if(!Reach(dock.position,15))return false;
                int item=held;held=-1;HumanoidGripMesh.Restore(rig);Data.recoveredMask|=1<<item;fallen[item].SetActive(false);BeachBoatSoundscape.Instance?.PlayInteraction();Tell(Recovered==3?"ĐÃ CỨU ĐỦ HÀNG • Chọn giá tên hoặc hòm thuốc tại bến":$"Đã đưa {Recovered}/3 kiện lên xe");RefreshWorld();return true;
            }
            if(Recovered==3)
            {
                if(Reach(arrowsChoice.transform.position,11))return Choose(1);
                if(Reach(medicineChoice.transform.position,11))return Choose(2);
                return false;
            }
            for(int i=0;i<3;i++)if((Data.recoveredMask&(1<<i))==0&&Reach(fallen[i].transform.position,10))
            {RiverBoardingCombat.Instance?.StowForCargo();PlayerShield.Instance?.SetEquipped(false);held=i;pickup=fallen[i].transform.position;fallen[i].GetComponent<Collider>().enabled=false;RiverPlayerCombat.Instance?.SetAim(false);BeachBoatSoundscape.Instance?.PlayInteraction();RefreshInteraction();return true;}
            return false;
        }
        bool Choose(int choice)
        {
            if(Data.incidentResolved||Recovered!=3)return false;Data.incidentResolved=true;
            bool onTime=Data.rescueSeconds<RescueLimit;Data.choice=onTime?choice:0;
            if(onTime){if(choice==1){RiverPlayerCombat.Instance?.AddPreparedArrow(2);Tell("TIẾP TẾ CUNG THỦ • Bao tên +2 sức chứa");}else{player.GetComponent<PlayerVitals>().GrantHealingItems(2);Tell("TIẾP TẾ CỨU THƯƠNG • +2 thuốc hồi máu");}}
            else Tell("Đã giữ được hàng • Chuyến tiếp tế thưởng đã rời bến vì quá giờ");
            BeachBoatSoundscape.Instance?.PlayResult(true);ClearPrompt();RefreshWorld();return true;
        }
        public void SkipRescue(){if(!RescueActive)return;DropHeld();Data.incidentSkipped=true;ClearPrompt();RefreshWorld();Tell("Đã nhờ dân binh lo chuyến hàng • Tiếp tục nhiệm vụ chính");}
        bool Reach(Vector3 point,float radius)=>QuestRowboat.FlatDistance(player.position,point)<=radius&&Mathf.Abs(player.position.y-point.y)<22;
        void RefreshInteraction()
        {
            if(!RescueActive){ClearPrompt();return;}
            string text=null;GuideTarget=null;
            if(Carrying){GuideTarget=dock;text=Reach(dock.position,15)?"[E] Xếp kiện hàng lên xe • [X] đặt lại":"Mang kiện hàng về xe • [X] đặt lại";}
            else if(Recovered==3)
            {
                GuideTarget=Data.rescueSeconds<RescueLimit?arrowsChoice.transform:medicineChoice.transform;
                if(Reach(arrowsChoice.transform.position,11))text="[E] Ưu tiên CUNG THỦ • +2 sức chứa tên";
                else if(Reach(medicineChoice.transform.position,11))text="[E] Ưu tiên CỨU THƯƠNG • +2 thuốc";
            }
            else
            {
                float best=float.MaxValue;for(int i=0;i<3;i++)if((Data.recoveredMask&(1<<i))==0){float d=QuestRowboat.FlatDistance(player.position,fallen[i].transform.position);if(d<best){GuideTarget=fallen[i].transform;best=d;}if(Reach(fallen[i].transform.position,10))text="[E] Nhấc kiện hàng bị rơi • Mang về xe";}
            }
            if(text!=null){BachDangStoryManager.Instance.SetInteractPrompt(true,text,this);prompt=true;}else ClearPrompt();
        }
        void ClearPrompt(){if(prompt){BachDangStoryManager.Instance?.SetInteractPrompt(false,owner:this);prompt=false;}}
        public void DropHeld()
        {
            if(held<0)return;HumanoidGripMesh.Restore(rig);var cargo=fallen[held];if(cargo!=null){cargo.transform.SetPositionAndRotation(pickup,Quaternion.identity);cargo.GetComponent<Collider>().enabled=true;}held=-1;
        }
        void LateUpdate()
        {
            foreach(var courier in couriers)courier.Pose();foreach(var label in labels)if(label!=null)label.transform.rotation=BeachBoatQuestSession.Instance.storyCamera.transform.rotation;
            if(Carrying)
            {
                var shoulders=(rig.GetBoneTransform(HumanBodyBones.LeftUpperArm).position+rig.GetBoneTransform(HumanBodyBones.RightUpperArm).position)*.5f;float reach=Vector3.Distance(rig.GetBoneTransform(HumanBodyBones.RightUpperArm).position,rig.GetBoneTransform(HumanBodyBones.RightLowerArm).position)+Vector3.Distance(rig.GetBoneTransform(HumanBodyBones.RightLowerArm).position,rig.GetBoneTransform(HumanBodyBones.RightHand).position);var cargo=fallen[held];cargo.transform.SetPositionAndRotation(shoulders+player.forward*(reach*.5f)-Vector3.up*(reach*.3f),player.rotation);
                foreach(bool left in new[]{true,false}){var upper=rig.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);var lower=rig.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var hand=rig.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);(left?leftGrip:rightGrip).Align(upper,lower,cargo.transform.position+player.right*(left?-3:3),upper.position+player.right*(left?-4:4)-Vector3.up*3,player.forward);}
            }
            if(Carrying)HumanoidGripMesh.Apply(rig,leftGrip,rightGrip,.6f,.6f);
            if(hud!=null)
            {
                bool show=BeachBoatQuestSession.Instance.storyMode&&!BeachBoatQuestSession.InputBlocked&&!CampaignJournalUI.IsOpen;hud.SetActive(show);
                hint.text=RescueActive&&QuestRowboat.FlatDistance(player.position,dock.position)<180?$"CHUYẾN HÀNG TRƯỚC GIỜ XUẤT QUÂN • {Recovered}/3 kiện • {(RescueRemaining>0?RescueRemaining.ToString("0")+"s":"ĐÃ TRỄ CHUYẾN THƯỞNG")}\n"+(Carrying?"Mang về xe • E xếp hàng • X đặt lại":Recovered==3?"Chọn một: giá TÊN hoặc hòm THUỐC • E nhận":"E nhấc kiện • X nhờ dân binh lo hàng và bỏ qua"):"";
                toast.text=Time.unscaledTime<NoticeUntil?Notice:"";hint.transform.parent.gameObject.SetActive(hint.text.Length>0);toast.transform.parent.gameObject.SetActive(toast.text.Length>0);
            }
        }
        Material Mat(string name,Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};materials.Add(m);return m;}
        static float Ground(Vector3 p)=>Terrain.activeTerrain.SampleHeight(p)+Terrain.activeTerrain.transform.position.y;
        Vector3 ClearSite(Vector3 near)
        {
            for(int i=0;i<30;i++)
            {
                float a=i*2.399963f,r=35+(i/10)*15;var p=near+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);p.y=Ground(p);
                if(p.y<TideSystem.Instance.highTideY+3)continue;
                if(!Physics.OverlapBox(p+Vector3.up*5,new Vector3(15,5,11),Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(c=>!(c is TerrainCollider)&&c.GetComponentInParent<Animator>()==null))return p;
            }
            var fallback=near+Vector3.right*55;fallback.y=Ground(fallback);return fallback;
        }
        void Build()
        {
            var gates=FindObjectsByType<CampGateTransition>(FindObjectsSortMode.None);var village=gates.FirstOrDefault(g=>g.gateType==CampGateTransition.GateType.VillageGate);var river=gates.FirstOrDefault(g=>g.gateType==CampGateTransition.GateType.RiverGate);if(village==null||river==null)return;
            root=new GameObject("Campaign_Supply_Logistics").transform;root.SetParent(transform,false);wood=Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m=>m.name.Contains("Courtyard_Weathered_Wood")&&m.mainTexture!=null)??Mat("Supply weathered timber",new Color(.29f,.17f,.08f));fibre=Mat("Supply sack canvas",new Color(.63f,.51f,.3f));green=Mat("Medical cloth",new Color(.32f,.48f,.3f));
            warehouse=new GameObject("Kho_van_luong_lang").transform;warehouse.SetParent(root,false);warehouse.position=ClearSite(village.transform.position);
            dock=new GameObject("Xe_tiep_te_ben_song").transform;dock.SetParent(root,false);dock.position=ClearSite(river.transform.position+Vector3.right*20);
            BuildCart(warehouse);BuildCart(dock);
            for(int i=0;i<4;i++)
            {
                freight[i]=Cargo("Supply_"+i,dock,new Vector3(-8+i*5,i<2?6:8,0),(VillageWorkStall.WorkKind)i);
                var stall=VillagePreparationQuest.Instance.stalls.First(s=>(int)s.kind==i);sourceFreight[i]=Cargo("Prepared_for_courier_"+i,stall.Workstation.Environment,new Vector3(25,i<2?7:9,-14),(VillageWorkStall.WorkKind)i);
            }
            for(int i=0;i<3;i++)
            {
                var offset=new[]{new Vector3(-25,0,-19),new Vector3(25,0,-25),new Vector3(10,0,30)}[i];var point=dock.position+offset;point.y=Ground(point)+3;
                fallen[i]=Cargo("Kien_hang_roi_"+i,root,Vector3.zero,(VillageWorkStall.WorkKind)(i+1));fallen[i].transform.position=point;recoveredFreight[i]=Cargo("Recovered_freight_"+i,dock,new Vector3(-7+i*7,13,4),(VillageWorkStall.WorkKind)(i+1));
            }
            arrowsChoice=Cargo("Uu_tien_cung_thu",dock,new Vector3(-20,3,6),VillageWorkStall.WorkKind.Rope);medicineChoice=Cargo("Uu_tien_cuu_thuong",dock,new Vector3(20,3,6),VillageWorkStall.WorkKind.Provisions);
            Label(arrowsChoice.transform,"CUNG THỦ\n+2 sức chứa tên",new Vector3(0,7,0),7);Label(medicineChoice.transform,"CỨU THƯƠNG\n+2 thuốc",new Vector3(0,7,0),7);
            depotBoard=Label(dock,"VẬT TƯ TỪ LÀNG",new Vector3(0,17,3),8);Label(warehouse,"KHO VẬN LƯƠNG\nHàng từ bốn sạp → bến sông",new Vector3(0,17,3),8);
            hud=new GameObject("SupplyFeedbackHUD",typeof(UnityEngine.Canvas),typeof(UnityEngine.UI.CanvasScaler));hud.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;hud.GetComponent<Canvas>().sortingOrder=115;var scale=hud.GetComponent<UnityEngine.UI.CanvasScaler>();scale.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);
            TMP_Text Text(string name,Vector2 a,Vector2 b){var panel=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));panel.transform.SetParent(hud.transform,false);var r=panel.GetComponent<RectTransform>();r.anchorMin=a;r.anchorMax=b;r.offsetMin=r.offsetMax=Vector2.zero;panel.GetComponent<UnityEngine.UI.Image>().color=new Color(.025f,.065f,.055f,.92f);panel.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;var g=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI));g.transform.SetParent(panel.transform,false);var t=g.GetComponent<TextMeshProUGUI>();StoryFont.Apply(t);t.fontSize=17;t.alignment=TextAlignmentOptions.Center;t.color=new Color(1,.86f,.56f);t.raycastTarget=false;t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;t.rectTransform.offsetMin=new Vector2(8,4);t.rectTransform.offsetMax=new Vector2(-8,-4);return t;}
            hint=Text("Optional_freight",new Vector2(.3f,.2f),new Vector2(.7f,.3f));toast=Text("Preparation_reward",new Vector2(.25f,.68f),new Vector2(.75f,.75f));built=true;RefreshWorld();Physics.SyncTransforms();
        }
        void BuildCart(Transform parent)
        {
            Part("Cart_bed",parent,new Vector3(0,4,0),new Vector3(25,2,14),wood,true);
            for(int side=-1;side<=1;side+=2){Part("Cart_side",parent,new Vector3(side*12,7,0),new Vector3(1,5,14),wood);Part("Cart_shaft",parent,new Vector3(side*6,3,-14),new Vector3(1,1,17),wood);var wheel=Part("Cart_wheel",parent,new Vector3(side*13,3,0),new Vector3(6,1,6),wood);wheel.transform.localRotation=Quaternion.Euler(0,0,90);}
        }
        GameObject Part(string name,Transform parent,Vector3 p,Vector3 size,Material mat,bool solid=false)
        {var g=GameObject.CreatePrimitive(name=="Canvas_package"?PrimitiveType.Sphere:name=="Cart_wheel"?PrimitiveType.Cylinder:PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;if(!solid){g.GetComponent<Collider>().enabled=false;Destroy(g.GetComponent<Collider>());}return g;}
        public GameObject Cargo(string name,Transform parent,Vector3 p,VillageWorkStall.WorkKind kind)
        {
            var cargo=new GameObject(name);cargo.transform.SetParent(parent,false);cargo.transform.localPosition=p;
            if(kind==VillageWorkStall.WorkKind.Timber||kind==VillageWorkStall.WorkKind.Rope)
            {
                for(int i=0;i<3;i++){var log=GameObject.CreatePrimitive(PrimitiveType.Cylinder);log.name="Bound_wood";log.transform.SetParent(cargo.transform,false);log.transform.localPosition=new Vector3((i-1)*1.7f,0,0);log.transform.localRotation=Quaternion.Euler(90,0,0);log.transform.localScale=new Vector3(1.6f,4,1.6f);log.GetComponent<Renderer>().sharedMaterial=wood;log.GetComponent<Collider>().enabled=false;Destroy(log.GetComponent<Collider>());}
                for(int side=-1;side<=1;side+=2)Part("Rope_binding",cargo.transform,new Vector3(0,0,side*2.4f),new Vector3(5.4f,2,.35f),fibre);
            }
            else{Part("Canvas_package",cargo.transform,Vector3.zero,new Vector3(6,5,6),fibre);Part("Cloth_binding",cargo.transform,new Vector3(0,.15f,0),new Vector3(.5f,5.3f,6.3f),kind==VillageWorkStall.WorkKind.Provisions?green:wood);}
            var solid=cargo.AddComponent<BoxCollider>();solid.size=kind==VillageWorkStall.WorkKind.Timber||kind==VillageWorkStall.WorkKind.Rope?new Vector3(6,2,8):new Vector3(6,5,6);return cargo;
        }
        TMP_Text Label(Transform parent,string text,Vector3 p,int size)
        {var go=new GameObject("Supply_sign");go.transform.SetParent(parent,false);go.transform.localPosition=p;var label=go.AddComponent<TextMeshPro>();StoryFont.Apply(label);label.text=text;label.fontSize=size;label.alignment=TextAlignmentOptions.Center;label.color=new Color(1,.85f,.54f);label.rectTransform.sizeDelta=new Vector2(48,14);labels.Add(label);return label;}
        void RefreshWorld()
        {
            if(!built)return;
            for(int i=0;i<4;i++){freight[i].SetActive((Data.deliveredMask&(1<<i))!=0);sourceFreight[i].SetActive((Data.preparedMask&(1<<i))!=0&&(Data.deliveredMask&(1<<i))==0&&Data.transit[i]<0&&!IsCarried(i));}
            bool rescue=RescueActive;
            for(int i=0;i<3;i++){fallen[i].SetActive(rescue&&(Data.recoveredMask&(1<<i))==0);recoveredFreight[i].SetActive((Data.recoveredMask&(1<<i))!=0);}
            arrowsChoice.SetActive(rescue&&Recovered==3);medicineChoice.SetActive(rescue&&Recovered==3);
            if(lastDelivered==Data.deliveredMask)return;lastDelivered=Data.deliveredMask;
            depotBoard.text=$"VẬT TƯ TỪ LÀNG • {Count(Data.deliveredMask)}/4\n"+((Data.deliveredMask&3)!=0?"Cọc, dây đã tới • ":"")+((Data.deliveredMask&12)!=0?"Gạo, lương cho quân":"Đội vận lương đang chuyển hàng");
        }
        bool IsCarried(int work){foreach(var courier in couriers)if(courier.Work==work&&courier.Holding)return true;return false;}
        void ReleaseCouriers(){foreach(var c in couriers)c.Release();couriers.Clear();}
        public void SuspendForDefeat(){ReleaseCouriers();DropHeld();ClearPrompt();}
        void OnDisable(){SuspendForDefeat();}
        void OnDestroy(){if(Instance==this)Instance=null;ReleaseCouriers();foreach(var cargo in sourceFreight)if(cargo!=null)Destroy(cargo);if(root!=null)Destroy(root.gameObject);if(hud!=null)Destroy(hud);foreach(var material in materials)if(material!=null)Destroy(material);}
    }
}
