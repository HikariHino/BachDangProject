using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Five authored work positions; each collected stake is planted once.</summary>
    public sealed class RiverStakeZone : MonoBehaviour
    {
        [Serializable]
        public sealed class Placement
        {
            public Transform approach;
            public GameObject marker;
            public Transform stake;
            [NonSerialized] public bool planted;
            [NonSerialized] public Vector3 finalPosition;
            [NonSerialized] public TMP_Text label;
        }

        public static RiverStakeZone Instance { get; private set; }
        [Min(1)] public float interactDistance = 24f;
        [Min(.1f)] public float plantingDuration = 1.4f;
        public Placement[] placements = new Placement[0];
        public int PlantedCount { get; private set; }
        public int RequiredCount => placements == null ? 0 : placements.Length;
        public bool IsComplete => RequiredCount > 0 && PlantedCount == RequiredCount;
        public bool IsPlanting { get; private set; }
        int workingIndex = -1;
        Coroutine planting;

        void Awake()
        {
            Instance = this;
            foreach (var point in placements)
            {
                point.planted = false;
                if (point.stake != null)
                {
                    point.finalPosition = point.stake.position;
                    point.stake.gameObject.SetActive(false);
                }
                if (point.marker != null)
                {
                    point.label = point.marker.GetComponentInChildren<TMP_Text>(true);
                    point.marker.SetActive(false);
                }
            }
        }

        public int NearestUnplanted(Vector3 position)
        {
            int best = -1; float distance = float.PositiveInfinity;
            for (int i = 0; i < RequiredCount; i++)
            {
                var p = placements[i];
                if (p == null || p.planted || p.approach == null || p.stake == null) continue;
                float d = (position - p.approach.position).sqrMagnitude;
                if (d < distance) { distance = d; best = i; }
            }
            return best;
        }

        public bool TryPlant(int index)
        {
            var story = BachDangStoryManager.Instance;
            if (!isActiveAndEnabled || BeachBoatQuestSession.InteractionBlocked || IsPlanting ||
                story == null || story.currentState != BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver ||
                RequiredCount == 0 || story.collectedWood < RequiredCount || index < 0 || index >= RequiredCount) return false;
            var p = placements[index]; var player = story.GetPlayerTransform();
            if (p == null || p.planted || p.approach == null || p.stake == null || player == null ||
                !player.gameObject.activeInHierarchy || Vector3.Distance(player.position, p.approach.position) > interactDistance) return false;
            workingIndex = index; IsPlanting = true;
            story.SetInteractPrompt(false, owner: this);
            story.UpdateUI();
            planting = StartCoroutine(Plant(index));
            return true;
        }

        IEnumerator Plant(int index)
        {
            var p = placements[index];
            p.stake.gameObject.SetActive(true);
            Vector3 raised = p.finalPosition + Vector3.up * 12f;
            float elapsed = 0;
            while (elapsed < plantingDuration)
            {
                elapsed += Time.deltaTime;
                float amount = Mathf.Clamp01(elapsed / plantingDuration);
                p.stake.position = Vector3.Lerp(raised, p.finalPosition, Mathf.SmoothStep(0, 1, amount));
                yield return null;
            }
            p.stake.position = p.finalPosition;
            p.planted = true; PlantedCount++;
            workingIndex = -1; IsPlanting = false; planting = null;
            var story = BachDangStoryManager.Instance;
            if (story != null)
            {
                if (IsComplete) story.OnPlantStakesComplete();
                else story.UpdateUI();
            }
        }

        void Update()
        {
            var story = BachDangStoryManager.Instance;
            bool active = story != null && story.currentState == BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver &&
                !BeachBoatQuestSession.InteractionBlocked;
            var camera = BeachBoatQuestSession.Instance != null ? BeachBoatQuestSession.Instance.storyCamera : null;
            foreach (var p in placements)
            {
                if (p == null || p.marker == null) continue;
                bool show = active && !p.planted;
                if (p.marker.activeSelf != show) p.marker.SetActive(show);
                if (show && p.label != null && camera != null)
                    p.label.transform.rotation = Quaternion.LookRotation(p.label.transform.position - camera.transform.position);
            }
            if (!active) return;
            var player = story.GetPlayerTransform();
            if (player == null) return;
            int index = NearestUnplanted(player.position);
            if (index < 0 || Vector3.Distance(player.position, placements[index].approach.position) > interactDistance) return;
            story.SetInteractPrompt(true, $"[E] Đóng cọc tại vị trí {index + 1} • {PlantedCount}/{RequiredCount}", this);
            if (!StoryDialogueUI.InputConsumedThisFrame && Input.GetKeyDown(KeyCode.E) && StoryDialogueUI.TryConsumeInput())
                TryPlant(index);
        }

        void OnDisable()
        {
            if (planting != null) StopCoroutine(planting);
            if (workingIndex >= 0 && workingIndex < RequiredCount)
            {
                var p = placements[workingIndex];
                if (p.stake != null) { p.stake.position = p.finalPosition; p.stake.gameObject.SetActive(false); }
            }
            planting = null; workingIndex = -1; IsPlanting = false;
            foreach (var p in placements) if (p?.marker != null) p.marker.SetActive(false);
            if (BachDangStoryManager.Instance != null) BachDangStoryManager.Instance.SetInteractPrompt(false, owner: this);
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
