using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;
using Dialogue=BeachBoatStory.StoryDialogueUI;
using Intro=BeachBoatStory.OpeningEvent;

/// <summary>Exercise the same contextual Escape handler called by live Update, on separate rendered frames.</summary>
public static class BeachBoatEscapePlaytest
{
    const string Flag="Escape938QA";static double began;static int phase,index,lastFrame=-1;static string error;
    static float best,stamina;static int trained;static readonly List<string> checks=new List<string>();
    public static void RunInEditor(){began=0;phase=index=0;lastFrame=-1;error=null;checks.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string message,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Exception||type==LogType.Error))error=message+"\n"+stack;}
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
    static void Move(Vector3 p){var player=BeachBoatQuestSession.Instance.player;player.transform.position=p;player.GetComponent<Rigidbody>().position=p;Physics.SyncTransforms();}
    static BeachBoatMinigameStation Station(BeachBoatMinigameStation.ActivityKind kind)=>Object.FindObjectsByType<BeachBoatMinigameStation>().First(s=>s.kind==kind);
    static CampFacility Armory=>Object.FindObjectsByType<CampFacility>().First(f=>f.kind==CampFacility.FacilityKind.Armory);
    static void Leave(BachDangFrontEnd front,string label)
    {
        Check(front.HandleEscape(),label+": Escape is handled");
        Check(front.Page==BachDangFrontEnd.MenuPage.None&&Time.timeScale==1&&Cursor.lockState==CursorLockMode.Locked,label+": return directly to world controls without Pause");
        Check(!front.HandleEscape()&&front.Page==BachDangFrontEnd.MenuPage.None,label+": the same key press cannot also open Pause");
    }
    static CampRecoveryGame StartTraining(bool win)
    {
        var facility=Armory;Move(facility.transform.position);Check(CampRecoveryGame.Open(facility),"Open stamina exercise");var game=CampRecoveryGame.Active;game.StartAttempt();
        if(win)
        {
            float last=-100;
            for(int i=0;i<1500&&game.Phase==CampRecoveryGame.GamePhase.Playing;i++)
            {game.AdvanceTime(.02f);if(game.RhythmPosition>=game.GreenMin&&game.RhythmPosition<=game.GreenMax&&game.Elapsed-last>=.45f){if(game.Submit(0))last=game.Elapsed;}}
            Check(game.Phase==CampRecoveryGame.GamePhase.Won,"Actually win the rhythm exercise before testing result-screen Escape");
        }
        return game;
    }
    static void Finish(string failure)
    {
        File.WriteAllText("Logs/EscapePlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=failure==null,error=failure,phase,checks},Newtonsoft.Json.Formatting.Indented));SessionState.SetBool(Flag,false);Time.timeScale=1;EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(began==0)began=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-began<4||Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
        try
        {
            if(error!=null)throw new Exception(error);if(EditorApplication.timeSinceStartup-began>90)throw new Exception("Escape review timed out");
            var front=BachDangFrontEnd.Instance;var story=Story.Instance;var vitals=BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>();
            var drum=Station(BeachBoatMinigameStation.ActivityKind.WarDrum);
            switch(phase)
            {
                case 0:
                    front.Play();Object.FindAnyObjectByType<Intro>().SkipOpening();Dialogue.Instance.DismissForCheckpoint();story.TransitionTo(Story.StoryState.Act1_Training,true);Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;break;
                case 1:
                    Move(drum.GetComponentInParent<DrumTrainingGround>().Approach);drum.BeginActivity();Check(BeachBoatMinigameStation.Active==drum,"Open the mandatory first drum attempt");Leave(front,"Mandatory drum cancel");Check(BeachBoatMinigameStation.Active==null&&!drum.HasCompleted&&story.currentState==Story.StoryState.Act1_Training,"Cancelling never completes the mandatory drum quest");break;
                case 2:
                    Check(!BeachBoatMinigameUI.Instance.transform.Find("DrumMinigame").gameObject.activeSelf,"Cancelled drum screen disappears on the next rendered frame");Check(front.HandleEscape()&&front.Page==BachDangFrontEnd.MenuPage.Pause&&Time.timeScale==0,"A new Escape press while playing the world opens the real Pause menu");break;
                case 3:
                    front.OpenSettings();Check(front.HandleEscape()&&front.Page==BachDangFrontEnd.MenuPage.Pause,"Escape backs out of settings to its Pause parent");break;
                case 4:
                    Check(front.HandleEscape()&&front.Page==BachDangFrontEnd.MenuPage.None&&Time.timeScale==1,"Escape from Pause resumes normal gameplay");break;
                case 5:
                    Move(drum.GetComponentInParent<DrumTrainingGround>().Approach);drum.BeginActivity();for(int i=0;i<16&&BeachBoatMinigameStation.Active==drum;i++)drum.SubmitRhythm(drum.ExpectedBeat);Check(drum.HasCompleted,"Earn the actual drum completion before an accidental replay");Dialogue.Instance.Close();Dialogue.Instance.DismissForCheckpoint();best=drum.BestTime;break;
                case 6:
                    Move(drum.GetComponentInParent<DrumTrainingGround>().Approach);drum.BeginActivity();Check(BeachBoatMinigameStation.Active==drum,"A completed drum can be replayed");Leave(front,"Accidental drum replay");Check(drum.HasCompleted&&drum.BestTime==best&&CampaignProgress.Instance.MiniRecord(0)!=null,"Leaving a replay preserves the completed drum, achievement and best time");index=0;break;
                case 7:
                    {var stall=VillagePreparationQuest.Instance.stalls[index];stall.RestoreLearningProgress(1);Move(stall.InteractionPosition);Check(stall.TryOpen(),"Open instructions at "+stall.kind);Leave(front,"Trade instructions "+stall.kind);Check(VillageWorkStall.Active==null&&stall.CompletedLevels==1,"Leaving instructions keeps the earned level at "+stall.kind);if(++index<4)return;index=0;break;}
                case 8:
                    {var stall=VillagePreparationQuest.Instance.stalls[index];stall.RestoreLearningProgress(3);Move(stall.InteractionPosition);Check(stall.TryOpen(),"Replay a completed trade "+stall.kind);stall.StartAttempt(0);Leave(front,"Trade replay "+stall.kind);Check(VillageWorkStall.Active==null&&stall.Completed&&stall.CompletedLevels==3,"Leaving an active replay preserves collected material at "+stall.kind);if(++index<4)return;break;}
                case 9:
                    {var stall=VillagePreparationQuest.Instance.stalls.First(s=>s.kind==VillageWorkStall.WorkKind.Timber);stall.RestoreLearningProgress(1);Move(stall.InteractionPosition);Check(stall.TryOpen(),"Open a new medium timber attempt");stall.StartAttempt(1);for(int task=0;task<stall.Goal;task++){int mask=Enumerable.Range(0,1<<stall.TimberOptions).First(stall.TimberSelectionValid);for(int bit=0;bit<stall.TimberOptions;bit++)if((mask&(1<<bit))!=0)stall.SubmitOption(bit);stall.SubmitOption(8);}Check(stall.Phase==VillageWorkStall.WorkPhase.Won,"Actually solve the timber tasks before leaving the result screen");Leave(front,"Trade won screen");Check(stall.CompletedLevels==1,"Escape does not claim an unconfirmed material reward");break;}
                case 10:
                    {var stall=VillagePreparationQuest.Instance.stalls.First(s=>s.kind==VillageWorkStall.WorkKind.Timber);Move(stall.InteractionPosition);stall.TryOpen();stall.StartAttempt(1);stall.AdvanceTime(stall.TimeLimit+1);Check(stall.Phase==VillageWorkStall.WorkPhase.Lost,"Reach the actual timeout screen");Leave(front,"Trade lost screen");Check(stall.CompletedLevels==1,"Escape on failure keeps previous completed levels");break;}
                case 11:
                    stamina=vitals.maxStamina;trained=vitals.CompletedTrainingLevels;Move(Armory.transform.position);CampRecoveryGame.Open(Armory);Leave(front,"Stamina instructions");Check(CampRecoveryGame.Active==null&&vitals.maxStamina==stamina,"Leaving optional training instructions grants no reward");break;
                case 12:
                    {var game=StartTraining(false);game.AdvanceTime(game.TimeLimit+1);Leave(front,"Stamina failed attempt");Check(CampRecoveryGame.Active==null&&vitals.CompletedTrainingLevels==trained,"Escape exits a failed stamina exercise without changing completion");break;}
                case 13:
                    StartTraining(true);Leave(front,"Stamina unclaimed win");Check(CampRecoveryGame.Active==null&&vitals.maxStamina==stamina,"Escape from an unclaimed stamina win does not silently award stamina");break;
                case 14:
                    {var game=StartTraining(true);Check(game.ClaimReward(),"Claim a real +20 stamina reward");Leave(front,"Next optional training level");Check(CampRecoveryGame.Active==null&&vitals.maxStamina==stamina+20&&vitals.CompletedTrainingLevels==trained+1,"Escape from the next level keeps the already claimed +20 reward");break;}
                case 15:
                    CampaignJournalUI.Instance.Open();Check(!front.HandleEscape()&&front.Page==BachDangFrontEnd.MenuPage.None,"An open Tab journal retains Escape priority over the Pause menu");CampaignJournalUI.Instance.Close();Check(!front.HandleEscape(),"Closing the journal cannot leak the same Escape into Pause");break;
                case 16:
                    Check(front.HandleEscape()&&front.Page==BachDangFrontEnd.MenuPage.Pause,"After every minigame has closed, a fresh Escape still opens Pause");break;
                case 17:
                    Check(front.HandleEscape()&&front.Page==BachDangFrontEnd.MenuPage.None&&!front.QuitRequested&&BachDangSaveSystem.CanSave,"Resume restores the world and ordinary save availability");Finish(null);return;
            }
            phase++;
        }
        catch(Exception exception){Finish(exception.ToString());}
    }
}
