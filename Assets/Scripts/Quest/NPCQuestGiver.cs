using UnityEngine;

/// <summary>
/// Gắn lên Chủ tướng Ngô Quyền để giao nhiệm vụ trong Hồi 1 và Hồi 2.
/// </summary>
[DefaultExecutionOrder(-1000)]
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
        // Create the dialogue host before this NPC starts accepting input.
        // The scene bootstrap normally creates it, but this also covers a
        // runtime-added quest giver when Play Mode has domain reload disabled.
        StoryDialogueUI.EnsureExists();

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
        if (OpeningEvent.IsOpeningActive) return;
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        var story = BachDangStoryManager.Instance;
        if (story == null) return;
        // Keep proximity and prompt state current every frame. Other nearby
        // interactables can consume this frame's E input or clear the shared
        // prompt; this quest giver is the priority interaction while nearby.
        if (StoryDialogueUI.Instance != null && StoryDialogueUI.Instance.IsOpen) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        bool pressedE = Input.GetKeyDown(KeyCode.E);

        // The legacy WoodQuestManager tracks the village pickups separately from
        // BachDangStoryManager. Bridge the completed legacy quest when the player
        // returns to Ngo Quyen, so the report dialogue becomes available.
        if (dist <= interactDistance &&
            story.currentState == BachDangStoryManager.StoryState.Act1_GoToCampGate)
        {
            var legacyWoodQuest = FindFirstObjectByType<WoodQuestManager>();
            if (legacyWoodQuest != null && legacyWoodQuest.IsComplete)
            {
                story.collectedWood = story.totalWoodRequired;
                story.currentState = BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen;
                story.UpdateUI();
            }
        }

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
            }
            if (interactPromptUI != null) interactPromptUI.SetActive(true);
            // Refresh every frame so another nearby interactable cannot permanently clear this prompt.
            story.SetInteractPrompt(true, prompt);

            if (pressedE)
            {
                if (StoryDialogueUI.TryConsumeInput())
                {
                    if (story.currentState == BachDangStoryManager.StoryState.Act1_ExploreCamp)
                    {
                        StoryDialogueUI.Instance?.Show("Ngô Quyền", "Quân Nam Hán đang lăm le xâm phạm bờ cõi. Hãy đến làng ven sông, cùng bà con chuẩn bị gỗ làm cọc. Ta trông cậy vào ngươi.", story.OnTalkToNgoQuyenAct1);
                    }
                    else if (story.currentState == BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen)
                    {
                        StoryDialogueUI.Instance?.Show("Ngô Quyền", "Tốt lắm. Hãy đưa số cọc này ra mép sông, cho quân đóng thành trận địa. Việc này quyết định thắng bại.", story.OnReportToNgoQuyenAct2);
                    }
                    else if (story.currentState == BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp)
                    {
                        StoryDialogueUI.Instance?.Show("Ngô Quyền", "Trận địa cọc đã hoàn thành. Toàn quân hãy giữ kín kế hoạch, chờ thủy triều và thời cơ đánh giặc!", story.OnReportStakesComplete);
                    }

                    story.SetInteractPrompt(false);
                    if (interactPromptUI != null) interactPromptUI.SetActive(false);
                }
            }
        }
        else
        {
            if (isPlayerNear)
            {
                isPlayerNear = false;
                if (interactPromptUI != null) interactPromptUI.SetActive(false);
                story.SetInteractPrompt(false);
            }
        }
    }

    private void LateUpdate()
    {
        // Reassert this prompt after other NPCs/gates update their shared prompt.
        if (!isPlayerNear || playerTransform == null ||
            (StoryDialogueUI.Instance != null && StoryDialogueUI.Instance.IsOpen)) return;

        var story = BachDangStoryManager.Instance;
        if (story == null || Vector3.Distance(transform.position, playerTransform.position) > interactDistance) return;

        string prompt = null;
        if (story.currentState == BachDangStoryManager.StoryState.Act1_ExploreCamp)
            prompt = "[E] Diện kiến Chủ tướng Ngô Quyền nhận lệnh";
        else if (story.currentState == BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen)
            prompt = "[E] Báo cáo cọc gỗ & Nhận lệnh cắm cọc";
        else if (story.currentState == BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp)
            prompt = "[E] Báo cáo đã hoàn thành trận địa cọc";

        if (!string.IsNullOrEmpty(prompt)) story.SetInteractPrompt(true, prompt);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
