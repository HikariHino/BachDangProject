using UnityEngine;

public class WoodPickup : MonoBehaviour
{
    public float pickupDistance = 8f;

    private Transform player;
    private WoodQuestManager quest;
    private bool nearby;
    private bool collected;

    private void Start()
    {
        quest = FindFirstObjectByType<WoodQuestManager>();

        if (quest != null)
        {
            player = quest.player;
        }
    }

    private void Update()
    {
        if (collected || quest == null || player == null || !quest.QuestStarted)
        {
            nearby = false;
            return;
        }

        float currentDistance = Vector3.Distance(transform.position, player.position);
        nearby = currentDistance <= pickupDistance;

        if (nearby && Input.GetKeyDown(KeyCode.E))
        {
            collected = true;
            quest.CollectWood();
            Destroy(gameObject);
        }
    }

    private void OnGUI()
    {
        if (!nearby) return;

        GUIStyle promptStyle = new GUIStyle(GUI.skin.box);
        promptStyle.fontSize = 24;
        promptStyle.alignment = TextAnchor.MiddleCenter;

        GUI.Box(
            new Rect(Screen.width / 2 - 200, Screen.height - 120, 400, 60),
            "Bấm E để nhặt gỗ",
            promptStyle
        );
    }
}