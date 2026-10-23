// Adapted from origin/thai 03aad75 for beachBoat only.
namespace BeachBoatStory
{
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
    private void OnEnable(){NearbyConversation.Register(this);}

    private void Update()
    {
        if (BeachBoatQuestSession.InputBlocked) return;
        var story = BachDangStoryManager.Instance;
        if (story == null) return;
        if (OpeningEvent.IsOpeningActive) return;
        if (StoryDialogueUI.InputConsumedThisFrame) return;
        var player = story.GetPlayerTransform();
        if (player == null) return;

        // Ordinary NPCs are always conversational and never change the quest state.
        bool canTalk = BeachBoatQuestSession.Instance != null && BeachBoatQuestSession.Instance.IsPreferredTalker(this);
        bool near = canTalk && player.gameObject.activeInHierarchy &&
                    Vector3.Distance(transform.position, player.position) <= interactDistance;

        if (StoryDialogueUI.Instance != null && StoryDialogueUI.Instance.IsOpen)
            near = false;

        if (near != isPlayerNear)
        {
            isPlayerNear = near;
            if (interactPromptUI != null) interactPromptUI.SetActive(near);
            if (!near && (StoryDialogueUI.Instance == null || !StoryDialogueUI.Instance.IsOpen))
                story.SetInteractPrompt(false, owner: this);
        }

        if (!near || StoryDialogueUI.Instance == null || StoryDialogueUI.Instance.IsOpen) return;
        story.SetInteractPrompt(true, "[E] Trò chuyện với " + speakerName, this);
        if (Input.GetKeyDown(KeyCode.E) && StoryDialogueUI.TryConsumeInput())
        {
            TryTalk();
        }
    }

    public bool TryTalk()
    {
        var session = BeachBoatQuestSession.Instance;
        if (!isActiveAndEnabled || BeachBoatQuestSession.InteractionBlocked || session == null ||
            StoryDialogueUI.Instance == null || !session.IsPreferredTalker(this)) return false;
        BachDangStoryManager.Instance.SetInteractPrompt(false, owner: this);
        StoryDialogueUI.Instance.Show(speakerName, dialogueText,null,transform);
        return true;
    }

    private void OnDisable()
    {
        NearbyConversation.Remove(this);
        BachDangStoryManager.Instance?.SetInteractPrompt(false,owner:this);
        if (interactPromptUI != null) interactPromptUI.SetActive(false);
    }
}

}
