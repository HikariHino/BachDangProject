using UnityEngine;

public class WoodQuestManager : MonoBehaviour
{
    public Transform player;
    public int totalWood = 5;

    public int CollectedWood { get; private set; }
    public bool QuestStarted { get; private set; }

    public bool IsComplete
    {
        get { return CollectedWood >= totalWood; }
    }

    private void Awake()
    {
        CollectedWood = 0;
        QuestStarted = false;
    }

    private void Update()
    {
        if (QuestStarted) return;

        var story = BachDangStoryManager.Instance;
        if (story == null) return;

        // Nói chuyện xong với Ngô Quyền sẽ rời trạng thái Act1_ExploreCamp.
        if (story.currentState != BachDangStoryManager.StoryState.Act1_ExploreCamp)
        {
            QuestStarted = true;
            Debug.Log("Đã nhận nhiệm vụ thu thập gỗ.");
        }
    }

    public void CollectWood()
    {
        if (!QuestStarted || IsComplete) return;

        CollectedWood++;
        Debug.Log($"Đã nhặt gỗ: {CollectedWood}/{totalWood}");
    }

    private void OnGUI()
    {
        // Ẩn bảng gỗ cho đến khi nhận lệnh.
        if (!QuestStarted) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        GUI.Label(
            new Rect(20, 20, 420, 40),
            $"Nhiệm vụ: Thu thập gỗ ({CollectedWood}/{totalWood})",
            style
        );

        if (IsComplete)
        {
            GUI.Label(
                new Rect(20, 60, 420, 40),
                "Đã thu thập đủ gỗ!",
                style
            );
        }
    }
}