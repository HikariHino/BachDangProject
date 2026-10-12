using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class OpeningEvent : MonoBehaviour
{
    [Header("UI")]
    public GameObject openingPanel;
    public TMP_Text storyText;

    [Header("Player")]
    public GameObject player;

    private Canvas[] hiddenCanvases;
    private MonoBehaviour[] hiddenScripts;

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
        // Ẩn tất cả Canvas UI
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
            storyText.fontSize = 28; // Thu nhỏ chữ
            storyText.color = new Color(storyText.color.r, storyText.color.g, storyText.color.b, 0);
        }

        if (player != null)
            player.SetActive(false);

        StartCoroutine(PlayOpening());
    }

    IEnumerator PlayOpening()
    {
        yield return new WaitForSeconds(1.5f); // Đợi 1 chút tĩnh lặng đầu game

        for (int i = 0; i < storyLines.Length; i++)
        {
            if (storyText != null)
            {
                storyText.text = storyLines[i];
                
                // Fade in (hiện từ từ)
                yield return StartCoroutine(FadeText(0f, 1f, 1.5f));
                
                // Đợi người chơi đọc (câu dài đợi lâu hơn, câu ngắn lướt nhanh)
                float waitTime = Mathf.Clamp(storyLines[i].Length * 0.08f, 2.5f, 5f);
                yield return new WaitForSeconds(waitTime);
                
                // Fade out (biến mất từ từ)
                yield return StartCoroutine(FadeText(1f, 0f, 1.5f));
                
                yield return new WaitForSeconds(0.5f); // Quãng nghỉ giữa 2 câu
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
                while (t < 2f)
                {
                    t += Time.deltaTime;
                    bgImage.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t / 2f));
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
    }
}