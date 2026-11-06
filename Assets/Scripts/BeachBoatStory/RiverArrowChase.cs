using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>Steer the real rowboat away from telegraphed, non-homing arrow volleys.</summary>
    public sealed class RiverArrowChase
    {
        public const int Goal=5;public const float Damage=16,WarningSeconds=1.2f,FlightSeconds=.7f;
        readonly RiverFleetPursuit pursuit;GameObject root;LineRenderer[] warnings;
        Transform[] fills,arrows;
        bool[] blocked;
        Material red,redFill,wood;
        GUIStyle dangerStyle;
        readonly Vector3[] corners=new Vector3[5];
        public int Volleys {get;private set;}public int Resolved {get;private set;}public int Hits {get;private set;}
        public bool Completed=>Resolved>=Goal;public bool Pending {get;private set;}public Vector3 Landing {get;private set;}
        bool started;float startZ,elapsed,nextVolley,shotTime;Vector3 from;
        public RiverArrowChase(RiverFleetPursuit owner){pursuit=owner;}
        public void Reset(){Volleys=Resolved=Hits=0;started=Pending=false;startZ=elapsed=nextVolley=shotTime=0;if(root!=null)root.SetActive(false);if(arrows!=null)foreach(var a in arrows)a.gameObject.SetActive(false);}
        void Build()
        {
            if(root!=null){root.SetActive(true);return;}
            root=new GameObject("Nhu_dich_ne_ten_3D");root.transform.SetParent(pursuit.transform,false);
            red=new Material(Shader.Find("Universal Render Pipeline/Unlit")){color=new Color(.95f,.18f,.08f)};wood=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.33f,.22f,.1f)};
            redFill=new Material(Shader.Find("Sprites/Default")){color=new Color(1,.1f,.04f,.42f),renderQueue=3100};
            warnings=new LineRenderer[3];
            fills=new Transform[warnings.Length];
            for(int i=0;i<warnings.Length;i++)
            {
                var warning=new GameObject("Vung_ten_sap_roi_"+i).AddComponent<LineRenderer>();
                warning.transform.SetParent(root.transform,false);
                warning.useWorldSpace=true;
                warning.sharedMaterial=red;
                warning.widthMultiplier=2;
                warning.positionCount=5;
                warnings[i]=warning;

                var fill=GameObject.CreatePrimitive(PrimitiveType.Quad);
                fill.name="Mat_nuoc_vung_ten_"+i;
                fill.transform.SetParent(warning.transform,false);
                fill.transform.rotation=Quaternion.Euler(90,0,0);
                var collider=fill.GetComponent<Collider>();
                collider.enabled=false;
                Object.Destroy(collider);
                var renderer=fill.GetComponent<Renderer>();
                renderer.sharedMaterial=redFill;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                fills[i]=fill.transform;
            }
            arrows=new Transform[21];blocked=new bool[21];
            for(int i=0;i<arrows.Length;i++){var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Ten_truy_duoi_"+i;go.transform.SetParent(root.transform,false);go.transform.localScale=new Vector3(.15f,3,.15f);go.GetComponent<Collider>().enabled=false;Object.Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=wood;arrows[i]=go.transform;go.SetActive(false);}
        }
        public void BeginVolley()
        {
            if(Pending||Completed||Volleys>=Goal)return;Build();var boat=QuestRowboat.Instance;
            Landing=boat.transform.position+boat.transform.forward*boat.Speed*(Telegraph+FlightSeconds);Landing=new Vector3(Landing.x,TideSystem.CurrentWaterHeight+3,Landing.z);pursuit.Routes.Call("Tên tới! Bẻ lái ra khỏi chỗ tên sắp rơi!");
            from=Landing+Vector3.forward*250+Vector3.up*90;float nearest=float.PositiveInfinity;
            foreach(var ship in EnemyFleetDirector.Instance.enemyShips)if(ship!=null&&ship.gameObject.activeInHierarchy&&!ship.isDestroyed){float d=QuestRowboat.FlatDistance(ship.transform.position,boat.transform.position);if(d<nearest){nearest=d;from=ship.transform.position+Vector3.up*65;}}
            shotTime=0;Pending=true;Volleys++;nextVolley=elapsed+(pursuit.Routes.Choice==1?4.3f:2.6f);System.Array.Clear(blocked,0,blocked.Length);foreach(var a in arrows)a.gameObject.SetActive(false);UpdateWarnings();BeachBoatSoundscape.Instance?.PlayBow(true);
        }
        public int WarningCount=>pursuit.Routes.Choice==1?1:pursuit.Routes.Choice==2?3:2;
        public float Telegraph=>pursuit.Routes.Choice==1?1.65f:WarningSeconds;
        float Width{get{var h=BoatHullCollision.Get(QuestRowboat.Instance.transform).SolidHull;return Mathf.Max(8,h.size.x*Mathf.Abs(h.transform.lossyScale.x)*.55f);}}
        public Vector3 WarningCenter(int index)=>Landing+(index==0?Vector3.zero:new Vector3((index==1?1:-1)*(Volleys%2==0?1:-1)*(Width*2+35),0,index==2?100:12));
        Vector3 End(int i)=>WarningCenter(i/7)+new Vector3((i%7%3-1)*6,0,(i%7/3-1)*5);
        public void Step(float dt)
        {
            if(dt<=0||Completed||BachDangFrontEnd.IsBlocking||BeachBoatQuestSession.InteractionBlocked)return;
            var boat=QuestRowboat.Instance;if(!boat.IsAboard||boat.IsBroken)return;
            if(!started){started=true;startZ=boat.transform.position.z;}
            elapsed+=dt;float span=Mathf.Max(100,startZ-RiverStakeZone.Instance.FieldBounds.center.z);float progress=Mathf.Clamp01((startZ-boat.transform.position.z)/span);
            if(!Pending&&Volleys<Goal&&elapsed>=nextVolley&&progress>=Volleys*.18f)BeginVolley();
            if(!Pending)return;Build();shotTime+=dt;
            var hull=BoatHullCollision.Get(boat.transform).SolidHull;
            UpdateWarnings();
            float t=Mathf.Clamp01((shotTime-Telegraph)/FlightSeconds);
            for(int i=0;i<arrows.Length;i++)
            {
                bool flying=i/7<WarningCount&&shotTime>=Telegraph&&!blocked[i];var end=End(i);var point=Vector3.Lerp(from+Vector3.right*(i-3)*3,end,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*20);
                if(flying&&arrows[i].gameObject.activeSelf&&Physics.Linecast(arrows[i].position,point,out var hit,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(boat.transform)&&hit.collider.GetComponentInParent<EnemyShip>()==null){blocked[i]=true;flying=false;}
                arrows[i].gameObject.SetActive(flying);arrows[i].SetPositionAndRotation(point,Quaternion.FromToRotation(Vector3.up,end-from));
            }
            if(shotTime<Telegraph+FlightSeconds)return;
            bool struck=false;for(int i=0;i<arrows.Length;i++)if(i/7<WarningCount&&!blocked[i]&&Vector3.Distance(hull.ClosestPoint(End(i)),End(i))<2){struck=true;break;}
            if(struck&&boat.TakeChaseArrow(Damage))Hits++;Resolved++;Pending=false;root.SetActive(false);
        }
        void UpdateWarnings()
        {
            float width=Width;
            corners[0]=corners[4]=new Vector3(-width,0,-13);
            corners[1]=new Vector3(width,0,-13);
            corners[2]=new Vector3(width,0,13);
            corners[3]=new Vector3(-width,0,13);
            for(int i=0;i<warnings.Length;i++)
            {
                bool visible=i<WarningCount;
                warnings[i].gameObject.SetActive(visible);
                if(!visible)continue;
                var center=WarningCenter(i);
                center.y=TideSystem.CurrentWaterHeight+.75f;
                for(int j=0;j<corners.Length;j++)warnings[i].SetPosition(j,center+corners[j]);
                fills[i].position=center-Vector3.up*.15f;
                fills[i].localScale=new Vector3(width*2,26,1);
            }
        }
        public void FillSave(BachDangSaveData.Fleet data)
        {data.baitVersion=1;data.baitVolleys=Volleys;data.baitResolved=Resolved;data.baitHits=Hits;data.baitStarted=started;data.baitPending=Pending;data.baitStartZ=startZ;data.baitElapsed=elapsed;data.baitDelay=Mathf.Max(0,nextVolley-elapsed);data.baitTime=shotTime;data.baitTarget=Landing;data.baitFrom=from;}
        public void RestoreSave(BachDangSaveData.Fleet data,bool committed)
        {
            Reset();if(committed){Volleys=Resolved=Goal;return;}
            if(data.baitVersion==0){if(pursuit.Stage==RiverFleetPursuit.LureStage.Pursuit&&QuestRowboat.Instance.transform.position.z-RiverStakeZone.Instance.FieldBounds.center.z<350)Volleys=Resolved=Goal;return;}
            Volleys=Mathf.Clamp(data.baitVolleys,0,Goal);Resolved=Mathf.Clamp(data.baitResolved,0,Volleys);Hits=Mathf.Clamp(data.baitHits,0,Resolved);started=data.baitStarted;startZ=data.baitStartZ;elapsed=data.baitElapsed;nextVolley=elapsed+Mathf.Max(0,data.baitDelay);Landing=data.baitTarget;from=data.baitFrom;
            Pending=data.baitPending&&Volleys>Resolved;if(Pending){Build();shotTime=Mathf.Clamp(data.baitTime,0,Telegraph+FlightSeconds-.01f);UpdateWarnings();}
        }
        public void Draw()
        {
            if(Event.current.type!=EventType.Repaint||pursuit.Stage!=RiverFleetPursuit.LureStage.Pursuit||
                EnemyFleetDirector.Instance?.BattleStarted!=true||BachDangFrontEnd.IsBlocking||RiverInvasionIntro.IsRunning||
                QuestRowboat.Instance?.IsAboard!=true)return;

            pursuit.Routes.DrawMarkers();
            if(!Pending)return;

            float unit=Mathf.Max(.65f,Mathf.Min(Screen.height/720f,Screen.width/900f));
            if(dangerStyle==null)
                dangerStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
            dangerStyle.fontSize=Mathf.RoundToInt(23*unit);
            var oldColor=GUI.color;
            var badge=new Rect(Screen.width*.5f-85*unit,Screen.height*.14f,170*unit,42*unit);
            var danger=new Color(1,.36f,.23f);
            GUI.color=new Color(.22f,.035f,.02f,.96f);
            GUI.DrawTexture(badge,Texture2D.whiteTexture);
            GUI.color=danger;
            float remaining=1-Mathf.Clamp01(shotTime/(Telegraph+FlightSeconds));
            GUI.DrawTexture(new Rect(badge.x,badge.yMax-4*unit,badge.width*remaining,4*unit),Texture2D.whiteTexture);
            GUI.Label(badge,"!   NÉ TÊN",dangerStyle);
            GUI.color=oldColor;
        }
        public void Dispose(){if(root!=null)Object.Destroy(root);if(red!=null)Object.Destroy(red);if(redFill!=null)Object.Destroy(redFill);if(wood!=null)Object.Destroy(wood);}
    }
}
