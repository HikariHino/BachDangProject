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
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) p = GameObject.Find("Main_Character");
        if (p == null) p = GameObject.Find("Player_Main_Animated");
        if (p != null) playerTransform = p.transform;
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        var story = BachDangStoryManager.Instance;
        if (story == null) return;

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

        if (dist <= interactDistance && canTalk)
        {
            if (!isPlayerNear)
            {
                isPlayerNear = true;
                if (interactPromptUI != null) interactPromptUI.SetActive(true);
                story.SetInteractPrompt(true, prompt);
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (story.currentState == BachDangStoryManager.StoryState.Act1_ExploreCamp)
                {
                    story.OnTalkToNgoQuyenAct1();
                    story.SetInteractPrompt(false);
                    if (interactPromptUI != null) interactPromptUI.SetActive(false);
                }
                else if (story.currentState == BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen)
                {
                    story.OnReportToNgoQuyenAct2();
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

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}