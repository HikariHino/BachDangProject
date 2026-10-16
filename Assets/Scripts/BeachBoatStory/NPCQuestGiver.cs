// Adapted from origin/thai 03aad75 for beachBoat only.
namespace BeachBoatStory
{
using UnityEngine;

/// <summary>
/// Gắn lên Chủ tướng Ngô Quyền để giao nhiệm vụ trong Hồi 1 và Hồi 2.
/// </summary>
public class NPCQuestGiver : MonoBehaviour
{
    [Header("Cự ly tương tác")]
    public float interactDistance = 8f;

    [Header("Giao diện Tương tác")]
    public GameObject interactPromptUI;

    private bool isPlayerNear = false;
    private Transform playerTransform;

    private void Start()
    {
        if (interactPromptUI != null)
            interactPromptUI.SetActive(false);

        FindPlayer();
    }

    private void FindPlayer()
    {
        GameObject p = OpeningEvent.FindPlayerInScene();
        if (p != null) playerTransform = p.transform;
    }

    private void Update()
    {
        if (BeachBoatQuestSession.InputBlocked) return;
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        var story = BachDangStoryManager.Instance;
        if (story == null) return;
        if (StoryDialogueUI.InputConsumedThisFrame || (StoryDialogueUI.Instance != null && StoryDialogueUI.Instance.IsOpen)) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        bool canTalk = false;
        string prompt = "";

        if (story.currentState == BachDangStoryManager.StoryState.Act1_ExploreCamp)
        {
            canTalk = true;
            prompt = "[E] Diện kiến Chủ tướng Ngô Quyền nhận lệnh";
        }
        else if (story.currentState == BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen)
        {
            canTalk = true;
            prompt = "[E] Báo cáo cọc gỗ & Nhận lệnh cắm cọc";
        }
        else if (story.currentState == BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp)
        {
            canTalk = true;
            prompt = "[E] Báo cáo đã hoàn thành trận địa cọc";
        }

        if (dist <= interactDistance && canTalk)
        {
            if (!isPlayerNear)
            {
                isPlayerNear = true;
                if (interactPromptUI != null) interactPromptUI.SetActive(true);
            }
            // Refresh every frame so another nearby interactable cannot permanently clear this prompt.
            story.SetInteractPrompt(true, prompt, this);

            if (Input.GetKeyDown(KeyCode.E) && StoryDialogueUI.TryConsumeInput())
            {
                TryTalk();
            }
        }
        else
        {
            if (isPlayerNear)
            {
                isPlayerNear = false;
                if (interactPromptUI != null) interactPromptUI.SetActive(false);
                story.SetInteractPrompt(false, owner: this);
            }
        }
    }


    public bool TryTalk()
    {
        var story = BachDangStoryManager.Instance;
        var player = story?.GetPlayerTransform();
        if (BeachBoatQuestSession.InteractionBlocked || player == null || Vector3.Distance(transform.position, player.position) > interactDistance) return false;
                if (story.currentState == BachDangStoryManager.StoryState.Act1_ExploreCamp)
                {
                    StoryDialogueUI.Instance?.Show("Ngô Quyền", "Quân Nam Hán đang lăm le xâm phạm bờ cõi. Hãy đến làng ven sông, cùng bà con chuẩn bị gỗ làm cọc. Ta trông cậy vào ngươi.", story.OnTalkToNgoQuyenAct1);
                    story.SetInteractPrompt(false, owner: this);
                    if (interactPromptUI != null) interactPromptUI.SetActive(false);
                }
                else if (story.currentState == BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen)
                {
                    StoryDialogueUI.Instance?.Show("Ngô Quyền", "Tốt lắm. Mang năm cọc này qua cổng ra bãi sông. Ta đã đánh dấu năm vị trí: đến từng vòng đánh số và bấm E để đóng một cọc. Xong cả năm, hãy về báo cáo. Việc này quyết định thắng bại.", story.OnReportToNgoQuyenAct2);
                    story.SetInteractPrompt(false, owner: this);
                    if (interactPromptUI != null) interactPromptUI.SetActive(false);
                }
                else if (story.currentState == BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp)
                {
                    StoryDialogueUI.Instance?.Show("Ngô Quyền", "Trận địa cọc đã hoàn thành. Toàn quân hãy giữ kín kế hoạch, chờ thủy triều và thời cơ đánh giặc!", story.OnReportStakesComplete);
                    story.SetInteractPrompt(false, owner: this);
                    if (interactPromptUI != null) interactPromptUI.SetActive(false);
                }
        return true;
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}

}
