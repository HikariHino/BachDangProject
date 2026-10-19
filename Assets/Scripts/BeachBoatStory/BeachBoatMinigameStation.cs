using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Repeatable activities that reuse the existing interaction HUD and character controller.</summary>
    public sealed class BeachBoatMinigameStation : MonoBehaviour
    {
        public enum ActivityKind { WarDrum, SupplyRelay }
        public ActivityKind kind;
        public string displayName = "Trò chơi phụ";
        public float interactDistance = 36;
        public Transform[] checkpoints = new Transform[0];
        public GameObject[] checkpointMarkers = new GameObject[0];
        public static BeachBoatMinigameStation Active { get; private set; }
        public float BestTime { get; private set; }
        public int BestMistakes { get; private set; } = int.MaxValue;
        public bool HasCompleted { get; private set; }
        public int Progress => progress;
        public int Mistakes => mistakes;
        public int Round => round;
        public int RhythmLength => rhythm.Length;
        public int ExpectedBeat => progress < rhythm.Length ? rhythm[progress] : 0;
        public float Elapsed => elapsed;
        public int FeedbackVersion { get; private set; }
        public bool LastInputCorrect { get; private set; }
        public float CurrentTargetDistance => kind == ActivityKind.SupplyRelay && player != null && checkpoints != null &&
            progress >= 0 && progress < checkpoints.Length && checkpoints[progress] != null
                ? QuestRowboat.FlatDistance(player.position, checkpoints[progress].position)
                : 0f;

        readonly int[] rhythm = { 1, 2, 3, 2, 1, 3, 1, 2, 3, 1, 2, 1 };
        BachDangStoryManager story;
        Transform player;
        int progress, mistakes, round;
        float elapsed;
        bool showingPrompt;
        bool restoreCursorLock;

        public int GetRhythmBeat(int index)
        {
            return index >= 0 && index < rhythm.Length ? rhythm[index] : 0;
        }

        void Start()
        {
            story = BachDangStoryManager.Instance;
            player = story != null ? story.GetPlayerTransform() : null;
            if (kind == ActivityKind.SupplyRelay) displayName = "Chuyển cọc và vật tư ra điểm tập kết";
            SetMarkers(-1);
        }

        void Update()
        {
            if (story == null) story = BachDangStoryManager.Instance;
            if (player == null && story != null) player = story.GetPlayerTransform();
            if (story == null || player == null) return;
            if (Active == this) { RunActivity(); return; }
            if (Active != null || !CanOffer()) { HidePrompt(); return; }
            if (QuestRowboat.FlatDistance(player.position, transform.position) > interactDistance) { HidePrompt(); return; }
            string record = BestTime > 0 ? $" • Kỷ lục {BestTime:0.0}s" : "";
            story.SetInteractPrompt(true, $"[E] {displayName}{record}", this);
            showingPrompt = true;
            if (Input.GetKeyDown(KeyCode.E) && StoryDialogueUI.TryConsumeInput()) BeginActivity();
        }

        bool CanOffer()
        {
            var session = BeachBoatQuestSession.Instance;
            bool mandatory = story != null &&
                ((story.currentState == BachDangStoryManager.StoryState.Act1_Training && kind == ActivityKind.WarDrum) ||
                 (story.currentState == BachDangStoryManager.StoryState.Act2_SupplyPreparation && kind == ActivityKind.SupplyRelay));
            bool wrongMandatoryStation = story != null &&
                ((story.currentState == BachDangStoryManager.StoryState.Act1_Training && kind != ActivityKind.WarDrum) ||
                 (story.currentState == BachDangStoryManager.StoryState.Act2_SupplyPreparation && kind != ActivityKind.SupplyRelay));
            return !wrongMandatoryStation && isActiveAndEnabled && session != null && session.storyMode && !OpeningEvent.IsOpeningActive &&
                !BeachBoatQuestSession.InteractionBlocked && (QuestRowboat.Instance == null || !QuestRowboat.Instance.IsAboard) &&
                story.currentState != BachDangStoryManager.StoryState.Act3_BattleInProgress &&
                (mandatory || story.currentState != BachDangStoryManager.StoryState.Act1_Training &&
                 story.currentState != BachDangStoryManager.StoryState.Act2_SupplyPreparation);
        }

        public void BeginActivity()
        {
            if (Active != null || !CanOffer() || QuestRowboat.FlatDistance(player.position, transform.position) > interactDistance) return;
            Active = this; progress = mistakes = round = 0; elapsed = 0;
            FeedbackVersion = 0;
            HidePrompt();
            SetMarkers(kind == ActivityKind.SupplyRelay ? 0 : -1);
            restoreCursorLock = Cursor.lockState == CursorLockMode.Locked;
            if (kind == ActivityKind.WarDrum)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void RunActivity()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { Finish(false); return; }
            elapsed += Time.deltaTime;
            if (kind == ActivityKind.WarDrum) RunDrum(); else RunSupply();
        }

        void RunDrum()
        {
            int expected = rhythm[progress];
            if (BeachBoatMinigameUI.Instance == null)
                story.SetInteractPrompt(true,
                    $"TRỐNG TRẬN  Lượt {round + 1}/2 • Nhịp {progress}/{rhythm.Length}\nBấm [{expected}] theo hiệu lệnh • Sai {mistakes} • Esc thoát", this);
            int pressed = Input.GetKeyDown(KeyCode.Alpha1) ? 1 : Input.GetKeyDown(KeyCode.Alpha2) ? 2 : Input.GetKeyDown(KeyCode.Alpha3) ? 3 : 0;
            if (pressed != 0) SubmitRhythm(pressed);
        }

        public bool SubmitRhythm(int pressed)
        {
            if (Active != this || kind != ActivityKind.WarDrum || progress >= rhythm.Length || pressed < 1 || pressed > 3) return false;
            bool correct = pressed == rhythm[progress];
            if (correct) progress++; else { mistakes++; progress = Mathf.Max(0, progress - 1); }
            LastInputCorrect = correct;
            FeedbackVersion++;
            if (progress >= rhythm.Length)
            {
                round++;
                if (round >= 2) Finish(true); else progress = 0;
            }
            return correct;
        }

        void RunSupply()
        {
            if (checkpoints == null || checkpoints.Length == 0) { Finish(false); return; }
            var target = checkpoints[progress];
            float distance = QuestRowboat.FlatDistance(player.position, target.position);
            if (BeachBoatMinigameUI.Instance == null)
                story.SetInteractPrompt(true,
                    $"TIẾP TẾ DOANH TRẠI  {progress}/{checkpoints.Length}\nTới điểm sáng: {Mathf.RoundToInt(distance)} m • Đến gần bấm [E] • Esc thoát", this);
            if (distance > interactDistance || !Input.GetKeyDown(KeyCode.E) || !StoryDialogueUI.TryConsumeInput()) return;
            TryDeliver();
        }

        public bool TryDeliver()
        {
            if (Active != this || kind != ActivityKind.SupplyRelay || checkpoints == null || progress >= checkpoints.Length) return false;
            if (QuestRowboat.FlatDistance(player.position, checkpoints[progress].position) > interactDistance) return false;
            progress++;
            if (progress >= checkpoints.Length) Finish(true); else SetMarkers(progress);
            return true;
        }

        void Finish(bool completed)
        {
            HidePrompt(); SetMarkers(-1); Active = null;
            RestoreCursor();
            if (!completed) return;
            HasCompleted = true;
            if (BestTime <= 0 || elapsed < BestTime) BestTime = elapsed;
            BestMistakes = Mathf.Min(BestMistakes, mistakes);
            string result = kind == ActivityKind.WarDrum
                ? $"Hoàn thành nhịp trống trong {elapsed:0.0} giây, sai {mistakes} lần."
                : $"Chuyển đủ {checkpoints.Length} chuyến tiếp tế trong {elapsed:0.0} giây.";
            System.Action advance = null;
            if (story != null) advance = kind == ActivityKind.WarDrum
                ? (System.Action)story.OnTrainingComplete
                : story.OnSupplyPreparationComplete;
            if (StoryDialogueUI.Instance != null) StoryDialogueUI.Instance.Show("Luyện quân", result, advance);
            else advance?.Invoke();
        }

        void SetMarkers(int active)
        {
            if (checkpointMarkers == null) return;
            for (int i = 0; i < checkpointMarkers.Length; i++)
                if (checkpointMarkers[i] != null) checkpointMarkers[i].SetActive(i == active);
        }

        void OnDisable()
        {
            if (Active == this) { HidePrompt(); Active = null; RestoreCursor(); }
            SetMarkers(-1);
        }

        void RestoreCursor()
        {
            if (kind != ActivityKind.WarDrum || !restoreCursorLock) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            restoreCursorLock = false;
        }

        void HidePrompt()
        {
            if (story != null && (showingPrompt || Active == this)) story.SetInteractPrompt(false, owner: this);
            showingPrompt = false;
        }
    }
}
