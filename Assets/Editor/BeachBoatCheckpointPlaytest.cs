using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatCheckpointPlaytest
{
    const string Flag="CheckpointPlaytest";
    static double started,waitUntil;
    static int phase;
    static bool woodRestored,supplyPreserved,stakesRestored,battleReset,secondBattle,sawDefeat,collisionDefeat,staminaNotDeath,ebbReset;
    static string failure;
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void SetProgress(BeachBoatStory.RiverStakeZone zone,int value,int clusters)
    {
        typeof(BeachBoatStory.RiverStakeZone).GetField("<PlantedCount>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(zone,value);
        typeof(BeachBoatStory.RiverStakeZone).GetField("<CompletedClusterCount>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(zone,clusters);
    }
    static void Move(Vector3 p)
    {var go=BeachBoatQuestSession.Instance.player;go.transform.position=p;var b=go.GetComponent<Rigidbody>();b.position=p;if(!b.isKinematic)b.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
    static void Kill()
    {
        var vitals=BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>();vitals.TakeImpact(10000,"Kiểm tra checkpoint");
        if(!QuestCheckpoint.Instance.IsDefeated||vitals.Health!=0||Time.timeScale!=0||!BeachBoatQuestSession.InputBlocked||Cursor.lockState!=CursorLockMode.None)throw new Exception("Death did not show a blocking defeat state");
            var canvas=GameObject.Find("QuestDefeatCanvas");if(canvas==null||!canvas.transform.Find("Defeated").gameObject.activeSelf)throw new Exception("Defeat screen missing");sawDefeat=true;
    }
    static void Retry()
    {
        if(!QuestCheckpoint.Instance.Retry())throw new Exception("Checkpoint retry rejected");
        var session=BeachBoatQuestSession.Instance;var v=session.player.GetComponent<PlayerVitals>();
        if(v.Health!=v.maxHealth||v.Stamina!=v.maxStamina||QuestCheckpoint.Instance.IsDefeated||Time.timeScale<=0||!session.player.GetComponent<CapsuleCollider>().enabled||!session.player.GetComponent<BeachBoatStory.Character_Movement>().enabled||session.player.GetComponent<Rigidbody>().isKinematic)throw new Exception("Retry did not restore player controls and vitals");
    }
    static void Finish()
    {
        Time.timeScale=1;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CheckpointPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,woodRestored,supplyPreserved,stakesRestored,battleReset,secondBattle,sawDefeat,collisionDefeat,staminaNotDeath,ebbReset},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<3||EditorApplication.timeSinceStartup<waitUntil)return;
        if(EditorApplication.timeSinceStartup-started>55){failure="Checkpoint timeout at phase "+phase;Finish();return;}
        try
        {
            var session=BeachBoatQuestSession.Instance;var story=Story.Instance;var boat=QuestRowboat.Instance;var zone=BeachBoatStory.RiverStakeZone.Instance;var fleet=EnemyFleetDirector.Instance;var checkpoint=QuestCheckpoint.Instance;var village=VillagePreparationQuest.Instance;
            var vitals=session.player.GetComponent<PlayerVitals>();
            if(phase==0)
            {
                // This suite deliberately tests the fallback when no recovery file
                // has been written. The full snapshot path has its own QA suite.
                BachDangAutoSave.Instance.enabled=false;
                BeachBoatStory.BachDangFrontEnd.Instance?.Play();
                foreach(var opening in UnityEngine.Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))opening.SkipOpening();BeachBoatStory.StoryDialogueUI.Instance?.Close();
                story.collectedWood=0;foreach(var item in session.woodItems)item.RestoreCollection(false);
                story.TransitionTo(Story.StoryState.Act2_CollectWoodInVillage,true);
                var first=session.woodItems[0];Move(first.transform.position);if(!first.TryCollect())throw new Exception("Cannot collect checkpoint test wood");
                vitals.TryExert(10000,1);staminaNotDeath=!checkpoint.IsDefeated&&vitals.Health>0&&vitals.IsExhausted;
                Kill();phase=1;waitUntil=EditorApplication.timeSinceStartup+.4;return;
            }
            if(phase==1)
            {
                CaptureDefeat();Retry();woodRestored=story.currentState==Story.StoryState.Act2_CollectWoodInVillage&&story.collectedWood==0&&session.woodItems.All(w=>w!=null&&w.gameObject.activeSelf&&!w.IsCollected);
                if(!woodRestored)throw new Exception("Wood checkpoint did not restore collectible objects");
                story.collectedWood=story.totalWoodRequired;story.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);
                village.Collect(village.stalls[0]);Kill();Retry();
                supplyPreserved=story.currentState==Story.StoryState.Act2_SupplyPreparation&&village.CompletedCount==1&&village.stalls[0].Completed;
                if(!supplyPreserved)throw new Exception("Completed market stall was lost on death");
                zone.RestoreCheckpoint(new bool[zone.ClusterCount]);story.TransitionTo(Story.StoryState.Act2_PlantStakesAtRiver,true);
                var partial=new bool[zone.ClusterCount];partial[0]=partial[1]=true;zone.RestoreCheckpoint(partial);Kill();Retry();
                stakesRestored=story.currentState==Story.StoryState.Act2_PlantStakesAtRiver&&zone.PlantedCount==0&&zone.CompletedClusterCount==0&&!zone.IsPlanting;
                if(!stakesRestored)throw new Exception("Stake mission did not restart cleanly");
                zone.RestoreCheckpoint(Enumerable.Repeat(true,zone.ClusterCount).ToArray());story.TransitionTo(Story.StoryState.Act2_Completed,true);story.OnTalkToNgoQuyenAct3();Move(boat.boardingPoint.position);
                if(!boat.TryBoard()||!fleet.BattleStarted)throw new Exception("Cannot start initial battle");phase=2;waitUntil=EditorApplication.timeSinceStartup+3;return;
            }
            if(phase==2)
            {
                var ship=fleet.enemyShips.First(s=>s.gameObject.activeSelf);ship.TakeDamage();story.destroyedEnemyShips=3;
                Kill();Retry();
                battleReset=story.currentState==Story.StoryState.Act3_WaitForLowTide&&checkpoint.SavedState==Story.StoryState.Act3_WaitForLowTide&&story.destroyedEnemyShips==0&&fleet.CommandScore==0&&!fleet.BattleStarted&&fleet.enemyShips.All(s=>!s.gameObject.activeSelf&&!s.isDestroyed)&&zone.IsComplete&&boat.AtDock&&boat.CanOccupy(boat.transform.position,boat.transform.rotation);
                if(!battleReset)throw new Exception("Act 3 retry lost stakes or retained battle state");
                Move(boat.boardingPoint.position);if(!boat.TryBoard()||!fleet.BattleStarted)throw new Exception("Cannot reboard and restart battle after death");
                phase=3;waitUntil=EditorApplication.timeSinceStartup+3;return;
            }
            if(phase==3)
            {
                secondBattle=fleet.enemyShips.Any(s=>s.gameObject.activeSelf&&!s.isDestroyed)&&story.currentState==Story.StoryState.Act3_BattleInProgress;
                if(!secondBattle)throw new Exception("Fleet did not spawn on second battle attempt");
                var rock=GameObject.CreatePrimitive(PrimitiveType.Cube);rock.name="Rock_CheckpointFatal";var p=boat.transform.position+boat.transform.forward*90;p.y=TideSystem.CurrentWaterHeight+2;rock.transform.position=p;rock.transform.localScale=new Vector3(28,35,28);Physics.SyncTransforms();
                boat.RestoreDurability(1);
                for(int i=0;i<100&&!checkpoint.IsDefeated;i++)boat.Step(1,0,.05f,true);
                collisionDefeat=checkpoint.IsDefeated&&boat.IsBroken&&vitals.Health>0&&!boat.IsAboard;
                if(!collisionDefeat)throw new Exception("Fatal collision did not enter defeat state");
                UnityEngine.Object.DestroyImmediate(rock);Physics.SyncTransforms();Retry();
                if(story.currentState!=Story.StoryState.Act3_WaitForLowTide||!zone.IsComplete||!staminaNotDeath)throw new Exception("Final mission/stamina check failed");
                BachDangBattleTimeline.Instance.BeginEbb();phase=4;waitUntil=EditorApplication.timeSinceStartup+2;return;
            }
            if(phase==4)
            {
                if(!TideSystem.Instance.IsControlledEbb)throw new Exception("Controlled ebb did not start");
                Kill();Retry();ebbReset=!TideSystem.Instance.IsControlledEbb&&Mathf.Abs(TideSystem.Instance.TargetWaterHeight-TideSystem.Instance.highTideY)<.1f&&zone.IsComplete;
                if(!ebbReset)throw new Exception("Checkpoint retained an unfinished ebb transition");Finish();
            }
        }
        catch(Exception e){failure=e.ToString();Finish();}
    }
    static void CaptureDefeat()
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;var canvas=GameObject.Find("QuestDefeatCanvas").GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=2;
        var rt=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/Checkpoint_Defeat.png",pixels.EncodeToPNG());
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);
    }
}
