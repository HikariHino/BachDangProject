using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using Object=UnityEngine.Object;
using Dialogue=BeachBoatStory.StoryDialogueUI;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatPhysicalDrumPlaytest
{
    const string Flag="PhysicalDrumQA";static int phase,frame;static double began;static string error;static float volume;static bool initialized;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static BeachBoatMinigameStation drum;static Vector3 position;static float elevation;static NPCWander[] borrowed;static RuntimeAnimatorController[] controllers;
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=0;frame=-1;began=0;error=null;initialized=false;checks.Clear();hashes.Clear();BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/PhysicalDrum/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))error=m+"\n"+stack;}
    static void Finish(string failure){if(initialized)BachDangPreferences.SetVolume(volume);SessionState.SetBool(Flag,false);File.WriteAllText("Logs/PhysicalDrumPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=failure==null,error=failure,phase,checks},Newtonsoft.Json.Formatting.Indented));drum?.CancelActivity();BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static void Move(Vector3 p){var actor=BeachBoatQuestSession.Instance.player;actor.transform.position=p;actor.GetComponent<Rigidbody>().position=p;Physics.SyncTransforms();}
    static void Advance(BeachBoatMinigameStation station,float dt){while(dt>.00001f&&BeachBoatMinigameStation.Active==station&&!station.Failed){float step=Mathf.Min(.01f,dt);station.AdvanceDrum(step);dt-=step;}}
    public static void Solve(BeachBoatMinigameStation station,Action<PhysicalDrumGame> response=null)
    {
        var game=station.PhysicalDrum;int guard=0;
        while(game.Running&&!station.Failed&&guard++<100)
        {
            if(game.Phase==PhysicalDrumGame.LessonPhase.Response)response?.Invoke(game);
            if(game.Phase!=PhysicalDrumGame.LessonPhase.Repeat){Advance(station,.02f);guard--;if(station.Elapsed>50)throw new Exception("Drum solver exhausted timing budget");continue;}
            float release=game.NextHitTime-.13f;if(game.BeatIndex==0)release=game.Clock+.12f;
            bool left=game.BeatIndex%2==0;var ray=game.AimAt(game.StrikeCenter+Vector3.right*(left?-2:2));
            if(!game.Raise(left,ray))throw new Exception("Physical stick was unavailable at beat "+game.BeatIndex);
            Advance(station,Mathf.Max(.001f,release-game.Clock));if(!game.Release(left,ray))throw new Exception("Physical stick could not release");Advance(station,.14f);
        }
        if(!station.HasCompleted)throw new Exception("The physical rhythm lesson did not complete; beat="+game.BeatIndex+", phase="+game.Phase+", elapsed="+station.Elapsed+", mistakes="+station.Mistakes);
    }
    static void Visual(PhysicalDrumGame game)
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;var ground=drum.GetComponentInParent<DrumTrainingGround>();camera.transform.SetPositionAndRotation(ground.Eye,Quaternion.LookRotation(ground.StrikeCenter-ground.Eye));camera.fieldOfView=58;
        typeof(PhysicalDrumGame).GetMethod("LateUpdate",Private).Invoke(game,null);
    }
    static void Capture(string name)
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;var canvases=Object.FindObjectsByType<Canvas>().Where(c=>c.isActiveAndEnabled&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();var saved=canvases.Select(c=>new{c,c.worldCamera,c.planeDistance}).ToArray();var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;foreach(var s in saved){s.c.renderMode=RenderMode.ScreenSpaceOverlay;s.c.worldCamera=s.worldCamera;s.c.planeDistance=s.planeDistance;}Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
    }
    static void Restored(string context)
    {
        var session=BeachBoatQuestSession.Instance;Check(session.player.GetComponent<BeachBoatStory.Character_Movement>().enabled&&session.player.GetComponent<CapsuleCollider>().enabled&&!session.player.GetComponent<Rigidbody>().isKinematic,context+": motor and solid body restored");
        Check(!session.storyCamera.GetComponent<BeachBoatStory.Camera_Script>().WorkFocusActive&&Vector3.Distance(session.player.transform.position,position)<.1f,context+": camera focus clears and player returns to approach");
        Check(borrowed.All(n=>n.enabled)&&borrowed.Select((n,i)=>n.GetComponentInChildren<Animator>().runtimeAnimatorController==controllers[i]).All(v=>v),context+": existing soldiers recover their patrols and animation controllers");
        Check(!Object.FindObjectsByType<Renderer>().Any(r=>r.forceRenderingOff&&r.transform.IsChildOf(session.player.transform)),context+": player body visible again");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<5)return;
        try
        {
            if(error!=null)throw new Exception(error);if(EditorApplication.timeSinceStartup-began>120)throw new Exception("Physical drum QA timed out");var session=BeachBoatQuestSession.Instance;var front=BachDangFrontEnd.Instance;
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);BachDangAutoSave.Instance.enabled=false;volume=BachDangPreferences.Volume;initialized=true;BachDangPreferences.SetVolume(0);
                front.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();Dialogue.Instance.DismissForCheckpoint();Story.Instance.TransitionTo(Story.StoryState.Act1_Training,true);phase=1;return;
            }
            if(phase==1)
            {
                if(BeachBoatQuestSession.InteractionBlocked)return;drum=Object.FindObjectsByType<BeachBoatMinigameStation>().First(s=>s.kind==BeachBoatMinigameStation.ActivityKind.WarDrum);var ground=drum.GetComponentInParent<DrumTrainingGround>();Move(ground.Approach);position=session.player.transform.position;var orbit=session.storyCamera.GetComponent<BeachBoatStory.Camera_Script>();orbit.SetElevation(24);elevation=orbit.Elevation;
                borrowed=CampDiscipline.Instance.Actors.Where(a=>a.Type==ProtectedCampNPC.Kind.Soldier&&a.GetComponent<NPCWander>()?.enabled==true&&a.GetComponent<ActorSolidBody>()?.Carrier==null&&QuestRowboat.FlatDistance(a.transform.position,ground.transform.position)<350).OrderBy(a=>(a.transform.position-ground.transform.position).sqrMagnitude).Take(4).Select(a=>a.GetComponent<NPCWander>()).ToArray();controllers=borrowed.Select(n=>n.GetComponentInChildren<Animator>().runtimeAnimatorController).ToArray();
                drum.BeginActivity();Check(drum.PhysicalDrum.Running&&drum.PhysicalDrum.HandsOnly,"Training opens physical first-person hands and two real drumsticks");Check(ground.StrikeSurface is MeshCollider&&ground.StrikeSurface.enabled,"Hits use the drum's real flat mesh collider");Check(!BachDangSaveSystem.CanSave&&BeachBoatQuestSession.InputBlocked,"Saving and unrelated combat/movement are blocked during hands-on training");
                Check(drum.PhysicalDrum.SquadCount==4&&borrowed.All(n=>!n.enabled),"The lesson borrows four existing patrols rather than spawning a duplicate crowd");Check(!drum.SubmitRhythm(1)&&drum.Progress==0,"Number shortcuts cannot bypass drumstick contact");Visual(drum.PhysicalDrum);phase=2;return;
            }
            if(phase==2)
            {
                var game=drum.PhysicalDrum;Visual(game);Capture("Physical_Drum_Listen");while(game.Phase!=PhysicalDrumGame.LessonPhase.Repeat)Advance(drum,.02f);Check(game.ImpactCount==4&&game.CorrectHits==0,"The audible four-hit example teaches rhythm without granting player progress");
                var miss=game.AimAt(game.StrikeCenter+Vector3.right*30);game.Raise(true,miss);Advance(drum,.12f);game.Release(true,miss);Advance(drum,.14f);Check(drum.Mistakes==1&&game.CorrectHits==0,"A drumstick released outside the head misses instead of scoring");Advance(drum,.3f);
                var ray=game.AimAt(game.StrikeCenter);game.Raise(true,ray);Advance(drum,.15f);Check(game.CorrectHits==0,"Raising and holding a stick cannot submit a beat");game.Release(true,ray);Advance(drum,.1f);Check(game.CorrectHits==0,"A released stick must travel down before damage/contact is counted");Advance(drum,.04f);Check(game.CorrectHits==1,"Progress is awarded when the 3D stick tip reaches the actual drumhead");Visual(game);Check(game.HandStretch<.05f&&game.HandGripError<.2f,"Both drumstick hands keep fixed-length arms and contact the wrapped grips");
                phase=21;return;
            }
            if(phase==21)
            {
                Capture("Physical_Drum_Strike");var game=drum.PhysicalDrum;Check(Vector3.Distance(session.storyCamera.transform.position,drum.GetComponentInParent<DrumTrainingGround>().Eye)<1,"The real camera remains outside the drum instead of being pushed into its collider");
                Advance(drum,1.1f);Check(drum.Mistakes>=2&&game.CorrectHits==1,"Missing a rhythm window gives immediate feedback without deleting earned beats");
                Check(front.HandleEscape()&&BeachBoatMinigameStation.Active==null&&front.Page==BachDangFrontEnd.MenuPage.None,"ESC exits hands-on training directly without also opening Pause");Restored("Cancel");Check(Mathf.Abs(session.storyCamera.GetComponent<BeachBoatStory.Camera_Script>().Elevation-elevation)<.01f,"Cancel restores the previous camera elevation");phase=3;return;
            }
            if(phase==3)
            {
                position=session.player.transform.position;drum.BeginActivity();drum.AdvanceDrum(56);Check(drum.Failed&&!drum.PhysicalDrum.Running&&!drum.HasCompleted,"Timeout restores the world and grants no training completion");Restored("Timeout");drum.RetryActivity();Check(drum.PhysicalDrum.Running&&drum.Elapsed==0&&drum.Round==0,"Retry resets all three physical commands");
                var orders=new HashSet<int>();Solve(drum,g=>{if(!orders.Add(g.CommandIndex))return;Check(g.SquadCommands==g.CommandIndex+1&&g.SquadMovement>1,"Existing soldiers visibly respond to physical command "+g.CommandIndex);if(g.CommandIndex==2)Check(g.SquadGuarding==4,"The final guard order raises actual swords in a blocking animation");});Check(orders.Count==3,"All three military orders reach the squad");Check(drum.Round==3&&drum.Mistakes==0&&drum.Elapsed<=22,"A clean physical performance completes all three orders within the existing S deadline");Check(Story.Instance.currentState==Story.StoryState.Act1_Training,"Story advances only after acknowledging the training result");Check(CampaignProgress.Instance.MiniRecord(0).seconds==drum.Elapsed,"Achievements record actual lesson time, including listening and soldier responses");Restored("Success");Dialogue.Instance.Close();Check(Story.Instance.currentState==Story.StoryState.Act1_GoToCampGate,"The actual completed lesson advances quest 1 once");Dialogue.Instance.DismissForCheckpoint();phase=4;return;
            }
            if(phase==4)
            {
                if(BeachBoatQuestSession.InteractionBlocked)return;float best=drum.BestTime;drum.BeginActivity();Check(drum.PhysicalDrum.Running,"Completed saves can replay the new physical lesson");drum.CancelActivity();Check(drum.HasCompleted&&drum.BestTime==best,"Leaving a replay preserves the completed save and personal best");
                var saved=BachDangSaveSystem.Capture("QA drum completed");drum.BeginActivity();BachDangSaveSystem.Apply(saved);Check(PhysicalDrumGame.Active==null&&BeachBoatMinigameStation.Active==null&&drum.HasCompleted&&drum.BestTime==best,"Loading a completed checkpoint during replay closes the physical rig and preserves completion");Check(session.player.GetComponent<BeachBoatStory.Character_Movement>().enabled&&session.player.GetComponent<CapsuleCollider>().enabled,"Loading during a lesson restores player movement and collisions");
                foreach(var p in hashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
