using TMPro;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>One lightweight HUD guide; references are cached once, never a scene scan per frame.</summary>
    public sealed class StoryRouteGuide : MonoBehaviour
    {
        BeachBoatQuestSession session;
        BachDangStoryManager story;
        NPCQuestGiver commander;
        RiverStakeZone stakes;
        CampGateTransition[] gates;
        GameObject panel;
        TMP_Text caption, arrow;
        Transform target;
        float nextRefresh;

        public void Initialize(BeachBoatQuestSession owner, GameObject box, TMP_Text text, TMP_Text pointer)
        {
            session = owner; story = owner.GetComponent<BachDangStoryManager>();
            commander = owner.GetComponentInChildren<NPCQuestGiver>(true);
            stakes = owner.GetComponentInChildren<RiverStakeZone>(true);
            gates = owner.GetComponentsInChildren<CampGateTransition>(true);
            panel = box; caption = text; arrow = pointer;
        }

        public Transform CurrentTarget => target;

        // River travel shares quest states with work/reporting. Choose the local end of that leg.
        public Transform ResolveTarget(out string title)
        {
            title = null;
            if (story == null || session.player == null) return null;
            var state = story.currentState;
            if (state == BachDangStoryManager.StoryState.Act2_Completed) return null;
            Transform best = null; float distance = float.PositiveInfinity;
            foreach (var gate in gates)
                if (gate != null && gate.isActiveAndEnabled && gate.destinationTarget != null && gate.IsAvailable(state))
                    Consider(gate.transform, gate.gateType == CampGateTransition.GateType.CampGate ? "Cổng sang làng ven sông" :
                        gate.gateType == CampGateTransition.GateType.CampRiverGate ? "Cổng ra bãi cọc" : "Cổng về doanh trại",
                        ref best, ref distance, ref title);
            if (state == BachDangStoryManager.StoryState.Act1_ExploreCamp ||
                state == BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen ||
                state == BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp)
                if (commander != null) Consider(commander.transform, "Chủ tướng Ngô Quyền", ref best, ref distance, ref title);
            if (state == BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver && stakes != null)
                Consider(stakes.transform, "Bãi cọc Bạch Đằng", ref best, ref distance, ref title);
            if (state == BachDangStoryManager.StoryState.Act2_CollectWoodInVillage)
                foreach (var wood in session.woodItems)
                    if (wood != null && wood.isActiveAndEnabled)
                        Consider(wood.transform, "Cọc gỗ trong làng", ref best, ref distance, ref title);
            return best;
        }

        void Consider(Transform candidate, string name, ref Transform best, ref float distance, ref string title)
        {
            var offset = candidate.position - session.player.transform.position; offset.y = 0;
            if (offset.sqrMagnitude >= distance) return;
            best = candidate; distance = offset.sqrMagnitude; title = name;
        }

        void LateUpdate()
        {
            if (panel == null) return;
            if (BeachBoatQuestSession.InteractionBlocked) { panel.SetActive(false); nextRefresh = 0; return; }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .2f;
                target = ResolveTarget(out var title);
                panel.SetActive(target != null);
                if (target != null)
                {
                    var offset = target.position - session.player.transform.position; offset.y = 0;
                    caption.text = title + "\n" + Mathf.RoundToInt(offset.magnitude) + " m • Đến gần để bấm E";
                }
            }
            if (target == null || session.storyCamera == null) return;
            var direction = target.position - session.player.transform.position; direction.y = 0;
            var forward = session.storyCamera.transform.forward; forward.y = 0;
            float angle = Vector3.SignedAngle(forward, direction, Vector3.up);
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, 90 - angle);
        }
    }
}
