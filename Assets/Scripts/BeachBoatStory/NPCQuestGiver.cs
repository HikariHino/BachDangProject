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
    public bool CanReachPlayer => isActiveAndEnabled&&NearbyConversation.InReach(transform,interactDistance);

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
        else if ((EnemyFleetDirector.Instance != null && EnemyFleetDirector.Instance.IsConfigured) &&
                 (story.currentState == BachDangStoryManager.StoryState.Act2_Completed ||
                  story.currentState == BachDangStoryManager.StoryState.Act3_PrepareForBattle))
        {
            canTalk = true;
            prompt = ArcheryTraining.Instance?.Completed==true?"[E] Nhận lệnh XUẤT QUÂN":"[E] Nhận hướng dẫn TẬP CUNG trước xuất quân";
        }
        else if (story.currentState == BachDangStoryManager.StoryState.Act3_Victory || story.currentState == BachDangStoryManager.StoryState.Act3_FinalReport)
        {
            canTalk = true;
            prompt = "[E] Báo cáo Đại thắng Bạch Đằng 938";
        }

        if (CanReachPlayer && canTalk)
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
        if (BeachBoatQuestSession.InteractionBlocked || player == null || !CanReachPlayer) return false;
                if (story.currentState == BachDangStoryManager.StoryState.Act1_ExploreCamp)
                {
                    StoryDialogueUI.Instance?.Show("Ngô Quyền", "Quân Nam Hán đang lăm le xâm phạm bờ cõi. Hãy đến làng ven sông, cùng bà con chuẩn bị gỗ làm cọc. Ta trông cậy vào ngươi.", story.OnTalkToNgoQuyenAct1,transform);
                    story.SetInteractPrompt(false, owner: this);
                    if (interactPromptUI != null) interactPromptUI.SetActive(false);
                }
                else if (story.currentState == BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen)
                {
                    StoryDialogueUI.Instance?.Show("Ngô Quyền", PreparationBriefing.Stakes, story.OnReportToNgoQuyenAct2,transform);
                    story.SetInteractPrompt(false, owner: this);
                    if (interactPromptUI != null) interactPromptUI.SetActive(false);
                }
                else if (story.currentState == BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp)
                {
                    StoryDialogueUI.Instance?.Show("Ngô Quyền", "Trận địa cọc đã hoàn thành. Toàn quân hãy giữ kín kế hoạch, chờ thủy triều và thời cơ đánh giặc!", story.OnReportStakesComplete,transform);
                    story.SetInteractPrompt(false, owner: this);
                    if (interactPromptUI != null) interactPromptUI.SetActive(false);
                }
                else if ((EnemyFleetDirector.Instance != null && EnemyFleetDirector.Instance.IsConfigured) &&
                         (story.currentState == BachDangStoryManager.StoryState.Act2_Completed ||
                          story.currentState == BachDangStoryManager.StoryState.Act3_PrepareForBattle))
                {
                    StoryDialogueUI.Instance?.Show("Ngô Quyền", ArcheryTraining.Instance?.Completed==true?PreparationBriefing.Lure:PreparationBriefing.Archery, story.OnTalkToNgoQuyenAct3,transform);
                    story.SetInteractPrompt(false, owner: this);
                    if (interactPromptUI != null) interactPromptUI.SetActive(false);
                }
                else if(story.currentState==BachDangStoryManager.StoryState.Act3_FinalReport)
                {
                    StoryDialogueUI.Instance?.Show("Ngô Quyền","Tướng giặc đã bị hạ, tù binh đã quy hàng. Tướng sĩ đã tận lực vì non sông. Toàn quân mừng đại thắng Bạch Đằng!",story.OnFinalVictoryReported,transform);
                    story.SetInteractPrompt(false,owner:this);
                }
                else if (story.currentState == BachDangStoryManager.StoryState.Act3_Victory)
                {
                    StoryDialogueUI.Instance?.Show("Ngô Quyền", "Đại thắng! Quân Nam Hán bị đánh tan trên sông Bạch Đằng. Từ nay, nước Việt ta bước vào thời kỳ độc lập, tự chủ. Toàn quân trở về đại bản doanh báo công!", story.OnVictoryReported,transform);
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
