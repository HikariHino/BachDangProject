using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Timed camp activities; the story advances only after a completed attempt.</summary>
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
        public float TimeLimit => kind == ActivityKind.WarDrum ? 38f : 105f;
        public float TimeRemaining => Mathf.Max(0f, TimeLimit - elapsed);
        public bool Failed { get; private set; }
        public bool IsSelectingCargo => kind == ActivityKind.SupplyRelay && !Failed && selectedCargo == 0 && !returningToWarehouse;
        public bool ReturningToWarehouse => returningToWarehouse;
        public int RequestedCargo => requestedCargo;
        public int SelectedCargo => selectedCargo;
        public static string CargoName(int cargo) => cargo == 1 ? "CỌC GỖ" : cargo == 2 ? "LƯƠNG KHÔ" : "DÂY THỪNG";
        public int FeedbackVersion { get; private set; }
        public bool LastInputCorrect { get; private set; }
        public float CurrentTargetDistance => kind == ActivityKind.SupplyRelay && player != null && returningToWarehouse
            ? QuestRowboat.FlatDistance(player.position, transform.position) :
            kind == ActivityKind.SupplyRelay && player != null && checkpoints != null &&
            progress >= 0 && progress < checkpoints.Length && checkpoints[progress] != null
                ? QuestRowboat.FlatDistance(player.position, checkpoints[progress].position) : 0f;

        readonly int[] rhythm = new int[12];
        BachDangStoryManager story;
        Transform player;
        int progress, mistakes, round, requestedCargo, selectedCargo;
        float elapsed;
        bool showingPrompt, restoreCursorLock, returningToWarehouse;

        public int GetRhythmBeat(int index) => index >= 0 && index < rhythm.Length ? rhythm[index] : 0;

        void Start()
        {
            story = BachDangStoryManager.Instance;
            player = story != null ? story.GetPlayerTransform() : null;
            if (kind == ActivityKind.SupplyRelay) displayName = "Chuẩn bị vật tư cho doanh trại";
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
            if (Active != null || player == null || !CanOffer() ||
                QuestRowboat.FlatDistance(player.position, transform.position) > interactDistance) return;
            Active = this;
            restoreCursorLock = Cursor.lockState == CursorLockMode.Locked;
            HidePrompt();
            ResetRound();
        }

        void ResetRound()
        {
            progress = mistakes = round = 0;
            elapsed = 0f;
            Failed = false;
            FeedbackVersion = 0;
            selectedCargo = 0;
            returningToWarehouse = false;
            if (kind == ActivityKind.WarDrum) ShuffleRhythm();
            else requestedCargo = Random.Range(1, 4);
            SetMarkers(-1);
            SetCursorFree(true);
        }

        void ShuffleRhythm()
        {
            for (int i = 0; i < rhythm.Length; i++)
            {
                rhythm[i] = Random.Range(1, 4);
                if (i >= 2 && rhythm[i] == rhythm[i - 1] && rhythm[i] == rhythm[i - 2])
                    rhythm[i] = rhythm[i] % 3 + 1;
            }
        }

        void RunActivity()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { Finish(false); return; }
            if (Failed) { if (Input.GetKeyDown(KeyCode.R)) RetryActivity(); return; }
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= TimeLimit) { FailActivity(); return; }
            if (kind == ActivityKind.WarDrum) RunDrum(); else RunSupply();
        }

        void RunDrum()
        {
            if (BeachBoatMinigameUI.Instance == null)
                story.SetInteractPrompt(true, $"TRỐNG TRẬN • Lượt {round + 1}/2 • Còn {TimeRemaining:0}s\nNhịp cần đánh: {ExpectedBeat} • Sai {mistakes}", this);
            int pressed = Input.GetKeyDown(KeyCode.Alpha1) ? 1 : Input.GetKeyDown(KeyCode.Alpha2) ? 2 : Input.GetKeyDown(KeyCode.Alpha3) ? 3 : 0;
            if (pressed != 0) SubmitRhythm(pressed);
        }

        public bool SubmitRhythm(int pressed)
        {
            if (Active != this || Failed || kind != ActivityKind.WarDrum || progress >= rhythm.Length || pressed < 1 || pressed > 3) return false;
            bool correct = pressed == rhythm[progress];
            if (correct) progress++;
            else { mistakes++; elapsed += 2f; progress = Mathf.Max(0, progress - 1); }
            LastInputCorrect = correct;
            FeedbackVersion++;
            if (elapsed >= TimeLimit) { FailActivity(); return false; }
            if (progress >= rhythm.Length)
            {
                round++;
                if (round >= 2) Finish(true);
                else { progress = 0; ShuffleRhythm(); }
            }
            return correct;
        }

        void RunSupply()
        {
            if (checkpoints == null || checkpoints.Length == 0) { Finish(false); return; }
            if (selectedCargo == 0)
            {
                if (returningToWarehouse)
                {
                    if (CurrentTargetDistance <= interactDistance)
                    {
                        returningToWarehouse = false;
                        SetCursorFree(true);
                    }
                    return;
                }
                int pressed = Input.GetKeyDown(KeyCode.Alpha1) ? 1 : Input.GetKeyDown(KeyCode.Alpha2) ? 2 : Input.GetKeyDown(KeyCode.Alpha3) ? 3 : 0;
                if (pressed != 0) ChooseCargo(pressed);
                return;
            }
            if (BeachBoatMinigameUI.Instance == null)
                story.SetInteractPrompt(true, $"Giao {CargoName(selectedCargo)} tới điểm sáng • {CurrentTargetDistance:0} m • Còn {TimeRemaining:0}s", this);
            if (CurrentTargetDistance <= interactDistance && Input.GetKeyDown(KeyCode.E) && StoryDialogueUI.TryConsumeInput()) TryDeliver();
        }

        public bool ChooseCargo(int cargo)
        {
            if (Active != this || Failed || kind != ActivityKind.SupplyRelay || !IsSelectingCargo || cargo < 1 || cargo > 3) return false;
            bool correct = cargo == requestedCargo;
            LastInputCorrect = correct;
            FeedbackVersion++;
            if (!correct)
            {
                mistakes++;
                elapsed += 8f;
                if (elapsed >= TimeLimit) FailActivity();
                return false;
            }
            selectedCargo = cargo;
            SetMarkers(progress);
            SetCursorFree(false);
            return true;
        }

        public bool TryDeliver()
        {
            if (Active != this || Failed || kind != ActivityKind.SupplyRelay || selectedCargo == 0 ||
                checkpoints == null || progress >= checkpoints.Length || checkpoints[progress] == null ||
                QuestRowboat.FlatDistance(player.position, checkpoints[progress].position) > interactDistance) return false;
            progress++;
            if (progress >= checkpoints.Length) Finish(true);
            else
            {
                selectedCargo = 0;
                returningToWarehouse = true;
                requestedCargo = Random.Range(1, 4);
                SetMarkers(-1);
            }
            return true;
        }

        void FailActivity()
        {
            Failed = true;
            SetMarkers(-1);
            SetCursorFree(true);
            HidePrompt();
        }

        public void RetryActivity()
        {
            if (Active != this || !Failed) return;
            if (kind == ActivityKind.SupplyRelay && player != null &&
                QuestRowboat.FlatDistance(player.position, transform.position) > interactDistance)
            {
                Vector3 restart = transform.position + Vector3.right * Mathf.Min(12f, interactDistance * .4f);
                player.position = restart;
                var body = player.GetComponent<Rigidbody>();
                if (body != null) { body.position = restart; body.linearVelocity = Vector3.zero; }
                Physics.SyncTransforms();
            }
            ResetRound();
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
                ? $"Hoàn thành hiệu lệnh trống trong {elapsed:0.0} giây, sai {mistakes} lần."
                : $"Chọn đúng vật tư và giao đủ {checkpoints.Length} chuyến trong {elapsed:0.0} giây.";
            System.Action advance = null;
            if (story != null) advance = kind == ActivityKind.WarDrum
                ? (System.Action)story.OnTrainingComplete : story.OnSupplyPreparationComplete;
            if (StoryDialogueUI.Instance != null) StoryDialogueUI.Instance.Show("Luyện quân", result, advance);
            else advance?.Invoke();
        }
        public void CancelActivity(){if(Active==this)Finish(false);}

        void SetMarkers(int active)
        {
            if (checkpointMarkers == null) return;
            for (int i = 0; i < checkpointMarkers.Length; i++)
                if (checkpointMarkers[i] != null)
                {
                    var sign = checkpointMarkers[i].GetComponent<SupplyDestinationSign>();
                    if (sign != null && i == active) sign.SetCargo(CargoName(selectedCargo), i + 1);
                    checkpointMarkers[i].SetActive(i == active);
                }
        }

        void SetCursorFree(bool free)
        {
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        void RestoreCursor()
        {
            if (restoreCursorLock) SetCursorFree(false);
            restoreCursorLock = false;
        }

        void OnDisable()
        {
            if (Active == this) { HidePrompt(); Active = null; RestoreCursor(); }
            SetMarkers(-1);
        }

        void HidePrompt()
        {
            if (story != null && (showingPrompt || Active == this)) story.SetInteractPrompt(false, owner: this);
            showingPrompt = false;
        }
    }
}
