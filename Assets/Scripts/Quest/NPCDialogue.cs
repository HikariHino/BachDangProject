using UnityEngine;

/// <summary>Proximity based, keyboard accessible dialogue for ordinary NPCs.</summary>
public class NPCDialogue : MonoBehaviour
{
    [Header("Hội thoại")]
    public string speakerName = "Dân làng";
    [TextArea(3, 5)]
    public string dialogueText = "Quân ta sẽ góp sức cùng Chủ tướng Ngô Quyền bảo vệ dòng sông.";
    public float interactDistance = 8f;

    [Header("Tương thích UI cũ (không bắt buộc)")]
    public GameObject interactPromptUI;

    private bool isPlayerNear;

    private void Update()
    {
        var story = BachDangStoryManager.Instance;
        if (story == null) return;
        if (OpeningEvent.IsOpeningActive) return;
        if (StoryDialogueUI.InputConsumedThisFrame) return;
        var player = story.GetPlayerTransform();
        if (player == null) return;

        // Ordinary NPCs are always conversational and never change the quest state.
        bool canTalk = true;
        bool near = canTalk && player.gameObject.activeInHierarchy &&
                    Vector3.Distance(transform.position, player.position) <= interactDistance;

        if (StoryDialogueUI.Instance != null && StoryDialogueUI.Instance.IsOpen)
            near = false;

        if (near != isPlayerNear)
        {
            isPlayerNear = near;
            if (interactPromptUI != null) interactPromptUI.SetActive(near);
            if (!near && (StoryDialogueUI.Instance == null || !StoryDialogueUI.Instance.IsOpen))
                story.SetInteractPrompt(false);
        }

        if (!near || StoryDialogueUI.Instance == null || StoryDialogueUI.Instance.IsOpen) return;
        story.SetInteractPrompt(true, "[E] Trò chuyện với " + speakerName);
        if (Input.GetKeyDown(KeyCode.E) && StoryDialogueUI.TryConsumeInput())
        {
            story.SetInteractPrompt(false);
            StoryDialogueUI.Instance.Show(speakerName, dialogueText);
        }
    }

    private void OnDisable()
    {
        if (interactPromptUI != null) interactPromptUI.SetActive(false);
    }
}
