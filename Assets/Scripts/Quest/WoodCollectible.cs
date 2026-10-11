using UnityEngine;

/// <summary>
/// Khúc cọc gỗ người chơi nhặt tại Làng Ven Sông trong Hồi 2.
/// </summary>
public class WoodCollectible : MonoBehaviour
{
    [Header("Giao diện (Chữ [E] Nhặt)")]
    public GameObject interactPromptUI;

    public float interactDistance = 8f;
    private static int lastCollectFrame = -1;
    private bool isPlayerNear = false;
    private Transform playerTransform;
    private bool collected;

    void Start()
    {
        if (interactPromptUI != null) interactPromptUI.SetActive(false);
        
        GameObject p = OpeningEvent.FindPlayerInScene();
        if (p != null) playerTransform = p.transform;
    }

    void Update()
    {
        if (playerTransform == null)
        {
            GameObject p = OpeningEvent.FindPlayerInScene();
            if (p != null) playerTransform = p.transform;
            return;
        }

        var story = BachDangStoryManager.Instance;
        if (story == null || story.currentState != BachDangStoryManager.StoryState.Act2_CollectWoodInVillage)
        {
            isPlayerNear = false;
            if (interactPromptUI != null) interactPromptUI.SetActive(false);
            return;
        }

        if (!playerTransform.gameObject.activeInHierarchy || StoryDialogueUI.InputConsumedThisFrame || StoryDialogueUI.Instance?.IsOpen == true)
        {
            if (interactPromptUI != null) interactPromptUI.SetActive(false);
            return;
        }

        WoodCollectible nearest = FindNearest(playerTransform.position);
        isPlayerNear = nearest == this;
        if (interactPromptUI != null) interactPromptUI.SetActive(isPlayerNear);
        if (!isPlayerNear) return;

        story.SetInteractPrompt(true, $"[E] Nhặt cọc gỗ ({story.collectedWood}/{story.totalWoodRequired})");
        if (Input.GetKeyDown(KeyCode.E) && lastCollectFrame != Time.frameCount && StoryDialogueUI.TryConsumeInput())
        {
            lastCollectFrame = Time.frameCount;
            collected = true;
            story.OnCollectWood();
            story.SetInteractPrompt(false);
            if (interactPromptUI != null) interactPromptUI.SetActive(false);
            Destroy(gameObject);
        }
    }

    private WoodCollectible FindNearest(Vector3 playerPosition)
    {
        WoodCollectible nearest = null;
        float bestDistance = interactDistance;
        var items = FindObjectsByType<WoodCollectible>();
        foreach (var item in items)
        {
            if (item == null || item.collected) continue;
            float distance = Vector3.Distance(item.transform.position, playerPosition);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                nearest = item;
            }
        }
        return nearest;
    }

    private void OnDestroy()
    {
        if (BachDangStoryManager.Instance != null && isPlayerNear)
        {
            BachDangStoryManager.Instance.SetInteractPrompt(false);
        }
    }
}
