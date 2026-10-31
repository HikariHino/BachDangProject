using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using Object=UnityEngine.Object;
public static class BeachBoatShieldCrowdPlaytest
{
    const string Flag="ShieldCrowd30QA";static int phase,frame=-1,steps;static double began;static string failure;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static readonly List<Transform> actors=new List<Transform>();static readonly List<RiverActorNavigation> nav=new List<RiverActorNavigation>();static Vector3 origin;
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=steps=0;frame=-1;began=0;failure=null;checks.Clear();hashes.Clear();actors.Clear();nav.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error){SessionState.SetBool(Flag,false);File.WriteAllText("Logs/ShieldCrowdPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static void Launch(Vector3 from,Vector3 to,bool hostile=false,float draw=1.1f)
    {typeof(RiverPlayerCombat).GetMethod("Launch",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(RiverPlayerCombat.Instance,new object[]{from,to,null,12,hostile,draw});}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>130)throw new Exception("Shield/crowd QA timeout");
            var session=BeachBoatQuestSession.Instance;var shield=PlayerShield.Instance;var vitals=session.player.GetComponent<PlayerVitals>();var combat=RiverPlayerCombat.Instance;
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/ShieldCrowdQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();phase=1;return;
            }
            if(phase==1)
            {
                if(BeachBoatQuestSession.InputBlocked)return;session.player.transform.rotation=Quaternion.identity;session.storyCamera.transform.rotation=Quaternion.identity;
                Check(shield.SetEquipped(true),"G equipment can draw the physical wooden shield");shield.SetGuarding(true);float stamina=vitals.Stamina,health=vitals.Health;
                var front=session.player.transform.position+Vector3.forward*300+Vector3.up*10;
                Check(shield.TryBlockArrow(front)&&vitals.Health==health&&Mathf.Abs(vitals.Stamina-stamina+8)<.01f,"A frontal arrow is blocked for exactly eight stamina and no health");
                Check(!shield.TryBlockArrow(session.player.transform.position-Vector3.forward*300)&&vitals.Stamina==stamina-8,"An arrow from behind bypasses the shield without spending stamina");
                shield.SetGuarding(true);Launch(front,session.player.transform.position+Vector3.up*10,true);combat.StepArrows(1);Check(vitals.Health==health&&shield.BlockedArrows==2,"A real hostile projectile is stopped by the raised shield");
                shield.SetEquipped(false);Launch(front,session.player.transform.position+Vector3.up*10,true);combat.StepArrows(1);Check(vitals.Health<health,"Lowering the shield permits a genuine incoming arrow hit");
                var low=BachDangSaveSystem.Capture("QA low stamina");low.stamina=4;vitals.RestoreSave(low);shield.SetEquipped(true);shield.SetGuarding(true);Check(!shield.IsGuarding&&!shield.TryBlockArrow(front),"Insufficient stamina cannot sustain an arrow guard");
                var voice=EnemyFleetDirector.Instance.GetComponent<RiverBattleVoices>();voice.RestoreSave(new BachDangSaveData.Fleet{ambushVoiced=true,voiceWreckCalls=0});Check(typeof(RiverBattleVoices).GetField("queuedWreck",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(voice)!=null,"A mid-ambush load retains its pending wreck scream before the commander");voice.ResetForNewBattle();
                vitals.RestoreCheckpoint(0);shield.SetEquipped(true);shield.SetGuarding(true);shield.Pose();var chest=session.player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Chest);BeachBoatReview29Playtest.Picture("Shield",chest.position+session.player.transform.forward*28+session.player.transform.right*20+Vector3.up*4,chest.position);
                Check(BachDangSaveSystem.Save(1,"QA shield")&&BachDangSaveSystem.TryRead(1,out var saved)&&saved.shieldEquipped,"The shield equipment flag is written to a checksum-protected slot");BachDangSaveSystem.TryRead(1,out var checkpoint);shield.SetEquipped(false);BachDangSaveSystem.Apply(checkpoint);Check(shield.Equipped&&!shield.IsGuarding,"Load restores equipment while releasing the temporary guarding action");shield.SetEquipped(false);
                Check(combat.range>=600,"Bow range increases to at least six hundred world units");
                origin=BeachBoatStory.RiverStakeZone.Instance.FieldBounds.center+Vector3.up*500;Launch(origin,origin+Vector3.forward*600);combat.StepArrows(.2f);
                var arrow=Object.FindObjectsByType<Transform>().First(t=>t.name.StartsWith("PlayerBattleArrow_")&&t.gameObject.activeInHierarchy);float nearDrop=origin.y-arrow.position.y;combat.StepArrows(.3f);float farDrop=origin.y-arrow.position.y;
                Check(nearDrop>0&&farDrop>nearDrop*4,"The actual flying arrow falls increasingly below its sight line with distance");combat.StepArrows(2);
                var wall=new GameObject("QA_Arrow_Wall");var box=wall.AddComponent<BoxCollider>();box.size=new Vector3(12,20,1);wall.transform.position=origin+Vector3.forward*120;Physics.SyncTransforms();Launch(origin,origin+Vector3.forward*600);combat.StepArrows(1);
                Check(!Object.FindObjectsByType<Transform>().Any(t=>t.name.StartsWith("PlayerBattleArrow_")&&t.gameObject.activeInHierarchy),"Swept ballistic arrows stop on a real obstacle even during a long simulation step");Object.DestroyImmediate(wall);
                for(int i=0;i<10;i++){var go=new GameObject("QA_Crowd_"+i);go.transform.position=origin+new Vector3(i<5?-1:1,0,(i<5?-1:1)*(70+i%5*18));var c=go.AddComponent<CapsuleCollider>();c.radius=4;c.height=18;c.direction=2;c.center=Vector3.forward*4;go.transform.rotation=Quaternion.LookRotation(i<5?Vector3.forward:Vector3.back);go.AddComponent<ActorSolidBody>();actors.Add(go.transform);nav.Add(new RiverActorNavigation());}
                var pole=new GameObject("QA_Crowd_Solid_Pole");pole.transform.position=origin;var solid=pole.AddComponent<CapsuleCollider>();solid.height=35;solid.radius=3;Physics.SyncTransforms();phase=2;return;
            }
            if(phase==2)
            {
                for(int i=0;i<actors.Count;i++){var goal=origin+new Vector3((i%5-2)*20,0,i<5?100:-100);var from=actors[i].position;actors[i].position=nav[i].Move(actors[i],from+Vector3.up*4,goal+Vector3.up*4,24*.04f,3.5f)-Vector3.up*4;if(nav[i].LastDirection.sqrMagnitude>.1f)actors[i].rotation=Quaternion.LookRotation(nav[i].LastDirection);}
                if(++steps<500)return;
                Check(actors.Count(t=>t.position.z>origin.z+30)==5&&actors.Count(t=>t.position.z<origin.z-30)==5,"Both five-person groups with horizontal swimming bodies pass the opposing crowd and the central pole without remaining clumped");
                float overlap=0;for(int i=0;i<actors.Count;i++)for(int j=i+1;j<actors.Count;j++){var a=actors[i].GetComponent<Collider>();var b=actors[j].GetComponent<Collider>();if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float d))overlap=Mathf.Max(overlap,d);}
                Check(overlap<.15f,"Crowd avoidance preserves solid bodies instead of phasing swimmers through each other; penetration="+overlap);
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
