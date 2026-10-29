using System.Collections.Generic;
using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>Carry a sandbag through physical gates, then return it to the drill station.</summary>
    public sealed class PhysicalTrainingCourse
    {
        public static bool AllowsMovement=>CampRecoveryGame.Active?.Course!=null&&CampRecoveryGame.Active.Phase==CampRecoveryGame.GamePhase.Playing&&!BachDangFrontEnd.IsBlocking;
        readonly CampRecoveryGame game;readonly Transform player;readonly GameObject root,sack;readonly List<GameObject> gates=new List<GameObject>();readonly Material timber,gold,green,cloth;
        readonly Vector3 home;readonly List<Vector3> route=new List<Vector3>();bool carrying,disposed,clearedBar;int gate;Vector3 previous;float travel;
        public bool Carrying=>carrying;public Vector3 Target=>!carrying||game.Progress>=game.Goal-1?home:route[gate%route.Count];
        public int PassedGates=>gate;
        public PhysicalTrainingCourse(CampRecoveryGame owner)
        {
            game=owner;player=BeachBoatQuestSession.Instance.player.transform;home=owner.TrainingHome;previous=player.position;root=new GameObject("Strength_3D_Course");
            Material M(string name,Color c)=>new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=c};
            timber=M("Training timber",new Color(.28f,.19f,.1f));gold=M("Training next gate",new Color(.95f,.72f,.28f));green=M("Training gate",new Color(.2f,.5f,.35f));cloth=M("Sandbag linen",new Color(.57f,.48f,.3f));
            for(int i=0;i<3;i++)
            {
                bool found=false;Vector3 p=home;
                for(int attempt=0;attempt<48;attempt++)
                {
                    float a=(i*120+attempt*7.5f)*Mathf.Deg2Rad;p=home+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(28+attempt/12*8);var terrain=Terrain.activeTerrain;p.y=terrain.SampleHeight(p)+terrain.transform.position.y;
                    if(p.y<=TideSystem.CurrentWaterHeight+2||ArcheryTraining.Instance?.ContainsLane(p,10)==true)continue;
                    bool blocked=false;foreach(var c in Physics.OverlapBox(p+Vector3.up*12,new Vector3(11,10,8),Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))if(!(c is TerrainCollider)&&!c.transform.IsChildOf(player)){blocked=true;break;}
                    if(!blocked&&route.TrueForAll(q=>QuestRowboat.FlatDistance(q,p)>18)){found=true;break;}
                }
                if(!found){Dispose();throw new System.InvalidOperationException("No clear ground for the strength course");}
                var from=route.Count>0?route[route.Count-1]:home;route.Add(p);var marker=new GameObject("Cong_vac_bao_"+(i+1));marker.transform.SetParent(root.transform);marker.transform.position=p;var facing=p-from;facing.y=0;marker.transform.rotation=Quaternion.LookRotation(facing);gates.Add(marker);
                Part("Pole_left",marker.transform,new Vector3(-8,15,0),new Vector3(.8f,30,.8f),timber,true);Part("Pole_right",marker.transform,new Vector3(8,15,0),new Vector3(.8f,30,.8f),timber,true);
                Part("Gate_banner",marker.transform,new Vector3(0,31,0),new Vector3(16,2,.5f),green,false);
                Part("Low_step",marker.transform,new Vector3(0,1.5f,0),new Vector3(13,3,2.5f),timber,true);
            }
            Part("Training_return_pad",root.transform,home-root.transform.position+Vector3.up*.15f,new Vector3(9,.3f,9),gold,false);
            sack=Part("Training_sandbag",root.transform,Vector3.zero,new Vector3(6,4,4),cloth,false);sack.transform.position=home+player.forward*4+Vector3.up*2;Physics.SyncTransforms();
        }
        GameObject Part(string name,Transform parent,Vector3 p,Vector3 size,Material material,bool solid)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;if(!solid){go.GetComponent<Collider>().enabled=false;Object.Destroy(go.GetComponent<Collider>());}return go;}
        public bool Interact()
        {
            if(disposed||CampRecoveryGame.Active!=game||game.Phase!=CampRecoveryGame.GamePhase.Playing)return false;
            if(!carrying&&QuestRowboat.FlatDistance(player.position,home)<12){carrying=true;previous=player.position;BeachBoatSoundscape.Instance?.PlayInteraction();return true;}
            if(carrying&&game.Progress>=game.Goal-1&&QuestRowboat.FlatDistance(player.position,home)<12){game.TrainingCheckpoint();return true;}return false;
        }
        public void Tick()
        {
            if(disposed||BachDangFrontEnd.IsBlocking||game.Phase!=CampRecoveryGame.GamePhase.Playing)return;
            if(Input.GetKeyDown(KeyCode.E))Interact();
            if(!carrying)return;
            float moved=QuestRowboat.FlatDistance(previous,player.position);previous=player.position;if(moved<8)travel+=moved;
            for(int i=0;i<gates.Count;i++)gates[i].transform.Find("Gate_banner").GetComponent<Renderer>().sharedMaterial=i==gate%route.Count?gold:green;
            var activeGate=gates[gate%route.Count].transform;var local=activeGate.InverseTransformPoint(player.position);var capsule=player.GetComponent<CapsuleCollider>();float feet=player.position.y+(capsule.center.y-capsule.height*.5f)*player.lossyScale.y;
            if(Mathf.Abs(local.x)<5&&Mathf.Abs(local.z)<7&&feet-activeGate.position.y>3.05f)clearedBar=true;
            if(game.Progress<game.Goal-1&&travel>15&&clearedBar&&Mathf.Abs(local.x)<5&&local.z>4&&local.z<12)
            {gate++;travel=0;clearedBar=false;game.TrainingCheckpoint();BeachBoatSoundscape.Instance?.PlayInteraction();}
            var rig=player.GetComponentInChildren<Animator>();var chest=rig.GetBoneTransform(HumanBodyBones.Chest);sack.transform.SetPositionAndRotation(chest.position+player.forward*5-Vector3.up,player.rotation);
        }
        public void Pose()
        {
            if(!carrying||disposed)return;var rig=player.GetComponentInChildren<Animator>();var p=sack.transform.position;
            QuestRowboat.SolveArm(rig.GetBoneTransform(HumanBodyBones.RightUpperArm),rig.GetBoneTransform(HumanBodyBones.RightLowerArm),rig.GetBoneTransform(HumanBodyBones.RightHand),p+player.right*2,rig.GetBoneTransform(HumanBodyBones.RightUpperArm).position+player.right*4);
            QuestRowboat.SolveArm(rig.GetBoneTransform(HumanBodyBones.LeftUpperArm),rig.GetBoneTransform(HumanBodyBones.LeftLowerArm),rig.GetBoneTransform(HumanBodyBones.LeftHand),p-player.right*2,rig.GetBoneTransform(HumanBodyBones.LeftUpperArm).position-player.right*4);
        }
        public void Draw()
        {
            if(disposed)return;var style=new GUIStyle(GUI.skin.box){fontSize=20,alignment=TextAnchor.MiddleCenter,wordWrap=true};
            GUI.Box(new Rect(Screen.width*.26f,Screen.height*.79f,Screen.width*.48f,Screen.height*.16f),$"VÁC BAO VƯỢT CỔNG • Cấp {game.TrainingLevel}/3 • {game.Progress}/{game.Goal}\nCòn {Mathf.Max(0,game.TimeLimit-game.Elapsed):0}s • "+(!carrying?"[E] Nhấc bao cát":game.Progress>=game.Goal-1?"Trở lại điểm xuất phát • [E] đặt bao":"WASD tới cổng vàng • SPACE nhảy qua thanh gỗ")+"\nESC: rời bài tập",style);
        }
        public void Dispose(){if(disposed)return;disposed=true;if(root!=null){root.SetActive(false);Object.Destroy(root);}foreach(var m in new[]{timber,gold,green,cloth})if(m!=null)Object.Destroy(m);}
    }
}
