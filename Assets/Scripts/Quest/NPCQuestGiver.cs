using UnityEngine;
using TMPro;

public class NPCQuestGiver : MonoBehaviour
{
    [Header("Nội dung Nhiệm vụ")]
    public string questTitle = "Thu thập cọc gỗ";
    [TextArea(3, 5)] // Tạo khung nhập text rộng hơn trong Inspector
    public string questDescription = "Hãy ra bìa rừng nhặt 5 cọc gỗ và đem ra bờ sông Bạch Đằng.";

    [Header("Giao diện Tương tác")]
    public GameObject interactPromptUI; // Kéo thả cái Text "Nhấn E để nói chuyện" vào đây

    private bool isPlayerNear = false;
    private bool hasGivenQuest = false;

    private void Start()
    {
        // Giấu chữ "Nhấn E" đi lúc mới đầu
        if (interactPromptUI != null)
            interactPromptUI.SetActive(false);
    }

    private void Update()
    {
        // Nếu người chơi đứng gần + Chưa giao quest + Bấm phím E
        if (isPlayerNear && !hasGivenQuest && Input.GetKeyDown(KeyCode.E))
        {
            // Gửi dữ liệu nhiệm vụ sang QuestManager
            QuestManager.Instance.ReceiveQuest(questTitle, questDescription);

            hasGivenQuest = true; // Đánh dấu là đã giao rồi, không giao lại nữa

            if (interactPromptUI != null)
                interactPromptUI.SetActive(false); // Tắt chữ "Nhấn E"
        }
    }

    // Phát hiện người chơi đi VÀO vùng tương tác (Nhớ tích chọn "Is Trigger" ở Collider)
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasGivenQuest)
        {
            isPlayerNear = true;
            if (interactPromptUI != null)
                interactPromptUI.SetActive(true); // Hiện chữ "Nhấn E"
        }
    }

    // Phát hiện người chơi đi RA KHỎI vùng tương tác
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            if (interactPromptUI != null)
                interactPromptUI.SetActive(false); // Ẩn chữ "Nhấn E"
        }
    }
}