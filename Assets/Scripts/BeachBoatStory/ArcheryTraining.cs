using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>A physical camp range using the same drawn bow and travelling arrows as battle.</summary>
    [DefaultExecutionOrder(-70)]
    public sealed class ArcheryTraining : MonoBehaviour
    {
        public static ArcheryTraining Instance {get;private set;}
        public static ArcheryTraining Active {get;private set;}
        public int Shots {get;private set;}public int Score {get;private set;}public int Hits {get;private set;}
        public int BestScore {get;private set;}public bool Completed {get;private set;}
        public int ResolvedShots {get;private set;}
        public bool Finished=>Shots>=6&&ResolvedShots==6&&Time.time-lastShot>.35f;
        public bool CanFire=>Active==this&&Shots<6&&NextTarget.IsReady;
        public string Grade=>Score>=50?"S":Score>=40?"A":Score>=25?"B":"C";
        public string LastFeedback {get;private set;}="";
        public int ShotScore(int index)=>index>=0&&index<results.Length?results[index]:-1;
        public float RoundSeconds=>(resultShown?endedAt:Time.time)-beganAt;
        public Canvas HudCanvas=>hud?.Canvas;
        public Vector3 Approach=>origin-forward*8;
        public Transform GuideTarget {get;private set;}
        public bool Available=>built&&BachDangStoryManager.Instance!=null&&BachDangStoryManager.Instance.currentState!=BachDangStoryManager.StoryState.Act3_BattleInProgress&&BachDangStoryManager.Instance.currentState!=BachDangStoryManager.StoryState.Act3_Victory&&BachDangStoryManager.Instance.currentState!=BachDangStoryManager.StoryState.Act3_Celebration&&(BachDangStoryManager.Instance.currentState!=BachDangStoryManager.StoryState.Act3_WaitForLowTide||QuestRowboat.Instance?.IsAboard!=true);
        public bool Required=>!Completed&&(BachDangStoryManager.Instance?.currentState==BachDangStoryManager.StoryState.Act2_Completed||BachDangStoryManager.Instance?.currentState==BachDangStoryManager.StoryState.Act3_PrepareForBattle||BachDangStoryManager.Instance?.currentState==BachDangStoryManager.StoryState.Act3_WaitForLowTide);
        public ArcheryPracticeTarget[] Targets {get;private set;}
        public ArcheryPracticeTarget NextTarget=>Targets==null?null:Targets[order[Mathf.Min(5,Shots)]];
        readonly int[] order={0,1,2,0,2,1};
        readonly int[] results=new int[6];
        readonly List<Material> owned=new List<Material>();readonly List<GameObject> embedded=new List<GameObject>();
        Transform player;Vector3 origin,forward;float lastShot;TextMeshPro sign;Material straw,red,white,wood,gold;bool built;
        int priorArrows;float priorDistance,priorHeight,priorRight;Camera_Script orbit;
        float beganAt,endedAt;bool resultShown;ArcheryTrainingHUD hud;
        Vector3 LanePoint(Vector3 p){var delta=p-origin;return new Vector3(Vector3.Dot(delta,Vector3.Cross(Vector3.up,forward)),0,Vector3.Dot(delta,forward));}
        Bounds LaneBounds(float margin)=>new Bounds(new Vector3(0,0,55),new Vector3(72+margin*2,2,190+margin*2));
        public bool ContainsLane(Vector3 p,float margin=0)=>built&&LaneBounds(margin).Contains(LanePoint(p));
        public bool BlocksNPCPath(Vector3 from,Vector3 to,float margin)
        {
            if(!built)return false;var box=LaneBounds(margin);var a=LanePoint(from);var b=LanePoint(to);
            float Depth(Vector3 p)=>Mathf.Min(box.max.x-p.x,p.x-box.min.x,box.max.z-p.z,p.z-box.min.z);
            // An actor caught inside by a checkpoint may walk out, but never farther in.
            if(box.Contains(a))return Depth(b)>=Depth(a)-.0001f;
            var delta=b-a;return delta.sqrMagnitude>.00001f&&box.IntersectRay(new Ray(a,delta.normalized),out float distance)&&distance<=delta.magnitude;
        }
        void Awake(){Instance=this;}
        Material Make(string name,Color c){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=c};m.SetFloat("_Smoothness",.08f);owned.Add(m);return m;}
        float Ground(Vector3 p)=>Terrain.activeTerrain.SampleHeight(p)+Terrain.activeTerrain.transform.position.y;
        System.Collections.IEnumerator Start()
        {yield return null;yield return null;BuildRange();}
        void BuildRange()
        {
            var camp=GameObject.Find("TestQuest_Camp");var center=camp!=null?camp.transform.Find("CampLayoutOrigin"):null;
            if(center==null||Terrain.activeTerrain==null)return;
            player=BeachBoatQuestSession.Instance.player.transform;orbit=BeachBoatQuestSession.Instance.storyCamera.GetComponent<Camera_Script>();
            int best=int.MaxValue;
            foreach(int x in new[]{85,-100,140,-150})foreach(int z in new[]{-150,-220,-290})foreach(var dir in new[]{Vector3.back,Vector3.right,Vector3.left})
            {
                var p=center.position+new Vector3(x,0,z);p.y=Ground(p);
                var end=p+dir*135;if(Ground(p)<TideSystem.CurrentWaterHeight+2||Ground(end)<TideSystem.CurrentWaterHeight+2||Mathf.Abs(Ground(end)-p.y)>10)continue;
                bool clear=true;var firing=p-dir*8+Vector3.up*17.2f;var right=Vector3.Cross(Vector3.up,dir);
                for(int i=0;i<3&&clear;i++)
                {
                    var aim=p+dir*(65+i*30)+right*(i-1)*17;aim.y=Ground(aim)+14;
                    if(aim.y<TideSystem.CurrentWaterHeight+16){clear=false;break;}
                    var line=aim-firing;
                    clear=!Physics.SphereCastAll(new Ray(firing,line.normalized),.6f,line.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(h=>!h.transform.IsChildOf(player)&&!h.transform.IsChildOf(transform));
                }
                if(!clear)continue;
                var box=Physics.OverlapBox(p+dir*65+Vector3.up*15,new Vector3(22,13,70),Quaternion.LookRotation(dir),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                int blocked=box.Count(c=>!(c is TerrainCollider)&&!c.transform.IsChildOf(player)&&!c.transform.IsChildOf(transform));
                if(blocked>=best)continue;best=blocked;origin=p;forward=dir;
            }
            if(best==int.MaxValue){Debug.LogWarning("No dry camp archery lane found.");return;}
            straw=Make("Range straw",new Color(.53f,.40f,.19f));red=Make("Range red",new Color(.55f,.08f,.04f));white=Make("Range linen",new Color(.8f,.73f,.54f));wood=Make("Range timber",new Color(.2f,.12f,.06f));gold=Make("Range selected target",new Color(1,.7f,.12f));
            Targets=new ArcheryPracticeTarget[3];var side=Vector3.Cross(Vector3.up,forward);
            for(int i=0;i<3;i++)
            {
                var p=origin+forward*(65+i*30)+side*(i-1)*17;p.y=Ground(p);
                var root=new GameObject("Bia_Rom_"+(i+1));root.transform.SetParent(transform);root.transform.SetPositionAndRotation(p+Vector3.up*14,Quaternion.LookRotation(-forward));
                var target=root.AddComponent<ArcheryPracticeTarget>();target.index=i;Targets[i]=target;
                Part("Target_leg",root.transform,new Vector3(0,-7,1),new Vector3(1.2f,14,1.2f),wood,PrimitiveType.Cube,true);
                for(int ring=0;ring<4;ring++)
                {
                    float size=10-ring*2.6f;var disc=Part("Woven_target_ring_"+ring,root.transform,new Vector3(0,0,ring*.4f),new Vector3(size,.3f,size),ring==0?straw:ring==1?white:ring==2?red:gold,PrimitiveType.Cylinder,ring==0);
                    disc.transform.localRotation=Quaternion.Euler(90,0,0);
                    if(ring==0)target.surface=disc.GetComponent<Renderer>();
                }
                var halo=new GameObject("Vong_bia_dang_chon");halo.transform.SetParent(root.transform,false);var line=halo.AddComponent<LineRenderer>();line.sharedMaterial=gold;line.useWorldSpace=false;line.widthMultiplier=.25f;line.positionCount=49;
                for(int n=0;n<49;n++){float a=n/48f*Mathf.PI*2;line.SetPosition(n,new Vector3(Mathf.Cos(a)*5.6f,Mathf.Sin(a)*5.6f,1.5f));}target.highlight=halo;target.Initialize();
            }
            var board=new GameObject("Huong_dan_ban_cung");board.transform.SetParent(transform);board.transform.SetPositionAndRotation(origin+Vector3.up*10,Quaternion.LookRotation(forward));
            sign=board.AddComponent<TextMeshPro>();StoryFont.Apply(sign);sign.fontSize=10;sign.alignment=TextAlignmentOptions.Center;sign.rectTransform.sizeDelta=new Vector2(65,24);sign.color=new Color(1,.82f,.35f);
            GuideTarget=new GameObject("Chi_dan_bai_tap_cung").transform;GuideTarget.SetParent(transform,true);GuideTarget.position=Approach;
            built=true;Physics.SyncTransforms();
            var circle=new GameObject("Vi_tri_dung_tap_cung").AddComponent<LineRenderer>();circle.transform.SetParent(transform,false);circle.sharedMaterial=gold;circle.useWorldSpace=true;circle.widthMultiplier=.4f;circle.positionCount=49;
            for(int n=0;n<49;n++){float a=n/48f*Mathf.PI*2;var p=Approach+new Vector3(Mathf.Cos(a)*7,.25f,Mathf.Sin(a)*7);p.y=Ground(p)+.25f;circle.SetPosition(n,p);}
            foreach(int flank in new[]{-1,1})
            {
                var rope=new GameObject("Ranh_gioi_lan_ban").AddComponent<LineRenderer>();rope.transform.SetParent(transform,false);rope.sharedMaterial=wood;rope.useWorldSpace=true;rope.widthMultiplier=.3f;rope.positionCount=9;
                for(int i=0;i<9;i++){var p=origin+side*(flank*36)+forward*(-30+i*22);p.y=Ground(p)+3;rope.SetPosition(i,p);Part("Cot_day_thao_truong",transform,transform.InverseTransformPoint(p-Vector3.up),new Vector3(.8f,4,.8f),wood,PrimitiveType.Cube,false);}
            }
            // Reserve the lane before play starts, then normal routes go around it.
            foreach(var walker in FindObjectsByType<NPCWander>(FindObjectsSortMode.None))walker.ClearRangeAtSetup();
            hud=new ArcheryTrainingHUD(this);Physics.SyncTransforms();
        }
        GameObject Part(string name,Transform parent,Vector3 p,Vector3 scale,Material m,PrimitiveType type,bool solid)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=m;if(!solid){go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());}else if(type==PrimitiveType.Cylinder){go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());go.AddComponent<MeshCollider>().sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;}return go;}
        public bool Begin()
        {
            if(!Available||Active!=null||BeachBoatQuestSession.InteractionBlocked||QuestRowboat.FlatDistance(player.position,Approach)>24||BeachBoatMinigameStation.Active!=null)return false;
            Active=this;Shots=Score=Hits=ResolvedShots=0;lastShot=0;beganAt=Time.time;endedAt=0;resultShown=false;LastFeedback="Bắn bia có vòng vàng • Trúng ít nhất 4/6";for(int i=0;i<results.Length;i++)results[i]=-1;
            foreach(var target in Targets)target.ResetPose();priorArrows=RiverPlayerCombat.Instance.Arrows;priorDistance=orbit.distance;priorHeight=orbit.heightOffset;priorRight=orbit.rightOffset;
            foreach(var go in embedded)if(go!=null)Destroy(go);embedded.Clear();
            player.rotation=Quaternion.LookRotation(forward);var body=player.GetComponent<Rigidbody>();if(body!=null)body.rotation=player.rotation;
            orbit.distance=32;orbit.heightOffset=14;orbit.rightOffset=7;orbit.SnapToTarget();orbit.CaptureCursor(true);RiverPlayerCombat.Instance.BeginPractice();hud?.Refresh();return true;
        }
        void Update()
        {
            if(!built)return;
            bool near=QuestRowboat.FlatDistance(player.position,Approach)<24;
            sign.gameObject.SetActive(Active!=this);
            sign.text=near?"BÃI TẬP CUNG\n[E] BẮT ĐẦU • TRÚNG 4/6 BIA":"BÃI TẬP CUNG\nĐẾN VÒNG BẮN PHÍA TRƯỚC";
            sign.transform.rotation=BeachBoatQuestSession.Instance.storyCamera.transform.rotation;
            if(Active==this&&Shots<6&&NextTarget.IsDown&&NextTarget.DownSeconds>.75f)NextTarget.Raise();
            for(int i=0;i<Targets.Length;i++)Targets[i].highlight.SetActive(Active==this&&Shots<6&&i==order[Shots]&&Targets[i].IsReady);
            if(Active==null&&Available&&near&&!BeachBoatQuestSession.InteractionBlocked)
            {
                BachDangStoryManager.Instance.SetInteractPrompt(true,"[E] TẬP BẮN CUNG • 6 mũi, trúng ít nhất 4 bia"+(Required?" • BẮT BUỘC TRƯỚC XUẤT QUÂN":""),this);
                if(Input.GetKeyDown(KeyCode.E)&&StoryDialogueUI.TryConsumeInput())Begin();
            }
            else BachDangStoryManager.Instance.SetInteractPrompt(false,owner:this);
            hud?.Refresh();if(Active!=this)return;
            if(!Available||QuestRowboat.FlatDistance(player.position,Approach)>55){Close();return;}
            if(BeachBoatQuestSession.InteractionBlocked){Close();return;}
            if(Finished)
            {
                if(!resultShown){resultShown=true;endedAt=Time.time;BestScore=Mathf.Max(BestScore,Score);bool prior=Completed;Completed|=Hits>=4;if(Completed&&!prior)BachDangStoryManager.Instance.UpdateUI();RiverPlayerCombat.Instance.SetAim(false);hud?.Refresh();}
                orbit.CaptureCursor(false);if(Input.GetKeyDown(KeyCode.Return))Close();else if(Input.GetKeyDown(KeyCode.R))Replay();
            }
        }
        public Vector3 ResolveShot(Ray ray,out ArcheryPracticeTarget target,out int score)
        {
            target=null;score=0;var end=ray.GetPoint(220);
            foreach(var hit in Physics.SphereCastAll(ray,.25f,220,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                if(hit.transform.IsChildOf(player)||QuestRowboat.Instance!=null&&hit.transform.IsChildOf(QuestRowboat.Instance.transform))continue;
                end=hit.point;target=hit.collider.GetComponentInParent<ArcheryPracticeTarget>();
                if(target!=null&&target.IsReady&&Shots<6&&target.index==order[Shots]){var p=target.transform.InverseTransformPoint(end);float radius=new Vector2(p.x,p.y).magnitude;score=radius<=1.1f?10:radius<=2.4f?7:radius<=5.1f?4:0;}
                return end;
            }
            return end;
        }
        public int RecordShot(){if(!CanFire)return -1;int index=Shots++;results[index]=-2;lastShot=Time.time;return index;}
        public void RegisterShotResult(int index,ArcheryPracticeTarget target,Vector3 point,int points)
        {
            if(Active!=this||index<0||index>=6||results[index]!=-2)return;
            points=target!=null&&target.index==order[index]&&target.IsReady?Mathf.Clamp(points,0,10):0;results[index]=points;ResolvedShots++;
            if(points>0){Score+=points;Hits++;LastFeedback=points==10?"TRÚNG TÂM  +10":$"TRÚNG BIA  +{points}";}
            else LastFeedback=target!=null&&target.index!=order[index]?"NHẦM BIA • Ngắm bia có vòng vàng":"TRƯỢT • Giữ ngắm, kéo dây rồi thả";
            if(target!=null&&target.IsReady)
            {
                var arrow=Part("Ten_cam_bia",target.transform,target.transform.InverseTransformPoint(point),new Vector3(.15f,2.5f,.15f),wood,PrimitiveType.Cylinder,false);arrow.transform.rotation=Quaternion.FromToRotation(Vector3.up,forward);embedded.Add(arrow);
                target.KnockDown();BeachBoatSoundscape.Instance?.PlayStake();
            }
            hud?.Refresh();
        }
        public void Replay(){Close();Begin();}
        public void Restore(int best,bool done){Close();BestScore=Mathf.Clamp(best,0,60);Completed=done;}
        public void Close()
        {
            if(Active!=this)return;Active=null;RiverPlayerCombat.Instance?.EndPractice();RiverPlayerCombat.Instance?.RestorePracticeAmmo(priorArrows);
            foreach(var target in Targets)target.ResetPose();Physics.SyncTransforms();hud?.Refresh();
            if(orbit!=null){orbit.distance=priorDistance;orbit.heightOffset=priorHeight;orbit.rightOffset=priorRight;orbit.CaptureCursor(!BeachBoatQuestSession.InteractionBlocked);}
        }
        void OnGUI()
        {
            if(!built||BachDangFrontEnd.IsBlocking||CampaignJournalUI.IsOpen)return;
            if(Active!=this)
            {
                if(!Available||QuestRowboat.FlatDistance(player.position,Approach)>100)return;
                var label=new GUIStyle(GUI.skin.box){fontSize=Mathf.Max(17,Mathf.RoundToInt(Screen.height*.026f)),alignment=TextAnchor.MiddleCenter,wordWrap=true};
                float metres=QuestRowboat.FlatDistance(player.position,Approach)/Mathf.Max(1,BeachBoatQuestSession.Instance.WorldUnitsPerMetre);
                GUI.Box(new Rect(Screen.width*.28f,Screen.height*.69f,Screen.width*.44f,Screen.height*.1f),(Required?"BẮT BUỘC • ":"")+"TẬP BẮN CUNG\n"+(metres<3?"Bấm E tại vị trí bắn • Trúng ít nhất 4/6 bia":$"Đi tới vị trí bắn • Còn {metres:0} m"),label);return;
            }
        }
        void OnDestroy(){Close();hud?.Dispose();foreach(var m in owned)if(m!=null)Destroy(m);if(Instance==this)Instance=null;}
    }
    public sealed class ArcheryPracticeTarget : MonoBehaviour
    {
        public int index;public Renderer surface;public GameObject highlight;
        public bool IsDown {get;private set;}public bool IsReady=>!IsDown&&angle<.1f;
        public float DownSeconds=>IsDown?Time.time-fellAt:0;
        public float FallAngle=>angle;
        Vector3 pivot;Quaternion standing;float angle,fellAt;bool initialized;
        public void Initialize(){standing=transform.rotation;pivot=transform.position-Vector3.up*14;initialized=true;}
        public void KnockDown(){if(!IsReady)return;IsDown=true;fellAt=Time.time;if(highlight!=null)highlight.SetActive(false);}
        public void Raise(){IsDown=false;}
        public void ResetPose(){if(!initialized)return;IsDown=false;angle=0;ApplyPose();}
        void Update(){if(!initialized)return;float next=Mathf.MoveTowards(angle,IsDown?86:0,Time.deltaTime*(IsDown?230:180));if(Mathf.Approximately(next,angle))return;angle=next;ApplyPose();Physics.SyncTransforms();}
        void ApplyPose(){var rotation=standing*Quaternion.Euler(-angle,0,0);transform.SetPositionAndRotation(pivot+rotation*Vector3.up*14,rotation);}
    }
}
