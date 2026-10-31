using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BeachBoatBowMovementPlaytest
{
    const string Flag="BowMovement31QA";const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int phase,frame;static double began;static string failure;static BachDangSaveData save;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=0;frame=-1;began=0;failure=null;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/BowMovementQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&(type==LogType.Error||type==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error){SessionState.SetBool(Flag,false);Time.timeScale=1;File.WriteAllText("Logs/BowMovementPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>100)throw new Exception("Bow movement QA timeout");
            var session=BeachBoatQuestSession.Instance;var combat=RiverPlayerCombat.Instance;
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);
                BachDangAutoSave.Instance.enabled=false;BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();phase=1;return;
            }
            if(phase==1)
            {
                if(BeachBoatQuestSession.InputBlocked)return;save=BachDangSaveSystem.Capture("QA before bow movement");TestGround(session,combat);phase=2;return;
            }
            if(phase==2)
            {
                TestCarriers(session,combat);
                BachDangSaveSystem.Apply(save);BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();Check(session.player.GetComponent<BeachBoatStory.Character_Movement>().enabled&&!session.player.GetComponent<Rigidbody>().isKinematic,"Returning from bow movement restores normal walking ownership");
                foreach(var p in hashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
    static void TestCarriers(BeachBoatQuestSession session,RiverPlayerCombat combat)
    {
        var boat=QuestRowboat.Instance;var boarding=RiverBoardingCombat.Instance;var actor=session.player.transform;var camera=session.storyCamera;var orbit=camera.GetComponent<BeachBoatStory.Camera_Script>();
        combat.enabled=false;orbit.enabled=false;
        try
        {
            boat.RestoreSave(true,BeachBoatStory.RiverStakeZone.Instance.FieldBounds.center,Quaternion.identity);combat.SetAim(true);camera.transform.rotation=Quaternion.Euler(0,90,0);
            var turn=Quaternion.Euler(0,80,0);actor.rotation=turn;actor.GetComponent<Rigidbody>().rotation=turn;
            typeof(QuestRowboat).GetMethod("AttachPlayer",Private).Invoke(boat,null);
            Check(Quaternion.Angle(turn,actor.rotation)<.01f,"Rowboat seating preserves the current bow facing instead of resetting it every render frame");
            typeof(RiverPlayerCombat).GetMethod("LateUpdate",Private).Invoke(combat,null);var aimed=actor.rotation;typeof(QuestRowboat).GetMethod("AttachPlayer",Private).Invoke(boat,null);
            Check(Quaternion.Angle(aimed,actor.rotation)<.01f,"Aiming turn survives the next seated-player update");
            var friendly=boarding.friendlyBoats.First(b=>b.gameObject.activeInHierarchy);
            var data=new BachDangSaveData.Boarding{state=(int)RiverBoardingCombat.PlayerState.FriendlyDeck,sword=false,deckShip=friendly.name,deckLocal=Vector3.zero};boarding.RestoreSave(data);
            combat.SetAim(false);combat.SetAim(true);actor.rotation=Quaternion.Euler(0,90,0);actor.GetComponent<Rigidbody>().rotation=actor.rotation;var facing=actor.rotation;
            boarding.StepPlayer(.02f,Vector2.right);
            Check(Quaternion.Angle(facing,actor.rotation)<.01f,"Strafing on a friendly deck cannot overwrite the bow's facing with movement direction");
            Check(actor.GetComponentInChildren<Animator>().GetFloat("AimMove")>0,"Deck walking drives the lower-body aim locomotion blend");
            combat.SetAim(false);
        }
        finally{combat.enabled=true;orbit.enabled=true;}
    }
    static void TestGround(BeachBoatQuestSession session,RiverPlayerCombat combat)
    {
        var player=session.player;var motor=player.GetComponent<BeachBoatStory.Character_Movement>();var body=player.GetComponent<Rigidbody>();var rig=player.GetComponentInChildren<Animator>();var camera=session.storyCamera;var orbit=camera.GetComponent<BeachBoatStory.Camera_Script>();
        var mode=Physics.simulationMode;var motorType=motor.GetType();var oldOrbit=motorType.GetField("orbit",Private).GetValue(motor);var oldView=motorType.GetField("view",Private).GetValue(motor);
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="QA_Bow_Movement_Runway";var origin=BeachBoatStory.RiverStakeZone.Instance.FieldBounds.center+Vector3.up*500;ground.transform.position=origin-Vector3.up*3;ground.transform.localScale=new Vector3(900,6,900);
        motor.enabled=false;combat.enabled=false;orbit.enabled=false;body.interpolation=RigidbodyInterpolation.Interpolate;body.isKinematic=false;
        try
        {
            Physics.simulationMode=SimulationMode.Script;motorType.GetField("orbit",Private).SetValue(motor,null);motorType.GetField("view",Private).SetValue(motor,camera.transform);motorType.GetField("freeLook",Private).SetValue(motor,false);motorType.GetField("sprint",Private).SetValue(motor,false);motorType.GetField("aim",Private).SetValue(motor,true);
            body.position=origin+Vector3.up*4;body.rotation=Quaternion.Euler(0,35,0);player.transform.SetPositionAndRotation(body.position,body.rotation);Physics.SyncTransforms();
            combat.SetAim(true);Check(rig.runtimeAnimatorController.name=="Player_Bow_Locomotion"&&rig.layerCount==2,"Aiming selects separate locomotion and upper-body bow layers");
            // The synchronous simulation below manually invokes FixedUpdate,
            // but must retain the real walking ownership flag for bow posing.
            motor.enabled=true;
            var controller=rig.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;var mask=controller.layers[1].avatarMask;
            Check(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body)&&mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm)&&!mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg)&&!rig.applyRootMotion,"Bow mask owns the aim body while legs retain locomotion and physics owns the actor root");
            camera.transform.rotation=Quaternion.identity;rig.SetFloat("AimMove",0);rig.Update(0);typeof(RiverPlayerCombat).GetField("charge",Private).SetValue(combat,.8f);typeof(RiverPlayerCombat).GetMethod("LateUpdate",Private).Invoke(combat,null);
            var chest=rig.GetBoneTransform(HumanBodyBones.Chest);var chestRest=Quaternion.Inverse(player.transform.rotation)*chest.rotation;
            var left=rig.GetBoneTransform(HumanBodyBones.LeftHand);var right=rig.GetBoneTransform(HumanBodyBones.RightHand);var profile=combat.GripProfile;
            var bow=GameObject.Find("Cung_Go_Quan_Si").transform;float worstTwist=0,worstContact=0,worstLateTurn=0;
            for(int direction=0;direction<8;direction++)
            {
                float angle=direction*Mathf.PI/4;var input=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));motorType.GetField("input",Private).SetValue(motor,input);
                body.position=origin+Vector3.up*4;player.transform.position=body.position;if(!body.isKinematic)body.linearVelocity=Vector3.zero;Physics.SyncTransforms();var start=body.position;
                for(int step=0;step<45;step++)
                {
                    motorType.GetMethod("FixedUpdate",Private).Invoke(motor,null);Physics.Simulate(Time.fixedDeltaTime);
                    float speed=new Vector2(body.linearVelocity.x,body.linearVelocity.z).magnitude;rig.SetFloat("AimMove",Mathf.Clamp01(speed/(motor.walkSpeed*.65f)));rig.Update(Time.fixedDeltaTime);
                    var before=body.rotation;typeof(RiverPlayerCombat).GetMethod("LateUpdate",Private).Invoke(combat,null);worstLateTurn=Mathf.Max(worstLateTurn,Quaternion.Angle(before,body.rotation));
                    worstTwist=Mathf.Max(worstTwist,Quaternion.Angle(chestRest,Quaternion.Inverse(player.transform.rotation)*chest.rotation));worstContact=Mathf.Max(worstContact,Vector3.Distance(left.TransformPoint(profile.leftCenter),bow.position));
                }
                Check(QuestRowboat.FlatDistance(start,body.position)>8,"Actual aimed walking moves in direction "+direction);
                Check(rig.GetCurrentAnimatorStateInfo(1).IsName("Bow_Aim"),"Walking direction "+direction+" retains the upper-body bow aim state");
                Check(Quaternion.Angle(body.rotation,Quaternion.Euler(0,35,0))<1,"Movement direction "+direction+" does not rotate the body away from the sight line");
                if(direction==2)Capture(camera,chest.position,player.transform.forward,player.transform.right);
            }
            Check(worstLateTurn<.01f,"Late bow posing never writes the dynamic body's rotation; worst="+worstLateTurn);
            Check(worstTwist<12,"Walking does not twist the aim torso between idle/walk poses; worst="+worstTwist);
            Check(worstContact<.08f,"The bow remains on its support-hand contact throughout walking; error="+worstContact);
            Check(Vector3.Distance(right.TransformPoint(profile.rightCenter),rig.GetBoneTransform(HumanBodyBones.Head).position)<5,"The drawn string stays anchored near the cheek after movement");
            // Mouse turns between physics ticks must not snap the rendered body.
            camera.transform.rotation=Quaternion.Euler(0,90,0);var rotation=body.rotation;for(int i=0;i<4;i++)typeof(RiverPlayerCombat).GetMethod("LateUpdate",Private).Invoke(combat,null);
            Check(Quaternion.Angle(rotation,body.rotation)<.01f,"Four render updates between physics steps cannot snap the aiming body to the new camera yaw");
            for(int i=0;i<30;i++){motorType.GetMethod("FixedUpdate",Private).Invoke(motor,null);Physics.Simulate(Time.fixedDeltaTime);}
            Check(Quaternion.Angle(body.rotation,Quaternion.Euler(0,125,0))<1&&body.interpolation==RigidbodyInterpolation.Interpolate,"Physics turns smoothly toward a new sight direction while retaining interpolation");
            combat.SetAim(false);Check(rig.runtimeAnimatorController==RiverBoardingCombat.Instance.FootController,"Lowering the bow restores the ordinary walking controller");
        }
        finally{Physics.simulationMode=mode;motorType.GetField("orbit",Private).SetValue(motor,oldOrbit);motorType.GetField("view",Private).SetValue(motor,oldView);motor.enabled=true;combat.enabled=true;orbit.enabled=true;Object.DestroyImmediate(ground);}
    }
    static void Capture(Camera source,Vector3 chest,Vector3 forward,Vector3 right)
    {
        var preview=new GameObject("QA_Bow_Move_Camera").AddComponent<Camera>();preview.CopyFrom(source);preview.enabled=false;preview.transform.position=chest+forward*24+right*20+Vector3.up*6;preview.transform.LookAt(chest+Vector3.up*2);CinematicCharacterLOD.RefreshForCamera(preview);
        foreach(var skin in BeachBoatQuestSession.Instance.player.GetComponentsInChildren<SkinnedMeshRenderer>())skin.forceMatrixRecalculationPerRender=true;
        var rt=new RenderTexture(1200,850,24);preview.targetTexture=rt;preview.Render();RenderTexture.active=rt;var image=new Texture2D(1200,850,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,850),0,0);image.Apply();File.WriteAllBytes("Logs/BowMovement_Strafe.png",image.EncodeToPNG());RenderTexture.active=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(preview.gameObject);CinematicCharacterLOD.RefreshForCamera(source);
    }
}
