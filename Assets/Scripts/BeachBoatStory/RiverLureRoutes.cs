using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>The helmsman chooses a water route by sailing between its two flags, not by a menu.</summary>
    public sealed class RiverLureRoutes
    {
        static readonly Color SafeColor=new Color(.34f,1,.66f),FastColor=new Color(1,.72f,.24f);
        readonly RiverFleetPursuit owner;
        readonly Transform[] gates=new Transform[2];
        GameObject root;
        Material safeMaterial,fastMaterial;
        GUIStyle badgeStyle;
        public int Choice {get;private set;}public int Passed {get;private set;}public bool Initialized {get;private set;}
        public bool Completed=>Initialized&&Choice!=0&&Passed>=(Choice==1?3:2);
        public Vector3 Origin {get;private set;}public string Name=>Choice==1?"LUỒNG VÒNG • ÍT TÊN":Choice==2?"LUỒNG THẲNG • NGUY HIỂM":"CHỌN LUỒNG NHỬ ĐỊCH";
        Vector3 previous;bool havePrevious;
        public RiverLureRoutes(RiverFleetPursuit pursuit){owner=pursuit;}
        public void Begin(Vector3 at)
        {if(Initialized)return;Initialized=true;Origin=at;Origin=new Vector3(Mathf.Clamp(at.x,RiverStakeZone.Instance.FieldBounds.min.x+400,RiverStakeZone.Instance.FieldBounds.max.x-400),TideSystem.CurrentWaterHeight,Mathf.Min(at.z-180,Mathf.Max(at.z-320,RiverStakeZone.Instance.FieldBounds.max.z+280)));Build();Call("Hai luồng phía trước! Vòng qua cờ xanh để tránh tầm tên, hoặc đi thẳng để nhử địch nhanh hơn!");}
        public Vector3 Gate(int route,int index)
        {
            float end=Mathf.Min(RiverStakeZone.Instance.FieldBounds.max.z+200,Origin.z-180);
            float x=Origin.x+(route==1?(index==1?-420:-240):0);
            float z=Mathf.Lerp(Origin.z,end,index/(float)(route==1?2:1));var desired=new Vector3(x,TideSystem.CurrentWaterHeight,z);
            // A route may only advertise navigable water. Small adjustments avoid riverbank props.
            foreach(float offset in new[]{0f,40f,-40f,80f,-80f}){var point=desired+Vector3.right*offset;if(RiverWaterNavigation.ClearWater(point,25))return point;}
            return desired;
        }
        public Vector3 Guide=>!Initialized?owner.FormationAnchor:Completed?new Vector3(Origin.x,TideSystem.CurrentWaterHeight,RiverStakeZone.Instance.FieldBounds.min.z-180):Choice==0?Gate(2,0):Gate(Choice,Passed);
        void Build()
        {
            if(root!=null){root.SetActive(true);return;}
            root=new GameObject("Lua_chon_luong_nhu_dich");
            root.transform.SetParent(owner.transform,false);
            safeMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){color=SafeColor};
            fastMaterial=new Material(safeMaterial){color=FastColor};
            for(int i=0;i<gates.Length;i++)
            {
                var gate=new GameObject("Cong_luong_"+i).transform;
                gates[i]=gate;
                gate.SetParent(root.transform,false);
                var material=i==0?safeMaterial:fastMaterial;
                for(int side=-1;side<=1;side+=2)
                {
                    AddBeaconPart(gate,PrimitiveType.Cylinder,new Vector3(side*65,13,0),new Vector3(4,13,4),material);
                    AddBeaconPart(gate,PrimitiveType.Sphere,new Vector3(side*65,27,0),Vector3.one*13,material);
                }
                for(int row=0;row<3;row++)
                {
                    var arrow=new GameObject("Mui_ten_tren_mat_nuoc").AddComponent<LineRenderer>();
                    arrow.transform.SetParent(gate,false);
                    arrow.useWorldSpace=false;
                    arrow.sharedMaterial=material;
                    arrow.widthMultiplier=4;
                    arrow.numCornerVertices=3;
                    float z=24+row*18;
                    arrow.positionCount=3;
                    arrow.SetPositions(new[]{new Vector3(-28,.7f,z),new Vector3(0,.7f,z-13),new Vector3(28,.7f,z)});
                }
            }
            RefreshMarkers();
        }
        static void AddBeaconPart(Transform gate,PrimitiveType shape,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(shape);
            part.transform.SetParent(gate,false);
            part.transform.localPosition=position;
            part.transform.localScale=scale;
            var collider=part.GetComponent<Collider>();
            collider.enabled=false;
            Object.Destroy(collider);
            var renderer=part.GetComponent<Renderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        void RefreshMarkers()
        {
            for(int i=0;i<gates.Length;i++)
            {
                int route=i+1;
                bool visible=!Completed&&(Choice==0||Choice==route);
                gates[i].gameObject.SetActive(visible);
                if(visible)gates[i].position=Gate(route,Choice==0?0:Passed);
            }
            if(Completed)root.SetActive(false);
        }
        public void DrawMarkers()
        {
            if(!Initialized||Completed||root==null||!root.activeInHierarchy)return;
            var camera=BeachBoatQuestSession.Instance.storyCamera;
            float unit=Mathf.Max(.65f,Mathf.Min(Screen.height/720f,Screen.width/900f));
            if(badgeStyle==null)
                badgeStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
            badgeStyle.fontSize=Mathf.RoundToInt(19*unit);
            var oldColor=GUI.color;
            Rect previous=new Rect();
            for(int i=0;i<gates.Length;i++)
            {
                if(!gates[i].gameObject.activeSelf)continue;
                var point=camera.WorldToViewportPoint(gates[i].position+Vector3.up*36);
                bool behind=point.z<=0;
                float x=behind?(point.x>.5f?.08f:.92f):Mathf.Clamp(point.x,.08f,.92f);
                float y=behind?.5f:Mathf.Clamp(1-point.y,.26f,.69f);
                var badge=new Rect(x*Screen.width-48*unit,y*Screen.height-17*unit,96*unit,34*unit);
                if(i==1&&badge.Overlaps(previous))badge.y=previous.yMax+5*unit;
                var accent=i==0?SafeColor:FastColor;
                GUI.color=new Color(.025f,.06f,.055f,.94f);
                GUI.DrawTexture(badge,Texture2D.whiteTexture);
                GUI.color=accent;
                GUI.DrawTexture(new Rect(badge.x,badge.y,badge.width,3*unit),Texture2D.whiteTexture);
                string name=i==0?"VÒNG":"TẮT";
                if(behind||point.x<.08f||point.x>.92f)name=x<.5f?"‹ "+name:name+" ›";
                GUI.Label(badge,name,badgeStyle);
                previous=badge;
            }
            GUI.color=oldColor;
        }
        public void Step(float dt,Vector3 position)
        {
            if(!Initialized||dt<=0||Completed)return;Build();
            if(Choice==0)
            {for(int route=1;route<=2;route++)if(Crossed(position,Gate(route,0))){Choice=route;Passed=1;BeachBoatSoundscape.Instance?.PlayInteraction();Call(route==1?"Bám luồng vòng! Giữ khoảng cách, quân địch đang áp sát!":"Đi thẳng! Coi chừng tên phía trước, đổi hướng sớm!");break;}}
            else if(Crossed(position,Gate(Choice,Passed))){Passed++;BeachBoatSoundscape.Instance?.PlayInteraction();if(Completed)Call("Đã qua luồng nhử! Dẫn chúng vào giữa trận địa, quân ta đã sẵn sàng!");}
            previous=position;havePrevious=true;
            RefreshMarkers();
        }
        bool Crossed(Vector3 point,Vector3 gate)
        {
            if(QuestRowboat.FlatDistance(point,gate)<78)return true;if(!havePrevious||(point-previous).sqrMagnitude>250*250)return false;
            var line=point-previous;line.y=0;float t=Mathf.Clamp01(Vector3.Dot(gate-previous,line)/Mathf.Max(.001f,line.sqrMagnitude));return QuestRowboat.FlatDistance(previous+line*t,gate)<78;
        }
        public void Call(string text)
        {
            string id=text.StartsWith("Hai luồng")?"route_intro":text.StartsWith("Bám luồng")?"safe_route":text.StartsWith("Đi thẳng")?"fast_route":text.StartsWith("Tên tới")?"arrows":"route_done";CampaignFieldCalls.Say(id);
        }
        public void FillSave(BachDangSaveData.Fleet data){data.routeVersion=1;data.baitRoute=Choice;data.routePassed=Passed;data.routeInitialized=Initialized;data.routeOrigin=Origin;}
        public void RestoreSave(BachDangSaveData.Fleet data,bool committed)
        {
            Reset();if(committed){Initialized=true;Choice=2;Passed=2;return;}if(data.routeVersion==0){if(owner.IsPursuing){Initialized=true;Choice=2;Passed=2;}return;}
            Initialized=data.routeInitialized;Choice=Mathf.Clamp(data.baitRoute,0,2);Passed=Mathf.Clamp(data.routePassed,0,Choice==1?3:2);Origin=data.routeOrigin;if(Initialized&&!Completed)Build();
        }
        public void Reset(){Choice=Passed=0;Initialized=havePrevious=false;if(root!=null)root.SetActive(false);}
        public void Dispose(){if(root!=null)Object.Destroy(root);if(safeMaterial!=null)Object.Destroy(safeMaterial);if(fastMaterial!=null)Object.Destroy(fastMaterial);}
    }
}
