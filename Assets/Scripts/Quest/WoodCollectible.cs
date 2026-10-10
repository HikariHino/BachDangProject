using UnityEngine;

/// <summary>
/// Khúc cọc gỗ người chơi nhặt tại Làng Ven Sông trong Hồi 2.
/// </summary>
public class WoodCollectible : MonoBehaviour
{
    [Header("Giao diện (Chữ [E] Nhặt)")]
    public GameObject interactPromptUI;

    public float interactDistance = 8f;
    private bool isPlayerNear = false;
    private Transform playerTransform;

    void Start()
    {
        if (interactPromptUI == null)
            interactPromptUI = GameObject.Find("InteractPrompt");

        if (interactPromptUI != null) interactPromptUI.SetActive(false);
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) p = GameObject.Find("Main_Character");
        if (p == null) p = GameObject.Find("Player_Main_Animated");
        if (p != null) playerTransform = p.transform;
    }

    void Update()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player_Main_Animated") ?? GameObject.Find("Main_Character");
            if (p != null) playerTransform = p.transform;
            return;
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        isPlayerNear = (dist < interactDistance);

        if (isPlayerNear && playerTransform.gameObject.activeInHierarchy)
        {
            if (interactPromptUI != null && !interactPromptUI.activeSelf)
                interactPromptUI.SetActive(true);

            if (BachDangStoryManager.Instance != null)
                BachDangStoryManager.Instance.SetInteractPrompt(true, "[E] Nhặt cọc gỗ");

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (BachDangStoryManager.Instance != null)
                {
                    BachDangStoryManager.Instance.OnCollectWood();
                    BachDangStoryManager.Instance.SetInteractPrompt(false);
                }

                if (QuestManager.Instance != null)
                {
                    QuestManager.Instance.AddWood();
                }
                
                if (interactPromptUI != null) interactPromptUI.SetActive(false);
                Destroy(gameObject);
            }
        }
        else
        {
            if (interactPromptUI != null && interactPromptUI.activeSelf)
                interactPromptUI.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (BachDangStoryManager.Instance != null)
        {
            BachDangStoryManager.Instance.SetInteractPrompt(false);
        }
    }
}
