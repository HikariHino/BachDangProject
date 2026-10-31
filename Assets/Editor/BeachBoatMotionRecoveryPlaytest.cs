using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Exercise the actual veteran rig, hand contacts and death/reload locomotion.</summary>
public static class BeachBoatMotionRecoveryPlaytest
{
    public static void BuildHandSources()
    {
        const string path="Assets/Resources/BachDang938/Player/CombatHandSources.asset";
        var result=AssetDatabase.LoadAssetAtPath<CombatHandSources>(path);if(result==null){result=ScriptableObject.CreateInstance<CombatHandSources>();AssetDatabase.CreateAsset(result,path);}
        var entries=new List<CombatHandSources.Source>();
        foreach(var lod in Object.FindObjectsByType<CinematicCharacterLOD>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var rig=lod.GetComponentInParent<Animator>();if(rig==null||rig.avatar==null)continue;
            var original=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(rig.avatar)).OfType<Mesh>().FirstOrDefault(m=>m.isReadable&&m.vertexCount>1000);
            if(original==null||entries.Any(e=>e.original==original&&Array.IndexOf(e.crowd,lod.closeMesh)>=0))continue;
            entries.Add(new CombatHandSources.Source{original=original,crowd=new[]{lod.closeMesh,lod.middleMesh,lod.farMesh}});
        }
        result.sources=entries.ToArray();EditorUtility.SetDirty(result);AssetDatabase.SaveAssets();
    }
    public static void DumpHand()
    {
        var actor=Object.FindObjectsByType<Animator>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(a=>a.isHuman&&AssetDatabase.GetAssetPath(a.avatar).Contains("OldSoldier"));
        var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);var skin=actor.GetComponentInChildren<SkinnedMeshRenderer>(true);var mesh=skin.sharedMesh;int hi=Array.IndexOf(skin.bones,hand),ei=Array.IndexOf(skin.bones,hand.GetChild(0));var bind=mesh.bindposes[hi];var weights=mesh.boneWeights;
        var points=mesh.vertices.Select((v,i)=>new {p=bind.MultiplyPoint3x4(v),w=weights[i]}).Select(a=>new {a.p,weight=(a.w.boneIndex0==hi||a.w.boneIndex0==ei?a.w.weight0:0)+(a.w.boneIndex1==hi||a.w.boneIndex1==ei?a.w.weight1:0)+(a.w.boneIndex2==hi||a.w.boneIndex2==ei?a.w.weight2:0)+(a.w.boneIndex3==hi||a.w.boneIndex3==ei?a.w.weight3:0)}).Where(a=>a.weight>.2f);
        File.WriteAllText("Logs/HandPoints.json",Newtonsoft.Json.JsonConvert.SerializeObject(points.Select(a=>new {x=a.p.x,y=a.p.y,z=a.p.z,a.weight})));
    }
    const string Flag="MotionRecovery30QA";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int phase,lastFrame,sample;static double began;static string failure;
    static ProtectedCampNPC soldier;static RiverFighterPose pose;static Animator rig;
    static BachDangSaveData baseline;static Camera preview;static Vector3 stage,firstHand;static float sweep,armLength;
    static readonly List<string> checks=new List<string>();
    static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=sample=0;lastFrame=-1;began=0;failure=null;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/MotionRecoveryQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&(type==LogType.Error||type==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error)
    {
        SessionState.SetBool(Flag,false);Time.timeScale=1;
        File.WriteAllText("Logs/MotionRecoveryPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));
        BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
    }
    static void PoseLate(){typeof(RiverFighterPose).GetMethod("LateUpdate",Private).Invoke(pose,null);}
    static float Length()=>Vector3.Distance(rig.GetBoneTransform(HumanBodyBones.RightUpperArm).position,rig.GetBoneTransform(HumanBodyBones.RightLowerArm).position)+Vector3.Distance(rig.GetBoneTransform(HumanBodyBones.RightLowerArm).position,rig.GetBoneTransform(HumanBodyBones.RightHand).position);
    static void Capture(string name)
    {
        // Bake this frame's live skin for the isolated, off-screen proof camera.
        // Population distance culling otherwise hides the body at the QA stage.
        var baked=new List<GameObject>();var meshes=new List<Mesh>();var details=new List<object>();
        foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if(skin.sharedMesh==null)continue;var mesh=new Mesh();skin.BakeMesh(mesh,false);meshes.Add(mesh);
            var copy=new GameObject("QA_BakedSkin",typeof(MeshFilter),typeof(MeshRenderer));copy.GetComponent<MeshFilter>().sharedMesh=mesh;copy.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
            copy.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);copy.transform.localScale=skin.transform.lossyScale;
            // Imported humanoid bone scale can already be present in BakeMesh.
            if(copy.GetComponent<Renderer>().bounds.size.magnitude>EnemyDeckGuard.RigHeight(rig)*5)copy.transform.localScale=Vector3.one;
            baked.Add(copy);
            details.Add(new {skin=skin.name,vertices=mesh.vertexCount,bounds=mesh.bounds.ToString(),world=copy.GetComponent<Renderer>().bounds.ToString(),scale=copy.transform.lossyScale.ToString(),material=skin.sharedMaterials.Select(m=>m!=null?m.name+" / "+m.shader.name:"null").ToArray()});
        }
        var chest=rig.GetBoneTransform(HumanBodyBones.Chest).position;
        bool close=name.Contains("HandClose");var focus=close?pose.GripPosition:chest+Vector3.up*2;
        var gripFrame=new HumanoidHandGrip(rig,false);
        preview.transform.position=close?focus+gripFrame.PalmAxis*10+gripFrame.ThumbAxis*5-gripFrame.FingerAxis*3:chest+pose.transform.forward*32+pose.transform.right*28+Vector3.up*10;preview.transform.LookAt(focus);
        var rt=new RenderTexture(1000,850,24);preview.targetTexture=rt;preview.Render();RenderTexture.active=rt;
        File.WriteAllText("Logs/Motion_"+name+"_mesh.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {details,camera=preview.transform.position.ToString(),chest=chest.ToString()},Newtonsoft.Json.Formatting.Indented));
        var image=new Texture2D(1000,850,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1000,850),0,0);image.Apply();File.WriteAllBytes("Logs/Motion_"+name+".png",image.EncodeToPNG());
        RenderTexture.active=null;preview.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
        foreach(var go in baked)Object.DestroyImmediate(go);foreach(var mesh in meshes)Object.DestroyImmediate(mesh);
    }
    static void Recover()
    {Time.timeScale=1;BeachBoatQuestSession.Instance.storyCamera.GetComponent<BeachBoatStory.Camera_Script>().enabled=true;BachDangSaveSystem.Apply(baseline);BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();RiverPlayerCombat.Instance.SetAim(false);}
    static bool WalkWithoutUnlockingBody()
    {
        var player=BeachBoatQuestSession.Instance.player;var motor=player.GetComponent<BeachBoatStory.Character_Movement>();var body=player.GetComponent<Rigidbody>();
        Check(motor.enabled&&!body.isKinematic&&player.GetComponent<CapsuleCollider>().enabled,"Retry restores motor, dynamic rigidbody and capsule before any QA movement");
        var type=motor.GetType();var orbit=type.GetField("orbit",Private).GetValue(motor);var view=type.GetField("view",Private).GetValue(motor);var mode=Physics.simulationMode;
        try
        {
            motor.enabled=false;type.GetField("orbit",Private).SetValue(motor,null);type.GetField("view",Private).SetValue(motor,null);type.GetField("freeLook",Private).SetValue(motor,true);type.GetField("input",Private).SetValue(motor,Vector2.up);
            Physics.simulationMode=SimulationMode.Script;var start=body.position;
            for(int i=0;i<65;i++){type.GetMethod("FixedUpdate",Private).Invoke(motor,null);Physics.Simulate(Time.fixedDeltaTime);}
            return QuestRowboat.FlatDistance(start,body.position)>12;
        }
        finally{Physics.simulationMode=mode;type.GetField("orbit",Private).SetValue(motor,orbit);type.GetField("view",Private).SetValue(motor,view);motor.enabled=true;}
    }
    static void DieAndRetry(bool useSave)
    {
        if(useSave)BachDangAutoSave.Instance.AcceptSavedPoint(BachDangSaveSystem.Capture("QA pre-death"));else BachDangAutoSave.Instance.BeginNewJourney();
        BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>().TakeImpact(10000,"QA defeat");
        Check(QuestCheckpoint.Instance.IsDefeated&&Time.timeScale==0,"Real lethal damage opens defeat and pauses");
        Check(QuestCheckpoint.Instance.Retry()&&!QuestCheckpoint.Instance.IsDefeated&&Time.timeScale>0,"Actual retry handler closes defeat and resumes time");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>150)throw new Exception("Motion QA timeout at "+phase);
            var session=BeachBoatQuestSession.Instance;var combat=RiverPlayerCombat.Instance;var boarding=RiverBoardingCombat.Instance;
            if(phase==0)
            {
                foreach(var path in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[path]=Hash(path);
                BachDangAutoSave.Instance.enabled=false;BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                baseline=BachDangSaveSystem.Capture("QA camp baseline");CampDiscipline.Instance.Discover();CampDiscipline.Instance.enabled=false;
                soldier=CampDiscipline.Instance.Actors.First(a=>a.Type==ProtectedCampNPC.Kind.Soldier&&a.gameObject.activeInHierarchy&&a.GetComponent<NPCWander>()!=null&&a.GetComponent<NPCUprightPosture>()!=null);
                soldier.Retaliate();pose=soldier.GetComponent<RiverFighterPose>();rig=pose.Animator;rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;stage=BeachBoatStory.RiverStakeZone.Instance.FieldBounds.center+Vector3.up*500;soldier.transform.SetPositionAndRotation(stage,Quaternion.identity);
                var lod=rig.GetComponent<CinematicCharacterLOD>();if(lod!=null){lod.enabled=false;lod.target.forceRenderingOff=false;lod.target.sharedMesh=lod.closeMesh;}
                session.storyCamera.GetComponent<BeachBoatStory.Camera_Script>().enabled=false;session.storyCamera.transform.position=stage+Vector3.up*20+Vector3.forward*40;
                preview=new GameObject("QA_MotionCamera").AddComponent<Camera>();preview.CopyFrom(session.storyCamera);preview.enabled=false;preview.farClipPlane=800;preview.fieldOfView=40;
                var light=new GameObject("QA_MotionLight").AddComponent<Light>();light.type=LightType.Point;light.range=200;light.intensity=12;light.transform.position=stage+Vector3.up*40+Vector3.forward*20;
                pose.SetSword(true);pose.Slash();armLength=Length();firstHand=rig.GetBoneTransform(HumanBodyBones.RightHand).position;sweep=0;sample=0;phase=1;return;
            }
            if(phase==1)
            {
                rig.Play("Attack",0,.1f+sample*.085f);rig.Update(0);var wrist=rig.GetBoneTransform(HumanBodyBones.RightHand);var before=wrist.rotation;
                rig.GetComponent<NPCUprightPosture>().Balance();Check(Quaternion.Angle(before,wrist.rotation)<.01f,"Ambient posture does not erase the combat wrist at sample "+sample);
                PoseLate();sweep=Mathf.Max(sweep,Vector3.Distance(firstHand,wrist.position));
                Check(Mathf.Abs(Length()-armLength)<.02f,"Sword retargeting preserves actual arm bone lengths at sample "+sample);
                var bounds=pose.Weapon.GetComponent<MeshFilter>().sharedMesh.bounds;var handle=new Vector3(bounds.center.x,bounds.min.y+bounds.size.y*.105f,bounds.center.z);
                Check(pose.Weapon.gameObject.activeInHierarchy&&Vector3.Distance(pose.Weapon.TransformPoint(handle),pose.GripPosition)<.05f,"The visible sword handle follows the palm at sample "+sample);
                if(sample==2||sample==5){Capture("VeteranSword_"+sample);Capture("VeteranHandClose_"+sample);}
                if(sample==2)
                {
                    var hand=rig.GetBoneTransform(HumanBodyBones.RightHand).Find("Right_Closed_Weapon_Hand");
                    Check(hand!=null&&hand.gameObject.activeInHierarchy,"The veteran's visible fingerless hand actually uses a closed sword grip mesh");
                    Check(hand.GetComponent<MeshFilter>().sharedMesh.vertexCount>1000&&rig.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.name.Contains("Weapon_Right"),"Four flexed fingers and opposed thumb replace the open-hand surface while retaining the wrist");
                }
                if(++sample<9)return;
                Check(sweep>armLength*.35f,"The actual sword swing moves the wrist through an arc instead of shrugging: "+sweep);
                pose.SetSword(false);pose.BeginBowShot(stage+Vector3.forward*300+Vector3.up*20);sample=0;phase=2;return;
            }
            if(phase==2)
            {
                typeof(RiverFighterPose).GetField("bowBegan",Private).SetValue(pose,Time.time-new[]{.05f,.4f,.68f}[sample]);rig.Update(0);PoseLate();
                var left=new HumanoidHandGrip(rig,true);var right=new HumanoidHandGrip(rig,false);var bow=pose.transform.Find("DaiViet_Boarding_Bow");var line=bow.Find("Day_cung_keo_tha").GetComponent<LineRenderer>();
                Check(Vector3.Distance(left.Contact,bow.position)<.05f,"Veteran bow stave stays inside its own hand at draw stage "+sample);
                Check(Vector3.Distance(right.Contact,bow.TransformPoint(line.GetPosition(1)))<.05f,"Veteran string stays on its own drawing fingers at stage "+sample);
                Check(Vector3.Angle(left.FingerAxis,rig.GetBoneTransform(HumanBodyBones.LeftHand).position-rig.GetBoneTransform(HumanBodyBones.LeftLowerArm).position)<18,"Veteran support wrist remains aligned at stage "+sample);
                Check(Vector3.Angle(right.FingerAxis,rig.GetBoneTransform(HumanBodyBones.RightHand).position-rig.GetBoneTransform(HumanBodyBones.RightLowerArm).position)<18,"Veteran drawing wrist remains aligned at stage "+sample);
                Check(Mathf.Abs(Length()-armLength)<.02f,"NPC bow does not stretch the skeleton at stage "+sample);Capture("VeteranBow_"+sample);
                if(++sample<3)return;Recover();phase=3;return;
            }
            if(phase==3)
            {if(BeachBoatQuestSession.InputBlocked)return;Check(!QuestRowboat.Instance.IsAboard,"Camp recovery test starts on ordinary ground");DieAndRetry(true);phase=4;return;}
            if(phase==4)
            {if(BeachBoatQuestSession.InputBlocked)return;Check(WalkWithoutUnlockingBody(),"Player actually walks after death and automatic-save retry");Recover();phase=5;return;}
            if(phase==5)
            {if(BeachBoatQuestSession.InputBlocked)return;Check(boarding.ToggleSword(),"Draw sword before second death");DieAndRetry(true);phase=6;return;}
            if(phase==6)
            {if(BeachBoatQuestSession.InputBlocked)return;Check(boarding.SwordDrawn&&WalkWithoutUnlockingBody(),"Sword save also restores a movable character after death");Recover();phase=7;return;}
            if(phase==7)
            {if(BeachBoatQuestSession.InputBlocked)return;combat.SetAim(true);Check(combat.IsAiming,"Enter bow aim before death");DieAndRetry(true);phase=8;return;}
            if(phase==8)
            {if(BeachBoatQuestSession.InputBlocked)return;Check(!combat.IsAiming&&WalkWithoutUnlockingBody(),"Death during bow aim clears aim and restores walking");Recover();phase=9;return;}
            if(phase==9)
            {if(BeachBoatQuestSession.InputBlocked)return;QuestCheckpoint.Instance.Capture(BeachBoatStory.BachDangStoryManager.Instance.currentState);DieAndRetry(false);phase=10;return;}
            if(phase==10)
            {
                if(BeachBoatQuestSession.InputBlocked)return;Check(WalkWithoutUnlockingBody(),"Fallback checkpoint retry also restores walking");Recover();phase=11;return;
            }
            if(phase==11)
            {
                if(BeachBoatQuestSession.InputBlocked)return;
                var boat=QuestRowboat.Instance;boat.RestoreSave(true,BeachBoatStory.RiverStakeZone.Instance.FieldBounds.center,Quaternion.identity);
                Check(boat.IsAboard,"Board the actual player rowboat before death");DieAndRetry(true);phase=12;return;
            }
            if(phase==12)
            {
                if(BeachBoatQuestSession.InputBlocked)return;var boat=QuestRowboat.Instance;
                Check(boat.IsAboard&&session.player.GetComponent<Rigidbody>().isKinematic&&!session.player.GetComponent<BeachBoatStory.Character_Movement>().enabled,"Aboard retry preserves the boat movement owner");
                var start=boat.transform.position;for(int i=0;i<20;i++)boat.Step(1,0,.1f,false);
                Check(QuestRowboat.FlatDistance(start,boat.transform.position)>1,"The restored rowboat can actually row after death");
                boat.LeaveForCheckpoint();PlayerArrival.Teleport(session.player,baseline.playerPosition,baseline.playerRotation);
                Check(WalkWithoutUnlockingBody(),"Leaving a restored boat does not restore the dead frozen-body cache");Recover();phase=13;return;
            }
            if(phase==13)
            {
                if(BeachBoatQuestSession.InputBlocked)return;
                var at=BeachBoatStory.RiverStakeZone.Instance.FieldBounds.center;typeof(RiverBoardingCombat).GetMethod("BeginSwim",Private).Invoke(boarding,new object[]{at});
                var swimming=BachDangSaveSystem.Capture("QA swimming save");BachDangSaveSystem.Apply(swimming);
                Check(boarding.FreeSwimming&&session.player.GetComponent<Rigidbody>().isKinematic&&!session.player.GetComponent<CapsuleCollider>().enabled,"Swimming save still reacquires its kinematic traversal mode");
                boarding.EndFreeWater();boarding.ResetBattle();Check(!session.player.GetComponent<Rigidbody>().isKinematic&&session.player.GetComponent<BeachBoatStory.Character_Movement>().enabled,"Leaving a restored swim returns to dynamic walking");
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
}
