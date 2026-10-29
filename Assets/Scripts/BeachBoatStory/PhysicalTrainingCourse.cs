using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace BeachBoatStory
{
    /// <summary>A separate supply-carry drill; the camp entry is restored on every exit.</summary>
    public sealed class PhysicalTrainingCourse
    {
        public static bool AllowsMovement=>CampRecoveryGame.Active?.Course!=null&&CampRecoveryGame.Active.Phase==CampRecoveryGame.GamePhase.Playing&&!BachDangFrontEnd.IsBlocking;
        public static float MovementScale=>AllowsMovement&&CampRecoveryGame.Active.Course.Carrying?.74f:1;
        readonly CampRecoveryGame game;readonly Transform player;readonly Vector3 entry;readonly Quaternion entryRotation;TerrainData practiceGround;VolumeProfile readability;
        readonly GameObject root,sack;readonly List<GameObject> stations=new List<GameObject>();readonly Material timber,gold,green,cloth,dirt;
        readonly List<Vector3> route=new List<Vector3>();bool carrying,disposed,entered;Vector3 previous;float travel;
        public Vector3 Home {get;private set;}public bool Carrying=>carrying;
        public Vector3 Target=>carrying?route[game.Progress%route.Count]:Home;
        public int PassedGates=>game.Progress; // Retained for diagnostic compatibility; there are no gates.
        public PhysicalTrainingCourse(CampRecoveryGame owner)
        {
            game=owner;player=BeachBoatQuestSession.Instance.player.transform;entry=owner.TrainingHome;entryRotation=player.rotation;
            root=new GameObject("Strength_3D_Course");
            Material M(string name,Color c)=>new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=c};
            timber=M("Training timber",new Color(.28f,.19f,.1f));gold=M("Training destination",new Color(.88f,.65f,.25f));green=M("Training linen",new Color(.35f,.45f,.32f));cloth=M("Sandbag linen",new Color(.57f,.48f,.3f));dirt=M("Training packed soil",new Color(.3f,.25f,.16f));
            // A dedicated practice stage outside the campaign terrain avoids
            // terrain-instanced rocks that only acquire collision near the camera.
            var mainTerrain=Terrain.activeTerrain;float edge=mainTerrain.transform.position.x+mainTerrain.terrainData.size.x;
            var centre=new Vector3(edge+500,Mathf.Max(entry.y,TideSystem.Instance.highTideY+20),entry.z);
            practiceGround=new TerrainData{heightmapResolution=33,alphamapResolution=32,baseMapResolution=32,size=new Vector3(260,5,260)};
            practiceGround.SetHeights(0,0,new float[33,33]);practiceGround.terrainLayers=mainTerrain.terrainData.terrainLayers;
            var ground=Terrain.CreateTerrainGameObject(practiceGround);ground.name="Dia_hinh_san_luyen_rieng";ground.transform.SetParent(root.transform,true);ground.transform.position=centre+new Vector3(-130,-.1f,-130);
            var lighting=new GameObject("Anh_sang_san_luyen");lighting.transform.SetParent(root.transform,true);lighting.transform.position=centre+Vector3.up*50;var region=lighting.AddComponent<BoxCollider>();region.isTrigger=true;region.size=new Vector3(260,200,260);
            var volume=lighting.AddComponent<Volume>();volume.isGlobal=false;volume.priority=500;volume.blendDistance=10;readability=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=readability;readability.Add<ColorAdjustments>().postExposure.Override(.9f);readability.Add<LiftGammaGain>().gamma.Override(new Vector4(1,1,1,.14f));
            var fill=new GameObject("Anh_sang_bai_tap").AddComponent<Light>();fill.transform.SetParent(root.transform,false);fill.type=LightType.Directional;fill.transform.rotation=Quaternion.Euler(55,35,0);fill.color=new Color(1,.9f,.73f);fill.intensity=.9f;fill.shadows=LightShadows.None;
            for(int side=0;side<4;side++)
            {
                var rotation=Quaternion.Euler(0,side*90,0);var wall=new GameObject("Hang_rao_san_luyen_"+side).transform;wall.SetParent(root.transform,true);wall.SetPositionAndRotation(centre+rotation*Vector3.forward*108,rotation);
                for(int post=-8;post<=8;post++)Part("Cot_rao_go",wall,new Vector3(post*13,11,0),new Vector3(1.2f,22,1.2f),timber,true);
                foreach(float y in new[]{6f,14f})Part("Thanh_rao",wall,new Vector3(0,y,0),new Vector3(216,1.4f,1),timber,true);
            }
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                var at=centre+new Vector3(x*62,0,z*62);Part("Cot_den_san_tap",root.transform,at+Vector3.up*9,new Vector3(1,18,1),timber,true);
                Part("Den_long_san_tap",root.transform,at+Vector3.up*18,new Vector3(3,4,3),gold,false,PrimitiveType.Sphere);
                var lamp=new GameObject("Anh_den_san_luyen").AddComponent<Light>();lamp.transform.SetParent(root.transform,true);lamp.transform.position=at+Vector3.up*19;lamp.type=LightType.Point;lamp.color=new Color(1,.78f,.48f);lamp.intensity=4;lamp.range=150;lamp.shadows=LightShadows.None;
            }
            Part("San_tap_dat_nen",root.transform,centre-Vector3.up*.2f,new Vector3(86,.4f,86),dirt,true);
            Home=centre+Vector3.back*28;Part("Gia_bao_xuat_phat",root.transform,Home+Vector3.up*1.5f,new Vector3(14,3,9),timber,true);
            foreach(float x in new[]{-4f,0,4})Part("Bao_tiep_te_du_tru",root.transform,Home+new Vector3(x,5,0),new Vector3(3,2,3),cloth,false,PrimitiveType.Capsule);
            for(int i=0;i<3;i++)
            {
                var p=centre+new[]{new Vector3(-27,0,22),new Vector3(0,0,29),new Vector3(27,0,22)}[i];route.Add(p);
                var station=new GameObject("Ben_giao_bao_"+(i+1));station.transform.SetParent(root.transform);station.transform.position=p;stations.Add(station);
                Part("Pallet_go",station.transform,Vector3.up,new Vector3(13,2,10),timber,true);
                Part("Vai_danh_dau",station.transform,new Vector3(0,2.2f,0),new Vector3(11,.15f,8),green,false);
                Part("Cot_co_go",station.transform,new Vector3(-9,12,3),new Vector3(.6f,24,.6f),timber,true);
                Part("Co_hieu",station.transform,new Vector3(-6,22,3),new Vector3(6,4,.15f),green,false);
            }
            sack=Part("Training_sandbag",root.transform,Home+Vector3.up*5,new Vector3(6,2.5f,4),cloth,false,PrimitiveType.Capsule);sack.SetActive(false);
            Teleport(Home+Vector3.back*10,Quaternion.identity);entered=true;previous=player.position;Physics.SyncTransforms();
        }
        GameObject Part(string name,Transform parent,Vector3 p,Vector3 size,Material material,bool solid,PrimitiveType type=PrimitiveType.Cube)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;if(!solid){go.GetComponent<Collider>().enabled=false;Object.Destroy(go.GetComponent<Collider>());}return go;}
        void Teleport(Vector3 ground,Quaternion rotation)
        {
            var capsule=player.GetComponent<CapsuleCollider>();float sole=(capsule.center.y-capsule.height*.5f)*player.lossyScale.y;
            var p=ground-Vector3.up*sole+Vector3.up*.4f;player.SetPositionAndRotation(p,rotation);var body=player.GetComponent<Rigidbody>();body.position=p;body.rotation=rotation;if(!body.isKinematic)body.linearVelocity=Vector3.zero;
            BeachBoatQuestSession.Instance.storyCamera.GetComponent<Camera_Script>().SnapToTarget();
        }
        public bool Interact()
        {
            if(disposed||CampRecoveryGame.Active!=game||game.Phase!=CampRecoveryGame.GamePhase.Playing)return false;
            if(!carrying&&QuestRowboat.FlatDistance(player.position,Home)<17){carrying=true;sack.SetActive(true);previous=player.position;travel=0;BeachBoatSoundscape.Instance?.PlayInteraction();return true;}
            if(carrying&&travel>20&&QuestRowboat.FlatDistance(player.position,Target)<13)
            {
                var placed=Object.Instantiate(sack,root.transform);placed.name="Bao_da_giao_"+game.Progress;placed.transform.SetPositionAndRotation(Target+Vector3.up*(4+game.Progress/3*4),Quaternion.identity);
                carrying=false;sack.SetActive(false);BeachBoatSoundscape.Instance?.PlayInteraction();game.TrainingCheckpoint();return true;
            }
            return false;
        }
        public void Tick()
        {
            if(disposed||BachDangFrontEnd.IsBlocking||game.Phase!=CampRecoveryGame.GamePhase.Playing)return;
            if(Input.GetKeyDown(KeyCode.E))Interact();if(disposed)return;
            float moved=QuestRowboat.FlatDistance(previous,player.position);previous=player.position;if(carrying&&moved<8)travel+=moved;
            for(int i=0;i<stations.Count;i++){var material=carrying&&i==game.Progress%3?gold:green;stations[i].transform.Find("Vai_danh_dau").GetComponent<Renderer>().sharedMaterial=material;stations[i].transform.Find("Co_hieu").GetComponent<Renderer>().sharedMaterial=material;}
            if(carrying){var rig=player.GetComponentInChildren<Animator>();var chest=rig.GetBoneTransform(HumanBodyBones.Chest);sack.transform.SetPositionAndRotation(chest.position+player.forward*4-Vector3.up*2,player.rotation);}
        }
        public void Pose()
        {
            if(!carrying||disposed)return;var rig=player.GetComponentInChildren<Animator>();var p=sack.transform.position;var grip=BowGripProfile.Load();
            foreach(bool left in new[]{true,false}){var upper=rig.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);var lower=rig.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var hand=rig.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);var at=p+player.right*(left?-2.5f:2.5f)-Vector3.up*.5f;if(grip!=null)grip.Align(upper,lower,hand,at,upper.position+player.right*(left?-4:4)-Vector3.up*3,left,true,player.forward);else QuestRowboat.SolveArm(upper,lower,hand,at,upper.position-player.up*5);}
        }
        public void Draw()
        {
            if(disposed)return;var style=new GUIStyle(GUI.skin.box){fontSize=20,alignment=TextAnchor.MiddleCenter,wordWrap=true};
            GUI.Box(new Rect(Screen.width*.23f,Screen.height*.79f,Screen.width*.54f,Screen.height*.16f),$"SÂN LUYỆN TIẾP TẾ • Cấp {game.TrainingLevel}/3 • Đã giao {game.Progress}/{game.Goal}\nCòn {Mathf.Max(0,game.TimeLimit-game.Elapsed):0}s • "+(!carrying?"Trở về giá bao • [E] nhấc bao":"Mang tới bệ có cờ vàng • [E] đặt bao")+"\nWASD di chuyển • ESC: kết thúc bài tập, trở về lều",style);
        }
        public void Dispose(){if(disposed)return;disposed=true;if(root!=null){root.SetActive(false);Object.Destroy(root);}if(entered){player.SetPositionAndRotation(entry,entryRotation);var body=player.GetComponent<Rigidbody>();body.position=entry;body.rotation=entryRotation;if(!body.isKinematic)body.linearVelocity=Vector3.zero;BeachBoatQuestSession.Instance.storyCamera.GetComponent<Camera_Script>().SnapToTarget();}if(practiceGround!=null)Object.Destroy(practiceGround);if(readability!=null){foreach(var component in readability.components)Object.Destroy(component);Object.Destroy(readability);}foreach(var m in new[]{timber,gold,green,cloth,dirt})if(m!=null)Object.Destroy(m);}
    }
}
