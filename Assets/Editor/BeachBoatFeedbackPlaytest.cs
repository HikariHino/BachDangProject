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
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatFeedbackPlaytest
{
    const string Flag="Feedback938QA";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> checks=new List<string>();
    static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static int phase,lastFrame=-1;static double began;static string runtimeError;
    static VillageWorkStall food;static float campaignSeconds;static int miniCount;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    static void Invoke(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
    static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
    public static void RunInEditor(){phase=0;lastFrame=-1;began=0;runtimeError=null;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    public static void RunBatch(){UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");RunInEditor();}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string text,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Error||type==LogType.Exception))runtimeError=text+"\n"+stack;}
    static void Finish(string failure)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/FeedbackPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=failure==null,failure,phase,checks},Newtonsoft.Json.Formatting.Indented));
        SessionState.SetBool(Flag,false);BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
        if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(failure==null?0:1);
    }
    static void Move(Vector3 p){var player=BeachBoatQuestSession.Instance.player;player.transform.position=p;player.GetComponent<Rigidbody>().position=p;Physics.SyncTransforms();}
    static void Setup()
    {
        foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))if(p.EndsWith(".json")||p.EndsWith(".bak"))hashes[p]=Hash(p);
        BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/FeedbackQA/"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));BachDangAutoSave.Instance.enabled=false;
        BachDangFrontEnd.Instance.Play();UnityEngine.Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        var story=Story.Instance;story.TransitionTo(Story.StoryState.Act2_CollectWoodInVillage,true);story.collectedWood=0;
        var guide=UnityEngine.Object.FindAnyObjectByType<StoryRouteGuide>();
        Check(story.objectiveDescText.text.Contains("nhấn E")&&story.objectiveTitleText.text.Contains("0/5"),"Wood objective explains E pickup and five-piece progress");
        Move(new Vector3(0,100,0));Check(guide.ResolveTarget(out _)==null,"Distant wood has no floating pickup target");
        var wood=BeachBoatQuestSession.Instance.woodItems[0];Move(wood.transform.position);
        Check(guide.ResolveTarget(out var title)==wood.transform&&title.Contains("0/5"),"Nearby wood gets the contextual E guide and count");
        Check(wood.TryCollect()&&story.collectedWood==1,"Nearby wood pickup increments progress");
        var saved=BachDangSaveSystem.Capture("QA wood");BachDangSaveSystem.Apply(saved);
        Check(story.collectedWood==1&&story.objectiveTitleText.text.Contains("1/5"),"An existing wood save gets the new instruction and retained progress");
        foreach(var item in BeachBoatQuestSession.Instance.woodItems.Where(w=>!w.IsCollected)){Move(item.transform.position);Check(item.TryCollect(),"All remaining fixed wood is collectible: "+item.name);}
        Check(story.currentState==Story.StoryState.Act2_SupplyPreparation&&CampaignProgress.Instance.Record.woodFinished,"Five wood still starts material preparation and finishes the wood achievement timer");
        food=VillagePreparationQuest.Instance.stalls.First(s=>s.kind==VillageWorkStall.WorkKind.Provisions);Move(food.InteractionPosition);Check(food.TryOpen(),"Rations opens at the actual stall");
        var ui=UnityEngine.Object.FindAnyObjectByType<VillageWorkUI>();Invoke(ui,"Update");
        var button=ui.transform.Find("WorkOverlay/Workbench/Instructions/VideoControls/Practice").GetComponent<Button>();
        Check(button.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("TẬP THỬ"),"The video screen offers a separate untimed hands-on sample");
        button.onClick.Invoke();Check(food.IsPractice,"The practice button opens the untimed sample independently of Start");
        campaignSeconds=CampaignProgress.Instance.Record.seconds;miniCount=CampaignProgress.Instance.RecordedMinigames;
    }
    static void Practice()
    {
        food.AdvanceTime(200);Invoke(CampaignProgress.Instance,"Update");
        Check(food.Elapsed==0&&food.Mistakes==0&&CampaignProgress.Instance.Record.seconds==campaignSeconds,"Practice does not charge minigame or campaign time");
        Check(food.RequiredIngredient(0)==4&&food.RequiredIngredient(1)==2&&food.RequiredIngredient(2)==1,"Worked sample uses two people with four rice, two fish and one total salt");
        Check(!food.SubmitOption(3)&&food.Mistakes==0&&food.Elapsed==0&&food.IsPractice,"A practice mistake explains the recipe without penalties or failure");
        food.SubmitOption(2);food.SubmitOption(2);Check(food.Undo()&&food.IngredientCount(2)==1,"An extra salt can be undone during practice");
        for(int i=0;i<4;i++)food.SubmitOption(0);for(int i=0;i<2;i++)food.SubmitOption(1);
        var ui=UnityEngine.Object.FindAnyObjectByType<VillageWorkUI>();Invoke(ui,"Update");
        var salt=ui.transform.Find("WorkOverlay/Workbench/Work/Ingredients/Ingredient2");
        Check(salt.Find("Recipe").GetComponent<TMPro.TMP_Text>().text.Contains("CẢ NHÓM: 1")&&salt.Find("Name").GetComponent<TMPro.TMP_Text>().text.Contains("1/1"),"Salt card distinguishes the whole group and shows the added/required count");
        typeof(BeachBoatVillageWorkPlaytest).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"FeedbackRationsPractice"});
    }
    static void Timed()
    {
        Check(food.SubmitOption(3)&&food.Phase==VillageWorkStall.WorkPhase.Instructions&&!food.NeedsPractice,"Completing the sample returns to instructions for the real attempt");
        Check(food.Progress==0&&food.CompletedLevels==0&&CampaignProgress.Instance.RecordedMinigames==miniCount,"Practice grants no supplies, levels or achievement record");
        food.StartFromInstructions();Check(food.Phase==VillageWorkStall.WorkPhase.Playing&&food.Elapsed==0,"The real attempt starts its own timer at zero");
        CheckRationColors();
        food.AdvanceTime(.2f);food.SubmitOption(3);Check(food.Elapsed>=2.2f&&food.Mistakes==1,"The timed attempt still applies the normal mistake penalty");
        Check(!food.Feedback.Contains("Cần đủ")&&!food.Feedback.Contains(food.RequiredIngredient(0)+" gạo"),"A wrong timed ration gives color guidance without revealing calculated totals");
        while(food.Phase==VillageWorkStall.WorkPhase.Playing)
        {
            for(int i=0;i<3;i++)for(int n=food.IngredientCount(i);n<food.RequiredIngredient(i);n++)food.SubmitOption(i);
            Check(food.SubmitOption(3),"Timed rations recipe remains solvable");
        }
        Check(food.Phase==VillageWorkStall.WorkPhase.Won&&CampaignProgress.Instance.RecordedMinigames==miniCount+1,"Only the real completed attempt records a minigame result");
        Check(food.ClaimReward()&&food.CompletedLevels==1&&!food.NeedsPractice,"Passing Easy unlocks Medium without another sample");food.Close();
        var saved=BachDangSaveSystem.Capture("QA learned rations");BachDangSaveSystem.Apply(saved);Move(food.InteractionPosition);Check(food.TryOpen()&&!food.NeedsPractice&&food.CompletedLevels==1,"Old learning progress survives load without forcing the sample again");food.Close();
        ReportHealing();BattleGuide();
        foreach(var p in hashes)Check(File.Exists(p.Key)&&Hash(p.Key)==p.Value,"User save unchanged: "+Path.GetFileName(p.Key));
    }
    static void CheckRationColors()
    {
        // Construct three real input states: missing rice, enough fish, excess salt.
        for(int n=0;n<food.RequiredIngredient(1);n++)food.SubmitOption(1);
        for(int n=0;n<=food.RequiredIngredient(2);n++)food.SubmitOption(2);
        var ui=UnityEngine.Object.FindAnyObjectByType<VillageWorkUI>();Invoke(ui,"Update");
        var cards=ui.transform.Find("WorkOverlay/Workbench/Work/Ingredients");
        var colors=new Color[3];
        for(int i=0;i<3;i++)
        {
            var card=cards.Find("Ingredient"+i);var recipe=card.Find("Recipe").GetComponent<TMPro.TMP_Text>().text;
            var label=card.Find("Name").GetComponent<TMPro.TMP_Text>().text;colors[i]=card.GetComponent<Image>().color;
            Check(!recipe.Contains("=")&&!label.Contains("ĐÃ THÊM")&&!label.Contains("/"),"Timed card hides computed answers and quantity counters: "+i);
            Check(label.Contains(new[]{"THIẾU","ĐỦ","DƯ"}[i]),"Live inputs display the matching missing/enough/excess status: "+i);
        }
        Check(colors[0].r>colors[0].g&&colors[0].g>colors[0].b*2&&colors[1].g>colors[1].r*2&&colors[2].r>colors[2].g*2,"Missing is yellow, enough green and excess red");
        Check(cards.Find("Ingredient2/Recipe").GetComponent<TMPro.TMP_Text>().text=="CẢ NHÓM: "+food.SaltPerBundle+" MUỐI","Timed salt retains the unmultiplied whole-group requirement");
        typeof(BeachBoatVillageWorkPlaytest).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"RationsColorFeedback"});
        Check(food.RestoreCurrentTask()&&food.IngredientCount(0)==0&&food.IngredientCount(1)==0&&food.IngredientCount(2)==0,"Reset clears the sample inputs without changing the live recipe");
    }
    static void ReportHealing()
    {
        var story=Story.Instance;var vitals=BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>();vitals.ReinforceArmor();vitals.TrainAtArmory(1);vitals.TakeImpact(20,"QA");vitals.TryExert(10,1);
        int armor=vitals.ArmorCharges;float maxStamina=vitals.maxStamina;
        story.TransitionTo(Story.StoryState.Act2_ReportToNgoQuyen,true);float wounded=vitals.Health;
        story.TransitionTo(Story.StoryState.Act2_ReportToNgoQuyen,true);Check(vitals.Health==wounded,"Merely entering the camp/report state does not refill health");
        story.OnReportToNgoQuyenAct2();Check(vitals.Health==vitals.maxHealth&&vitals.Stamina==vitals.maxStamina&&vitals.ArmorCharges==armor&&vitals.maxStamina==maxStamina,"Reporting supplies fully restores health and stamina while preserving armor and upgrades");
        vitals.TakeImpact(10,"QA");wounded=vitals.Health;story.OnReportToNgoQuyenAct2();Check(vitals.Health==wounded,"Repeating an already completed report cannot heal again");
        var save=BachDangSaveSystem.Capture("QA wounded after report");BachDangSaveSystem.Apply(save);Check(vitals.Health==wounded,"Loading a post-report save keeps its recorded health rather than granting free healing");
        story.TransitionTo(Story.StoryState.Act2_CompleteReturnToCamp,true);story.OnReportStakesComplete();Check(vitals.Health==vitals.maxHealth,"Reporting completed stakes gives the next camp recovery");
        vitals.TakeImpact(10,"QA");story.TransitionTo(Story.StoryState.Act3_Victory,true);story.OnVictoryReported();Check(vitals.Health==vitals.maxHealth,"The final victory report also restores health");BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
    }
    static void BattleGuide()
    {
        var fleet=EnemyFleetDirector.Instance;var story=Story.Instance;var timeline=BachDangBattleTimeline.Instance;var guide=UnityEngine.Object.FindAnyObjectByType<StoryRouteGuide>();
        story.TransitionTo(Story.StoryState.Act3_BattleInProgress,true);Set(fleet,"<BattleStarted>k__BackingField",true);Set(fleet,"<EbbStarted>k__BackingField",false);Set(fleet,"<AmbushStarted>k__BackingField",false);timeline.BeginLure(true);story.UpdateUI();
        var target=guide.ResolveTarget(out var title);Check(target!=null&&target.GetComponent<EnemyDeckGuard>()==null&&title.Contains("Vùng nhử"),"The lure arrow points to a staging area rather than an enemy soldier");
        Check(story.objectiveTitleText.text.Contains("Nhử")&&story.objectiveDescText.text.Contains("Chưa tấn công")&&!RiverBattleObjective.ControlHint.Contains("Cung"),"Lure HUD explains waiting for the trap instead of attacking");
        timeline.BeginEbb();story.UpdateUI();guide.ResolveTarget(out title);Check(title.Contains("Chờ nước rút")&&story.objectiveTitleText.text.Contains("Chờ nước rút")&&!RiverBattleObjective.Counterattack,"Falling water keeps the waiting objective until the actual ambush");
        var save=BachDangSaveSystem.Capture("QA waiting phase");save.fleet.active=false;BachDangSaveSystem.Apply(save);guide.ResolveTarget(out title);Check(title.Contains("Chờ nước rút"),"Loading a falling-water save retains the waiting instructions");
        timeline.BeginAmbush();story.UpdateUI();Check(RiverBattleObjective.Counterattack&&story.objectiveTitleText.text.Contains("Phản công")&&RiverBattleObjective.ControlHint.Contains("Cung/kiếm"),"Only the exposed-stake ambush shows combat controls");
        timeline.MarkVictory();story.TransitionTo(Story.StoryState.Act3_Victory,true);Check(story.objectiveDescText.text.Contains("báo tin chiến thắng"),"Victory tells the player to return and report");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<3)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);if(EditorApplication.timeSinceStartup-began>120)throw new Exception("Feedback QA timeout");
            if(phase==0){Setup();phase=1;return;}if(phase==1){Practice();phase=2;return;}Timed();Finish(null);
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
