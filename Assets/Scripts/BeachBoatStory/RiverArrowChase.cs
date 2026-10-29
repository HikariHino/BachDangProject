using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>Steer the real rowboat away from telegraphed, non-homing arrow volleys.</summary>
    public sealed class RiverArrowChase
    {
        public const int Goal=5;public const float Damage=16,WarningSeconds=1.2f,FlightSeconds=.7f;
        readonly RiverFleetPursuit pursuit;GameObject root;LineRenderer warning;Transform[] arrows;bool[] blocked;Material red,wood;
        public int Volleys {get;private set;}public int Resolved {get;private set;}public int Hits {get;private set;}
        public bool Completed=>Resolved>=Goal;public bool Pending {get;private set;}public Vector3 Landing {get;private set;}
        bool started;float startZ,elapsed,nextVolley,shotTime;Vector3 from;string feedback;float feedbackUntil;
        public RiverArrowChase(RiverFleetPursuit owner){pursuit=owner;}
        public void Reset(){Volleys=Resolved=Hits=0;started=Pending=false;startZ=elapsed=nextVolley=shotTime=0;if(root!=null)root.SetActive(false);if(arrows!=null)foreach(var a in arrows)a.gameObject.SetActive(false);}
        void Build()
        {
            if(root!=null){root.SetActive(true);return;}
            root=new GameObject("Nhu_dich_ne_ten_3D");root.transform.SetParent(pursuit.transform,false);
            red=new Material(Shader.Find("Universal Render Pipeline/Unlit")){color=new Color(.95f,.18f,.08f)};wood=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.33f,.22f,.1f)};
            warning=new GameObject("Vung_ten_sap_roi").AddComponent<LineRenderer>();warning.transform.SetParent(root.transform,false);warning.useWorldSpace=true;warning.sharedMaterial=red;warning.widthMultiplier=.55f;warning.positionCount=5;
            arrows=new Transform[7];blocked=new bool[7];
            for(int i=0;i<arrows.Length;i++){var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Ten_truy_duoi_"+i;go.transform.SetParent(root.transform,false);go.transform.localScale=new Vector3(.15f,3,.15f);go.GetComponent<Collider>().enabled=false;Object.Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=wood;arrows[i]=go.transform;go.SetActive(false);}
        }
        public void BeginVolley()
        {
            if(Pending||Completed||Volleys>=Goal)return;Build();var boat=QuestRowboat.Instance;
            Landing=boat.transform.position+boat.transform.forward*boat.Speed*(WarningSeconds+FlightSeconds);Landing=new Vector3(Landing.x,TideSystem.CurrentWaterHeight+3,Landing.z);
            from=Landing+Vector3.forward*250+Vector3.up*90;float nearest=float.PositiveInfinity;
            foreach(var ship in EnemyFleetDirector.Instance.enemyShips)if(ship!=null&&ship.gameObject.activeInHierarchy&&!ship.isDestroyed){float d=QuestRowboat.FlatDistance(ship.transform.position,boat.transform.position);if(d<nearest){nearest=d;from=ship.transform.position+Vector3.up*65;}}
            shotTime=0;Pending=true;Volleys++;nextVolley=elapsed+3.5f;System.Array.Clear(blocked,0,blocked.Length);foreach(var a in arrows)a.gameObject.SetActive(false);warning.gameObject.SetActive(true);BeachBoatSoundscape.Instance?.PlayBow(true);
        }
        Vector3 End(int i)=>Landing+new Vector3((i%3-1)*6,0,(i/3-1)*5);
        public void Step(float dt)
        {
            if(dt<=0||Completed||BachDangFrontEnd.IsBlocking||BeachBoatQuestSession.InteractionBlocked)return;
            var boat=QuestRowboat.Instance;if(!boat.IsAboard||boat.IsBroken)return;
            if(!started){started=true;startZ=boat.transform.position.z;}
            elapsed+=dt;float span=Mathf.Max(100,startZ-RiverStakeZone.Instance.FieldBounds.center.z);float progress=Mathf.Clamp01((startZ-boat.transform.position.z)/span);
            if(!Pending&&Volleys<Goal&&elapsed>=nextVolley&&progress>=Volleys*.18f)BeginVolley();
            if(!Pending)return;Build();shotTime+=dt;
            var hull=BoatHullCollision.Get(boat.transform).SolidHull;float width=Mathf.Max(8,hull.size.x*Mathf.Abs(hull.transform.lossyScale.x)*.55f);
            var corners=new[]{new Vector3(-width,0,-13),new Vector3(width,0,-13),new Vector3(width,0,13),new Vector3(-width,0,13),new Vector3(-width,0,-13)};
            for(int i=0;i<5;i++)warning.SetPosition(i,Landing+corners[i]-Vector3.up*2.7f);warning.gameObject.SetActive(shotTime<WarningSeconds);
            float t=Mathf.Clamp01((shotTime-WarningSeconds)/FlightSeconds);
            for(int i=0;i<arrows.Length;i++)
            {
                bool flying=shotTime>=WarningSeconds&&!blocked[i];var end=End(i);var point=Vector3.Lerp(from+Vector3.right*(i-3)*3,end,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*20);
                if(flying&&arrows[i].gameObject.activeSelf&&Physics.Linecast(arrows[i].position,point,out var hit,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(boat.transform)&&hit.collider.GetComponentInParent<EnemyShip>()==null){blocked[i]=true;flying=false;}
                arrows[i].gameObject.SetActive(flying);arrows[i].SetPositionAndRotation(point,Quaternion.FromToRotation(Vector3.up,end-from));
            }
            if(shotTime<WarningSeconds+FlightSeconds)return;
            bool struck=false;for(int i=0;i<arrows.Length;i++)if(!blocked[i]&&Vector3.Distance(hull.ClosestPoint(End(i)),End(i))<2){struck=true;break;}
            if(struck&&boat.TakeChaseArrow(Damage))Hits++;Resolved++;Pending=false;root.SetActive(false);feedback=struck?"Trúng tên • Đổi hướng sớm khi vùng đỏ xuất hiện":"Đã né loạt tên";feedbackUntil=Time.unscaledTime+2.5f;
        }
        public void FillSave(BachDangSaveData.Fleet data)
        {data.baitVersion=1;data.baitVolleys=Volleys;data.baitResolved=Resolved;data.baitHits=Hits;data.baitStarted=started;data.baitPending=Pending;data.baitStartZ=startZ;data.baitElapsed=elapsed;data.baitDelay=Mathf.Max(0,nextVolley-elapsed);data.baitTime=shotTime;data.baitTarget=Landing;data.baitFrom=from;}
        public void RestoreSave(BachDangSaveData.Fleet data,bool committed)
        {
            Reset();if(committed){Volleys=Resolved=Goal;return;}
            if(data.baitVersion==0){if(pursuit.Stage==RiverFleetPursuit.LureStage.Pursuit&&QuestRowboat.Instance.transform.position.z-RiverStakeZone.Instance.FieldBounds.center.z<350)Volleys=Resolved=Goal;return;}
            Volleys=Mathf.Clamp(data.baitVolleys,0,Goal);Resolved=Mathf.Clamp(data.baitResolved,0,Volleys);Hits=Mathf.Clamp(data.baitHits,0,Resolved);started=data.baitStarted;startZ=data.baitStartZ;elapsed=data.baitElapsed;nextVolley=elapsed+Mathf.Max(0,data.baitDelay);Landing=data.baitTarget;from=data.baitFrom;
            Pending=data.baitPending&&Volleys>Resolved;if(Pending){Build();shotTime=Mathf.Clamp(data.baitTime,0,WarningSeconds+FlightSeconds-.01f);}
        }
        public void Draw()
        {
            if(pursuit.Stage!=RiverFleetPursuit.LureStage.Pursuit||EnemyFleetDirector.Instance?.BattleStarted!=true||BachDangFrontEnd.IsBlocking||RiverInvasionIntro.IsRunning)return;
            var style=new GUIStyle(GUI.skin.box){fontSize=Mathf.RoundToInt(Screen.height*.022f),alignment=TextAnchor.MiddleCenter,wordWrap=true};
            string instruction=Pending?"VÙNG ĐỎ: TÊN SẮP RƠI • A / D đổi hướng để né":Completed?"ĐÃ VƯỢT MƯA TÊN • Tiếp tục dẫn địch vào bãi cọc":"Giữ thuyền chạy về bãi cọc • Né vùng tên đỏ trên mặt nước";
            GUI.Box(new Rect(Screen.width*.29f,Screen.height*.15f,Screen.width*.42f,Screen.height*.09f),$"NHỬ ĐỊCH • Qua {Resolved}/{Goal} loạt tên • Trúng {Hits}\n"+(Time.unscaledTime<feedbackUntil?feedback:instruction),style);
        }
        public void Dispose(){if(root!=null)Object.Destroy(root);if(red!=null)Object.Destroy(red);if(wood!=null)Object.Destroy(wood);}
    }
}
