using UnityEngine;

public class WoodCollectible : MonoBehaviour
{
    [Header("Giao dien (Chữ [E] Nhặt)")]
    public GameObject interactPromptUI;

    private bool isPlayerNear = false;
    private Transform playerTransform;

    void Start()
    {
        if (interactPromptUI != null) interactPromptUI.SetActive(false);
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;
    }

    void Update()
    {
        if (playerTransform == null) return;

        // Tinh khoang cach (do nhan vat khong lo 7x, ban kinh nhan = 15)
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        isPlayerNear = (dist < 15f);

        if (isPlayerNear && playerTransform.gameObject.activeInHierarchy)
        {
            if (interactPromptUI != null && !interactPromptUI.activeSelf)
                interactPromptUI.SetActive(true);

            // Nguoi choi bam E de nhat
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (QuestManager.Instance != null && QuestManager.Instance.hasActiveQuest)
                {
                    QuestManager.Instance.AddWood();
                    
                    // Xoa chu [E]
                    if (interactPromptUI != null) interactPromptUI.SetActive(false);
                    
                    // Huy model go
                    Destroy(gameObject);
                }
            }
        }
        else
        {
            if (interactPromptUI != null && interactPromptUI.activeSelf)
                interactPromptUI.SetActive(false);
        }
    }
}
