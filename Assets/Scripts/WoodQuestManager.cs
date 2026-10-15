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
}
