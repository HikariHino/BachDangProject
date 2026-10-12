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

    // beachBoat uses its existing physical gateways; no invisible wall is added.

    private void Update()
    {
        if (BeachBoatQuestSession.InputBlocked) return;
        var story = BachDangStoryManager.Instance;
        if (story == null) return;

        Transform player = story.GetPlayerTransform();
        if (player == null) return;

        Vector3 gateFlat = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 playerFlat = new Vector3(player.position.x, 0f, player.position.z);
        float dist = Vector3.Distance(gateFlat, playerFlat);
        bool canInteract = false;
        string promptMessage = "";

        if (gateType == GateType.CampGate)
        {
            if (story.currentState == BachDangStoryManager.StoryState.Act1_GoToCampGate)
            {
                canInteract = true;
                promptMessage = "Bấm E để đến dân làng bên sông";
            }
            else if (story.currentState == BachDangStoryManager.StoryState.Act1_ExploreCamp)
            {
                // Chưa nhận lệnh từ Ngô Quyền
                canInteract = false;
            }
        }
        else if (gateType == GateType.CampRiverGate && story.currentState == BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver)
        {
            canInteract = true;
            promptMessage = "[E] Ra bờ sông đóng cọc";
        }
        else if (gateType == GateType.RiverGate && story.currentState == BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp)
        {
            canInteract = true;
            promptMessage = "[E] Quay về doanh trại báo tin";
        }
        else if (gateType == GateType.VillageGate)
        {
            if (story.currentState == BachDangStoryManager.StoryState.Act2_ReturnToCampGate)
            {
                canInteract = true;
                promptMessage = "[E] Quay về Doanh Trại";
            }
            else if (story.currentState == BachDangStoryManager.StoryState.Act2_CollectWoodInVillage)
            {
                // Chưa nhặt đủ 5 gỗ
                promptMessage = $"Cần thu thập đủ 5 cọc gỗ ({story.collectedWood}/{story.totalWoodRequired})";
            }
        }

        bool canShowPrompt = canInteract || (!canInteract && !string.IsNullOrEmpty(promptMessage));
        if (dist <= interactDistance && canShowPrompt)
        {
            isPlayerInRange = true;
            story.SetInteractPrompt(true, promptMessage, this);

            if (canInteract && !StoryDialogueUI.InputConsumedThisFrame &&
                (StoryDialogueUI.Instance == null || !StoryDialogueUI.Instance.IsOpen) && Input.GetKeyDown(KeyCode.E))
            {
                if (StoryDialogueUI.TryConsumeInput()) TriggerTransition();
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
        if (story == null) return;

        story.SetInteractPrompt(false, owner: this);

        if (gateType == GateType.CampGate)
        {
            string narrative = "Theo quân lệnh của Chủ tướng Ngô Quyền,\nbạn lên đường sang Làng Ven Sông quyên góp cọc gỗ...";
            System.Action move = () =>
            {
                Transform player = story.GetPlayerTransform();
                Vector3 targetPos = destinationTarget != null ? destinationTarget.position : new Vector3(225f, 17.6f, -1235f);
                TeleportPlayer(player, targetPos, destinationTarget != null ? destinationTarget.rotation : Quaternion.identity);
                story.currentState = BachDangStoryManager.StoryState.Act2_CollectWoodInVillage;
                story.UpdateUI();
            };
            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeTransition(narrative, move);
            else move();
        }
        else if (gateType == GateType.VillageGate)
        {
            string narrative = "Dân làng đồng lòng gom góp cọc gỗ,\ntoàn quân khẩn trương vận chuyển cọc về đại bản doanh...";
            System.Action move = () =>
            {
                Transform player = story.GetPlayerTransform();
                Vector3 targetPos = destinationTarget != null ? destinationTarget.position : new Vector3(-765f, 32.85f, 195f);
                TeleportPlayer(player, targetPos, destinationTarget != null ? destinationTarget.rotation : Quaternion.identity);
                story.currentState = BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen;
                story.UpdateUI();
            };
            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeTransition(narrative, move);
            else move();
        }
        else if (gateType == GateType.CampRiverGate || gateType == GateType.RiverGate)
        {
            string narrative = gateType == GateType.CampRiverGate
                ? "Mang cọc ra bờ sông Bạch Đằng để dựng trận địa."
                : "Trận địa cọc đã hoàn thành. Trở về doanh trại báo tin cho Chủ tướng.";
            System.Action move = () =>
            {
                Transform player = story.GetPlayerTransform();
                Quaternion targetRot = destinationTarget != null ? destinationTarget.rotation : Quaternion.identity;
                Vector3 targetPos = destinationTarget != null ? destinationTarget.position : Vector3.zero;
                TeleportPlayer(player, targetPos, targetRot);
                if (gateType == GateType.RiverGate) story.currentState = BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp;
                story.UpdateUI();
            };
            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeTransition(narrative, move);
            else move();
        }
    }

    private static void TeleportPlayer(Transform player, Vector3 position, Quaternion rotation)
    {
        if (player == null) return;
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        var body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            body.rotation = rotation;
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }
        player.SetPositionAndRotation(position, rotation);
        if (controller != null) controller.enabled = true;
        Physics.SyncTransforms();
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
