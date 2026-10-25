using System;
using System.Linq;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Per-journey records; serialized in each slot, never in global preferences.</summary>
    public sealed class CampaignProgress : MonoBehaviour
    {
        public static CampaignProgress Instance {get;private set;}
        public BachDangSaveData.Campaign Record {get;private set;}=new BachDangSaveData.Campaign();
        public static readonly int[] StoryOrder={0,12,1,2,13,3,4,5,6,7,8,9,10,11,14};
        public static readonly string[] MissionTitles={"Nhận lệnh Ngô Quyền","Luyện hiệu lệnh trống trận","Rời doanh trại tới làng","Thu gom cọc gỗ","Chuẩn bị đủ bốn loại vật tư","Trở về doanh trại","Báo cáo vật tư với Ngô Quyền","Dựng đủ năm cụm cọc","Báo cáo hoàn thành trận địa","Chờ lệnh xuất chiến","Nhận lệnh đánh giặc","Xuất chiến trên sông","Đánh chìm hạm đội và hạ hết địch","Báo công với Ngô Quyền","Hoàn thành chiến dịch Bạch Đằng"};
        public const int ScoredMinigames=13; // Drum plus four village trades at three difficulties.
        float stakeTarget;
        void Awake(){Instance=this;}
        void Start(){Visit((int)BachDangStoryManager.Instance.currentState);var ui=new GameObject("CampaignJournal");ui.transform.SetParent(transform,false);ui.AddComponent<CampaignJournalUI>();}
        void Update()
        {
            if(Record.finished||Time.timeScale<=0||!(BachDangFrontEnd.Instance?.GameStarted??false)||OpeningEvent.IsOpeningActive||StoryDialogueUI.Instance.IsOpen||QuestCheckpoint.Instance.IsDefeated)return;
            float dt=Mathf.Max(0,Time.unscaledDeltaTime);Record.seconds+=dt;
            if(Record.stakeStarted&&!Record.stakeFinished)Record.stakeSeconds+=dt;
        }
        public void Visit(int state){Record.visitedMask|=1<<state;}
        public void Transition(int previous,int next)
        {
            int from=Array.IndexOf(StoryOrder,previous),to=Array.IndexOf(StoryOrder,next);
            if(to>from&&from>=0)for(int i=from;i<to;i++)Record.completedMask|=1<<StoryOrder[i];Visit(next);
            if(next==(int)BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp)Record.stakeFinished=true;
            if(next==(int)BachDangStoryManager.StoryState.Act3_Celebration)Finish();
        }
        public bool MissionDone(int index)
        {
            if(index<0||index>=StoryOrder.Length)return false;
            if(StoryOrder[index]==13&&VillagePreparationQuest.Instance!=null)return VillagePreparationQuest.Instance.CompletedCount==VillagePreparationQuest.Instance.stalls.Length&&VillagePreparationQuest.Instance.stalls.Length>0;
            return (Record.completedMask&(1<<StoryOrder[index]))!=0;
        }
        public bool MissionCurrent(int index)=>BachDangStoryManager.Instance!=null&&(int)BachDangStoryManager.Instance.currentState==StoryOrder[index]&&!MissionDone(index);
        public int CompletedMissions=>Enumerable.Range(0,StoryOrder.Length).Count(MissionDone);
        public void StartStakes(){if(!Record.finished)Record.stakeStarted=true;}
        public void RecordDefeat(){if(!Record.finished)Record.defeats++;}
        public void RecordCasualty(){if(!Record.finished)Record.casualties++;}
        public void RecordFriendlyBoarding(){if(!Record.finished)Record.friendlyBoardings++;}
        public void RecordKill(EnemyDeckGuard guard)
        {
            if(Record.finished)return;string id=BachDangSaveSystem.GuardId(guard);
            if(!Record.kills.Contains(id))Record.kills=Record.kills.Concat(new[]{id}).ToArray();
        }
        public void RecordMinigame(string id,float seconds,float limit)
        {
            if(Record.finished||string.IsNullOrEmpty(id)||float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0||limit<=0)return;
            var old=Record.minigames.FirstOrDefault(x=>x.id==id);
            if(old==null){old=new BachDangSaveData.MiniRecord{id=id,seconds=seconds,limit=limit};Record.minigames=Record.minigames.Concat(new[]{old}).ToArray();}
            else if(seconds<old.seconds){old.seconds=seconds;old.limit=limit;}
        }
        public void BeginBattle(){Record.enemyCount=Mathf.Max(1,RiverPlayerCombat.Instance.guards.Length);}
        public int KillTarget=>Mathf.Max(1,Mathf.CeilToInt(Record.enemyCount*.25f));
        public float StakeTargetSeconds
        {
            get
            {
                if(stakeTarget>0)return stakeTarget;
                var zone=RiverStakeZone.Instance;var boat=QuestRowboat.Instance;if(zone==null||boat==null||zone.ClusterCount==0)return 180;
                var remaining=zone.placements.Where(p=>p?.approach!=null).Select(p=>p.approach.position).ToList();float distance=0;
                if(remaining.Count>0){var at=remaining[0];remaining.RemoveAt(0);while(remaining.Count>0){var next=remaining.OrderBy(p=>QuestRowboat.FlatDistance(at,p)).First();distance+=QuestRowboat.FlatDistance(at,next);at=next;remaining.Remove(next);}}
                stakeTarget=Mathf.Max(180,distance/Mathf.Max(1,boat.maxSpeed*.65f)+zone.ClusterCount*zone.plantingDuration+90);return stakeTarget;
            }
        }
        public static float MiniEfficiency(float ratio)=>ratio<=.4f?35:ratio<=.6f?28:ratio<=.8f?20:12;
        public float MiniPoints
        {
            get
            {
                float sum=0;int found=0;
                for(int i=0;i<ScoredMinigames;i++)
                {
                    string id=i==0?"drum":"village:"+((i-1)/3)+":"+((i-1)%3);
                    var m=Record.minigames.FirstOrDefault(x=>x.id==id);
                    if(m!=null){sum+=MiniEfficiency(m.seconds/Mathf.Max(1,m.limit));found++;}
                }
                if(Record.historyIncomplete)sum+=(ScoredMinigames-found)*20;
                return sum/ScoredMinigames;
            }
        }
        public float StakePoints=>!Record.stakeFinished?0:!Record.stakeStarted&&Record.historyIncomplete?15:Record.stakeSeconds<=StakeTargetSeconds?25:Record.stakeSeconds<=StakeTargetSeconds*1.5f?20:Record.stakeSeconds<=StakeTargetSeconds*2?15:8;
        public float KillPoints=>25*Mathf.Clamp01(Record.kills.Length/(float)KillTarget);
        public float TimePoints=>Record.historyIncomplete?8:Record.seconds<=40*60?15:Record.seconds<=60*60?12:Record.seconds<=90*60?8:4;
        public int Score=>Mathf.Clamp(Mathf.RoundToInt(MiniPoints+StakePoints+KillPoints+TimePoints),0,100);
        public static string RankFor(int score)=>score>=90?"S":score>=75?"A":score>=60?"B":"C";
        public void Finish()
        {
            if(Record.finished)return;Record.completedMask|=1<<(int)BachDangStoryManager.StoryState.Act3_Celebration;
            Record.finalScore=Score;Record.finalRank=RankFor(Record.finalScore);Record.finished=true;
        }
        public BachDangSaveData.Campaign Capture()=>JsonUtility.FromJson<BachDangSaveData.Campaign>(JsonUtility.ToJson(Record));
        public void Restore(BachDangSaveData.Campaign data,int state)
        {
            Record=data??new BachDangSaveData.Campaign{historyIncomplete=true};Record.minigames=Record.minigames??Array.Empty<BachDangSaveData.MiniRecord>();Record.kills=Record.kills??Array.Empty<string>();
            if(data==null)
            {
                int index=Array.IndexOf(StoryOrder,state);for(int i=0;i<index;i++)Record.completedMask|=1<<StoryOrder[i];
                Record.stakeFinished=index>Array.IndexOf(StoryOrder,5);
                foreach(var station in FindObjectsByType<BeachBoatMinigameStation>())if(station.kind==BeachBoatMinigameStation.ActivityKind.WarDrum&&station.HasCompleted&&station.BestTime>0)RecordMinigame("drum",station.BestTime,station.TimeLimit);
                var combat=RiverPlayerCombat.Instance;Record.enemyCount=Mathf.Max(1,combat.guards.Length);
                // Old saves contain a real kill count but no per-enemy identities.
                Record.kills=Enumerable.Range(0,combat.Kills).Select(i=>"legacy:"+i).ToArray();
            }
            Visit(state);if(state==(int)BachDangStoryManager.StoryState.Act3_Celebration&&!Record.finished)Finish();
        }
        public static string Duration(float seconds){var t=TimeSpan.FromSeconds(Mathf.Max(0,seconds));return t.TotalHours>=1?$"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}":$"{t.Minutes:00}:{t.Seconds:00}";}
        public string RankingRules=>$"TỔNG 100 ĐIỂM • S ≥ 90 • A ≥ 75 • B ≥ 60 • C < 60\n\nMINIGAME — 35 ĐIỂM\nLấy lần thắng nhanh nhất của trống trận và 12 mức Dễ/Vừa/Khó tại 4 sạp. Mỗi mức dùng tỷ lệ thời gian / giới hạn của chính mức đó: ≤40%: 35đ; ≤60%: 28đ; ≤80%: 20đ; còn lại: 12đ. Lấy trung bình 13 mức. Thời gian phạt lỗi vẫn tính.\n\nĐÓNG CỌC — 25 ĐIỂM\nTừ lúc bắt đầu cụm đầu tới khi xong đủ 5 cụm, gồm chèo và vớt cọc. ≤{Duration(StakeTargetSeconds)}: 25đ; ≤{Duration(StakeTargetSeconds*1.5f)}: 20đ; ≤{Duration(StakeTargetSeconds*2)}: 15đ; lâu hơn: 8đ. Mốc tính từ độ dài tuyến, tốc độ thuyền và thời gian thao tác, tối thiểu 3 phút.\n\nHẠ ĐỊCH — 25 ĐIỂM\n25 × min(số địch bạn tự hạ / {KillTarget}, 1). Cung và kiếm đều tính; đồng minh hạ không tính. Mỗi mã địch chỉ tính một lần để tránh cày lính hồi sinh.\n\nTOÀN CHIẾN DỊCH — 15 ĐIỂM\n≤40 phút: 15đ; ≤60 phút: 12đ; ≤90 phút: 8đ; lâu hơn: 4đ. Tính cả minigame, chèo và chiến đấu; bỏ qua sảnh, tạm dừng, bảng Tab, cốt truyện và thoại.\n\nHạng chỉ chốt sau khi thắng và báo công với Ngô Quyền. Chơi lại minigame có thể cải thiện kỷ lục trước khi kết thúc. Lưu/tải không đặt lại đồng hồ. Save cũ thiếu thời gian sẽ ghi TẠM TÍNH. Chỉ các mục thiếu dữ liệu dùng mức trung gian: minigame 20/35 mỗi mức, đóng cọc 15/25, tổng thời gian 8/15; các số đã ghi vẫn tính theo thực tế. Thành tích không gục ngã/không thương vong/hạng S chỉ trao cho hành trình có đủ thống kê.";
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
