using UnityEngine;

public class WoodCollectible : MonoBehaviour
{
    [Header("Giao dien (Chữ [E] Nhặt)")]
    public GameObject interactPromptUI;

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
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            return;
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        isPlayerNear = (dist < 15f);

        if (isPlayerNear && playerTransform.gameObject.activeInHierarchy)
        {
            if (interactPromptUI != null && !interactPromptUI.activeSelf)
                interactPromptUI.SetActive(true);

            if (Input.GetKeyDown(KeyCode.E))
            {
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
}
