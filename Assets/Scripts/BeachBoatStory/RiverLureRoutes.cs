using TMPro;
using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>The helmsman chooses a water route by sailing between its two flags, not by a menu.</summary>
    public sealed class RiverLureRoutes
    {
        readonly RiverFleetPursuit owner;GameObject root;Material safeMaterial,fastMaterial;Transform[] flags;TMP_Text[] labels;
        public int Choice {get;private set;}public int Passed {get;private set;}public bool Initialized {get;private set;}
        public bool Completed=>Initialized&&Choice!=0&&Passed>=(Choice==1?3:2);
        public Vector3 Origin {get;private set;}public string Name=>Choice==1?"LUỒNG VÒNG • ÍT TÊN":Choice==2?"LUỒNG THẲNG • NGUY HIỂM":"CHỌN LUỒNG NHỬ ĐỊCH";
        public string Warning {get;private set;}="";float callUntil;Vector3 previous;bool havePrevious;
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
            if(root!=null){root.SetActive(true);return;}root=new GameObject("Lua_chon_luong_nhu_dich");root.transform.SetParent(owner.transform,false);
            safeMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){color=new Color(.24f,.79f,.6f)};fastMaterial=new Material(safeMaterial){color=new Color(.96f,.56f,.22f)};
            flags=new Transform[4];labels=new TMP_Text[2];
            for(int i=0;i<4;i++)
            {
                var gate=new GameObject("Co_luong_"+i).transform;gate.SetParent(root.transform,false);flags[i]=gate;
                void Part(PrimitiveType shape,Vector3 pos,Vector3 size){var go=GameObject.CreatePrimitive(shape);go.transform.SetParent(gate,false);go.transform.localPosition=pos;go.transform.localScale=size;go.GetComponent<Collider>().enabled=false;Object.Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=i<2?safeMaterial:fastMaterial;}
                Part(PrimitiveType.Cylinder,new Vector3(0,8,0),new Vector3(.6f,8,.6f));Part(PrimitiveType.Cube,new Vector3(3,13,0),new Vector3(6,4,.2f));
                if(i%2==0){var go=new GameObject("Ten_luong");go.transform.SetParent(gate,false);go.transform.localPosition=new Vector3(65,28,0);labels[i/2]=go.AddComponent<TextMeshPro>();StoryFont.Apply(labels[i/2]);labels[i/2].fontSize=18;labels[i/2].alignment=TextAlignmentOptions.Center;labels[i/2].rectTransform.sizeDelta=new Vector2(145,30);}
            }
        }
        public void Step(float dt,Vector3 position)
        {
            if(!Initialized||dt<=0||Completed)return;Build();
            if(Choice==0)
            {for(int route=1;route<=2;route++)if(Crossed(position,Gate(route,0))){Choice=route;Passed=1;BeachBoatSoundscape.Instance?.PlayInteraction();Call(route==1?"Bám luồng vòng! Giữ khoảng cách, quân địch đang áp sát!":"Đi thẳng! Coi chừng tên phía trước, đổi hướng sớm!");break;}}
            else if(Crossed(position,Gate(Choice,Passed))){Passed++;BeachBoatSoundscape.Instance?.PlayInteraction();if(Completed)Call("Đã qua luồng nhử! Dẫn chúng vào giữa trận địa, quân ta đã sẵn sàng!");}
            previous=position;havePrevious=true;
            for(int r=1;r<=2;r++)for(int side=0;side<2;side++)
            {
                bool visible=Choice==0||Choice==r;flags[(r-1)*2+side].gameObject.SetActive(visible);if(!visible)continue;var at=Gate(r,Choice==0?0:Passed);flags[(r-1)*2+side].position=at+Vector3.right*(side==0?-65:65);
                if(side==0){labels[r-1].text=r==1?"LUỒNG VÒNG\nÍt tên • đường dài":"LUỒNG THẲNG\nNhiều tên • đường ngắn";labels[r-1].transform.rotation=BeachBoatQuestSession.Instance.storyCamera.transform.rotation;}
            }
            if(Completed&&root!=null)root.SetActive(false);
        }
        bool Crossed(Vector3 point,Vector3 gate)
        {
            if(QuestRowboat.FlatDistance(point,gate)<78)return true;if(!havePrevious||(point-previous).sqrMagnitude>250*250)return false;
            var line=point-previous;line.y=0;float t=Mathf.Clamp01(Vector3.Dot(gate-previous,line)/Mathf.Max(.001f,line.sqrMagnitude));return QuestRowboat.FlatDistance(previous+line*t,gate)<78;
        }
        public void Call(string text)
        {
            Warning=text;callUntil=Time.time+5;
            string id=text.StartsWith("Hai luồng")?"route_intro":text.StartsWith("Bám luồng")?"safe_route":text.StartsWith("Đi thẳng")?"fast_route":text.StartsWith("Tên tới")?"arrows":"route_done";CampaignFieldCalls.Say(id);
        }
        public bool ShowingCall=>Time.time<callUntil;
        public void FillSave(BachDangSaveData.Fleet data){data.routeVersion=1;data.baitRoute=Choice;data.routePassed=Passed;data.routeInitialized=Initialized;data.routeOrigin=Origin;}
        public void RestoreSave(BachDangSaveData.Fleet data,bool committed)
        {
            Reset();if(committed){Initialized=true;Choice=2;Passed=2;return;}if(data.routeVersion==0){if(owner.IsPursuing){Initialized=true;Choice=2;Passed=2;}return;}
            Initialized=data.routeInitialized;Choice=Mathf.Clamp(data.baitRoute,0,2);Passed=Mathf.Clamp(data.routePassed,0,Choice==1?3:2);Origin=data.routeOrigin;if(Initialized&&!Completed)Build();
        }
        public void Reset(){Choice=Passed=0;Initialized=havePrevious=false;Warning="";callUntil=0;if(root!=null)root.SetActive(false);}
        public void Dispose(){if(root!=null)Object.Destroy(root);if(safeMaterial!=null)Object.Destroy(safeMaterial);if(fastMaterial!=null)Object.Destroy(fastMaterial);}
    }
}
