using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Story = BeachBoatStory.BachDangStoryManager;
using StoryOpening = BeachBoatStory.OpeningEvent;
using StoryDialogue = BeachBoatStory.StoryDialogueUI;
using StoryFader = BeachBoatStory.ScreenFader;

/// <summary>Headless smoke test: run the real fleet, tide, signals, stake hits, and victory.</summary>
public static class BeachBoatQuest3Playtest
{
    const string Flag = "BeachBoatQuest3PlaytestActive";
    static double started;
    static int phase;
    static bool wasHeld;
    static bool rangeBlocked;
    static float initialShipZ;
    static string failure;

    public static void RunAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        SessionState.SetBool(Flag,true);
        started=0;phase=0;wasHeld=false;rangeBlocked=false;failure=null;
        EditorApplication.isPlaying=true;
    }

    [InitializeOnLoadMethod]
    static void Register() { EditorApplication.update-=Tick;EditorApplication.update+=Tick; }

    static void SetAutoProperty(object target,string property,int value)
    {
        var field=target.GetType().GetField("<"+property+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic);
        if(field==null)throw new InvalidOperationException("Missing field for "+property);
        field.SetValue(target,value);
    }

    static void Finish(bool success,EnemyFleetDirector director,Story story)
    {
        Time.timeScale=1;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BeachBoatQuest3Playtest.json",JsonConvert.SerializeObject(new {
            success,failure,heldForPlayer=wasHeld,rangeBlockedAtDock=rangeBlocked,orders=director?.CommandScore,
            requiredOrders=director?.CommandOpportunityCount,shipsDestroyed=story?.destroyedEnemyShips,
            totalShips=story?.totalEnemyShips,storyState=story?.currentState.ToString(),
            ships=director?.enemyShips.Select(s=>new {s.name,s.isDestroyed,active=s.gameObject.activeSelf,
                z=s.transform.position.z}).ToArray()
        },Formatting.Indented));
        SessionState.SetBool(Flag,false);
        EditorApplication.Exit(success?0:1);
    }

    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        double elapsed=EditorApplication.timeSinceStartup-started;
        var director=UnityEngine.Object.FindFirstObjectByType<EnemyFleetDirector>();
        var story=Story.Instance;
        var zone=UnityEngine.Object.FindFirstObjectByType<BeachBoatStory.RiverStakeZone>();
        var boat=QuestRowboat.Instance;
        var session=BeachBoatQuestSession.Instance;
        if(elapsed>55){failure="Battle timed out at accelerated time scale";Finish(false,director,story);return;}
        if(director==null||story==null||zone==null||boat==null||session==null)return;
        if(phase==0&&elapsed>2)
        {
            foreach(var opening in UnityEngine.Object.FindObjectsByType<StoryOpening>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                opening.SkipOpening();
            StoryDialogue.Instance?.Close();
            foreach(var point in zone.placements)point.planted=true;
            foreach(var stake in zone.fieldStakes)if(stake!=null)stake.gameObject.SetActive(true);
            SetAutoProperty(zone,"PlantedCount",zone.RequiredCount);
            SetAutoProperty(zone,"CompletedClusterCount",zone.ClusterCount);
            story.collectedWood=story.totalWoodRequired;
            story.TransitionTo(Story.StoryState.Act3_WaitForLowTide,true);
            var p=boat.boardingPoint.position;
            session.player.transform.position=p;
            var body=session.player.GetComponent<Rigidbody>();
            if(body!=null){body.position=p;body.linearVelocity=Vector3.zero;}
            Physics.SyncTransforms();
            phase=1;
        }
        if(phase==1&&elapsed>3)
        {
            if(StoryOpening.IsOpeningActive)
            {
                foreach(var opening in UnityEngine.Object.FindObjectsByType<StoryOpening>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    opening.SkipOpening();
                if(elapsed<8)return;
            }
            if(!boat.TryBoard()){
                failure=$"Could not board battle boat: blocked={BeachBoatQuestSession.InteractionBlocked}, " +
                    $"atDock={boat.AtDock}, distance={QuestRowboat.FlatDistance(session.player.transform.position,boat.boardingPoint.position):F1}, " +
                    $"wood={story.collectedWood}/{story.totalWoodRequired}, state={story.currentState}, " +
                    $"opening={StoryOpening.IsOpeningActive}, dialogue={StoryDialogue.Instance?.IsOpen}, " +
                    $"fader={StoryFader.Instance?.IsTransitioning}, planting={zone.IsPlanting}";
                Finish(false,director,story);return;
            }
            initialShipZ=director.enemyShips[0].spawnPoint.z;
            if(!director.BattleStarted){failure="Battle did not start after boarding";Finish(false,director,story);return;}
            Time.timeScale=12f;
            phase=2;
        }
        if(phase!=2)return;
        if(director.enemyShips.Any(s=>s!=null&&s.SignalReady))wasHeld=true;
        // Orders are deliberately withheld for a moment so the test proves ships wait
        // for the player's signal instead of automatically crossing the stake line.
        if(wasHeld&&elapsed>7&&director.IsShowingBattle)
        {
            var ready=director.enemyShips.FirstOrDefault(s=>s!=null&&s.SignalReady);
            if(ready!=null)
            {
                if(!rangeBlocked)rangeBlocked=!director.TrySignalAttack();
                var near=ready.transform.position+Vector3.left*320f;
                near.y=TideSystem.CurrentWaterHeight;
                boat.transform.position=near;
                director.TrySignalAttack();
            }
        }
        if(story.currentState==Story.StoryState.Act3_Victory)
        {
            bool success=wasHeld&&rangeBlocked&&director.CommandScore==5&&story.destroyedEnemyShips==10&&
                director.enemyShips[0].transform.position.z<initialShipZ-600;
            if(!success)failure="Victory occurred without five commands, ten wrecks, or upstream travel";
            Finish(success,director,story);
        }
    }
}
