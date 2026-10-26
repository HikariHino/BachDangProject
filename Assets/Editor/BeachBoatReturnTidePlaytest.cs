using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;
using Zone=BeachBoatStory.RiverStakeZone;

public static class BeachBoatReturnTidePlaytest
{
    const string Flag="ReturnTide938QA";static int phase,lastFrame=-1,originalSlot;static double began,waitUntil;static string runtimeError,originalDirectory;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static BachDangSaveData planted,victory;static Vector3[] wreckPositions,wreckMeshPositions;static Quaternion[] wreckRotations;
    static string qaDirectory;
    public static void RunInEditor(){phase=0;lastFrame=-1;began=waitUntil=0;runtimeError=null;checks.Clear();hashes.Clear();originalDirectory=BachDangSaveSystem.TestDirectory;originalSlot=Mathf.Clamp(BachDangSaveSystem.ActiveSlot,1,BachDangSaveSystem.SlotCount);SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string text,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Error||type==LogType.Exception))runtimeError=text+"\n"+stack;}
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
    static string Hash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
    static void Finish(string error)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ReturnTidePlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));
        SessionState.SetBool(Flag,false);BachDangSaveSystem.TestDirectory=originalDirectory;BachDangSaveSystem.SelectSlot(Mathf.Clamp(originalSlot,1,BachDangSaveSystem.SlotCount));BachDangSaveSystem.PendingLoad=null;Time.timeScale=1;EditorApplication.isPlaying=false;
    }
    static BachDangSaveData DiskRoundTrip(string label)
    {
        Check(BachDangSaveSystem.Save(10,label),label+": writes a verified save in the isolated QA directory");
        Check(BachDangSaveSystem.TryRead(10,out var data),label+": reads the actual checksum-protected save file");
        return data;
    }
    static void Apply(BachDangSaveData data)
    {
        BachDangSaveSystem.Apply(data);EnemyFleetDirector.Instance.StopAllCoroutines();TideSystem.Instance.enabled=false;
        foreach(var b in RiverBoardingCombat.Instance.friendlyBoats)b.enabled=false;foreach(var ship in EnemyFleetDirector.Instance.enemyShips)ship.enabled=false;
        RiverBoardingCombat.Instance.enabled=false;RiverPlayerCombat.Instance.enabled=false;RiverPlayerCombat.Instance.ReturnFireEnabled=false;
        BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
    }
    static void Advance(float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++)TideSystem.Instance.Step(.02f);}
    static void AssertRising(string label,float expectedHeight)
    {
        var tide=TideSystem.Instance;
        Check(tide.IsRising&&tide.IsTideLocked&&!tide.IsControlledEbb&&Mathf.Abs(tide.TargetWaterHeight-tide.highTideY)<.01f,label+": high water is reserved with no stale ebb/low lock");
        Check(Mathf.Abs(TideSystem.CurrentWaterHeight-expectedHeight)<.05f,label+": keeps the saved water height without a jump");
    }
    static void PlantingSetup()
    {
        BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        foreach(var path in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))if(path.EndsWith(".json",StringComparison.Ordinal)||path.EndsWith(".bak",StringComparison.Ordinal))hashes[path]=Hash(path);
        qaDirectory=Path.GetFullPath(".utmp/ReturnTideQA/"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));BachDangSaveSystem.TestDirectory=qaDirectory;
        var tide=TideSystem.Instance;var boat=QuestRowboat.Instance;var zone=Zone.Instance;tide.enabled=false;
        Story.Instance.collectedWood=5;zone.RestoreCheckpoint(new[]{true,true,true,true,false});Story.Instance.TransitionTo(Story.StoryState.Act2_PlantStakesAtRiver,true);
        tide.HoldTide(boat,false,true);Check(zone.CompletedClusterCount==4&&!zone.IsComplete&&tide.TargetWaterHeight==tide.lowTideY,"Incomplete planting keeps low tide and unfinished fifth work group");
        var point=zone.placements[4].approach.position;point.y=tide.lowTideY;boat.RestoreSave(true,point,Quaternion.identity);BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        foreach(var patrol in RiverBoardingCombat.Instance.friendlyBoats)patrol.enabled=false;RiverPlayerCombat.Instance.ReturnFireEnabled=false;
        Check(zone.TryPlant(4),"Player starts the actual final planting coroutine");
    }
    static void PlantingComplete()
    {
        var tide=TideSystem.Instance;
        Check(Story.Instance.currentState==Story.StoryState.Act2_CompleteReturnToCamp&&Zone.Instance.IsComplete,"Completing the real final cluster immediately advances to the report objective");
        AssertRising("Final planting callback",tide.lowTideY);
        typeof(QuestRowboat).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(QuestRowboat.Instance,null);
        Check(tide.TargetWaterHeight==tide.highTideY,"Rowboat Update no longer re-locks return-to-camp water at low tide");
        Advance(.4f);Check(TideSystem.CurrentWaterHeight>tide.lowTideY+1&&TideSystem.CurrentWaterHeight<tide.highTideY-1,"Water visibly rises after planting instead of teleporting or remaining low");
        planted=DiskRoundTrip("Mid-rise after planting");Apply(planted);AssertRising("Loading mid-rise planting save",planted.tide.height);
        float paused=TideSystem.CurrentWaterHeight;tide.Step(0);Check(TideSystem.CurrentWaterHeight==paused,"Paused game time does not advance the returning tide");
        Advance(6);Check(Zone.Instance.ExposedStakeCount==0&&Mathf.Abs(TideSystem.CurrentWaterHeight-tide.highTideY)<.02f,"Post-planting flood covers every stake");
        var flooded=DiskRoundTrip("Flooded return to camp");Apply(flooded);AssertRising("Loading fully flooded planting save",flooded.tide.height);
        var legacy=JsonUtility.FromJson<BachDangSaveData>(JsonUtility.ToJson(planted));legacy.tide.height=legacy.tide.target=tide.lowTideY;legacy.tide.high=legacy.tide.rising=legacy.tide.controlled=false;legacy.tide.locked=true;
        Apply(legacy);AssertRising("Legacy completed-planting low-water save",tide.lowTideY);Advance(6);Check(Zone.Instance.ExposedStakeCount==0,"Legacy completed-planting save now conceals stakes too");
        tide.ToggleTide();Check(tide.TargetWaterHeight==tide.highTideY,"The return journey retains high water instead of cycling down again");
    }
    static Vector3 WreckMeshPoint(EnemyShip ship)
    {
        var mesh=ship.transform.Find("BrokenWarship/Bow").GetComponentInChildren<MeshFilter>(true);return mesh.transform.TransformPoint(mesh.sharedMesh.bounds.center);
    }
    static void BattleAndVictory()
    {
        var tide=TideSystem.Instance;var fleet=EnemyFleetDirector.Instance;var story=Story.Instance;
        story.TransitionTo(Story.StoryState.Act3_BattleInProgress,true);fleet.StartBattle();fleet.StopAllCoroutines();
        BachDangBattleTimeline.Instance.BeginEbb();Advance(3);var ebb=DiskRoundTrip("During battle ebb");Apply(ebb);
        Check(tide.IsControlledEbb&&tide.TargetWaterHeight==tide.lowTideY&&Mathf.Abs(tide.CaptureSave().elapsed-ebb.tide.elapsed)<.01f,"Battle save retains controlled ebb and elapsed time; return policy does not raise water during combat");
        float before=TideSystem.CurrentWaterHeight;Advance(.5f);Check(TideSystem.CurrentWaterHeight<before,"Loaded battle ebb continues falling");
        tide.HoldTide(BachDangBattleTimeline.Instance,false,true);story.totalEnemyShips=fleet.ShipCount;story.destroyedEnemyShips=0;
        foreach(var ship in fleet.enemyShips)
        {
            ship.transform.SetPositionAndRotation(new Vector3(ship.targetPoint.position.x,tide.lowTideY,ship.targetPoint.position.z),Quaternion.Euler(0,180,0));ship.Activate();ship.enabled=false;ship.TakeDamage();ship.Step(8);
        }
        Check(fleet.AllShipsSunk&&fleet.RemainingEnemies>0&&story.currentState==Story.StoryState.Act3_BattleInProgress,"Settled wrecks with surviving enemies do not trigger an early victory or rising tide");
        Check(tide.TargetWaterHeight==tide.lowTideY,"Water stays low until the combat objective is actually complete");
        foreach(var guard in RiverPlayerCombat.Instance.guards)guard.DefeatFromVolley();story.OnBattleVictory();
        Check(story.currentState==Story.StoryState.Act3_Victory&&fleet.RemainingEnemies==0,"Defeating the final enemy and all ten ships really completes the quest");
        // The fixture stopped the director coroutine during the ebb load check.
        // Complete its normal exit too, before capturing a finished-battle save.
        var ended=fleet.CaptureSave();ended.active=false;fleet.RestoreSave(ended);
        AssertRising("Victory callback",tide.lowTideY);
        wreckPositions=fleet.enemyShips.Select(s=>s.transform.position).ToArray();wreckRotations=fleet.enemyShips.Select(s=>s.transform.rotation).ToArray();wreckMeshPositions=fleet.enemyShips.Select(WreckMeshPoint).ToArray();
        Advance(.4f);Check(TideSystem.CurrentWaterHeight>tide.lowTideY+1,"Victory immediately begins a visible flood over the stakes");AssertWrecks("Rising water");
        victory=DiskRoundTrip("Mid-rise after victory");Apply(victory);AssertRising("Loading mid-rise victory save",victory.tide.height);AssertWrecks("Loading mid-rise victory save");
        Advance(6);Check(Zone.Instance.ExposedStakeCount==0,"Victory flood hides all stakes for free travel");AssertWrecks("Fully flooded battlefield");
        var flooded=DiskRoundTrip("Flooded victory with wrecks");Apply(flooded);AssertRising("Loading fully flooded victory save",flooded.tide.height);AssertWrecks("Loading fully flooded victory save");
        var old=JsonUtility.FromJson<BachDangSaveData>(JsonUtility.ToJson(victory));old.tide.height=old.tide.target=tide.lowTideY;old.tide.high=old.tide.rising=old.tide.controlled=false;old.tide.locked=false;
        Apply(old);AssertRising("Legacy victory save stranded at low water",tide.lowTideY);AssertWrecks("Legacy victory load");Advance(6);
        var fresh=DiskRoundTrip("Cold-load victory with retained wrecks");BachDangSaveSystem.PendingLoad=fresh;UnityEngine.SceneManagement.SceneManager.LoadScene("beachBoat");waitUntil=EditorApplication.timeSinceStartup+3;
    }
    static void AssertWrecks(string label)
    {
        var ships=EnemyFleetDirector.Instance.enemyShips;
        for(int i=0;i<ships.Length;i++)
        {
            var ship=ships[i];Check(ship.gameObject.activeSelf&&ship.isDestroyed&&ship.HasSettled&&ship.HasSolidWreck,label+": wreck stays dead, settled and solid: "+ship.name);
            Check(Vector3.Distance(ship.transform.position,wreckPositions[i])<.03f&&Quaternion.Angle(ship.transform.rotation,wreckRotations[i])<.03f,label+": grounded hull does not refloat or move: "+ship.name);
            Check(Vector3.Distance(WreckMeshPoint(ship),wreckMeshPositions[i])<.06f,label+": broken timber geometry stays in the same world position: "+ship.name);
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(began==0)began=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-began<5||EditorApplication.timeSinceStartup<waitUntil||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);if(EditorApplication.timeSinceStartup-began>180)throw new Exception("Returning tide QA timed out");
            if(phase==0){PlantingSetup();phase=1;return;}
            if(phase==1){if(!Zone.Instance.IsComplete)return;PlantingComplete();phase=2;return;}
            if(phase==2){BattleAndVictory();phase=3;return;}
            if(phase==3)
            {
                if(BachDangSaveSystem.PendingLoad!=null||!BachDangFrontEnd.Instance.GameStarted)return;
                Check(Story.Instance.currentState==Story.StoryState.Act3_Victory&&TideSystem.Instance.TargetWaterHeight==TideSystem.Instance.highTideY,"Real front-end scene reload preserves victory and flooded return water");AssertWrecks("Front-end cold load");
                foreach(var pair in hashes)Check(Hash(pair.Key)==pair.Value,"Original save/backup unchanged: "+Path.GetFileName(pair.Key));
                Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
