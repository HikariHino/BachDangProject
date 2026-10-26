using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>A rolling recovery file, independent of all ten player-selected slots.</summary>
    [DefaultExecutionOrder(900)]
    public sealed class BachDangAutoSave : MonoBehaviour
    {
        public static BachDangAutoSave Instance {get;private set;}
        public const float Interval=60;
        public float Elapsed {get;private set;}
        public BachDangSaveData RetryPoint {get;private set;}
        bool needsBaseline;
        float nextWriteAttempt;
        void Awake()=>Instance=this;
        public void BeginNewJourney(){RetryPoint=null;Elapsed=0;needsBaseline=true;nextWriteAttempt=0;}
        public void AcceptSavedPoint(BachDangSaveData data){RetryPoint=JsonUtility.FromJson<BachDangSaveData>(JsonUtility.ToJson(data));Elapsed=0;needsBaseline=false;}
        public bool BeginLoadedJourney(BachDangSaveData data,bool selectedManual)
        {
            AcceptSavedPoint(data);
            // Selecting an older manual slot deliberately branches from that exact
            // progress; an autosave from another journey must not win Continue.
            if(!selectedManual)return true;
            var baseline=BachDangSaveSystem.Capture("Lưu tạm • từ ô lưu đã chọn");
            if(!BachDangSaveSystem.SaveTemporary(baseline)){needsBaseline=true;return false;}
            AcceptSavedPoint(baseline);return true;
        }
        public static bool SafeMoment=>BachDangSaveSystem.CanSave&&Time.timeScale>0&&
            (BachDangFrontEnd.Instance?.GameStarted??false)&&!BeachBoatQuestSession.InputBlocked&&
            BeachBoatMinigameStation.Active==null&&!(CampaignProgress.Instance?.Record.finished??false)&&
            RiverBoardingCombat.Instance?.State!=RiverBoardingCombat.PlayerState.Climbing;
        public static bool GameplayRunning=>Time.timeScale>0&&(BachDangFrontEnd.Instance?.GameStarted??false)&&
            !BachDangFrontEnd.IsBlocking&&!OpeningEvent.IsOpeningActive&&!(StoryDialogueUI.Instance?.IsOpen??false)&&
            !(QuestCheckpoint.Instance?.IsDefeated??false)&&!(ScreenFader.Instance?.IsTransitioning??false)&&
            !(CampaignProgress.Instance?.Record.finished??false);
        void Update()=>Step(Time.deltaTime);
        public void Step(float gameplaySeconds)
        {
            if(gameplaySeconds<=0||!GameplayRunning)return;
            Elapsed+=gameplaySeconds;
            if(!SafeMoment)return;
            if((!needsBaseline&&Elapsed<Interval)||Time.unscaledTime<nextWriteAttempt)return;
            var data=BachDangSaveSystem.Capture("Tự động lưu • "+BachDangSaveSystem.Chapter((int)BachDangStoryManager.Instance.currentState));
            if(BachDangSaveSystem.SaveTemporary(data))AcceptSavedPoint(data);
            else {nextWriteAttempt=Time.unscaledTime+10;BachDangFrontEnd.Instance?.Notify("Chưa lưu tạm được • "+BachDangSaveSystem.LastError);}
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
