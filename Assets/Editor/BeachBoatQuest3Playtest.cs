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
    static float healthWidthBefore, staminaWidthBefore;
    static float healthWidthAfter, staminaWidthAfter, healthAnchorAfter, staminaAnchorAfter;
    static bool vitalsBarsUpdated, minimapLowerRight, rowingControllerApplied;
    static bool hudChecked;
    static bool delayedFleetHeldHigh,hiddenUntilEntry=true,ebbAtField,gradualEbb,sankDuringEbb,ambushBeforeLow;
    static float[] shipSpeeds;
    static float holdUntil,ebbAt=-1,lastWater=float.PositiveInfinity;
    static bool midShot,impactShot;
    static float firstImpactAt=-1;
    static EnemyShip impactShip;
    static string failure;

    public static void RunAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        SessionState.SetBool(Flag,true);
        started=0;phase=0;wasHeld=false;rangeBlocked=false;failure=null;
        healthWidthBefore=staminaWidthBefore=healthWidthAfter=staminaWidthAfter=0;
        healthAnchorAfter=staminaAnchorAfter=0;
        vitalsBarsUpdated=minimapLowerRight=rowingControllerApplied=hudChecked=false;
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
            success,failure,heldForPlayer=wasHeld,rangeBlockedAtDock=rangeBlocked,
            delayedFleetHeldHigh,hiddenUntilEntry,ebbAtField,gradualEbb,sankDuringEbb,ambushBeforeLow,
            vitalsBarsUpdated,minimapLowerRight,rowingControllerApplied,
            healthWidthBefore,healthWidthAfter,staminaWidthBefore,staminaWidthAfter,
            healthAnchorAfter,staminaAnchorAfter,orders=director?.CommandScore,
            requiredOrders=director?.CommandOpportunityCount,shipsDestroyed=story?.destroyedEnemyShips,
            playerKills=RiverPlayerCombat.Instance?.Kills,playerHits=RiverPlayerCombat.Instance?.Hits,
            totalShips=story?.totalEnemyShips,storyState=story?.currentState.ToString(),
            ships=director?.enemyShips.Select(s=>new {s.name,s.isDestroyed,active=s.gameObject.activeSelf,
                z=s.transform.position.z}).ToArray()
        },Formatting.Indented));
        SessionState.SetBool(Flag,false);
        BeachBoatQAExit.AfterPlayMode(success?0:1);
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
            var animator=session.player.GetComponentInChildren<Animator>();
            rowingControllerApplied=animator!=null&&animator.runtimeAnimatorController==boat.rowingController;
            var healthFill=GameObject.Find("HealthFill")?.GetComponent<RectTransform>();
            var staminaFill=GameObject.Find("StaminaFill")?.GetComponent<RectTransform>();
            healthWidthBefore=healthFill!=null?healthFill.rect.width:0;
            staminaWidthBefore=staminaFill!=null?staminaFill.rect.width:0;
            var vitals=session.player.GetComponent<PlayerVitals>();
            vitals.TakeImpact(40,"HUD test");
            vitals.TryExert(60,1);
            var minimap=session.GetComponentInChildren<BeachBoatMinimap>(true)?.transform.Find("Minimap") as RectTransform;
            minimapLowerRight=minimap!=null&&minimap.anchorMin.x>.7f&&minimap.anchorMax.y<.5f;
            initialShipZ=director.enemyShips[0].spawnPoint.z;
            if(!director.BattleStarted){failure="Battle did not start after boarding";Finish(false,director,story);return;}
            if(RiverPlayerCombat.Instance!=null)RiverPlayerCombat.Instance.ReturnFireEnabled=false;
            shipSpeeds=director.enemyShips.Select(s=>s.moveSpeed).ToArray();
            foreach(var ship in director.enemyShips)ship.moveSpeed=0;
            holdUntil=Time.time+80f;
            phase=2;
        }
        if(phase!=2)return;
        if(!hudChecked&&elapsed>4)
        {
            hudChecked=true;
            var healthFill=GameObject.Find("HealthFill")?.GetComponent<RectTransform>();
            var staminaFill=GameObject.Find("StaminaFill")?.GetComponent<RectTransform>();
            healthWidthAfter=healthFill!=null?healthFill.rect.width:0;
            staminaWidthAfter=staminaFill!=null?staminaFill.rect.width:0;
            healthAnchorAfter=healthFill!=null?healthFill.anchorMax.x:0;
            staminaAnchorAfter=staminaFill!=null?staminaFill.anchorMax.x:0;
            vitalsBarsUpdated=healthFill!=null&&staminaFill!=null&&
                healthWidthBefore>0&&staminaWidthBefore>0&&
                healthWidthAfter<healthWidthBefore*.7f&&
                staminaWidthAfter<staminaWidthBefore*.5f;
            Time.timeScale=12f;
        }
        if(director.enemyShips.Any(s=>s!=null&&s.SignalReady))wasHeld=true;
        var tide=TideSystem.Instance;
        if(!director.EbbStarted)
        {
            hiddenUntilEntry&=zone.ExposedStakeCount==0&&Mathf.Abs(tide.TargetWaterHeight-tide.highTideY)<.01f&&director.enemyShips.All(s=>!s.isDestroyed);
            if(!hiddenUntilEntry){failure="Water fell or stakes appeared before hull entry";Finish(false,director,story);return;}
        }
        else
        {
            if(ebbAt<0)
            {
                ebbAt=Time.time;
                ebbAtField=director.EbbTriggerShip!=null&&director.EbbTriggerShip.IsHullInsideStakeField&&TideSystem.CurrentWaterHeight>tide.lowTideY+20;
                CaptureBattle("High",director.EbbTriggerShip);
            }
            if(TideSystem.CurrentWaterHeight>lastWater+.1f){failure="Water rose during the controlled ebb";Finish(false,director,story);return;}
            lastWater=TideSystem.CurrentWaterHeight;
            gradualEbb|=Time.time-ebbAt>3&&TideSystem.CurrentWaterHeight<tide.highTideY-1&&TideSystem.CurrentWaterHeight>tide.lowTideY+1;
            if(director.enemyShips.Any(s=>s.isDestroyed))sankDuringEbb|=tide.IsControlledEbb&&TideSystem.CurrentWaterHeight>tide.lowTideY+.5f;
            ambushBeforeLow|=BachDangBattleTimeline.Instance.CurrentMoment==BachDangBattleTimeline.BattleMoment.Ambush&&TideSystem.CurrentWaterHeight>tide.lowTideY+1;
            if(!midShot&&zone.ExposedStakeCount>0){midShot=true;CaptureBattle("Emerging",director.EbbTriggerShip);}
            if(firstImpactAt<0&&director.enemyShips.Any(s=>s.isDestroyed)){firstImpactAt=Time.time;impactShip=director.enemyShips.First(s=>s.isDestroyed);}
            if(!impactShot&&firstImpactAt>=0&&Time.time-firstImpactAt>.65f){impactShot=true;CaptureBattle("Impact",impactShip);}
        }
        if(!delayedFleetHeldHigh&&Time.time>=holdUntil)
        {
            if(director.EbbStarted){failure="A timer triggered the ebb while the fleet was held upstream";Finish(false,director,story);return;}
            delayedFleetHeldHigh=true;
        }
        if(delayedFleetHeldHigh)for(int i=0;i<shipSpeeds.Length;i++)
            if(Time.time>=holdUntil+(i/Mathf.Max(1,director.shipsPerWave))*director.waveDelay)director.enemyShips[i].moveSpeed=shipSpeeds[i];
        // Orders are deliberately withheld for a moment so the test proves ships wait
        // for the player's signal instead of automatically crossing the stake line.
        if(wasHeld&&elapsed>7&&director.IsShowingBattle&&BachDangBattleTimeline.Instance.CurrentMoment>=BachDangBattleTimeline.BattleMoment.Ambush)
        {
            var ready=director.enemyShips.FirstOrDefault(s=>s!=null&&s.SignalReady);
            if(ready!=null)
            {
                if(!rangeBlocked)rangeBlocked=!director.TrySignalAttack();
                var near=ready.transform.position+Vector3.left*320f;
                near.y=TideSystem.CurrentWaterHeight;
                boat.transform.position=near;
                var combat=RiverPlayerCombat.Instance;
                int wave=Array.IndexOf(director.enemyShips,ready)/Mathf.Max(1,director.shipsPerWave);
                if(combat!=null&&!combat.HasSuppressedWave(wave))
                {
                    var guard=combat.guards.First(g=>g.waveIndex==wave&&g.IsAlive);
                    var origin=guard.AimPoint+Vector3.left*200;origin.y=TideSystem.CurrentWaterHeight+14;
                    boat.transform.position=new Vector3(origin.x,TideSystem.CurrentWaterHeight,origin.z);
                    boat.Step(0,0,.001f);Physics.SyncTransforms();combat.SetAim(true);
                    combat.TryFire(new Ray(origin,(guard.AimPoint-origin).normalized),1);
                    combat.StepArrows(1);combat.SetAim(false);
                }
                director.TrySignalAttack();
            }
        }
        if(story.currentState==Story.StoryState.Act3_Victory)
        {
            bool success=wasHeld&&rangeBlocked&&vitalsBarsUpdated&&minimapLowerRight&&rowingControllerApplied&&
                delayedFleetHeldHigh&&hiddenUntilEntry&&ebbAtField&&gradualEbb&&sankDuringEbb&&ambushBeforeLow&&
                director.CommandScore==5&&story.destroyedEnemyShips==10&&
                director.enemyShips[0].transform.position.z<initialShipZ-600;
            if(!success)failure=$"Battle/HUD check failed: bars={vitalsBarsUpdated}, minimap={minimapLowerRight}, " +
                    $"rowing={rowingControllerApplied}, orders={director.CommandScore}, wrecks={story.destroyedEnemyShips}, " +
                    $"entry={ebbAtField}, gradual={gradualEbb}, sunkDuringEbb={sankDuringEbb}, hiddenUntilEntry={hiddenUntilEntry}";
            Finish(success,director,story);
        }
    }
    static void CaptureBattle(string name,EnemyShip ship)
    {
        if(ship==null)return;
        var go=new GameObject("EbbQACamera");var camera=go.AddComponent<Camera>();camera.CopyFrom(BeachBoatQuestSession.Instance.storyCamera);
        camera.enabled=false;camera.fieldOfView=55;camera.farClipPlane=1800;
        var focus=ship.transform.position;focus.y=TideSystem.CurrentWaterHeight+13;
        camera.transform.position=focus+new Vector3(155,110,-150);camera.transform.LookAt(focus);
        var rt=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);camera.targetTexture=rt;
        camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
        Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/Ebb_"+name+".png",pixels.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(go);
    }
}
