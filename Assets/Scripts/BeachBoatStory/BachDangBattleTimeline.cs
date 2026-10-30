using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>
    /// Event-driven reconstruction clock. Historical sources describe the order of events,
    /// but not an exact clock time, so the HUD labels every hour as a reconstructed milestone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BachDangBattleTimeline : MonoBehaviour
    {
        public enum BattleMoment { Preparation, LureAtHighTide, FleetInsideStakeField, TideFalling, Ambush, Victory }
        [Min(2)] public float battleEbbDuration = 4f;

        public static BachDangBattleTimeline Instance { get; private set; }
        public BattleMoment CurrentMoment { get; private set; }
        public float DisplayHour { get; private set; } = 5.75f;
        public string EventLabel { get; private set; } = "Bình minh — chuẩn bị kháng chiến";
        public string TideLabel { get; private set; } = "Triều theo nhiệm vụ";

        DayNightCycle cycle;
        float targetHour = 5.75f;
        GUIStyle boxStyle, headingStyle, bodyStyle;
        Texture2D panelTexture;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            cycle = FindAnyObjectByType<DayNightCycle>();
            if (cycle != null) cycle.autoAdvance = false;
            var story = GetComponent<BachDangStoryManager>();
            ApplyStoryState(story != null ? story.currentState : BachDangStoryManager.StoryState.Act1_ExploreCamp, true);
        }

        void Update()
        {
            DisplayHour = Mathf.MoveTowards(DisplayHour, targetHour, Time.deltaTime * .45f);
            if (cycle != null)
            {
                cycle.autoAdvance = false;
                cycle.SetTimeOfDay(DisplayHour);
            }
        }

        public void ApplyStoryState(BachDangStoryManager.StoryState state, bool immediate = false)
        {
            switch (state)
            {
                case BachDangStoryManager.StoryState.Act1_ExploreCamp:
                case BachDangStoryManager.StoryState.Act1_GoToCampGate:
                case BachDangStoryManager.StoryState.Act1_Training:
                    SetMoment(BattleMoment.Preparation, 5.75f, "Bình minh — Ngô Quyền chuẩn bị trận địa", "Triều đang rút", immediate);
                    SetFreeTide(false, immediate); break;
                case BachDangStoryManager.StoryState.Act2_CollectWoodInVillage:
                case BachDangStoryManager.StoryState.Act2_ReturnToCampGate:
                case BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen:
                case BachDangStoryManager.StoryState.Act2_SupplyPreparation:
                    SetMoment(BattleMoment.Preparation, 7.25f, "Dân binh chuẩn bị cọc gỗ", "Triều thấp", immediate);
                    SetFreeTide(false, immediate); break;
                case BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver:
                    SetMoment(BattleMoment.Preparation, 8f, "Đóng cọc ngầm ở lòng sông", "Triều rút — bãi sông lộ", immediate);
                    SetFreeTide(false, immediate);
                    break;
                case BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp:
                    SetMoment(BattleMoment.Preparation, 8.25f, "Trận địa đã sẵn sàng — trở về báo cáo", "TRIỀU DÂNG — nước phủ cọc, đường về bến thông thoáng", immediate);
                    HoldHighWater(immediate);
                    break;
                case BachDangStoryManager.StoryState.Act2_Completed:
                case BachDangStoryManager.StoryState.Act3_PrepareForBattle:
                    SetMoment(BattleMoment.Preparation, 8.5f, "Quân mai phục vào vị trí hai bờ", "Triều bắt đầu dâng", immediate);
                    HoldHighWater(immediate); break;
                case BachDangStoryManager.StoryState.Act3_WaitForLowTide:
                    BeginLure(immediate); break;
                case BachDangStoryManager.StoryState.Act3_SearchPrisoners:
                case BachDangStoryManager.StoryState.Act3_CommanderDuel:
                case BachDangStoryManager.StoryState.Act3_FinalReport:
                case BachDangStoryManager.StoryState.Act3_Victory:
                case BachDangStoryManager.StoryState.Act3_Celebration:
                    MarkVictory(); break;
            }
        }

        public void BeginLure(bool immediate = false)
        {
            SetMoment(BattleMoment.LureAtHighTide, 9f, "Thuyền nhẹ ra cửa sông nhử địch", "TRIỀU CAO — cọc chìm dưới nước", immediate);
            HoldHighWater(immediate);
        }

        public void MarkFleetInside()
        {
            SetMoment(BattleMoment.FleetInsideStakeField, 9.5f, "Hạm đội Nam Hán lọt vào bãi cọc", "Triều đứng — chuẩn bị đổi dòng");
        }

        public void BeginEbb()
        {
            SetMoment(BattleMoment.TideFalling, 10f, "Đội hình địch đã vào sâu trận địa", "TRIỀU RÚT DẦN — quân ta bắt đầu áp sát");
            TideSystem.Instance?.BeginEbb(this, Mathf.Clamp(battleEbbDuration,2,4));
        }

        public void BeginApproachEbb(float coveredHeight)
        {
            SetMoment(BattleMoment.TideFalling,9.75f,"Hạm đội đang lọt vào bãi cọc — nước bắt đầu rút","TRIỀU ĐANG RÚT — giữ thuyền nhử ngoài trận địa");
            TideSystem.Instance?.BeginEbbTo(this,4,coveredHeight);
        }

        public void BeginAmbush()
        {
            SetMoment(BattleMoment.Ambush, 10.67f, "Cọc đã lộ — quân hai bờ tự động phản công", "NƯỚC TIẾP TỤC RÚT — chiến thuyền mắc cọc");
        }

        public void MarkVictory()
        {
            SetMoment(BattleMoment.Victory, 11.5f, "Đại thắng Bạch Đằng — trở về báo công", "TRIỀU DÂNG — cọc ngập, xác thuyền địch còn tại trận địa");
            HoldHighWater();
        }

        void HoldHighWater(bool immediate=false)
        {
            var tide=TideSystem.Instance;if(tide==null)return;
            // The planting boat owns low water until the final work cluster finishes.
            // Release that owner before reserving the return journey's high water.
            tide.ReleaseTide(QuestRowboat.Instance);tide.HoldHighTide(this,immediate);
        }
        public void ReconcileAfterLoad(BachDangStoryManager.StoryState state)
        {
            switch(state)
            {
                case BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp:
                case BachDangStoryManager.StoryState.Act2_Completed:
                case BachDangStoryManager.StoryState.Act3_PrepareForBattle:
                case BachDangStoryManager.StoryState.Act3_SearchPrisoners:
                case BachDangStoryManager.StoryState.Act3_CommanderDuel:
                case BachDangStoryManager.StoryState.Act3_FinalReport:
                case BachDangStoryManager.StoryState.Act3_Victory:
                case BachDangStoryManager.StoryState.Act3_Celebration:
                    // Keep the saved height; correct old low-water targets and retain
                    // a new save's in-progress rise without jumping to full flood.
                    ApplyStoryState(state,false);break;
            }
        }

        void SetMoment(BattleMoment moment, float hour, string eventLabel, string tideLabel, bool immediate = false)
        {
            CurrentMoment = moment;
            targetHour = hour;
            if (immediate) DisplayHour = hour;
            EventLabel = eventLabel;
            TideLabel = tideLabel;
        }
        public void RestoreSave(BachDangSaveData.Tide data)
        {
            switch((BattleMoment)data.moment)
            {
                case BattleMoment.LureAtHighTide:BeginLure();break;
                case BattleMoment.FleetInsideStakeField:MarkFleetInside();break;
                case BattleMoment.TideFalling:
                    if(data.target>(TideSystem.Instance?.lowTideY??0)+.01f)BeginApproachEbb(data.target);else BeginEbb();
                    break;
                case BattleMoment.Ambush:BeginAmbush();break;
                case BattleMoment.Victory:MarkVictory();break;
                default:ApplyStoryState(BachDangStoryManager.Instance.currentState,true);break;
            }
            DisplayHour=data.hour;if(cycle!=null)cycle.SetTimeOfDay(DisplayHour);
        }

        void SetFreeTide(bool high, bool immediate)
        {
            var tide = TideSystem.Instance;
            if(tide==null)return;
            tide.ReleaseTide(this);tide.ReleaseTide(QuestRowboat.Instance);
            if(!tide.IsTideLocked)tide.SetTide(high,immediate);
        }

        void OnGUI()
        {
            if (RiverInvasionIntro.IsRunning || BachDangFrontEnd.IsBlocking || OpeningEvent.IsOpeningActive || BeachBoatMinigameStation.Active != null || VillageWorkStall.Active != null ||
                (QuestCheckpoint.Instance != null && QuestCheckpoint.Instance.IsDefeated) ||
                (StoryDialogueUI.Instance != null && StoryDialogueUI.Instance.IsOpen) ||
                (BeachBoatQuestSession.Instance != null && !BeachBoatQuestSession.Instance.storyMode)) return;
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 720f, .8f, 1.35f);
            var rect = new Rect(18f, 18f, 390f * scale, 112f * scale);
            GUI.Box(rect, GUIContent.none, boxStyle);
            var inner = new Rect(rect.x + 14f * scale, rect.y + 9f * scale, rect.width - 28f * scale, rect.height - 16f * scale);
            int minutes = Mathf.RoundToInt(DisplayHour * 60f) % (24 * 60);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 29f * scale),
                $"CUỐI NĂM MẬU TUẤT 938  •  {minutes / 60:00}:{minutes % 60:00}", headingStyle);
            GUI.Label(new Rect(inner.x, inner.y + 32f * scale, inner.width, 24f * scale), "MỐC GIỜ TÁI HIỆN", bodyStyle);
            GUI.Label(new Rect(inner.x, inner.y + 56f * scale, inner.width, 38f * scale), EventLabel + "\n" + TideLabel, bodyStyle);
        }

        void EnsureStyles()
        {
            if (boxStyle != null) return;
            panelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            panelTexture.SetPixel(0, 0, new Color(.025f, .035f, .045f, .9f)); panelTexture.Apply();
            boxStyle = new GUIStyle(GUI.skin.box); boxStyle.normal.background = panelTexture;
            headingStyle = new GUIStyle(GUI.skin.label) { font = StoryFont.LegacyFont, fontSize = 20, fontStyle = FontStyle.Bold };
            headingStyle.normal.textColor = new Color(1f, .82f, .34f);
            bodyStyle = new GUIStyle(GUI.skin.label) { font = StoryFont.LegacyFont, fontSize = 16, wordWrap = true };
            bodyStyle.normal.textColor = Color.white;
        }

        void OnDestroy()
        {
            TideSystem.Instance?.ReleaseTide(this);
            if (panelTexture != null) Destroy(panelTexture);
            if (Instance == this) Instance = null;
        }
    }
}
