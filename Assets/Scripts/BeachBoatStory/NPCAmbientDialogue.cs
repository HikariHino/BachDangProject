using UnityEngine;

namespace BeachBoatStory
{
    [DisallowMultipleComponent]
    public sealed class NPCAmbientDialogue : MonoBehaviour
    {
        public enum Role { Villager, Farmer, Fisher, Merchant, Porter, Smith, Guard }
        public string displayName="Dân làng";
        public Role role;
        public float interactDistance=12;
        public NPCWander dailyLife;
        Transform player;
        bool showingPrompt;
        int lineIndex;

        static readonly string[][] Lines=
        {
            new[]{"Làng ven sông hôm nay đông vui hơn mọi ngày.","Nhà tôi đang hong thóc, lát nữa còn phải vá lại mái tranh.","Mọi người đều góp sức chuẩn bị cho những ngày sắp tới."},
            new[]{"Ruộng ngoài bãi phù sa tốt lắm, mùa này lúa lên đều.","Tôi đang mang nông cụ ra đồng rồi sẽ về phơi thóc.","Nước rút là bà con tranh thủ làm đất ngay."},
            new[]{"Sáng sớm cá về nhiều, tôi đang sửa lưới để kịp chuyến sau.","Khúc sông này phải thuộc con nước mới chèo thuyền an toàn.","Tôi đang chuyển cá vào chợ trước khi nắng lên."},
            new[]{"Gạo, cá khô và chum nước đều đã được chuyển vào kho.","Chợ họp từ sáng, bà con đổi cá lấy thóc và củi.","Tôi đang kiểm lại hàng trước khi mang sang doanh trại."},
            new[]{"Kho lương phải luôn đầy để quân sĩ yên tâm luyện tập.","Tôi đang chuyển bó củi này sang bếp trại.","Đường qua cổng đã dọn trống, xe hàng có thể đi lại."},
            new[]{"Lò rèn phải đỏ lửa cả ngày để sửa giáo và nông cụ.","Tôi đang tôi lại mũi giáo, tránh để thép quá giòn.","Búa, kìm và than đã đủ cho một ngày làm việc."},
            new[]{"Tôi đang tuần tra từ cổng đến kho lương.","Cổng trại luôn phải thông thoáng để quân có thể xuất phát nhanh.","Ca gác này yên ổn, nhưng chúng tôi vẫn không lơ là."}
        };

        void Start(){dailyLife=dailyLife!=null?dailyLife:GetComponent<NPCWander>();FindPlayer();}
        void OnEnable(){NearbyConversation.Register(this);}
        void FindPlayer(){var go=OpeningEvent.FindPlayerInScene();player=go!=null?go.transform:null;}
        void Update()
        {
            if(player==null){FindPlayer();return;}
            if(BeachBoatQuestSession.InputBlocked||(StoryDialogueUI.Instance!=null&&StoryDialogueUI.Instance.IsOpen)){HidePrompt();return;}
            bool near=NearbyConversation.IsPreferred(this);
            if(!near){HidePrompt();return;}
            var story=BachDangStoryManager.Instance;if(story==null)return;
            showingPrompt=true;story.SetInteractPrompt(true,$"[E] Trò chuyện với {displayName}",this);
            if(Input.GetKeyDown(KeyCode.E)&&StoryDialogueUI.TryConsumeInput())Talk();
        }
        public bool Talk()
        {
            if(player==null)FindPlayer();
            if(player==null||!NearbyConversation.InReach(transform,interactDistance)||!NearbyConversation.IsPreferred(this)||BeachBoatQuestSession.InteractionBlocked)return false;
            dailyLife?.PauseForConversation(player);
            var lines=Lines[Mathf.Clamp((int)role,0,Lines.Length-1)];
            string activity=dailyLife!=null&&!string.IsNullOrWhiteSpace(dailyLife.CurrentActivity)?dailyLife.CurrentActivity+". ":"";
            StoryDialogueUI.Instance?.Show(displayName,activity+lines[lineIndex++%lines.Length],null,transform);
            HidePrompt();return true;
        }
        void HidePrompt(){if(!showingPrompt)return;showingPrompt=false;BachDangStoryManager.Instance?.SetInteractPrompt(false,owner:this);}
        void OnDisable(){HidePrompt();NearbyConversation.Remove(this);}
    }
}
