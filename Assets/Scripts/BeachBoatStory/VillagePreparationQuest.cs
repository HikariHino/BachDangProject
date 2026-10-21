using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Four physical village trades prepare the supplies required by Act 2.</summary>
    public sealed class VillagePreparationQuest : MonoBehaviour
    {
        public static VillagePreparationQuest Instance {get;private set;}
        public VillageWorkStall[] stalls = new VillageWorkStall[0];
        public int CompletedCount
        { get { int count=0;foreach(var stall in stalls)if(stall!=null&&stall.Completed)count++;return count; } }
        public bool CanWork
        {
            get
            {
                var story=BachDangStoryManager.Instance;var session=BeachBoatQuestSession.Instance;
                return isActiveAndEnabled&&story!=null&&session!=null&&session.storyMode&&!BeachBoatQuestSession.InteractionBlocked&&
                    BeachBoatMinigameStation.Active==null&&(QuestRowboat.Instance==null||!QuestRowboat.Instance.IsAboard)&&
                    story.currentState!=BachDangStoryManager.StoryState.Act1_Training&&story.currentState!=BachDangStoryManager.StoryState.Act3_BattleInProgress;
            }
        }
        void Awake(){Instance=this;}
        void Start(){VillageWorkUI.EnsureExists();}
        void OnDestroy(){if(Instance==this)Instance=null;}
        public void BeginPreparation(){foreach(var stall in stalls)if(stall!=null)stall.ResetCollection();}
        public VillageWorkStall NearestUnfinished(Vector3 from)
        {
            VillageWorkStall best=null;float distance=float.PositiveInfinity;
            foreach(var stall in stalls)
                if(stall!=null&&stall.isActiveAndEnabled&&!stall.Completed)
                {float d=(stall.InteractionPosition-from).sqrMagnitude;if(d<distance){distance=d;best=stall;}}
            return best;
        }
        public void Collect(VillageWorkStall stall)
        {
            if(stall==null||stall.Completed)return;
            stall.MarkCollected();
            QuestCheckpoint.Instance?.RefreshVillageProgress();
            var story=BachDangStoryManager.Instance;
            if(CompletedCount==stalls.Length&&stalls.Length>0&&story!=null&&story.currentState==BachDangStoryManager.StoryState.Act2_SupplyPreparation)
            {
                if(StoryDialogueUI.Instance!=null)StoryDialogueUI.Instance.Show("Bà con làng ven sông","Gỗ, dây buộc, gạo và lương khô đã chuẩn bị đủ. Hãy về doanh trại báo cáo Ngô Quyền.",story.OnSupplyPreparationComplete);
                else story.OnSupplyPreparationComplete();
            }
            story?.UpdateUI();
        }
    }
}
