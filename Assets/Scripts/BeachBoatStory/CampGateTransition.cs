// Adapted from origin/thai 03aad75 for beachBoat only.
namespace BeachBoatStory
{
using UnityEngine;

/// <summary>
/// Cổng chuyển cảnh tương tác giữa Doanh Trại và Làng Ven Sông.
/// Khi lại gần và đủ điều kiện nhiệm vụ, bấm [E] sẽ fade màn hình đen và dịch chuyển mượt mà.
/// </summary>
public class CampGateTransition : MonoBehaviour
{
    public enum GateType
    {
        CampGate,    // Cổng tại doanh trại (đi sang làng)
        VillageGate,
        CampRiverGate,
        RiverGate
    }

    [Header("Loại cổng")]
    public GateType gateType = GateType.CampGate;

    [Header("Cự ly tương tác")]
    public float interactDistance = 8f;

    [Header("Điểm đích đến")]
    public Transform destinationTarget;

    private bool isPlayerInRange = false;

    public bool IsAvailable(BachDangStoryManager.StoryState state)
    {
        return destinationTarget!=null;
    }

    public bool IsInRange(Transform player)
    {
        if (QuestRowboat.Instance != null && QuestRowboat.Instance.BlocksGroundInteractions) return false;
        if (player == null || !player.gameObject.activeInHierarchy) return false;
        var offset = player.position - transform.position;
        if(Mathf.Abs(offset.y)>18)return false;
        offset.y = 0;
        return offset.sqrMagnitude <= interactDistance * interactDistance;
    }

    // The keyboard and tests use the same guarded entry point.
    public bool TryInteract()
    {
        var story = BachDangStoryManager.Instance;
        if (!isActiveAndEnabled || BeachBoatQuestSession.InteractionBlocked || destinationTarget == null ||
            story == null || !IsAvailable(story.currentState) || !IsInRange(story.GetPlayerTransform()) ||
            !BeachBoatQuestSession.Instance.IsPreferredGate(this)) return false;
        BeachBoatSoundscape.Instance?.PlayInteraction();
        TriggerTransition();
        return true;
    }

    private void Update()
    {
        if (BeachBoatQuestSession.InputBlocked) return;
        if (QuestRowboat.Instance != null && QuestRowboat.Instance.BlocksGroundInteractions) return;
        var story = BachDangStoryManager.Instance;
        if (story == null) return;

        Transform player = story.GetPlayerTransform();
        if (player == null) return;

        Vector3 gateFlat = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 playerFlat = new Vector3(player.position.x, 0f, player.position.z);
        float dist = Vector3.Distance(gateFlat, playerFlat);
        bool canInteract=IsAvailable(story.currentState)&&BeachBoatQuestSession.Instance.IsPreferredGate(this);
        string promptMessage=gateType==GateType.CampGate?"[E] Đến LÀNG • vật tư nhiệm vụ 2":gateType==GateType.CampRiverGate?"[E] Ra BẾN SÔNG • đóng cọc / xuất chiến":"[E] Về DOANH TRẠI";
        bool canShowPrompt=canInteract;
        if (dist <= interactDistance && canShowPrompt)
        {
            isPlayerInRange = true;
            story.SetInteractPrompt(true, promptMessage, this);

            if (canInteract && !StoryDialogueUI.InputConsumedThisFrame &&
                (StoryDialogueUI.Instance == null || !StoryDialogueUI.Instance.IsOpen) && Input.GetKeyDown(KeyCode.E))
            {
                if (StoryDialogueUI.TryConsumeInput()) TryInteract();
            }
        }
        else
        {
            if (isPlayerInRange)
            {
                isPlayerInRange = false;
                story.SetInteractPrompt(false, owner: this);
            }
        }
    }

    private void TriggerTransition()
    {
        if (BeachBoatQuestSession.InteractionBlocked || destinationTarget == null) return;
        var story = BachDangStoryManager.Instance;
        if (story == null || !IsAvailable(story.currentState) || !IsInRange(story.GetPlayerTransform())) return;

        story.SetInteractPrompt(false, owner: this);

        var previous=story.currentState;
        string narrative=gateType==GateType.CampGate?"Đường làng ven sông — dân binh cùng chuẩn bị trận địa.":gateType==GateType.CampRiverGate?"Bến thuyền Bạch Đằng — nơi bố trí trận địa cọc.":"Trở về doanh trại Ngô Quyền.";
        System.Action move=()=>{
            TeleportPlayer(story.GetPlayerTransform(),destinationTarget.position,destinationTarget.rotation);
            if(gateType==GateType.CampGate&&previous==BachDangStoryManager.StoryState.Act1_GoToCampGate)
                story.TransitionTo(BachDangStoryManager.StoryState.Act2_CollectWoodInVillage);
            else if(gateType==GateType.VillageGate&&previous==BachDangStoryManager.StoryState.Act2_ReturnToCampGate)
                story.TransitionTo(BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen);
            else story.UpdateUI();
        };
        if(ScreenFader.Instance!=null)ScreenFader.Instance.FadeTransition(narrative,move);else move();
    }

    private static void TeleportPlayer(Transform player, Vector3 position, Quaternion rotation)
    {
        if (player == null) return;
        PlayerArrival.Teleport(player.gameObject,position,rotation);
        var camera = Camera.main;
        if (camera != null && camera.TryGetComponent<Camera_Script>(out var orbit)) orbit.SnapToTarget();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gateType == GateType.CampGate ? Color.cyan : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}

}
