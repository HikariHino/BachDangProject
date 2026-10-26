using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatNpcLoadPlaytest
{
    const string Flag="NpcLoad938QA";
    static int phase,index,lastFrame=-1;static double began,waitUntil;static string runtimeError;
    static readonly List<string> checks=new List<string>();
    static readonly List<BachDangSaveData> saves=new List<BachDangSaveData>();
    static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static BachDangSaveData fixture;static string smallName,hostileId;
    public static void RunInEditor(){phase=index=0;lastFrame=-1;began=waitUntil=0;runtimeError=null;checks.Clear();saves.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string message,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Error||type==LogType.Exception))runtimeError=message+"\n"+stack;}
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
    static string Hash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
    static void Finish(string error)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/NpcLoadPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,realSaveSlots=saves.Count,checks},Newtonsoft.Json.Formatting.Indented));
        SessionState.SetBool(Flag,false);BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
    }
    static void Audit(string label)
    {
        var game=RiverBoardingCombat.Instance;var reference=BeachBoatQuestSession.Instance.player.GetComponentInChildren<Animator>();int people=0;
        foreach(var boat in game.friendlyBoats)
        {
            var roster=boat.GetComponentInChildren<FleetDeckCrew>(true);var hull=BoatHullCollision.Get(boat.transform);
            foreach(var person in roster.soldiers)
            {
                if(!person.gameObject.activeInHierarchy)continue;people++;
                Check(hull.TryDeckSurface(person.position,out var floor),label+": crew remains on its own hull: "+boat.name+"/"+person.name);
                var rig=person.GetComponentInChildren<Animator>();ActorSolidBody.WalkingDimensions(rig,out _,out float sole,out _);
                Check(Mathf.Abs(rig.transform.position.y+sole-floor.y)<1,label+": boots supported by timber: "+boat.name+"/"+person.name);
                Check(Mathf.Abs(EnemyDeckGuard.RigHeight(rig)/EnemyDeckGuard.RigHeight(reference)-1)<.06f,label+": character stature retained: "+boat.name+"/"+person.name);
                Check(person.GetComponent<ActorSolidBody>().Carrier==boat.transform,label+": actor collision follows its actual carrier: "+boat.name+"/"+person.name);
            }
        }
        Check(people>0,label+": checks actual visible crew");
    }
    static void ApplyAndCheck(BachDangSaveData data,string label)
    {
        var walkers=Object.FindObjectsByType<NPCWander>().Select(w=>new{w,p=w.transform.position}).ToArray();
        BachDangSaveSystem.Apply(data);
        Check((int)Story.Instance.currentState==data.storyState&&Story.Instance.collectedWood==data.woodCount,label+": quest progress retained");
        Check(BeachBoatStory.RiverStakeZone.Instance.CaptureCheckpoint().SequenceEqual(data.stakes),label+": all five work clusters retained");
        Check(walkers.All(x=>Vector3.Distance(x.p,x.w.transform.position)<.01f),label+": loading does not teleport camp or village walkers");
        var restored=RiverBoardingCombat.Instance.CaptureSave();
        foreach(var d in restored.defenders)
        {
            var old=(data.boarding?.defenders??Array.Empty<BachDangSaveData.Fighter>()).FirstOrDefault(v=>v.id==d.id);if(old==null)continue;
            Check(d.health==old.health&&d.active==old.active,label+": crew health/death retained: "+d.id);
        }
        Audit(label);
    }
    static void MakeFixture()
    {
        fixture=BachDangSaveSystem.Capture("NPC load regression fixture");fixture.storyState=(int)Story.StoryState.Act2_PlantStakesAtRiver;fixture.dialogue=null;fixture.fleet.active=false;
        fixture.boarding.state=(int)RiverBoardingCombat.PlayerState.Rowboat;fixture.boarding.sword=false;
        foreach(var a in fixture.boarding.allies){a.active=false;a.health=6;}
        foreach(var h in fixture.boarding.hostiles)h.active=false;
        foreach(var s in fixture.ships){s.active=false;s.destroyed=false;s.started=false;}
        foreach(var d in fixture.boarding.defenders){d.active=true;d.health=4;d.position=new Vector3(4000,900,-3000);}
        fixture.boarding.defenders[0].health=2;fixture.boarding.defenders[1].health=0;fixture.boarding.defenders[1].active=false;
        var small=RiverBoardingCombat.Instance.friendlyBoats.First(b=>b.lightPatrol);smallName=small.name;
        var ally=fixture.boarding.allies[0];ally.active=true;ally.health=3;ally.state=3;ally.boat=smallName;ally.local=Vector3.forward*15;ally.position=new Vector3(4000,900,-3000);
        var hostile=fixture.boarding.hostiles.First(h=>h.id.StartsWith("pool/"));hostileId=hostile.id;hostile.active=true;hostile.state=3;hostile.boat=smallName;hostile.source=fixture.ships[0].name;hostile.local=Vector3.back*15;hostile.position=new Vector3(-4000,900,3000);
        var guard=fixture.guards.Single(g=>g.id==hostileId);guard.active=true;guard.health=1;guard.independent=true;guard.floatingCorpse=false;guard.position=hostile.position;
    }
    static void CheckFighters(string label)
    {
        var data=RiverBoardingCombat.Instance.CaptureSave();var carrier=RiverBoardingCombat.Instance.friendlyBoats.First(b=>b.name==smallName).transform;var hull=BoatHullCollision.Get(carrier);
        var ally=data.allies[0];var hostile=data.hostiles.Single(h=>h.id==hostileId);var guard=RiverPlayerCombat.Instance.guards.Single(g=>BachDangSaveSystem.GuardId(g)==hostileId);
        Check(ally.health==3&&ally.active&&ally.state==3&&hull.TryDeckSurface(ally.position,out _),label+": allied boarder stays alive on the actual deck");
        Check(guard.Health==1&&hostile.active&&hostile.state==3&&hull.TryDeckSurface(hostile.position,out _),label+": enemy boarder keeps health and actual deck support");
        Check(guard.GetComponent<ActorSolidBody>().Carrier==carrier&&guard.IsInMelee,label+": guard restore does not undo its boarding/melee state");
        var pose=guard.GetComponent<RiverFighterPose>();Check(pose.Traversal==RiverFighterPose.Motion.Idle&&pose.SwordVisible,label+": enemy keeps a standing sword pose after load");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(began==0)began=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-began<5||EditorApplication.timeSinceStartup<waitUntil||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);if(EditorApplication.timeSinceStartup-began>180)throw new Exception("NPC load regression timed out");
            if(phase==0)
            {
                for(int slot=1;slot<=BachDangSaveSystem.SlotCount;slot++)if(BachDangSaveSystem.TryRead(slot,out var data)){saves.Add(data);var path=BachDangSaveSystem.SlotPath(slot);if(File.Exists(path))hashes[path]=Hash(path);if(File.Exists(path+".bak"))hashes[path+".bak"]=Hash(path+".bak");}
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/NpcLoadQA/"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));
                BachDangAutoSave.Instance.enabled=false;
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                if(saves.Count==0)saves.Add(BachDangSaveSystem.Capture("No user slots; current-state fixture"));phase=1;return;
            }
            if(phase==1){ApplyAndCheck(saves[index],"Existing slot "+(index+1));phase=2;return;}
            if(phase==2){Audit("Next frame after slot "+(index+1));if(++index<saves.Count){phase=1;return;}phase=3;return;}
            if(phase==3){MakeFixture();ApplyAndCheck(fixture,"New save with deliberately stale world positions");CheckFighters("New save");phase=4;return;}
            if(phase==4)
            {
                var current=BachDangSaveSystem.Capture("Round-trip NPC fixture");var small=RiverBoardingCombat.Instance.friendlyBoats.First(b=>b.name==smallName);small.transform.position+=Vector3.right*300;
                ApplyAndCheck(current,"Repeated current-layout load");CheckFighters("Repeated load");
                foreach(var patrol in fixture.patrols)patrol.layoutVersion=0;
                ApplyAndCheck(fixture,"Legacy layout migration with boarders");CheckFighters("Legacy layout migration");
                BachDangSaveSystem.PendingLoad=saves[0];phase=5;UnityEngine.SceneManagement.SceneManager.LoadScene("beachBoat");waitUntil=EditorApplication.timeSinceStartup+3;return;
            }
            if(phase==5)
            {
                if(BachDangSaveSystem.PendingLoad!=null||RiverBoardingCombat.Instance==null||!BachDangFrontEnd.Instance.GameStarted)return;
                Audit("Real front-end scene reload");
                Check((int)Story.Instance.currentState==saves[0].storyState,"Cold scene load retains the actual saved quest stage");
                foreach(var pair in hashes)Check(Hash(pair.Key)==pair.Value,"User save remains byte-for-byte unchanged: "+Path.GetFileName(pair.Key));
                Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
