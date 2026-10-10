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
        VillageGate  // Cổng tại làng (quay về doanh trại)
    }

    [Header("Loại cổng")]
    public GateType gateType = GateType.CampGate;

    [Header("Cự ly tương tác")]
    public float interactDistance = 8f;

    [Header("Điểm đích đến")]
    public Transform destinationTarget;

    private bool isPlayerInRange = false;

    private void Update()
    {
        var story = BachDangStoryManager.Instance;
        if (story == null) return;

        Transform player = story.GetPlayerTransform();
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        bool canInteract = false;
        string promptMessage = "";

        if (gateType == GateType.CampGate)
        {
            if (story.currentState == BachDangStoryManager.StoryState.Act1_GoToCampGate)
            {
                canInteract = true;
                promptMessage = "[E] Đi đến Làng Ven Sông";
            }
            else if (story.currentState == BachDangStoryManager.StoryState.Act1_ExploreCamp)
            {
                // Chưa nhận lệnh từ Ngô Quyền
                canInteract = false;
            }
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

        if (dist <= interactDistance && canInteract)
        {
            if (!isPlayerInRange)
            {
                isPlayerInRange = true;
                story.SetInteractPrompt(true, promptMessage);
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                TriggerTransition();
            }
        }
        else
        {
            if (isPlayerInRange)
            {
                isPlayerInRange = false;
                story.SetInteractPrompt(false);
            }
        }
    }

    private void TriggerTransition()
    {
        var story = BachDangStoryManager.Instance;
        if (story == null) return;

        story.SetInteractPrompt(false);

        if (gateType == GateType.CampGate)
        {
            string narrative = "Theo quân lệnh của Chủ tướng Ngô Quyền,\nbạn lên đường sang Làng Ven Sông quyên góp cọc gỗ...";
            ScreenFader.Instance.FadeTransition(narrative, () =>
            {
                Transform player = story.GetPlayerTransform();
                Vector3 targetPos = destinationTarget != null ? destinationTarget.position : 
                    (story.villageSpawnPoint != null ? story.villageSpawnPoint.position : new Vector3(250f, 18f, -1200f));

                if (player != null)
                {
                    var cc = player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    player.position = targetPos;
                    if (destinationTarget != null) player.rotation = destinationTarget.rotation;
                    if (cc != null) cc.enabled = true;
                }

                story.currentState = BachDangStoryManager.StoryState.Act2_CollectWoodInVillage;
                story.UpdateUI();
            });
        }
        else if (gateType == GateType.VillageGate)
        {
            string narrative = "Dân làng đồng lòng gom góp cọc gỗ,\ntoàn quân khẩn trương vận chuyển cọc về đại bản doanh...";
            ScreenFader.Instance.FadeTransition(narrative, () =>
            {
                Transform player = story.GetPlayerTransform();
                Vector3 targetPos = destinationTarget != null ? destinationTarget.position : 
                    (story.campSpawnPoint != null ? story.campSpawnPoint.position : new Vector3(199f, 17.5f, -1254f));

                if (player != null)
                {
                    var cc = player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    player.position = targetPos;
                    if (destinationTarget != null) player.rotation = destinationTarget.rotation;
                    if (cc != null) cc.enabled = true;
                }

                story.currentState = BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen;
                story.UpdateUI();
            });
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gateType == GateType.CampGate ? Color.cyan : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
