using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>A physical camp range using the same drawn bow and travelling arrows as battle.</summary>
    public sealed class ArcheryTraining : MonoBehaviour
    {
        public static ArcheryTraining Instance {get;private set;}
        public static ArcheryTraining Active {get;private set;}
        public int Shots {get;private set;}public int Score {get;private set;}public int Hits {get;private set;}
        public int BestScore {get;private set;}public bool Completed {get;private set;}
        public bool Finished=>Shots>=6&&Time.time-lastShot>1;
        public Vector3 Approach=>origin-forward*8;
        public ArcheryPracticeTarget[] Targets {get;private set;}
        public ArcheryPracticeTarget NextTarget=>Targets==null?null:Targets[order[Mathf.Min(5,Shots)]];
        readonly int[] order={0,1,2,0,2,1};
        readonly List<Material> owned=new List<Material>();readonly List<GameObject> embedded=new List<GameObject>();
        Transform player;Vector3 origin,forward;float lastShot;TextMeshPro sign;Material straw,red,white,wood,gold;bool built;
        int priorArrows;float priorDistance,priorHeight,priorRight;Camera_Script orbit;
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
                for(int n=0;n<49;n++){float a=n/48f*Mathf.PI*2;line.SetPosition(n,new Vector3(Mathf.Cos(a)*5.6f,Mathf.Sin(a)*5.6f,1.5f));}target.highlight=halo;
            }
            var board=new GameObject("Huong_dan_ban_cung");board.transform.SetParent(transform);board.transform.SetPositionAndRotation(origin+Vector3.up*10,Quaternion.LookRotation(forward));
            sign=board.AddComponent<TextMeshPro>();StoryFont.Apply(sign);sign.fontSize=4;sign.alignment=TextAlignmentOptions.Center;sign.rectTransform.sizeDelta=new Vector2(40,14);
            built=true;Physics.SyncTransforms();
        }
        GameObject Part(string name,Transform parent,Vector3 p,Vector3 scale,Material m,PrimitiveType type,bool solid)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=m;if(!solid){go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());}else if(type==PrimitiveType.Cylinder){go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());go.AddComponent<MeshCollider>().sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;}return go;}
        public bool Begin()
        {
            if(!built||Active!=null||BeachBoatQuestSession.InteractionBlocked||QuestRowboat.FlatDistance(player.position,Approach)>20||BeachBoatMinigameStation.Active!=null||BachDangStoryManager.Instance.currentState>=BachDangStoryManager.StoryState.Act3_WaitForLowTide)return false;
            Active=this;Shots=Score=Hits=0;lastShot=0;priorArrows=RiverPlayerCombat.Instance.Arrows;priorDistance=orbit.distance;priorHeight=orbit.heightOffset;priorRight=orbit.rightOffset;
            foreach(var go in embedded)if(go!=null)Destroy(go);embedded.Clear();
            orbit.distance=32;orbit.heightOffset=14;orbit.rightOffset=7;RiverPlayerCombat.Instance.BeginPractice();return true;
        }
        void Update()
        {
            if(!built)return;
            bool near=QuestRowboat.FlatDistance(player.position,Approach)<20;
            sign.gameObject.SetActive(Active!=this);
            sign.text=Active==this?"LUYỆN CUNG • BẮN BIA ĐANG SÁNG":near?"LUYỆN CUNG 3D\n[E] 6 mũi tên • Kỷ lục "+BestScore+"/60":"BÃI TẬP CUNG";
            sign.transform.rotation=BeachBoatQuestSession.Instance.storyCamera.transform.rotation;
            for(int i=0;i<Targets.Length;i++)Targets[i].highlight.SetActive(Active==this&&Shots<6&&i==order[Shots]);
            if(Active==null&&near&&Input.GetKeyDown(KeyCode.E))Begin();
            if(Active!=this)return;
            if(BachDangStoryManager.Instance.currentState>=BachDangStoryManager.StoryState.Act3_WaitForLowTide||QuestRowboat.FlatDistance(player.position,Approach)>55){Close();return;}
            if(BeachBoatQuestSession.InteractionBlocked){Close();return;}
            if(Finished){BestScore=Mathf.Max(BestScore,Score);Completed|=Hits>=4;if(Input.GetKeyDown(KeyCode.Return))Close();else if(Input.GetKeyDown(KeyCode.R)){Close();Begin();}}
        }
        public Vector3 ResolveShot(Ray ray,out ArcheryPracticeTarget target,out int score)
        {
            target=null;score=0;var end=ray.GetPoint(220);
            foreach(var hit in Physics.SphereCastAll(ray,.25f,220,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                if(hit.transform.IsChildOf(player)||QuestRowboat.Instance!=null&&hit.transform.IsChildOf(QuestRowboat.Instance.transform))continue;
                end=hit.point;target=hit.collider.GetComponentInParent<ArcheryPracticeTarget>();
                if(target!=null&&Shots<6&&target.index==order[Shots]){var p=target.transform.InverseTransformPoint(end);float radius=new Vector2(p.x,p.y).magnitude;score=radius<=1.1f?10:radius<=2.4f?7:radius<=5.1f?4:0;}
                return end;
            }
            return end;
        }
        public void RecordShot(){if(Active==this&&Shots<6){Shots++;lastShot=Time.time;}}
        public void RegisterHit(ArcheryPracticeTarget target,Vector3 point,int points)
        {
            if(Active!=this||target==null)return;if(points>0){Score+=points;Hits++;BeachBoatSoundscape.Instance?.PlayStake();}
            var arrow=Part("Ten_cam_bia",target.transform,target.transform.InverseTransformPoint(point),new Vector3(.15f,2.5f,.15f),wood,PrimitiveType.Cylinder,false);arrow.transform.rotation=Quaternion.FromToRotation(Vector3.up,forward);embedded.Add(arrow);
        }
        public void Restore(int best,bool done){Close();BestScore=Mathf.Clamp(best,0,60);Completed=done;}
        public void Close()
        {
            if(Active!=this)return;Active=null;RiverPlayerCombat.Instance?.EndPractice();RiverPlayerCombat.Instance?.RestorePracticeAmmo(priorArrows);
            if(orbit!=null){orbit.distance=priorDistance;orbit.heightOffset=priorHeight;orbit.rightOffset=priorRight;}
        }
        void OnGUI()
        {
            if(Active!=this||BachDangFrontEnd.IsBlocking||CampaignJournalUI.IsOpen)return;
            var style=new GUIStyle(GUI.skin.box){fontSize=Mathf.RoundToInt(Screen.height*.023f),wordWrap=true,alignment=TextAnchor.MiddleCenter};
            string text=Finished?$"LUYỆN CUNG • {Score}/60 điểm • Trúng {Hits}/6 • {(Hits>=4?"ĐẠT":"CẦN LUYỆN THÊM")}\nR: tập lại • ENTER / ESC: rời bãi tập":$"LUYỆN CUNG • {Shots}/6 mũi • {Score} điểm • Bia {order[Mathf.Min(5,Shots)]+1}\nGiữ chuột phải ngắm • Giữ chuột trái kéo dây, THẢ để bắn • ESC: rời bãi tập";
            GUI.Box(new Rect(Screen.width*.2f,Screen.height*.82f,Screen.width*.6f,Screen.height*.12f),text,style);
        }
        void OnDestroy(){Close();foreach(var m in owned)if(m!=null)Destroy(m);if(Instance==this)Instance=null;}
    }
    public sealed class ArcheryPracticeTarget : MonoBehaviour {public int index;public Renderer surface;public GameObject highlight;}
}
