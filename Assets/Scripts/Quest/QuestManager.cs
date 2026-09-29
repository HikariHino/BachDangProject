using UnityEngine;
using TMPro; // Thư viện để dùng TextMeshPro

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance; // Singleton để các script khác dễ dàng gọi đến

    [Header("UI Elements (Kéo thả UI vào đây)")]
    public TextMeshProUGUI questTitleText;
    public TextMeshProUGUI questDescriptionText;
    public GameObject questUIPanel; // Khung nền chứa chữ nhiệm vụ

    [Header("Thông tin Quest hiện tại")]
    public string currentQuestTitle = "";
    public bool hasActiveQuest = false;

    private void Awake()
    {
        // Khởi tạo Singleton
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Khi mới vào game, ẩn bảng nhiệm vụ đi
        if (questUIPanel != null)
        {
            questUIPanel.SetActive(false);
        }
    }

    // Hàm này sẽ được NPC gọi khi người chơi bấm nút nhận quest
    public void ReceiveQuest(string title, string description)
    {
        hasActiveQuest = true;
        currentQuestTitle = title;

        // Cập nhật chữ trên giao diện
        if (questTitleText != null) questTitleText.text = "Nhiệm vụ: " + title;
        if (questDescriptionText != null) questDescriptionText.text = description;

        // Hiện bảng UI lên
        if (questUIPanel != null) questUIPanel.SetActive(true);

        Debug.Log("Đã nhận nhiệm vụ mới: " + title);
    }
}