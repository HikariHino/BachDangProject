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
        var scrolls=canvases.SelectMany(c=>c.GetComponentsInChildren<ScrollRect>()).Select(s=>new{s,s.horizontalNormalizedPosition,s.verticalNormalizedPosition}).ToArray();
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}camera.targetTexture=rt;Canvas.ForceUpdateCanvases();
        // A capture resizes the canvas to a different aspect ratio. Reapply the
        // intended scroll location after its bounds, rather than capturing stale bounds.
        foreach(var s in scrolls){s.s.horizontalNormalizedPosition=s.horizontalNormalizedPosition;s.s.verticalNormalizedPosition=s.verticalNormalizedPosition;}
        Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;foreach(var s in saved){s.c.renderMode=RenderMode.ScreenSpaceOverlay;s.c.worldCamera=s.worldCamera;s.c.planeDistance=s.planeDistance;}
        Canvas.ForceUpdateCanvases();foreach(var s in scrolls){s.s.horizontalNormalizedPosition=s.horizontalNormalizedPosition;s.s.verticalNormalizedPosition=s.verticalNormalizedPosition;}Canvas.ForceUpdateCanvases();Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
    }
    static void ScoreChecks()
    {
        var p=CampaignProgress.Instance;var empty=p.Capture();
        Check(CampaignProgress.MiniTarget(0,3)==22&&CampaignProgress.MiniTarget(0,2)==33&&CampaignProgress.MiniTarget(0,1)==44,"Drum S/A/B deadlines are the explicit 22/33/44 seconds shown to the player");
        SeedPerfect();Check(p.EarnedRank=="S"&&p.ConditionsFor(3).Length==8&&p.ConditionsFor(3).All(c=>c.met),"S requires every timed activity plus stakes, kills and campaign time");
        float before=p.MiniRecord(1).seconds;p.RecordMinigame("village:0:0",90,100);Check(p.MiniRecord(1).seconds==before,"Slower replays cannot erase a valid minigame record");
        var drum=p.MiniRecord(0);drum.seconds=22;Check(p.EarnedRank=="S","Exact drum S deadline is accepted");drum.seconds=22.1f;Check(p.EarnedRank=="A","One slow drum performance blocks S even when every other result is excellent");drum.seconds=33.1f;Check(p.EarnedRank=="B","A drum finish beyond A falls to B");drum.seconds=44.1f;Check(p.EarnedRank=="C","A drum finish beyond B blocks every higher campaign rank");SeedPerfect();
        for(int index=1;index<13;index++)
        {
            var m=p.MiniRecord(index);m.seconds=CampaignProgress.MiniTarget(index,3);Check(p.EarnedRank=="S","Exact S timing accepted for "+CampaignProgress.MiniName(index));
            m.seconds+=.1f;Check(p.EarnedRank=="A","A slow individual material level cannot be hidden by other fast records: "+CampaignProgress.MiniName(index));SeedPerfect();
        }
        p.Record.minigames=p.Record.minigames.Where(m=>m.id!="village:2:2").ToArray();Check(p.EarnedRank=="C","Missing hard-rice timing cannot grant a timed rank through assumed averages");SeedPerfect();
        var wood=p.MiniRecord(1);wood.seconds=80;Check(p.EarnedRank=="A","Exact material A deadline is accepted");wood.seconds=80.1f;Check(p.EarnedRank=="B","Material timing just above A caps campaign rank at B");wood.seconds=95;Check(p.EarnedRank=="B","Exact material B deadline is accepted before the game's failure timeout");wood.seconds=95.1f;Check(p.EarnedRank=="C","A slow but successful material level blocks timed ranks");SeedPerfect();
        p.Record.seconds=40*60;Check(p.EarnedRank=="S","Exact 40-minute campaign S deadline is accepted");p.Record.seconds+=1;Check(p.EarnedRank=="A","Campaign time above S caps rank at A");p.Record.seconds=60*60+1;Check(p.EarnedRank=="B","Campaign time above A caps rank at B");p.Record.seconds=90*60+1;Check(p.EarnedRank=="C","Campaign time above B results in C");SeedPerfect();
        p.Record.stakeSeconds=p.StakeTargetSeconds+.1f;Check(p.EarnedRank=="A","Missing the stake S deadline caps rank at A");p.Record.stakeSeconds=p.StakeTarget(2)+.1f;Check(p.EarnedRank=="B","Missing the stake A deadline caps rank at B");p.Record.stakeSeconds=p.StakeTarget(1)+.1f;Check(p.EarnedRank=="C","Missing the stake B deadline results in C");SeedPerfect();
        p.Record.kills=p.Record.kills.Take(p.KillRequirement(2)).ToArray();Check(p.EarnedRank=="A","Personal kills meet A's actual required count");p.Record.kills=p.Record.kills.Take(p.KillRequirement(1)).ToArray();Check(p.EarnedRank=="B","Personal kills meet B's actual required count");p.Record.kills=Array.Empty<string>();Check(p.EarnedRank=="C","Excellent times cannot compensate for missing personal combat achievements");SeedPerfect();
        var vitals=BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>();var originalVitals=new BachDangSaveData{maxHealth=vitals.maxHealth,maxStamina=vitals.maxStamina,health=vitals.Health,stamina=vitals.Stamina,armor=vitals.ArmorCharges,trainingLevels=vitals.CompletedTrainingLevels};
        Check(vitals.CompletedTrainingLevels==0&&p.EarnedRank=="S","Optional stamina training can be skipped entirely while earning S");for(int level=1;level<=3;level++)vitals.TrainAtArmory(level);Check(p.EarnedRank=="S"&&vitals.maxStamina==originalVitals.maxStamina+60,"All three optional stamina rewards total +60 and never affect rank");vitals.RestoreSave(originalVitals);
        var journal=CampaignJournalUI.Instance;journal.Open();journal.ShowPage(1);Capture("Campaign_Timed_Achievements");journal.ShowPage(2);Capture("Campaign_Timed_Rank_Conditions");journal.Close();
        var guard=RiverPlayerCombat.Instance.guards[0];p.Record.kills=Array.Empty<string>();p.RecordKill(guard);p.RecordKill(guard);Check(p.Record.kills.Length==1,"A respawned enemy identity cannot be farmed twice for rank points");
        var copy=p.Capture();p.Restore(copy,0);Check(Mathf.Abs(p.Record.seconds-copy.seconds)<.01f&&p.Record.kills.Length==1,"Rank timers and kill identities survive serialization");
        p.Restore(null,10);Check(p.Record.historyIncomplete&&p.MissionDone(1)&&!p.MissionDone(12),"Legacy quest 3 saves recover past task checks without inventing a completed battle");
        var oldEnding=new BachDangSaveData.Campaign{finished=true,finalRank="S",finalScore=100,seconds=60,stakeFinished=true};p.Restore(oldEnding,14);Check(p.Record.finalRank=="C"&&p.Record.rankRulesVersion==CampaignProgress.RankRulesVersion,"An old point-based S ending is rechecked and cannot invent missing timed achievements");
        p.Restore(empty,0);p.Transition(10,9);Check(!p.MissionDone(12),"Retrying a battle cannot mark that battle completed");p.Restore(empty,0);
        foreach(var trade in VillagePreparationQuest.Instance.stalls)trade.RestoreLearningProgress(0);Story.Instance.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);Story.Instance.OnSupplyPreparationComplete();
        Check(Story.Instance.currentState==Story.StoryState.Act2_SupplyPreparation&&!p.MissionDone(4),"An optional supply relay cannot bypass the four required trades or give them a false green check");p.Restore(empty,0);
        var stall=VillagePreparationQuest.Instance.stalls.First(s=>s.kind==VillageWorkStall.WorkKind.Timber);Move(stall.InteractionPosition);Check(stall.TryOpen(),"A real material minigame can open for timing-goal UI verification");var workUI=Object.FindAnyObjectByType<VillageWorkUI>();typeof(VillageWorkUI).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(workUI,null);
        Check(workUI.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="TimingGoals"&&t.text==p.TimingBands(1)),"The material instruction screen shows the exact same S/A/B deadlines as the journal");Capture("Timed_Minigame_Intro");stall.StartAttempt(0);typeof(VillageWorkUI).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(workUI,null);Check(workUI.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="Stats"&&t.text.Contains(p.TimingBands(1))),"Timing goals stay visible while actually playing the material minigame");Capture("Timed_Minigame_Playing");stall.Close();
    }
    static void JournalTaskChecks()
    {
        var ui=CampaignJournalUI.Instance;ui.ShowPage(0);var texts=ui.GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="Task").ToArray();
        Check(texts.Count(t=>t.text.Contains("VẬT TƯ •"))==4,"The mission page expands material preparation into all four trades");
        foreach(var name in CampaignProgress.DifficultyNames)Check(texts.Count(t=>t.text.StartsWith("    Minigame "+name))==4,"All four trades expose a mission subtask at difficulty "+name);
        Check(texts.Count(t=>t.text.StartsWith("    Luyện thể lực •"))==3&&texts.Any(t=>t.text.Contains("NHIỆM VỤ PHỤ")&&t.text.Contains("TÙY CHỌN")),"All three stamina minigames appear as explicitly optional mission subtasks");
        Check(texts.Where(t=>t.text.StartsWith("    Minigame ")).All(t=>t.text.Contains("S ≤")&&t.text.Contains("A ≤")&&t.text.Contains("B ≤")),"Every material mission subtask includes exact time targets before it is played");
        var scroller=ui.GetComponentsInChildren<ScrollRect>().First(s=>s.name=="Tasks");scroller.verticalNormalizedPosition=.88f;Capture("Campaign_Journal_Supply_Details");Capture("Campaign_Journal_Supply_Details_960",960,540);scroller.verticalNormalizedPosition=0;Capture("Campaign_Journal_Optional_Training");
        var last=(RectTransform)texts.Last(t=>t.text.StartsWith("    Luyện thể lực •")).transform.parent;var corners=new Vector3[4];last.GetWorldCorners(corners);float bottom=scroller.viewport.InverseTransformPoint(corners[0]).y,top=scroller.viewport.InverseTransformPoint(corners[1]).y;
        Check(bottom>=scroller.viewport.rect.yMin-1&&top<=scroller.viewport.rect.yMax+1,"Scrolling to the end exposes the entire third optional stamina subtask without clipping");scroller.verticalNormalizedPosition=1;
    }
    static void DrumGroundChecks()
    {
        var session=BeachBoatQuestSession.Instance;var drum=Object.FindObjectsByType<BeachBoatMinigameStation>().First(s=>s.kind==BeachBoatMinigameStation.ActivityKind.WarDrum);var ground=drum.GetComponentInParent<DrumTrainingGround>();
        Check(ground!=null&&QuestRowboat.FlatDistance(drum.transform.position,new Vector3(-5573.51f,0,1362.62f))>200,"The isolated old drum is relocated to a dedicated outdoor drill station");
        Check(ground.GetComponentsInChildren<Collider>().Any(c=>c.name=="Solid_Drum_Rack_Leg"&&!c.isTrigger)&&ground.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("THAO TRƯỜNG")),"The drill station includes a physical timber rack and a readable training sign");
        Check(!Physics.OverlapBox(ground.transform.position+Vector3.up*14,new Vector3(22,13,27),Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(c=>!(c is TerrainCollider)&&c.GetComponentInParent<Animator>()==null&&!c.transform.IsChildOf(ground.transform)),"The new drum footprint is clear of existing buildings and props");
        var camera=session.storyCamera;var orbit=camera.GetComponent<BeachBoatStory.Camera_Script>();bool enabled=orbit.enabled;orbit.enabled=false;var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
        var approach=ground.Approach;ActorSolidBody.WalkingDimensions(session.player.GetComponentInChildren<Animator>(),out _,out float sole,out _);approach.y=Terrain.activeTerrain.SampleHeight(approach)+Terrain.activeTerrain.transform.position.y-sole+.2f;Move(approach);
        camera.transform.position=ground.transform.position+new Vector3(-38,36,-64);camera.transform.LookAt(ground.transform.position+Vector3.up*13);Capture("Drum_Training_Ground");
        Move(approach+Vector3.back*70);drum.BeginActivity();Check(BeachBoatMinigameStation.Active!=drum,"The training drum cannot be activated from far away");Move(approach);drum.BeginActivity();Check(BeachBoatMinigameStation.Active==drum,"The rebuilt drum remains reachable by its nearby E interaction");
        var ui=BeachBoatMinigameUI.Instance;typeof(BeachBoatMinigameUI).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui,null);
        Check(ui.GetComponentsInChildren<DrumFaceGraphic>().Length==1&&ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="TimingGoals"&&t.text==CampaignProgress.Instance.TimingBands(0)),"The new military command screen includes its drum face and exact achievement deadlines");Capture("Drum_Command_Interface");Capture("Drum_Command_Interface_960",960,540);
        int progress=drum.Progress;int expected=drum.ExpectedBeat;ui.GetComponentsInChildren<Button>().First(b=>b.name=="Drum"+expected).onClick.Invoke();Check(drum.Progress==progress+1,"The redesigned command buttons still submit the actual highlighted signal");
        typeof(BeachBoatMinigameStation).GetMethod("Finish",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(drum,new object[]{false});typeof(BeachBoatMinigameUI).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui,null);camera.transform.SetPositionAndRotation(oldPosition,oldRotation);orbit.enabled=enabled;
    }
    static void SeedPerfect()
    {
        var p=CampaignProgress.Instance;p.Restore(new BachDangSaveData.Campaign(),11);p.Transition(0,11);p.Record.seconds=30*60;p.Record.stakeStarted=true;p.Record.stakeFinished=true;p.Record.stakeSeconds=p.StakeTargetSeconds;
        p.Record.completedMask|=1<<(int)Story.StoryState.Act3_Victory;
        for(int i=0;i<13;i++)p.RecordMinigame(CampaignProgress.MiniId(i),CampaignProgress.MiniLimit(i)*.35f,CampaignProgress.MiniLimit(i));
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
                Check(Time.timeScale==0&&BeachBoatQuestSession.InputBlocked,"Journal pauses the world and blocks combat input");float paused=p.Record.seconds;typeof(CampaignProgress).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);Check(p.Record.seconds==paused,"Reading the Tab journal cannot consume rank timing");ui.Refresh();var task=ui.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Task");Check(task.text.Contains("ĐÃ XONG"),"Completed tasks show green completion status");Check(ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("CHƯA XONG")),"Unfinished tasks remain in the journal");Capture("Campaign_Journal_Tasks");Capture("Campaign_Journal_Tasks_960",960,540);JournalTaskChecks();ui.Close();Check(Time.timeScale==1,"Closing Tab journal resumes the prior play speed");
                DrumGroundChecks();ScoreChecks();MarineChecks();SeedPerfect();Story.Instance.TransitionTo(Story.StoryState.Act3_Victory,true);Check(!p.Record.finished&&!CampaignJournalUI.IsOpen,"Winning the battle alone does not end the campaign before reporting");Story.Instance.OnVictoryReported();Check(p.Record.finished&&p.Record.finalRank=="S"&&p.Record.rankRulesVersion==CampaignProgress.RankRulesVersion,"Reporting victory seals the final grade using every documented timed condition");BeachBoatStory.StoryDialogueUI.Instance.Close();phase=1;return;
            }
            if(phase==1)
            {
                if(!CampaignJournalUI.IsOpen)return;Check(ui.IsEnding,"Final report automatically opens the end-game results");Capture("Campaign_End_Rank_S");Capture("Campaign_End_Rank_S_960",960,540);ui.ShowPage(1);Capture("Campaign_End_Achievements");ui.ShowPage(0);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath("Logs/MarineJournalSaveQA");ui.SaveResult();Check(front.Page==BachDangFrontEnd.MenuPage.Slots,"Results offer the normal ten-slot save picker");front.ChooseSlot(10);if(front.Page==BachDangFrontEnd.MenuPage.ConfirmOverwrite)front.ConfirmOverwrite();Check(BachDangSaveSystem.TryRead(10,out var saved)&&saved.campaign.finished&&saved.campaign.finalRank=="S"&&saved.campaign.rankRulesVersion==CampaignProgress.RankRulesVersion,"The chosen save slot retains the final rank and all timing records");Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
