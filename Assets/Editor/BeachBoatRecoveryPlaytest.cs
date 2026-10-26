using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatRecoveryPlaytest
{
    const string Flag="Recovery938QA";
    static int phase,lastFrame,oldSlot;static double started;static UnityEngine.SceneManagement.Scene originalScene;
    static string error,oldDirectory,qaDirectory,autoHash,manualHash;
    static BachDangSaveData boatSave,finishedSave;
    static readonly List<string> checks=new List<string>();
    static readonly Dictionary<string,string> originals=new Dictionary<string,string>();
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
    static string Hash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
    public static void RunInEditor()
    {
        if(EditorApplication.isPlaying)throw new Exception("Start in Edit Mode");
        phase=0;lastFrame=-1;started=0;error=null;checks.Clear();originals.Clear();
        oldDirectory=BachDangSaveSystem.TestDirectory;oldSlot=Mathf.Clamp(BachDangSaveSystem.ActiveSlot,1,10);
        SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string message,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Exception||type==LogType.Error))error=message+"\n"+stack;}
    static void Finish(string failure)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/RecoveryPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=failure==null,failure,phase,checks,loadSeconds=BachDangFrontEnd.Instance?.LastLoadSeconds,archerShots=Object.FindAnyObjectByType<RiverShoreArchers>()?.ShotsFired,archerHits=Object.FindAnyObjectByType<RiverShoreArchers>()?.Hits,archerKills=Object.FindAnyObjectByType<RiverShoreArchers>()?.Kills},Newtonsoft.Json.Formatting.Indented));
        SessionState.SetBool(Flag,false);Time.timeScale=0;BachDangSaveSystem.TestDirectory=oldDirectory;BachDangSaveSystem.SelectSlot(Mathf.Clamp(oldSlot,1,10));EditorApplication.isPlaying=false;
    }
    static void Invoke(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    static void Setup()
    {
        foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))if(p.EndsWith(".json")||p.EndsWith(".bak"))originals[p]=Hash(p);
        qaDirectory=Path.GetFullPath(".utmp/RecoveryQA/"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));Directory.CreateDirectory(qaDirectory);BachDangSaveSystem.TestDirectory=qaDirectory;
        var auto=BachDangAutoSave.Instance;auto.enabled=false;
        Check(!File.Exists(BachDangSaveSystem.TemporaryPath)&&auto.RetryPoint==null,"Title does not autosave or write to manual slots");
        Check(!Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(c=>c.enabled&&BeachBoatSoftVegetationRevision.SoftCollider(c)),"Every scene grass/bush collider is passable");
        var prototypes=Terrain.activeTerrain.terrainData.treePrototypes;
        Check(prototypes.Where(p=>BeachBoatSoftVegetationRevision.SoftObject(p.prefab)).All(p=>p.prefab.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled)),"All terrain grass, ferns and shrubs have disabled colliders");
        Check(prototypes.Where(p=>!BeachBoatSoftVegetationRevision.SoftObject(p.prefab)).All(p=>p.prefab.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled)),"Tree trunks and terrain rocks remain solid");
        Check(BeachBoatStory.RiverStakeZone.Instance.RequiredCount>=1246,"Stake field retained");
        BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
    }
    static void ClockStart()
    {
        var auto=BachDangAutoSave.Instance;auto.Step(.01f);
        Check(BachDangSaveSystem.TryReadTemporary(out _),"A new journey creates a valid isolated recovery baseline");autoHash=Hash(BachDangSaveSystem.TemporaryPath);
        auto.Step(59.9f);Check(Hash(BachDangSaveSystem.TemporaryPath)==autoHash,"Autosave does not run before 60 active seconds");
        BachDangFrontEnd.Instance.OpenPause();float elapsed=auto.Elapsed;auto.Step(120);
        Check(auto.Elapsed==elapsed&&Hash(BachDangSaveSystem.TemporaryPath)==autoHash,"Pause time neither advances autosave nor overwrites recovery data");BachDangFrontEnd.Instance.Resume();
    }
    static void BoatDamage()
    {
        var auto=BachDangAutoSave.Instance;auto.Step(.2f);
        Check(Hash(BachDangSaveSystem.TemporaryPath)!=autoHash&&auto.Elapsed==0,"Crossing 60 active seconds writes a new autosave and resets the clock");
        Check(Enumerable.Range(1,10).All(i=>!BachDangSaveSystem.HasData(i)),"Autosave never occupies any of the ten manual slots");
        Story.Instance.collectedWood=5;Story.Instance.TransitionTo(Story.StoryState.Act2_PlantStakesAtRiver,true);
        BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        var boat=QuestRowboat.Instance;boat.RestoreSave(true,new Vector3(-3400,TideSystem.CurrentWaterHeight,1880),Quaternion.identity);boat.RestoreDurability(100);
    }
    static void DamageAndRetry()
    {
        var boat=QuestRowboat.Instance;var auto=BachDangAutoSave.Instance;
        float health=BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>().Health;
        var wall=new GameObject("QA_Hull_Obstacle");var collider=wall.AddComponent<BoxCollider>();collider.size=new Vector3(200,100,4);
        wall.transform.position=boat.transform.position+Vector3.forward*(BoatHullCollision.Get(boat.transform).SolidHull.bounds.extents.z+2);
        Physics.SyncTransforms();var start=boat.transform.position;Set(boat,"<Speed>k__BackingField",95f);boat.Step(1,0,.05f);
        Check(boat.Durability<100&&QuestRowboat.FlatDistance(start,boat.transform.position)<.01f,"Actual movement sweep stops a fast boat at a solid obstacle and applies hull damage");
        Object.DestroyImmediate(wall);boat.RestoreDurability(100);Physics.SyncTransforms();
        boat.ApplyCollisionImpact(QuestRowboat.BoatObstacleKind.Stake,boat.maxSpeed);
        Check(boat.Durability==72&&BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>().Health==health,"A full-speed stake collision damages hull by 28 instead of player health");
        boat.ApplyCollisionImpact(QuestRowboat.BoatObstacleKind.Stake,boat.maxSpeed);Check(boat.Durability==72,"Persistent contact has a cooldown, not damage every frame");
        Check(QuestRowboat.ImpactDamage(QuestRowboat.BoatObstacleKind.Shore,3,95)==0,"Gentle docking is harmless");
        Check(QuestRowboat.ImpactDamage(QuestRowboat.BoatObstacleKind.Ship,30,95)<QuestRowboat.ImpactDamage(QuestRowboat.BoatObstacleKind.Ship,95,95),"Faster collision means greater hull damage");
        Check(BachDangSaveSystem.Save(1,"QA manual hull checkpoint"),"Manual slot stores damaged rowboat");
        Check(BachDangSaveSystem.TryRead(1,out boatSave)&&boatSave.boatDurability==72,"Durability roundtrips through the actual checksum-protected slot");manualHash=Hash(BachDangSaveSystem.SlotPath(1));
        float recorded=auto.RetryPoint.campaign.seconds;CampaignProgress.Instance.Record.seconds+=10;
        Check(auto.RetryPoint.campaign.seconds==recorded,"A live campaign timer cannot mutate an already saved retry snapshot");
        Check(BachDangSaveSystem.TryReadResume(out var latest,out int slot)&&slot==1&&latest.savedUtcTicks==boatSave.savedUtcTicks,"A newer manual save takes priority over an older temporary save");
        boat.RestoreDurability(20);boat.ApplyCollisionImpact(QuestRowboat.BoatObstacleKind.Ship,95);
        Check(boat.IsBroken&&QuestCheckpoint.Instance.IsDefeated&&Time.timeScale==0,"Zero hull durability pauses gameplay on defeat");
        Check(!boat.TryBoard()&&!BachDangSaveSystem.CanSave,"Broken boat cannot be boarded or written over a valid recovery save");
        string before=Hash(BachDangSaveSystem.TemporaryPath);auto.Step(1000);Check(before==Hash(BachDangSaveSystem.TemporaryPath),"Defeat cannot overwrite the last healthy autosave");
        originalScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();Check(QuestCheckpoint.Instance.Retry(),"Retry accepts the latest recovery point");
        Check(!QuestCheckpoint.Instance.IsDefeated&&boat.Durability==72&&boat.IsAboard,"Retry restores the saved hull health and seated player");
        Check(Vector3.Distance(boat.transform.position,boatSave.boatPosition)<.1f,"Retry restores exact saved river position instead of restarting the act");
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene()==originalScene,"Retry restores in place without rebuilding the scene");
    }
    static void SaveIsolation()
    {
        var boat=QuestRowboat.Instance;var auto=BachDangAutoSave.Instance;boat.RestoreDurability(51);auto.Step(60.1f);
        Check(BachDangSaveSystem.TryReadTemporary(out var temporary)&&temporary.boatDurability==51,"Autosave captures current hull durability");
        Check(Hash(BachDangSaveSystem.SlotPath(1))==manualHash,"Later autosaves leave a manual slot byte-identical");
        Check(BachDangSaveSystem.TryReadResume(out var latest,out int slot)&&slot==0&&latest.boatDurability==51,"Continue prefers a newer autosave");
        auto.Step(60);var bytes=File.ReadAllBytes(BachDangSaveSystem.TemporaryPath);File.WriteAllText(BachDangSaveSystem.TemporaryPath,"corrupt QA data");
        Check(BachDangSaveSystem.TryReadTemporary(out var fallback)&&fallback.boatDurability==51,"A corrupt temporary file recovers its verified backup");File.WriteAllBytes(BachDangSaveSystem.TemporaryPath,bytes);
        var legacy=JsonUtility.FromJson<BachDangSaveData>(JsonUtility.ToJson(boatSave));legacy.rowboatDurabilityVersion=0;legacy.boatDurability=0;BachDangSaveSystem.Apply(legacy);
        Check(boat.Durability==100,"Old saves without a hull field migrate to 100 durability");
        var p=CampaignProgress.Instance.Record;p.finished=true;p.finalRank="A";p.rankRulesVersion=CampaignProgress.RankRulesVersion;
        Check(BachDangSaveSystem.Save(2,"QA finished A rank"),"Completed result saves into a separate chosen slot");BachDangSaveSystem.TryRead(2,out finishedSave);
        var front=BachDangFrontEnd.Instance;Check(front.LoadGame(),"Load menu still exposes manual slot selection");
        var label=front.transform.Find("SaveSlots/Slot2/Label").GetComponent<TMPro.TMP_Text>();
        Check(label.text.Contains("✓ HOÀN THÀNH")&&label.text.Contains("HẠNG A"),"Completed slot displays a checkmark, completed label and rank");
        Check(front.transform.Find("SaveSlots/Slot2").GetComponent<Image>().color!=front.transform.Find("SaveSlots/Slot1").GetComponent<Image>().color,"Completed save has a distinct card color");
        CampaignJournalUI.Instance.enabled=false;Check(front.ChooseSlot(2),"Load completed slot through the real menu action");
    }
    static void FinishedLoaded()
    {
        var front=BachDangFrontEnd.Instance;Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene()==originalScene,"Completed save loads without reloading terrain and every asset");
        Check(CampaignProgress.Instance.Record.finished&&CampaignProgress.Instance.Record.finalRank=="A","Fast load restores the completed result and A rank");
        Check(front.LastLoadSeconds<5,"Warm completed-save restore takes under five seconds (measured)");
        Check(front.LoadGame()&&front.ChooseSlot(1),"Load an earlier unfinished slot after a completed result");
    }
    static void UnfinishedLoaded()
    {
        Check(!CampaignProgress.Instance.Record.finished&&QuestRowboat.Instance.Durability==72,"Earlier manual slot restores its own unfinished story and durability");
        Check(BachDangSaveSystem.TryReadTemporary(out var branch)&&branch.boatDurability==72&&!branch.campaign.finished,"Selecting an older slot branches temporary recovery from that exact progress");
        Check(Hash(BachDangSaveSystem.SlotPath(1))==manualHash,"Loading and branching do not change the original manual slot");
        int hit=0,lethal=0;for(int i=0;i<10000;i++){if(RiverShoreArchers.RollHit(i/10000f,0))hit++;if(RiverShoreArchers.RollLethal(i/10000f))lethal++;}
        Check(hit==6500&&lethal==2500,"Shore archer rolls have both misses and lethal/nonlethal outcomes at documented rates");
        Check(RiverShoreArchers.HitChance(4000)<RiverShoreArchers.HitChance(200),"Distant enemies are harder for shore archers to hit");
        var fleet=EnemyFleetDirector.Instance;Story.Instance.TransitionTo(Story.StoryState.Act3_BattleInProgress,true);BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        fleet.StopAllCoroutines();Set(fleet,"<BattleStarted>k__BackingField",true);Set(fleet,"<AmbushStarted>k__BackingField",true);BachDangBattleTimeline.Instance.BeginAmbush();
        foreach(var ship in fleet.enemyShips){ship.Activate();ship.enabled=false;ship.TakeDamage();ship.Step(8);}
        RiverBoardingCombat.Instance.enabled=false;RiverPlayerCombat.Instance.enabled=false;RiverPlayerCombat.Instance.ReturnFireEnabled=false;
        var archers=fleet.GetComponent<RiverShoreArchers>();archers.enabled=false;Invoke(archers,"BuildArchers");
        Check(archers.ArcherCount>=4,"At least four visible allied archers find actual dry-bank positions");
        foreach(var go in archers.GetComponentsInChildren<RiverFighterPose>(true))
            Check(go.transform.position.y>=TideSystem.Instance.highTideY,"Ambush archer stands on dry land above the high tide");
        Check(archers.ShotsFired==0,"Archers spawn before releasing their first arrows");
        // Accelerate only the finite projectile simulation and firing cooldowns.
        // Existing guards, real terrain, hulls and cover still resolve each shot.
        for(int round=0;round<160;round++)
        {
            var roster=(System.Collections.IEnumerable)typeof(RiverShoreArchers).GetField("archers",Private).GetValue(archers);
            foreach(var a in roster)a.GetType().GetField("nextShot").SetValue(a,-1f);
            archers.Step(.4f);
        }
        Check(archers.ShotsFired>12,"Ambush sends a finite pool of arrows from actual allied shoreline positions");
        Check(archers.Hits>0&&archers.ShotsFired>archers.Hits,"Some shoreline arrows hit real surviving enemies while others miss or hit cover");
        Check(archers.Kills>0&&RiverPlayerCombat.Instance.AlliedKills>=archers.Kills,"Successful shoreline kills count for allies");
        Check(RiverPlayerCombat.Instance.Kills==boatSave.fleet.kills,"Allied archers do not steal credit in the player's personal kill achievement");
        var voices=fleet.GetComponent<RiverBattleVoices>();voices.enabled=false;voices.ResetForNewBattle();
        Check(voices.AssetsReady,"All eight offline battle voice clips and three human cries are packaged and loadable");
        Set(fleet,"<AmbushStarted>k__BackingField",false);BachDangBattleTimeline.Instance.BeginLure();voices.Step(.1f);
        Check(voices.InvasionCues>0&&!voices.AmbushPlayed,"Invaders cheer during the approach, before the ambush");
        Set(fleet,"<AmbushStarted>k__BackingField",true);BachDangBattleTimeline.Instance.BeginAmbush();voices.Step(.1f);
        Check(voices.AmbushPlayed&&voices.CommanderCues==1,"Ngô Quyền commands the counterattack exactly when the ambush begins");
        var pending=fleet.CaptureSave();voices.RestoreSave(pending);voices.Step(.1f);
        Check(voices.CommanderCues==1&&pending.counterattackDelay>0,"A mid-command save retains the pending second order without replaying the first");
        for(int i=0;i<160;i++)voices.Step(.2f);
        Check(voices.PanicCues>0&&voices.CommanderCues==2,"Panic cries replace advance cheers and the second command follows once");
        var voiced=fleet.CaptureSave();voices.RestoreSave(voiced);int commands=voices.CommanderCues;voices.Step(.1f);
        Check(voices.InvasionPlayed&&voices.AmbushPlayed&&voices.CommanderCues==commands,"Save/load retains battle cue flags without repeating the commander event");
        voices.Silence();
        archers.ResetAmbush();Check(archers.GetComponentsInChildren<RiverFighterPose>().Length==0,"Shore ambush resets without leaving active archers in another load");
        foreach(var file in originals)Check(File.Exists(file.Key)&&Hash(file.Key)==file.Value,"Real user save unchanged: "+Path.GetFileName(file.Key));
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
        if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<3)return;
        if(EditorApplication.timeSinceStartup-started>180){Finish("Timeout");return;}
        try
        {
            if(error!=null)throw new Exception(error);
            if(BeachBoatQuestSession.Instance==null||RiverBoardingCombat.Instance==null)return;
            if(phase==0){Setup();phase=1;return;}
            if(phase==1){ClockStart();phase=2;return;}
            if(phase==2){BoatDamage();phase=3;return;}
            if(phase==3){DamageAndRetry();phase=4;return;}
            if(phase==4){SaveIsolation();phase=5;return;}
            if(phase==5){if(BachDangFrontEnd.Instance.Page==BachDangFrontEnd.MenuPage.Loading)return;FinishedLoaded();phase=6;return;}
            if(phase==6){if(BachDangFrontEnd.Instance.Page==BachDangFrontEnd.MenuPage.Loading)return;UnfinishedLoaded();Finish(null);}
        }
        catch(Exception exception){Finish(exception.ToString());}
    }
}
