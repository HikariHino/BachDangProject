using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using BeachBoatStory;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatMarineJournalPlaytest
{
    const string Flag="MarineJournal938QA";static double started;static int phase;static string runtimeError;static readonly List<string> checks=new List<string>();
    public static void RunInEditor(){SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string text,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Error||type==LogType.Exception))runtimeError=text+"\n"+stack;}
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
    static void Move(Vector3 p){var player=BeachBoatQuestSession.Instance.player;player.transform.position=p;var body=player.GetComponent<Rigidbody>();body.position=p;if(!body.isKinematic)body.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
    static void Capture(string name,int width=1280,int height=720)
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;var canvases=Object.FindObjectsByType<Canvas>().Where(c=>c.isActiveAndEnabled&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();var saved=canvases.Select(c=>new{c,c.worldCamera,c.planeDistance}).ToArray();var rt=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;foreach(var s in saved){s.c.renderMode=RenderMode.ScreenSpaceOverlay;s.c.worldCamera=s.worldCamera;s.c.planeDistance=s.planeDistance;}Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
    }
    static void ScoreChecks()
    {
        var p=CampaignProgress.Instance;var empty=p.Capture();
        Check(CampaignProgress.RankFor(59)=="C"&&CampaignProgress.RankFor(60)=="B"&&CampaignProgress.RankFor(74)=="B"&&CampaignProgress.RankFor(75)=="A"&&CampaignProgress.RankFor(89)=="A"&&CampaignProgress.RankFor(90)=="S","C/B/A/S boundaries are exact and deterministic");
        Check(CampaignProgress.MiniEfficiency(.4f)==35&&CampaignProgress.MiniEfficiency(.6f)==28&&CampaignProgress.MiniEfficiency(.8f)==20&&CampaignProgress.MiniEfficiency(.81f)==12,"Minigame timing tiers match the player-facing guide");
        SeedPerfect();Check(p.Score==100&&Mathf.Approximately(p.MiniPoints,35)&&p.StakePoints==25&&p.KillPoints==25&&p.TimePoints==15,"Four documented categories total exactly 100 points");
        float before=p.MiniPoints;p.RecordMinigame("village:0:0",90,100);Check(Mathf.Approximately(p.MiniPoints,before),"Slower replays cannot erase a valid minigame record");
        p.Record.seconds=40*60+1;Check(p.TimePoints==12,"Campaign time score changes at the documented 40-minute boundary");
        p.Record.stakeSeconds=p.StakeTargetSeconds*1.5f+.1f;Check(p.StakePoints==15,"Stake timer tier changes at the documented route-based boundary");
        var guard=RiverPlayerCombat.Instance.guards[0];p.Record.kills=Array.Empty<string>();p.RecordKill(guard);p.RecordKill(guard);Check(p.Record.kills.Length==1,"A respawned enemy identity cannot be farmed twice for rank points");
        var copy=p.Capture();p.Restore(copy,0);Check(Mathf.Abs(p.Record.seconds-copy.seconds)<.01f&&p.Record.kills.Length==1,"Rank timers and kill identities survive serialization");
        p.Restore(null,10);Check(p.Record.historyIncomplete&&p.MissionDone(1)&&!p.MissionDone(12),"Legacy quest 3 saves recover past task checks without inventing a completed battle");
        p.Restore(empty,0);p.Transition(10,9);Check(!p.MissionDone(12),"Retrying a battle cannot mark that battle completed");p.Restore(empty,0);
        foreach(var stall in VillagePreparationQuest.Instance.stalls)stall.RestoreLearningProgress(0);Story.Instance.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);Story.Instance.OnSupplyPreparationComplete();
        Check(Story.Instance.currentState==Story.StoryState.Act2_SupplyPreparation&&!p.MissionDone(4),"An optional supply relay cannot bypass the four required trades or give them a false green check");p.Restore(empty,0);
    }
    static void SeedPerfect()
    {
        var p=CampaignProgress.Instance;p.Restore(new BachDangSaveData.Campaign(),11);p.Transition(0,11);p.Record.seconds=30*60;p.Record.stakeStarted=true;p.Record.stakeFinished=true;p.Record.stakeSeconds=p.StakeTargetSeconds;
        for(int i=0;i<13;i++)p.RecordMinigame(i==0?"drum":"village:"+((i-1)/3)+":"+((i-1)%3),40,100);
        p.Record.kills=Enumerable.Range(0,p.KillTarget).Select(i=>"QA:"+i).ToArray();
        foreach(var stall in VillagePreparationQuest.Instance.stalls)stall.RestoreLearningProgress(3);
    }
    static void MarineChecks()
    {
        var session=BeachBoatQuestSession.Instance;var game=RiverBoardingCombat.Instance;var fleet=EnemyFleetDirector.Instance;var combat=RiverPlayerCombat.Instance;var boat=QuestRowboat.Instance;var story=Story.Instance;
        float referenceHeight=EnemyDeckGuard.RigHeight(session.player.GetComponentInChildren<Animator>());
        Check(game.friendlyBoats.All(b=>b.transform.Find("Warship_Size_120")!=null)&&fleet.enemyShips.All(s=>s.transform.Find("Warship_Size_120")!=null)&&boat.transform.Find("Warship_Size_120")==null,"Both warship fleets enlarge by 20 percent while the player's small rowboat keeps its size");
        Check(game.friendlyBoats.SelectMany(b=>b.GetComponentInChildren<FleetDeckCrew>().soldiers).All(s=>Mathf.Abs(EnemyDeckGuard.RigHeight(s.GetComponentInChildren<Animator>())/referenceHeight-1)<.04f),"Every authored allied deck soldier matches player stature within four percent");
        Check(game.CaptureSave().allies.All(a=>Mathf.Abs(EnemyDeckGuard.RigHeight(session.transform.Find(a.id).GetComponentInChildren<Animator>())/referenceHeight-1)<.04f),"All friendly boarding reinforcements use the same player stature");
        BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());story.collectedWood=story.totalWoodRequired;story.TransitionTo(Story.StoryState.Act3_WaitForLowTide,true);Move(boat.boardingPoint.position);Check(boat.TryBoard(),"Can board quest 3 test fixture");fleet.StopAllCoroutines();game.enabled=false;combat.enabled=false;combat.ReturnFireEnabled=false;
        foreach(var s in fleet.enemyShips)s.enabled=false;foreach(var b in game.friendlyBoats)b.enabled=false;var ship=fleet.enemyShips[0];ship.Activate();ship.enabled=false;
        var victim=ship.GetComponentsInChildren<EnemyDeckGuard>().First(g=>g.IsAlive);foreach(var guard in combat.guards)if(guard!=victim)guard.DefeatFromVolley();
        var data=game.CaptureSave();var ally=data.allies[0];ally.health=1;ally.state=3;ally.active=true;ally.target=BachDangSaveSystem.GuardId(victim);ally.position=victim.transform.position+Vector3.right*10;game.RestoreSave(data);
        var times=(Dictionary<EnemyDeckGuard,float>)typeof(RiverBoardingCombat).GetField("enemyAttacks",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);times[victim]=Time.time-1;
        int casualties=CampaignProgress.Instance.Record.casualties;game.StepEnemies(0);var dead=game.CaptureSave().allies[0];Check(dead.health==0&&!dead.active,"Enemy sword can permanently kill a friendly boarder");Check(CampaignProgress.Instance.Record.casualties==casualties+1,"A real friendly death is counted once");
        game.StepAllies(30);Check(game.CaptureSave().allies[0].health==0&&!game.CaptureSave().allies[0].active,"A fallen ally does not respawn after 30 seconds");
        var deathSave=game.CaptureSave();game.RestoreSave(deathSave);Check(game.CaptureSave().allies[0].health==0,"Loading preserves the ally's death");game.ResetBattle(true);Check(game.CaptureSave().allies[0].health==0,"Victory cleanup does not resurrect casualties");
        var enemyHull=BoatHullCollision.Get(ship.transform);var enemyBox=enemyHull.SolidHull;
        Check(game.TryTraverse(),"Player enters water beside an enlarged enemy ship");var enemyEdge=ship.transform.TransformPoint(enemyBox.center+new Vector3(-enemyBox.size.x*.5f-10/Mathf.Abs(ship.transform.lossyScale.x),0,0));enemyEdge.y=TideSystem.CurrentWaterHeight-5;Move(enemyEdge);
        Check(game.HandleActionKey(KeyCode.Space)&&game.State==RiverBoardingCombat.PlayerState.Climbing,"SPACE remains reachable at the actual enlarged enemy hull edge");game.StepPlayer(2);
        ActorSolidBody.WalkingDimensions(session.player.GetComponentInChildren<Animator>(),out _,out float enemyDeckSole,out _);
        Check(game.State==RiverBoardingCombat.PlayerState.EnemyDeck&&enemyHull.TryDeckSurface(session.player.transform.position,out var enemyDeckFloor)&&Mathf.Abs(session.player.transform.position.y+enemyDeckSole-enemyDeckFloor.y-.15f)<.12f,"Player boots also stand on the enlarged enemy's actual deck");
        Check(game.TryTraverse(),"Player can leave the enlarged enemy deck");Move(boat.transform.position);Check(game.TryTraverse()&&game.State==RiverBoardingCombat.PlayerState.Rowboat,"Player returns to the small rowboat after enemy deck review");
        // Compare a slow rendered frame with equivalent safe movement slices.
        boat.transform.SetPositionAndRotation(new Vector3(-2400,TideSystem.CurrentWaterHeight,1400),Quaternion.identity);var start=boat.transform.position;boat.Speed.Equals(0);boat.Step(.8f,0,.16f);var slow=boat.transform.position;boat.transform.position=start;typeof(QuestRowboat).GetProperty("Speed").GetSetMethod(true).Invoke(boat,new object[]{0f});for(int i=0;i<4;i++)boat.Step(.8f,0,.04f);Check(Vector3.Distance(slow,boat.transform.position)<.1f,"A 160ms rowing frame covers the same distance as four 40ms frames without dropping elapsed time");
        var friendly=game.friendlyBoats[0];friendly.RestorePosition(new Vector3(-2700,TideSystem.CurrentWaterHeight,1350),Quaternion.identity);boat.transform.position=new Vector3(-3200,TideSystem.CurrentWaterHeight,1400);Physics.SyncTransforms();
        Check(game.TryTraverse(),"Player can jump into water before assisting a friendly ship");var hull=BoatHullCollision.Get(friendly.transform).SolidHull;var swim=friendly.transform.TransformPoint(hull.center+new Vector3(-hull.size.x*.5f-10/Mathf.Abs(friendly.transform.lossyScale.x),0,0));swim.y=TideSystem.CurrentWaterHeight-5;Move(swim);
        Check(game.HandleActionKey(KeyCode.Space)&&game.State==RiverBoardingCombat.PlayerState.Climbing,"SPACE selects a nearby allied ship for climbing");game.StepPlayer(2);Check(game.State==RiverBoardingCombat.PlayerState.FriendlyDeck&&game.SwordDrawn,"Climbing finishes on the friendly deck with sword ready");
        Check(CampaignProgress.Instance.Record.friendlyBoardings>0,"Friendly boarding awards its actual achievement progress");
        var deckHull=BoatHullCollision.Get(friendly.transform);var playerRig=session.player.GetComponentInChildren<Animator>();
        void CheckSoles(string label)
        {
            Check(deckHull.TryDeckSurface(session.player.transform.position,out var floor),label+": real timber exists under the player");
            ActorSolidBody.WalkingDimensions(playerRig,out _,out float sole,out _);
            Check(Mathf.Abs(session.player.transform.position.y+sole-floor.y-.15f)<.12f,label+": visible boot soles touch the actual planks");
            float independent=BeachBoatFleetAndStallUpgrade.Deck(friendly.transform.Find("DaiVietPatrolVisual_Rebuilt"),friendly.transform,friendly.transform.InverseTransformPoint(session.player.transform.position),-100)-.3f;
            Check(Mathf.Abs(friendly.transform.InverseTransformPoint(floor).y-independent)<.01f,label+": cached support agrees with independent triangle intersection");
        }
        CheckSoles("After climbing the lower deck");
        var lower=game.CaptureSave();var raised=game.CaptureSave();var station=friendly.GetComponentInChildren<FleetDeckCrew>().soldiers[0];
        raised.deckLocal=friendly.transform.InverseTransformPoint(station.position+friendly.transform.right*8);game.RestoreSave(raised);game.StepPlayer(0);CheckSoles("On the raised stern deck");
        game.RestoreSave(lower);game.StepPlayer(0);CheckSoles("After returning to the lower deck");
        victim.ResetGuard();victim.IsIndependentBoarder=true;victim.transform.SetParent(session.transform,true);victim.transform.position=session.player.transform.position+session.player.transform.forward*15;Physics.SyncTransforms();int hp=victim.Health;Check(game.TrySlash()&&victim.Health==hp-1,"Player can slash an enemy who boarded an allied ship");
        game.ToggleSword();combat.SetAim(true);float y=session.player.transform.position.y;typeof(RiverPlayerCombat).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,null);Check(Mathf.Abs(session.player.transform.position.y-y)<.01f,"Aiming on an allied deck cannot pull the player to the waterline");combat.SetAim(false);
        var boarding=game.CaptureSave();Move(session.player.transform.position+Vector3.right*70);game.RestoreSave(boarding);game.StepPlayer(0);Check(game.State==RiverBoardingCombat.PlayerState.FriendlyDeck&&boarding.deckShip==friendly.name,"Allied-deck state keeps its moving carrier after restore");
        BachDangSaveSystem.TestDirectory=Path.GetFullPath("Logs/MarineJournalSaveQA");
        Check(BachDangSaveSystem.Save(9,"QA trên boong đồng minh")&&BachDangSaveSystem.TryRead(9,out var carrierSave),"An allied-deck encounter can be written to a real save slot");
        Check(BachDangSaveSystem.TryRead(9,out var restoredCarrier),"The allied-deck slot passes checksum and version validation");BachDangSaveSystem.Apply(restoredCarrier);game.enabled=false;combat.enabled=false;fleet.StopAllCoroutines();game.StepPlayer(0);
        Check(game.State==RiverBoardingCombat.PlayerState.FriendlyDeck&&game.CaptureSave().deckShip==friendly.name,"Loading the complete save restores the allied carrier instead of the player's rowboat");
        CheckSoles("After full disk save and load");
        var badHeight=game.CaptureSave();badHeight.deckLocal.y+=20;Move(session.player.transform.position+Vector3.up*280);game.RestoreSave(badHeight);game.StepPlayer(0);CheckSoles("An old floating deck save is repaired");
        var hostileDeck=game.CaptureSave();var boardingEnemy=hostileDeck.hostiles[0];boardingEnemy.active=true;boardingEnemy.state=3;boardingEnemy.source=ship.name;boardingEnemy.boat=friendly.name;
        boardingEnemy.position=session.player.transform.position+friendly.transform.forward*15;boardingEnemy.local=friendly.transform.InverseTransformPoint(boardingEnemy.position);
        var boarder=combat.guards.First(g=>BachDangSaveSystem.GuardId(g)==boardingEnemy.id);boarder.ResetGuard();game.RestoreSave(hostileDeck);game.StepHostiles(0);typeof(RiverBoardingCombat).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,null);
        Check(deckHull.TryDeckSurface(boarder.transform.position,out var enemyFloor)&&Mathf.Abs(boarder.transform.position.y-enemyFloor.y-.2f)<.1f,"Enemy boarders also stand on the real lower deck instead of the raised stern height");
        Check(deckHull.IsOwn(session.player.GetComponent<Collider>()),"A passenger cannot block the movement of their own allied carrier");
        Check(Mathf.Abs(EnemyDeckGuard.RigHeight(boarder.GetComponentInChildren<Animator>())/referenceHeight-1)<.04f,"An enemy who boards the allied ship keeps the same stature as the player");
        Check(game.CaptureSave().allies[0].health==0,"The complete saved encounter also preserves a dead friendly soldier");
        var camera=session.storyCamera;camera.GetComponent<BeachBoatStory.Camera_Script>().enabled=false;camera.transform.position=session.player.transform.position+new Vector3(-45,32,-60);camera.transform.LookAt(session.player.transform.position+Vector3.up*8);session.player.GetComponentInChildren<Animator>().Update(.2f);typeof(RiverBoardingCombat).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,null);Capture("Player_On_Allied_Deck");
        // Follow a real approach from deep water onto the sloping bank; do not
        // teleport the enemy onto dry terrain as the old shore smoke test did.
        TideSystem.Instance.HoldTide(BachDangBattleTimeline.Instance,false,true);var shore=new Vector3(-3768,TideSystem.CurrentWaterHeight-5,1800);boarding=game.CaptureSave();boarding.state=(int)RiverBoardingCombat.PlayerState.Swimming;boarding.sword=false;game.RestoreSave(boarding);Move(shore);game.StepPlayer(0);Check(game.State==RiverBoardingCombat.PlayerState.Land,"Player stands on the low-tide bank for pursuit review");
        boarding=game.CaptureSave();var hostile=boarding.hostiles[0];hostile.active=true;hostile.source=ship.name;hostile.boat=boat.name;hostile.targetPlayer=true;hostile.state=1;hostile.position=new Vector3(-3600,TideSystem.CurrentWaterHeight-4,1800);var enemy=combat.guards.First(g=>BachDangSaveSystem.GuardId(g)==hostile.id);enemy.ResetGuard();game.RestoreSave(boarding);
        foreach(var guard in combat.guards)if(guard!=enemy)guard.DefeatFromVolley();for(int i=0;i<240&&game.CaptureSave().hostiles.First(h=>h.id==hostile.id).state!=4;i++)game.StepHostiles(.1f);
        Check(game.CaptureSave().hostiles.First(h=>h.id==hostile.id).state==4,"Enemy swims across the actual shoreline and climbs onto walkable land");float before=QuestRowboat.FlatDistance(enemy.transform.position,session.player.transform.position);for(int i=0;i<12;i++)game.StepHostiles(.1f);Check(QuestRowboat.FlatDistance(enemy.transform.position,session.player.transform.position)<before,"Enemy continues pursuing the player up the bank");
    }
    static void Finish(string error)
    {
        if(CampaignJournalUI.IsOpen)CampaignJournalUI.Instance.Close();Time.timeScale=1;File.WriteAllText("Logs/MarineJournalPlaytest.json",JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Formatting.Indented));BachDangSaveSystem.TestDirectory=null;SessionState.SetBool(Flag,false);EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<4)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);if(EditorApplication.timeSinceStartup-started>110)throw new Exception("Marine/journal review timed out at "+phase);
            var p=CampaignProgress.Instance;var ui=CampaignJournalUI.Instance;var front=BachDangFrontEnd.Instance;
            if(phase==0)
            {
                front.transform.Find("Home/RankingGuide").GetComponent<Button>().onClick.Invoke();Check(CampaignJournalUI.IsOpen&&front.Page==BachDangFrontEnd.MenuPage.Home,"Ranking guide opens from the title without starting the story");Capture("Campaign_Ranking_Guide");ui.Close();
                front.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();Story.Instance.TransitionTo(Story.StoryState.Act1_Training,true);ui.Open();
                Check(Time.timeScale==0&&BeachBoatQuestSession.InputBlocked,"Journal pauses the world and blocks combat input");float paused=p.Record.seconds;typeof(CampaignProgress).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);Check(p.Record.seconds==paused,"Reading the Tab journal cannot consume rank timing");ui.Refresh();var task=ui.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Task");Check(task.text.Contains("ĐÃ XONG"),"Completed tasks show green completion status");Check(ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("CHƯA XONG")),"Unfinished tasks remain in the journal");Capture("Campaign_Journal_Tasks");Capture("Campaign_Journal_Tasks_960",960,540);ui.Close();Check(Time.timeScale==1,"Closing Tab journal resumes the prior play speed");
                ScoreChecks();MarineChecks();SeedPerfect();Story.Instance.TransitionTo(Story.StoryState.Act3_Victory,true);Check(!p.Record.finished&&!CampaignJournalUI.IsOpen,"Winning the battle alone does not end the campaign before reporting");Story.Instance.OnVictoryReported();Check(p.Record.finished&&p.Record.finalRank=="S"&&p.Record.finalScore==100,"Reporting victory seals the final grade using the documented score");BeachBoatStory.StoryDialogueUI.Instance.Close();phase=1;return;
            }
            if(phase==1)
            {
                if(!CampaignJournalUI.IsOpen)return;Check(ui.IsEnding,"Final report automatically opens the end-game results");Capture("Campaign_End_Rank_S");Capture("Campaign_End_Rank_S_960",960,540);ui.ShowPage(1);Capture("Campaign_End_Achievements");ui.ShowPage(0);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath("Logs/MarineJournalSaveQA");ui.SaveResult();Check(front.Page==BachDangFrontEnd.MenuPage.Slots,"Results offer the normal ten-slot save picker");front.ChooseSlot(10);if(front.Page==BachDangFrontEnd.MenuPage.ConfirmOverwrite)front.ConfirmOverwrite();Check(BachDangSaveSystem.TryRead(10,out var saved)&&saved.campaign.finished&&saved.campaign.finalRank=="S"&&saved.campaign.finalScore==100,"The chosen save slot retains the final rank and all timing records");Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
