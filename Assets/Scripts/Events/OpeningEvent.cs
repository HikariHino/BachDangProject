using UnityEngine;
using TMPro;
using System.Collections;

public class OpeningEvent : MonoBehaviour
{
    [Header("UI")]
    public GameObject openingPanel;
    public TMP_Text storyText;

    [Header("Player")]
    public GameObject player;

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
        openingPanel.SetActive(true);

        if (player != null)
            player.SetActive(false);

        StartCoroutine(PlayOpening());
    }

    IEnumerator PlayOpening()
    {
        for (int i = 0; i < storyLines.Length; i++)
        {
            storyText.text = storyLines[i];

            yield return new WaitForSeconds(3f);
        }

        EndOpening();
    }

    void EndOpening()
    {
        openingPanel.SetActive(false);

        if (player != null)
            player.SetActive(true);
    }
}