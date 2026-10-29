using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatRecovery3DPlaytest
{
    const string Flag="Recovery3D938QA";static int phase,frame,routeStage,lastGate;static double began;static float stamina;static string failure;static bool audioPause;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=routeStage=0;frame=-1;lastGate=-1;began=0;failure=null;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/Recovery3DQA/PrePlay");EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string t,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&(type==LogType.Error||type==LogType.Exception))failure=t+"\n"+stack;}
    static void Finish(string error){SessionState.SetBool(Flag,false);CampRecoveryGame.Active?.Close();File.WriteAllText("Logs/Recovery3DPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;AudioListener.pause=audioPause;EditorApplication.isPlaying=false;}
    static bool Move(Vector3 destination,float feetHeight)
    {
        var player=BeachBoatQuestSession.Instance.player;var capsule=player.GetComponent<CapsuleCollider>();float feet=(capsule.center.y-capsule.height*.5f)*player.transform.lossyScale.y;
        var p=Vector3.MoveTowards(player.transform.position,new Vector3(destination.x,player.transform.position.y,destination.z),3);p.y=destination.y-feet+feetHeight;
        player.transform.position=p;var body=player.GetComponent<Rigidbody>();body.position=p;if(!body.isKinematic)body.linearVelocity=Vector3.zero;Physics.SyncTransforms();return QuestRowboat.FlatDistance(p,destination)<1;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>145)throw new Exception("Recovery 3D QA timeout");var player=BeachBoatQuestSession.Instance.player;var vitals=player.GetComponent<PlayerVitals>();
            if(phase==0)
            {
                audioPause=AudioListener.pause;AudioListener.pause=false;foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/Recovery3DQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();phase=1;return;
            }
            if(phase==1)
            {
                if(BeachBoatQuestSession.InputBlocked)return;
                Check(!vitals.UseHealingItem()&&vitals.HealingItems==3,"Full health cannot consume a healing item");vitals.TakeImpact(60,"QA damage");Check(vitals.UseHealingItem()&&vitals.Health==75&&vitals.HealingItems==2,"H item restores 35 actual health and consumes exactly one item");
                var save=BachDangSaveSystem.Capture("Healing inventory QA");save.healingItems=0;BachDangSaveSystem.Apply(save);Check(vitals.HealingItems==0,"An empty versioned inventory stays empty after load");phase=2;return;
            }
            if(phase==2)
            {
                if(BeachBoatQuestSession.InputBlocked)return;Check(!vitals.UseHealingItem()&&vitals.HealingItems==0,"No item means no healing and no negative count");
                var save=BachDangSaveSystem.Capture("Old inventory migration");save.healingItemsVersion=0;BachDangSaveSystem.Apply(save);Check(vitals.HealingItems==3,"Old save data receives the starter inventory without changing its health");
                var slot=GameObject.Find("Healing_item_slot").GetComponent<RectTransform>();Check(Mathf.Abs(slot.rect.width-slot.rect.height)<.1f&&slot.anchorMin.x==.5f&&slot.anchorMin.y==0,"The healing item occupies a square slot at bottom centre");
                var facility=Object.FindObjectsByType<CampFacility>(FindObjectsSortMode.None).Where(f=>f.kind==CampFacility.FacilityKind.Armory).OrderBy(f=>QuestRowboat.FlatDistance(player.transform.position,f.transform.position)).First();
                BeachBoatPhysicalVillagePlaytest.Place(facility.transform.position+facility.transform.forward*Mathf.Min(8,facility.interactDistance*.5f));stamina=vitals.maxStamina;Check(facility.TryUse(),"The actual armory interaction opens a physical training course • "+facility.name+" • range "+facility.interactDistance+" • blocked "+BeachBoatQuestSession.InteractionBlocked+" • state "+Story.Instance.currentState);phase=3;return;
            }
            if(phase==3)
            {
                var game=CampRecoveryGame.Active;Check(game!=null&&game.Course!=null&&game.Phase==CampRecoveryGame.GamePhase.Playing,"Training begins in the 3D world with three physical gates");Check(PhysicalTrainingCourse.AllowsMovement&&player.GetComponent<BeachBoatStory.Character_Movement>().enabled,"The normal movement controller is available during the course");
                Check(!game.Submit(0)&&game.Progress==0,"Pressing the old rhythm action cannot complete the physical course");Check(!BachDangSaveSystem.CanSave,"Temporary exercise props are not saved mid-attempt");Check(game.Course.Interact(),"The player picks up the sandbag at its actual starting point");phase=4;return;
            }
            if(phase==4)
            {
                var game=CampRecoveryGame.Active;var course=game.Course;if(game.Phase==CampRecoveryGame.GamePhase.Won){phase=5;return;}
                if(game.Progress>=game.Goal-1)
                {
                    var home=game.TrainingHome;home.y=Terrain.activeTerrain.SampleHeight(home)+Terrain.activeTerrain.transform.position.y;if(Move(home,.5f))course.Interact();return;
                }
                if(lastGate!=course.PassedGates){lastGate=course.PassedGates;routeStage=0;}
                var gate=GameObject.Find("Cong_vac_bao_"+(course.PassedGates%3+1)).transform;
                var point=gate.position+gate.forward*(routeStage==0?-11:routeStage==1?-5:routeStage==2?1:7);
                if(Move(point,routeStage==1||routeStage==2?4.3f:.5f))routeStage=Mathf.Min(3,routeStage+1);return;
            }
            if(phase==5)
            {
                var game=CampRecoveryGame.Active;Check(game.TrainingDelivered&&game.Progress==game.Goal,"Real gate traversal and returning the bag finish the 3D exercise");Check(game.ClaimReward()&&vitals.maxStamina==stamina+20,"Only completed exercise grants the next stamina upgrade");Check(!Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.name=="Strength_3D_Course"),"Exiting training removes its physical course and carried sack");
                var save=BachDangSaveSystem.Capture("Training result");BachDangSaveSystem.Apply(save);Check(vitals.maxStamina==stamina+20&&vitals.CompletedTrainingLevels==1,"The earned upgrade survives save/load");
                var training=Object.FindObjectsByType<CampFacility>(FindObjectsSortMode.None).First(f=>f.kind==CampFacility.FacilityKind.Armory);
                Check(CampRecoveryGame.Open(training)&&CampRecoveryGame.Active.Course!=null,"A new physical attempt can start after loading the earned upgrade");
                BachDangSaveSystem.Apply(save);Check(CampRecoveryGame.Active==null&&!Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.name=="Strength_3D_Course"),"Loading during physical training clears temporary gates and movement overrides");phase=6;return;
            }
            if(phase==6)
            {
                if(BeachBoatQuestSession.InputBlocked)return;var sound=BeachBoatSoundscape.Instance;int cries=sound.WreckCriesPlayed;sound.PlayShipwreck(player.transform.position+Vector3.forward*30);
                var channel=sound.transform.Find("Shipwreck_Scream_Channel").GetComponent<AudioSource>();Check(sound.WreckCriesPlayed==cries+1&&channel.clip!=null&&channel.isPlaying,"A sinking event immediately plays an actual human scream");sound.PlayShipwreck(player.transform.position);Check(sound.WreckCriesPlayed==cries+1,"Simultaneous sinking events do not stack or restart the same scream");
                Story.Instance.TransitionTo(Story.StoryState.Act3_WaitForLowTide,true);BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());ArcheryTraining.Instance.Restore(0,false);Story.Instance.RestoreLureBriefing(false);
                QuestRowboat.Instance.RestoreSave(true,QuestRowboat.Instance.transform.position,QuestRowboat.Instance.transform.rotation);Story.Instance.OnReadyForBattle();Check(BeachBoatStory.StoryDialogueUI.Instance.IsOpen&&!EnemyFleetDirector.Instance.BattleStarted,"Missing required archery shows a reminder and cannot launch the lure mission");BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();ArcheryTraining.Instance.Restore(60,true);phase=7;return;
            }
            if(phase==7)
            {
                if(BeachBoatQuestSession.InputBlocked)return;Story.Instance.OnReadyForBattle();Check(BeachBoatStory.StoryDialogueUI.Instance.IsOpen&&!EnemyFleetDirector.Instance.BattleStarted,"A legacy pre-battle save must receive lure orders before launching");
                var save=BachDangSaveSystem.Capture("Pending lure orders");Check(save.dialogue.action==nameof(Story.OnLureOrdersAcknowledged),"Pending voiced orders have a known serializable callback");
                Check(save.dialogue.voiceProfile=="commander_brief"&&Resources.Load<AudioClip>("BachDang938/Voices/Preparation/ngo_lure")?.length>2,"The lure orders resolve to the bundled Vietnamese commander recording");
                BachDangSaveSystem.Apply(save);Check(BeachBoatStory.StoryDialogueUI.Instance.IsOpen&&!Story.Instance.LureBriefed,"Loading pending orders keeps the briefing pending");
                BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();Story.Instance.OnLureOrdersAcknowledged();Check(Story.Instance.LureBriefed&&Story.Instance.currentState==Story.StoryState.Act3_BattleInProgress,"Acknowledging orders unlocks the actual lure mission");
                foreach(var p in hashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
