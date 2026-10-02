using UnityEngine;
using TMPro;

public class NPCDialogue : MonoBehaviour
{
    [TextArea(3, 5)]
    public string dialogueText = "Xin chào, một ngày tốt lành nhé!";
    
    public GameObject interactPromptUI;

    private bool isPlayerNear = false;
    private bool hasTalked = false;

    private void Start()
    {
        if(interactPromptUI != null)
            interactPromptUI.SetActive(false);
    }

    private void Update()
    {
        if (isPlayerNear && Input.GetKeyDown(KeyCode.E))
        {
            if (QuestManager.Instance != null && !hasTalked)
            {
                // Mượn tạm QuestManager để hiển thị hội thoại
                QuestManager.Instance.ReceiveQuest("Dân Làng", dialogueText);
                hasTalked = true;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
            if(interactPromptUI != null)
                interactPromptUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            if(interactPromptUI != null)
                interactPromptUI.SetActive(false);
            hasTalked = false; // reset để có thể nói chuyện tiếp
        }
    }
}
