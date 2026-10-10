using UnityEngine;

/// <summary>
/// Vùng bờ sông Bạch Đằng nơi người chơi thực hiện nhiệm vụ đóng cọc.
/// Khi đã nhận lệnh từ Ngô Quyền ở Hồi 2, người chơi đến mép nước bấm [E] để đóng cọc.
/// </summary>
public class RiverStakeZone : MonoBehaviour
{
    [Header("Cự ly tương tác")]
    public float interactDistance = 15f;

    [Header("Nhóm cọc gỗ tại bờ sông")]
    public GameObject stakeGroupObject;

    private bool isPlayerInRange = false;
    private bool isStakesPlanted = false;

    private void Update()
    {
        if (isStakesPlanted) return;

        var story = BachDangStoryManager.Instance;
        if (story == null) return;

        if (story.currentState != BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver)
        {
            if (isPlayerInRange)
            {
                isPlayerInRange = false;
                story.SetInteractPrompt(false);
            }
            return;
        }

        Transform player = story.GetPlayerTransform();
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= interactDistance)
        {
            if (!isPlayerInRange)
            {
                isPlayerInRange = true;
                story.SetInteractPrompt(true, "[E] Đóng cọc xuống lòng sông Bạch Đằng");
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                PlantStakes();
            }
        }
        else
        {
            if (isPlayerInRange)
            {
                isPlayerInRange = false;
                story.SetInteractPrompt(false);
            }
        }
    }

    private void PlantStakes()
    {
        var story = BachDangStoryManager.Instance;
        if (story == null) return;

        isStakesPlanted = true;
        story.SetInteractPrompt(false);

        string narrative = "Dưới sự chỉ huy thần tốc của Chủ tướng Ngô Quyền,\ntoàn quân dốc sức cắm từng hàng cọc nhọn bọc sắt sâu vào lòng sông Bạch Đằng...";

        ScreenFader.Instance.FadeTransition(narrative, () =>
        {
            // Bật hoặc định hình nhóm cọc gỗ
            if (stakeGroupObject != null)
            {
                stakeGroupObject.SetActive(true);
            }

            story.OnPlantStakesComplete();
        }, 1f, 2.5f);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
