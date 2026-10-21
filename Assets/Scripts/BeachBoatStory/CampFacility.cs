using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Lightweight camp services, with barracks recovery at the sleeping mats inside.</summary>
    [DisallowMultipleComponent]
    public sealed class CampFacility : MonoBehaviour
    {
        public enum FacilityKind { Barracks, Forge, Armory, Provisions, Medical }

        public FacilityKind kind;
        public string displayName;
        public float interactDistance = 24f;
        public float cooldownSeconds = 45f;

        static readonly float[] LastUse = new float[5];
        bool showingPrompt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCooldowns()
        {
            for (int i = 0; i < LastUse.Length; i++) LastUse[i] = float.NegativeInfinity;
        }

        void Update()
        {
            var session = BeachBoatQuestSession.Instance;
            var story = session != null ? session.GetComponent<BachDangStoryManager>() : null;
            if (session == null || story == null || session.player == null || !CanOffer(session, story))
            { HidePrompt(); return; }
            if (QuestRowboat.FlatDistance(session.player.transform.position, transform.position) > interactDistance)
            { HidePrompt(); return; }

            bool ready = Time.unscaledTime - LastUse[(int)kind] >= cooldownSeconds;
            story.SetInteractPrompt(true, ready ? ActionLabel() : displayName + " • đang chuẩn bị lại", this);
            showingPrompt = true;
            if (ready && Input.GetKeyDown(KeyCode.E) && StoryDialogueUI.TryConsumeInput()) TryUse();
        }

        bool CanOffer(BeachBoatQuestSession session, BachDangStoryManager story)
        {
            return isActiveAndEnabled && session.storyMode && !BeachBoatQuestSession.InteractionBlocked &&
                   BeachBoatMinigameStation.Active == null &&
                   (QuestRowboat.Instance == null || !QuestRowboat.Instance.IsAboard) &&
                   story.currentState != BachDangStoryManager.StoryState.Act3_BattleInProgress;
        }

        string ActionLabel()
        {
            switch (kind)
            {
                case FacilityKind.Barracks: return "Nghỉ tại " + displayName;
                case FacilityKind.Forge: return "Gia cố giáp tại " + displayName;
                case FacilityKind.Armory: return "Luyện thể lực tại " + displayName;
                case FacilityKind.Medical: return "Băng bó tại " + displayName;
                default: return "Lấy lương thực tại " + displayName;
            }
        }

        public bool TryUse()
        {
            var session = BeachBoatQuestSession.Instance;
            var story = session != null ? session.GetComponent<BachDangStoryManager>() : null;
            if (session == null || story == null || session.player == null || !CanOffer(session, story) ||
                QuestRowboat.FlatDistance(session.player.transform.position, transform.position) > interactDistance ||
                Time.unscaledTime - LastUse[(int)kind] < cooldownSeconds) return false;
            var vitals = session.player.GetComponent<PlayerVitals>();
            if (vitals == null) return false;
            string message;
            switch (kind)
            {
                case FacilityKind.Barracks:
                    vitals.RestAtBarracks();
                    message = "Bạn nghỉ một lát cùng quân sĩ: hồi 35 máu và đầy thể lực.";
                    break;
                case FacilityKind.Forge:
                    vitals.ReinforceArmor();
                    message = "Thợ rèn gia cố giáp. Ba lần va chạm tiếp theo giảm 30% sát thương.";
                    break;
                case FacilityKind.Armory:
                    vitals.TrainAtArmory();
                    message = "Bạn tập luyện với quân sĩ. Thể lực tối đa tăng lên 120 trong lượt chơi này.";
                    break;
                case FacilityKind.Medical:
                    vitals.TreatAtMedicalTent();
                    message = "Quân y đã băng bó vết thương. Máu được hồi đầy; bạn có thể tiếp tục nhiệm vụ.";
                    break;
                default:
                    vitals.TakeProvisions();
                    message = "Bạn nhận lương khô và nước: hồi 25 máu, 35 thể lực.";
                    break;
            }
            LastUse[(int)kind] = Time.unscaledTime;
            HidePrompt();
            StoryDialogueUI.Instance?.Show(displayName, message);
            return true;
        }

        void HidePrompt()
        {
            if (!showingPrompt) return;
            showingPrompt = false;
            BeachBoatQuestSession.Instance?.GetComponent<BachDangStoryManager>()?.SetInteractPrompt(false, owner: this);
        }

        void OnDisable() { HidePrompt(); }
    }
}
