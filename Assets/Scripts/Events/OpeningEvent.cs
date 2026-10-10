using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// Đoạn dẫn truyện mở đầu "Năm 938...".
/// Đã hỗ trợ BỎ QUA TỨC THÌ (bấm Space / E / Enter / Click) để người chơi không phải chờ đợi!
/// Tự động ẩn các UI nhiệm vụ cũ trong lúc đang chạy intro để không bị lỗi chữ "Chưa có nhiệm vụ nào".
/// </summary>
public class OpeningEvent : MonoBehaviour
{
    [Header("UI")]
    public GameObject openingPanel;
    public TMP_Text storyText;

    [Header("Player")]
    public GameObject player;

    private Canvas[] hiddenCanvases;
    private MonoBehaviour[] hiddenScripts;
    private GameObject[] hiddenQuestPanels;
    private Coroutine openingCoroutine;
    private bool isOpeningEnded = false;

    private string[] storyLines =
    {
        "Năm 938.",
        "Sau nhiều năm đất nước chìm trong thời kỳ Bắc thuộc, vùng đất Giao Châu vẫn đang đứng trước những biến động lớn.",
        "Nhà Nam Hán ở phương Bắc đang tìm cách đưa quân tiến xuống Giao Châu, với ý định giành lại quyền kiểm soát vùng đất này.",
        "Trước nguy cơ xâm lược, Ngô Quyền tập hợp lực lượng, chuẩn bị chống lại quân Nam Hán.",
        "Ngô Quyền lựa chọn sông Bạch Đằng làm nơi quyết chiến.",
        "Tại đây, quân ta chuẩn bị một kế sách dựa vào địa hình và thủy triều của dòng sông.",
        "Những cọc gỗ được đẽo nhọn và bố trí dưới lòng sông, chờ thời cơ thích hợp.",
        "Nhưng trước khi trận chiến bắt đầu..."
    };

    void Start()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Main_Character");
        }

        // Tạm ẩn các bảng nhiệm vụ cũ để không bị lộ chữ "Chưa có nhiệm vụ nào" trên màn hình đen
        HideExistingQuestPanels();

        // Ẩn tất cả Canvas UI không liên quan
        hiddenCanvases = FindObjectsOfType<Canvas>();
        foreach (Canvas c in hiddenCanvases)
        {
            if (c.gameObject.name != "Canvas_Opening" && (openingPanel == null || c.gameObject.name != openingPanel.transform.parent.name))
            {
                c.enabled = false;
            }
        }

        // Ẩn luôn các UI OnGUI cứng đầu (như Thủy triều, SaBanHUD)
        hiddenScripts = FindObjectsOfType<MonoBehaviour>();
        foreach (MonoBehaviour mb in hiddenScripts)
        {
            if (mb != null)
            {
                string typeName = mb.GetType().Name;
                if (typeName == "SaBanHUD" || typeName == "TideSystem")
                {
                    mb.enabled = false;
                }
            }
        }

        if (openingPanel != null)
            openingPanel.SetActive(true);
            
        if (storyText != null)
        {
            storyText.fontStyle = FontStyles.Italic;
            storyText.fontSize = 28;
            storyText.color = new Color(storyText.color.r, storyText.color.g, storyText.color.b, 0);
        }

        if (player != null)
            player.SetActive(false);

        openingCoroutine = StartCoroutine(PlayOpening());
    }

    void Update()
    {
        // Cho phép người chơi BẤM PHÍM BẤT KỲ ĐỂ BỎ QUA ĐOẠN DẪN TRUYỆN MỞ ĐẦU
        if (!isOpeningEnded)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || 
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0))
            {
                SkipOpening();
            }
        }
    }

    private void HideExistingQuestPanels()
    {
        string[] panelNames = { "QuestPanel", "QuestUIPanel", "QuestDesc", "QuestTitle", "NhiemVuPanel" };
        var found = new System.Collections.Generic.List<GameObject>();
        foreach (string name in panelNames)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null && obj.activeSelf)
            {
                obj.SetActive(false);
                found.Add(obj);
            }
        }
        hiddenQuestPanels = found.ToArray();
    }

    public void SkipOpening()
    {
        if (isOpeningEnded) return;

        if (openingCoroutine != null)
        {
            StopCoroutine(openingCoroutine);
        }

        EndOpening();
    }

    IEnumerator PlayOpening()
    {
        yield return new WaitForSeconds(1.0f);

        for (int i = 0; i < storyLines.Length; i++)
        {
            if (storyText != null)
            {
                storyText.text = storyLines[i] + "\n\n<size=16><color=#AAAAAA>[Nhấn Space hoặc E để vào game]</color></size>";
                
                // Fade in
                yield return StartCoroutine(FadeText(0f, 1f, 1.0f));
                
                float waitTime = Mathf.Clamp(storyLines[i].Length * 0.06f, 2f, 4f);
                yield return new WaitForSeconds(waitTime);
                
                // Fade out
                yield return StartCoroutine(FadeText(1f, 0f, 0.8f));
                
                yield return new WaitForSeconds(0.3f);
            }
        }

        // Fade out cả màn hình đen mờ dần ra cảnh game
        if (openingPanel != null)
        {
            Image bgImage = openingPanel.GetComponent<Image>();
            if (bgImage != null)
            {
                float t = 0;
                Color startColor = bgImage.color;
                while (t < 1.5f)
                {
                    t += Time.deltaTime;
                    bgImage.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t / 1.5f));
                    yield return null;
                }
            }
        }

        EndOpening();
    }

    IEnumerator FadeText(float startAlpha, float endAlpha, float duration)
    {
        if (storyText == null) yield break;

        float t = 0;
        Color c = storyText.color;
        while (t < duration)
        {
            t += Time.deltaTime;
            storyText.color = new Color(c.r, c.g, c.b, Mathf.Lerp(startAlpha, endAlpha, t / duration));
            yield return null;
        }
        storyText.color = new Color(c.r, c.g, c.b, endAlpha);
    }

    void EndOpening()
    {
        if (isOpeningEnded) return;
        isOpeningEnded = true;

        if (openingPanel != null)
            openingPanel.SetActive(false);
            
        if (player != null)
            player.SetActive(true);
            
        // Bật lại các UI Canvas đã giấu
        if (hiddenCanvases != null)
        {
            foreach (Canvas c in hiddenCanvases)
            {
                if (c != null) c.enabled = true;
            }
        }

        // Bật lại các Script OnGUI
        if (hiddenScripts != null)
        {
            foreach (MonoBehaviour mb in hiddenScripts)
            {
                if (mb != null) mb.enabled = true;
            }
        }

        // Cập nhật lại UI cốt truyện Bạch Đằng Hồi 1
        if (BachDangStoryManager.Instance != null)
        {
            BachDangStoryManager.Instance.UpdateUI();
        }
    }
}