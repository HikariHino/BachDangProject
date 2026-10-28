using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatGlobalWaterPlaytest
{
    const string Flag="GlobalWater938QA";static int phase,index,frame;static double began;static float at;static string failure;static Vector3 deep,shallow;
    static readonly Story.StoryState[] states={Story.StoryState.Act1_ExploreCamp,Story.StoryState.Act2_SupplyPreparation,Story.StoryState.Act2_PlantStakesAtRiver};
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string path)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(path)));
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
    public static void RunInEditor(){phase=index=0;frame=-1;began=0;failure=null;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/GlobalWaterQA/PrePlay");EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string text,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&(type==LogType.Error||type==LogType.Exception))failure=text+"\n"+stack;}
    static void Finish(string error){SessionState.SetBool(Flag,false);File.WriteAllText("Logs/GlobalWaterPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,index,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static float Feet(){var p=BeachBoatQuestSession.Instance.player;var c=p.GetComponent<CapsuleCollider>();return(c.center.y-c.height*.5f)*p.transform.lossyScale.y;}
    static Vector3 Point(float depth)
    {
        var terrain=Terrain.activeTerrain;var origin=QuestRowboat.Instance.boardingPoint.position;
        for(float r=120;r<1500;r+=10)for(int i=0;i<72;i++)
        {
            var p=origin+Quaternion.Euler(0,i*5,0)*Vector3.forward*r;p.y=terrain.SampleHeight(p)+terrain.transform.position.y;
            if(Mathf.Abs(TideSystem.CurrentWaterHeight-p.y-depth)>.65f)continue;
            var local=p-terrain.transform.position;var size=terrain.terrainData.size;if(terrain.terrainData.GetInterpolatedNormal(local.x/size.x,local.z/size.z).y<.8f)continue;
            if(RiverHullCollision.ClearAt(p+Vector3.up*10,5,BeachBoatQuestSession.Instance.player.transform))return p;
        }
        throw new Exception("No supported shoreline point for depth "+depth);
    }
    static void Place(Vector3 p,bool stand)
    {
        var actor=BeachBoatQuestSession.Instance.player;var body=actor.GetComponent<Rigidbody>();p.y=stand?p.y-Feet()+.5f:TideSystem.CurrentWaterHeight-5;actor.transform.position=p;body.position=p;if(!body.isKinematic)body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>120)throw new Exception("Global water QA timeout");
            var game=RiverBoardingCombat.Instance;var actor=BeachBoatQuestSession.Instance.player;var body=actor.GetComponent<Rigidbody>();
            if(phase==0)
            {
                foreach(var path in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[path]=Hash(path);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/GlobalWaterQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;BachDangFrontEnd.Instance.Play();at=Time.time;phase=1;return;
            }
            if(phase==1)
            {
                if(Time.time-at<1.2f)return;
                var voice=Object.FindObjectsByType<DialogueVoice>(FindObjectsSortMode.None).FirstOrDefault(v=>v.ProfileId=="narrator");
                Check(voice?.CurrentClip!=null&&voice.CurrentClip.length<1.22f,"Opening year keeps its spoken phrase without the isolated trailing audio burst");
                var sound=BeachBoatSoundscape.Instance;Check(sound.RiverSource.volume==0&&sound.WindSource.volume==0,"Opening narration has no sustained procedural ambience beneath it");
                Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();TideSystem.Instance.enabled=false;TideSystem.Instance.HoldTide(BachDangBattleTimeline.Instance,true,true);
                deep=Point(game.SwimEntryDepth+3);shallow=Point(4);phase=2;return;
            }
            if(phase==2)
            {
                Story.Instance.TransitionTo(states[index],true);BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();TideSystem.Instance.HoldTide(BachDangBattleTimeline.Instance,true,true);
                Place(deep,true);phase=3;at=Time.time;return;
            }
            if(phase==3)
            {
                if(Time.time-at<.12f)return;
                Check(game.State==RiverBoardingCombat.PlayerState.Swimming&&game.FreeSwimming&&!QuestRowboat.Instance.IsAboard,"Walking into deep water automatically swims without a boat session: "+states[index]);
                Check(body.isKinematic&&!actor.GetComponent<CapsuleCollider>().enabled&&!actor.GetComponent<BeachBoatStory.Character_Movement>().enabled,"Swim owns movement instead of underwater walking physics: "+states[index]);
                var before=actor.transform.position;game.StepPlayer(.1f,Vector2.up);Check(QuestRowboat.FlatDistance(before,actor.transform.position)>1,"Free swimming actually moves the character: "+states[index]);
                var data=BachDangSaveSystem.Capture("Free swim regression");BachDangSaveSystem.Apply(data);
                Check(game.FreeSwimming&&game.State==RiverBoardingCombat.PlayerState.Swimming&&body.isKinematic&&!actor.GetComponent<CapsuleCollider>().enabled,"Save/load retains free swimming before quest 3: "+states[index]);
                Place(shallow,false);game.StepPlayer(.02f,Vector2.zero);
                Check(game.State==RiverBoardingCombat.PlayerState.Land&&!game.FreeSwimming&&actor.GetComponent<BeachBoatStory.Character_Movement>().enabled&&!body.isKinematic,"A shallow shore restores walking and physics outside battle: "+states[index]);
                for(int n=0;n<30;n++)game.StepPlayer(.02f,Vector2.zero);Check(game.State==RiverBoardingCombat.PlayerState.Land,"Shallow wading does not flicker between swim and walking: "+states[index]);
                if(++index<states.Length){phase=2;return;}
                QuestRowboat.Instance.RestoreSave(true,QuestRowboat.Instance.transform.position,QuestRowboat.Instance.transform.rotation);
                Check(game.State==RiverBoardingCombat.PlayerState.Rowboat&&!game.FreeSwimming,"Boarding after a free swim returns to the rowing state");QuestRowboat.Instance.LeaveForCheckpoint();
                foreach(var path in hashes)Check(File.Exists(path.Key)&&Hash(path.Key)==path.Value,"User save unchanged: "+Path.GetFileName(path.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
