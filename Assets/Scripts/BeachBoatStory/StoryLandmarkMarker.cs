// Adapted from origin/thai 03aad75 for beachBoat only.
namespace BeachBoatStory
{
using UnityEngine;

/// <summary>Billboard label for the current story destination.</summary>
public sealed class StoryLandmarkMarker : MonoBehaviour
{
    public enum Target
    {
        NgoQuyen,
        CampGate,
        VillageGate,
        RiverStakes,
        ReturnFromRiver,
        Wood
    }

    public Target target;
    public string label;
    private TextMesh labelMesh;

    private void Awake()
    {
        BuildLabel();
    }

    private void BuildLabel()
    {
        var labelObject = new GameObject("StoryLandmarkLabel");
        labelObject.transform.SetParent(transform, false);
        ApplyWorldSizedPlacement(labelObject.transform);
        labelMesh = labelObject.AddComponent<TextMesh>();
        labelMesh.text = label;
        labelMesh.anchor = TextAnchor.MiddleCenter;
        labelMesh.alignment = TextAlignment.Center;
        labelMesh.fontSize = 48;
        labelMesh.characterSize = 0.45f;
        labelMesh.fontStyle = FontStyle.Bold;
        labelMesh.color = new Color(1f, 0.83f, 0.38f, 1f);
    }

    public void Configure(Target markerTarget, string markerLabel)
    {
        target = markerTarget;
        label = markerLabel;
        if (labelMesh != null)
        {
            labelMesh.text = label;
            ApplyWorldSizedPlacement(labelMesh.transform);
        }
    }

    private void ApplyWorldSizedPlacement(Transform labelTransform)
    {
        Vector3 parentScale = transform.lossyScale;
        float sx = Mathf.Max(0.001f, Mathf.Abs(parentScale.x));
        float sy = Mathf.Max(0.001f, Mathf.Abs(parentScale.y));
        float sz = Mathf.Max(0.001f, Mathf.Abs(parentScale.z));
        labelTransform.localScale = new Vector3(1f / sx, 1f / sy, 1f / sz);
        float heightAboveTarget = target == Target.NgoQuyen ? 20f : target == Target.Wood ? 8f : 17f;
        labelTransform.localPosition = Vector3.up * (heightAboveTarget / sy);
    }

    private void Update()
    {
        if (labelMesh == null) return;
        var story = BachDangStoryManager.Instance;
        if (story == null) return;

        bool visible = !BeachBoatQuestSession.InputBlocked && IsCurrentTarget(story.currentState);
        if (visible && target == Target.Wood)
        {
            var player = story.GetPlayerTransform();
            visible = player != null && (player.position - transform.position).sqrMagnitude < 120f * 120f;
        }
        if (labelMesh.gameObject.activeSelf != visible)
            labelMesh.gameObject.SetActive(visible);

        var camera = Camera.main;
        if (visible && camera != null)
            labelMesh.transform.rotation = Quaternion.LookRotation(camera.transform.position - labelMesh.transform.position) * Quaternion.Euler(0f, 180f, 0f);
    }

    private bool IsCurrentTarget(BachDangStoryManager.StoryState state)
    {
        switch (target)
        {
            case Target.NgoQuyen:
                return state == BachDangStoryManager.StoryState.Act1_ExploreCamp ||
                       state == BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen ||
                       state == BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp;
            case Target.CampGate:
                return state == BachDangStoryManager.StoryState.Act1_GoToCampGate;
            case Target.VillageGate:
                return state == BachDangStoryManager.StoryState.Act2_ReturnToCampGate;
            case Target.RiverStakes:
                return state == BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver;
            case Target.ReturnFromRiver:
                return state == BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp;
            case Target.Wood:
                return state == BachDangStoryManager.StoryState.Act2_CollectWoodInVillage;
            default:
                return false;
        }
    }
}

}
