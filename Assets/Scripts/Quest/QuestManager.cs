using UnityEngine;
using TMPro;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    [Header("UI Elements")]
    public TextMeshProUGUI questTitleText;
    public TextMeshProUGUI questDescriptionText;
    public GameObject questUIPanel;

    [Header("Thong tin Quest")]
    public string currentQuestTitle = "";
    public bool hasActiveQuest = false;
    
    [Header("Tien do Nhap Go")]
    public int collectedWood = 0;
    public int totalWoodRequired = 5;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (questUIPanel != null)
        {
            questUIPanel.SetActive(false);
        }
    }

    public void ReceiveQuest(string title, string description)
    {
        hasActiveQuest = true;
        currentQuestTitle = title;
        collectedWood = 0; 

        if (questTitleText != null) questTitleText.text = "Nhiệm vụ: " + title;
        if (questDescriptionText != null) questDescriptionText.text = description + $"\n(Tiến độ: {collectedWood}/{totalWoodRequired} Gỗ)";

        if (questUIPanel != null) questUIPanel.SetActive(true);
    }

    public void AddWood()
    {
        if (!hasActiveQuest || collectedWood >= totalWoodRequired) return;

        collectedWood++;
        
        if (questDescriptionText != null)
        {
            questDescriptionText.text = $"- Hãy nhặt đủ gỗ trong doanh trại.\n- Mang ra bờ sông để cắm cọc.\n\nTiến độ: ({collectedWood}/{totalWoodRequired}) Gỗ";
        }

        if (collectedWood >= totalWoodRequired)
        {
            CompleteWoodQuest();
        }
    }

    private void CompleteWoodQuest()
    {
        if (questTitleText != null) questTitleText.text = "Nhiệm vụ: CẮM CỌC";
        if (questDescriptionText != null) questDescriptionText.text = "Bạn đã thu thập đủ gỗ!\nHãy chạy ra mép nước sông Bạch Đằng để cắm cọc.";
        Debug.Log("Da nhat du go!");
    }
}