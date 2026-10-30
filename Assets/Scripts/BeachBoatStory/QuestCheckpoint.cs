using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Scene-local mission checkpoints. Death never restarts already-finished acts.</summary>
    public sealed class QuestCheckpoint : MonoBehaviour
    {
        public static QuestCheckpoint Instance {get;private set;}
        public bool IsDefeated {get;private set;}
        public BachDangStoryManager.StoryState SavedState {get;private set;}
        public bool HasCheckpoint {get;private set;}
        BeachBoatQuestSession session;
        PlayerVitals vitals;
        Vector3 position;
        Quaternion rotation;
        int woodCount,destroyedCount,armor;
        bool[] woods,stalls,stakes;
        int[] stallLevels;
        bool restoring;
        float previousTimeScale=1;
        GameObject overlay;
        TMP_Text checkpointText,headingText,retryText;
        BachDangSaveData recoveryPoint;
        void Awake(){Instance=this;session=GetComponent<BeachBoatQuestSession>();vitals=session.player.GetComponent<PlayerVitals>();}
        void Start(){BuildUI();Capture(BachDangStoryManager.Instance.currentState);}
        public void Capture(BachDangStoryManager.StoryState state)
        {
            if(IsDefeated||restoring||session==null||state==BachDangStoryManager.StoryState.Act3_BattleInProgress)return;
            var story=BachDangStoryManager.Instance;if(story==null)return;
            SavedState=state;HasCheckpoint=true;woodCount=story.collectedWood;destroyedCount=story.destroyedEnemyShips;armor=vitals.ArmorCharges;
            var anchor=session.playerSpawn;
            switch(state)
            {
                case BachDangStoryManager.StoryState.Act2_CollectWoodInVillage:
                case BachDangStoryManager.StoryState.Act2_SupplyPreparation:
                case BachDangStoryManager.StoryState.Act2_ReturnToCampGate:anchor=story.villageSpawnPoint;break;
                case BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver:
                case BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp:
                case BachDangStoryManager.StoryState.Act3_WaitForLowTide:
                case BachDangStoryManager.StoryState.Act3_Victory:anchor=QuestRowboat.Instance?.boardingPoint;break;
                case BachDangStoryManager.StoryState.Act1_Training:anchor=null;break;
            }
            position=anchor!=null?anchor.position:session.player.transform.position;rotation=anchor!=null?anchor.rotation:session.player.transform.rotation;
            woods=new bool[session.woodItems.Length];for(int i=0;i<woods.Length;i++)woods[i]=session.woodItems[i]!=null&&session.woodItems[i].IsCollected;
            stakes=RiverStakeZone.Instance?.CaptureCheckpoint();RefreshVillageProgress();
        }
        public void RefreshVillageProgress()
        {
            if(restoring||IsDefeated)return;
            var village=VillagePreparationQuest.Instance;if(village==null)return;
            stalls=new bool[village.stalls.Length];stallLevels=new int[stalls.Length];for(int i=0;i<stalls.Length;i++){stalls[i]=village.stalls[i]!=null&&village.stalls[i].Completed;stallLevels[i]=village.stalls[i]!=null?village.stalls[i].CompletedLevels:0;}
        }
        public void Defeat(string title="BẠN ĐÃ GỤC NGÃ",string reason="")
        {
            if(IsDefeated)return;
            recoveryPoint=BachDangAutoSave.Instance?.RetryPoint;
            CampaignProgress.Instance?.RecordDefeat();
            if(!HasCheckpoint)Capture(BachDangStoryManager.Instance.currentState);
            IsDefeated=true;previousTimeScale=Time.timeScale>0?Time.timeScale:1;
            RiverBoardingCombat.Instance?.ResetBattle();CampRecoveryGame.Active?.Close();VillageWorkStall.Active?.Close();BeachBoatMinigameStation.Active?.CancelActivity();StoryDialogueUI.Instance?.DismissForCheckpoint();
            EnemyFleetDirector.Instance?.StopBattle();QuestRowboat.Instance?.LeaveForCheckpoint();
            var body=session.player.GetComponent<Rigidbody>();if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}body.isKinematic=true;
            Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(overlay==null)BuildUI();headingText.text=title;
            checkpointText.text=reason+(recoveryPoint!=null?"\nTiếp tục từ bản lưu gần nhất: "+recoveryPoint.savedAt+"\n"+recoveryPoint.location:"\nCHƠI LẠI TỪ: "+CheckpointName(SavedState));
            retryText.text=recoveryPoint!=null?"TIẾP TỤC TỪ BẢN LƯU  [R]":"CHƠI LẠI CHECKPOINT  [R]";overlay.SetActive(true);
            BachDangStoryManager.Instance?.SetInteractPrompt(false);
        }
        public bool Retry()
        {
            if(!IsDefeated||!HasCheckpoint)return false;
            if(recoveryPoint!=null)
            {
                var saved=recoveryPoint;PrepareForLoad();
                BachDangSaveSystem.Apply(saved);BachDangAutoSave.Instance?.AcceptSavedPoint(saved);
                Time.timeScale=previousTimeScale;
                var orbit=session.storyCamera.GetComponent<Camera_Script>();orbit.CaptureCursor(session.storyMode);
                return true;
            }
            restoring=true;CampDiscipline.Instance?.ResetAll();
            var story=BachDangStoryManager.Instance;var boat=QuestRowboat.Instance;var tide=TideSystem.Instance;
            EnemyFleetDirector.Instance?.ResetForCheckpoint();boat?.LeaveForCheckpoint();
            if(tide!=null){tide.ReleaseTide(BachDangBattleTimeline.Instance);tide.SetTide(false,true);}
            RiverStakeZone.Instance?.RestoreCheckpoint(stakes);
            for(int i=0;i<session.woodItems.Length;i++)if(session.woodItems[i]!=null)session.woodItems[i].RestoreCollection(woods!=null&&i<woods.Length&&woods[i]);
            story.collectedWood=woodCount;story.destroyedEnemyShips=destroyedCount;story.TransitionTo(SavedState,true);
            var village=VillagePreparationQuest.Instance;
            if(village!=null)for(int i=0;i<village.stalls.Length;i++)
                if(village.stalls[i]!=null){village.stalls[i].RestoreLearningProgress(stallLevels!=null&&i<stallLevels.Length?stallLevels[i]:0);if(stalls!=null&&i<stalls.Length&&stalls[i])village.stalls[i].MarkCollected();}
            foreach(var patrol in FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include,FindObjectsSortMode.None))patrol.ResetForCheckpoint();
            if(boat!=null){boat.RestoreDurability(QuestRowboat.MaximumDurability);boat.PrepareForBattle();}
            var player=session.player;var body=player.GetComponent<Rigidbody>();
            PlayerArrival.Teleport(player,position,rotation);body.isKinematic=!session.storyMode;
            player.GetComponent<CapsuleCollider>().enabled=true;player.GetComponent<Character_Movement>().enabled=session.storyMode;
            vitals.RestoreCheckpoint(armor);Physics.SyncTransforms();IsDefeated=false;restoring=false;Time.timeScale=previousTimeScale;
            overlay.SetActive(false);var camera=session.storyCamera.GetComponent<Camera_Script>();camera.SnapToTarget();camera.CaptureCursor(session.storyMode);story.UpdateUI();return true;
        }
        public void PrepareForLoad()
        {
            IsDefeated=false;restoring=false;recoveryPoint=null;if(overlay!=null)overlay.SetActive(false);
        }
        static string CheckpointName(BachDangStoryManager.StoryState state)
        {
            switch(state)
            {
                case BachDangStoryManager.StoryState.Act2_CollectWoodInVillage:return "HỒI 2 — GOM GỖ Ở LÀNG";
                case BachDangStoryManager.StoryState.Act2_SupplyPreparation:return "HỒI 2 — CHUẨN BỊ VẬT TƯ";
                case BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver:return "HỒI 2 — BẾN ĐÓNG CỌC";
                case BachDangStoryManager.StoryState.Act3_WaitForLowTide:return "HỒI 3 — BẾN XUẤT CHIẾN";
                case BachDangStoryManager.StoryState.Act1_Training:return "HỒI 1 — HUẤN LUYỆN TRỐNG";
                case BachDangStoryManager.StoryState.Act3_Victory:return "HỒI 3 — BÁO CÔNG";
                default:return "CHẶNG NHIỆM VỤ TRONG DOANH TRẠI";
            }
        }
        void Update(){if(IsDefeated&&(Input.GetKeyDown(KeyCode.R)||Input.GetKeyDown(KeyCode.Return)))Retry();}
        void BuildUI()
        {
            if(overlay!=null)return;
            var canvas=new GameObject("QuestDefeatCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=500;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            overlay=Panel("Defeated",canvas.transform,Vector2.zero,Vector2.one,new Color(.025f,.02f,.02f,.87f));
            var card=Panel("Card",overlay.transform,new Vector2(.20f,.25f),new Vector2(.80f,.75f),new Color(.055f,.06f,.065f,.99f)).transform;
            headingText=Text("Heading",card,new Vector2(.06f,.69f),new Vector2(.94f,.92f),34,new Color(1,.42f,.3f));headingText.text="BẠN ĐÃ GỤC NGÃ";
            checkpointText=Text("Checkpoint",card,new Vector2(.06f,.32f),new Vector2(.94f,.66f),21,Color.white);
            var button=Panel("RetryCheckpoint",card,new Vector2(.16f,.08f),new Vector2(.84f,.27f),new Color(.24f,.36f,.29f));var retry=button.AddComponent<Button>();retry.targetGraphic=button.GetComponent<Image>();retry.onClick.AddListener(()=>Retry());
            retryText=Text("Label",button.transform,Vector2.zero,Vector2.one,24,new Color(1,.83f,.45f));retryText.text="CHƠI LẠI CHECKPOINT  [R]";overlay.SetActive(false);
        }
        static GameObject Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;go.GetComponent<Image>().color=color;return go;}
        static TMP_Text Text(string name,Transform parent,Vector2 min,Vector2 max,float size,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;var t=go.GetComponent<TextMeshProUGUI>();StoryFont.Apply(t);t.fontSize=size;t.alignment=TextAlignmentOptions.Center;t.color=color;return t;}
        void OnDestroy(){if(IsDefeated)Time.timeScale=previousTimeScale;if(Instance==this)Instance=null;}
    }
}
