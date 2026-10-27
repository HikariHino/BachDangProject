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
        public const int RankRulesVersion=3;
        public static readonly string[] WorkNames={"Sạp gỗ","Nối dây","Hàng gạo","Gói lương khô"};
        public static readonly string[] DifficultyNames={"Dễ","Vừa","Khó"};
        float stakeTarget;
        void Awake(){Instance=this;}
        void Start(){Visit((int)BachDangStoryManager.Instance.currentState);var ui=new GameObject("CampaignJournal");ui.transform.SetParent(transform,false);ui.AddComponent<CampaignJournalUI>();}
        void Update()
        {
            if(RationsTutorialVideo.IsWatching||VillageWorkStall.Active?.IsPractice==true||Record.finished||Time.timeScale<=0||!(BachDangFrontEnd.Instance?.GameStarted??false)||OpeningEvent.IsOpeningActive||StoryDialogueUI.Instance.IsOpen||QuestCheckpoint.Instance.IsDefeated)return;
            float dt=Mathf.Max(0,Time.unscaledDeltaTime);Record.seconds+=dt;
            if(Record.stakeStarted&&!Record.stakeFinished)Record.stakeSeconds+=dt;
            if(Record.woodStarted&&!Record.woodFinished&&BachDangStoryManager.Instance.currentState==BachDangStoryManager.StoryState.Act2_CollectWoodInVillage&&!(ScreenFader.Instance?.IsTransitioning??false))Record.woodSeconds+=dt;
        }
        public void Visit(int state){Record.visitedMask|=1<<state;}
        public void Transition(int previous,int next)
        {
            int from=Array.IndexOf(StoryOrder,previous),to=Array.IndexOf(StoryOrder,next);
            if(to>from&&from>=0)for(int i=from;i<to;i++)Record.completedMask|=1<<StoryOrder[i];Visit(next);
            if(next==(int)BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp)Record.stakeFinished=true;
            if(next==(int)BachDangStoryManager.StoryState.Act2_CollectWoodInVillage&&!Record.woodStarted&&BachDangStoryManager.Instance.collectedWood==0)
            {Record.woodStarted=true;Record.woodTargetSeconds=VillageWoodHunt.Instance?.TargetSeconds??180;}
            if(previous==(int)BachDangStoryManager.StoryState.Act2_CollectWoodInVillage&&next==(int)BachDangStoryManager.StoryState.Act2_SupplyPreparation&&Record.woodStarted)Record.woodFinished=true;
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
            if(Record.finished||MiniIndex(id)<0||float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0||!FinitePositive(limit)||seconds>=limit)return;
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
        public static string MiniId(int index)=>index==0?"drum":$"village:{(index-1)/3}:{(index-1)%3}";
        public static int MiniIndex(string id){for(int i=0;i<ScoredMinigames;i++)if(MiniId(i)==id)return i;return -1;}
        public static string MiniName(int index)=>index==0?"Hiệu lệnh trống trận":WorkNames[(index-1)/3]+" • "+DifficultyNames[(index-1)%3];
        public static float MiniLimit(int index)=>index==0?BeachBoatMinigameStation.WarDrumTimeLimit:VillageWorkStall.LimitFor((VillageWorkStall.WorkKind)((index-1)/3),(index-1)%3);
        public static string RankName(int tier)=>tier==3?"S":tier==2?"A":tier==1?"B":"C";
        public static float MiniTarget(int index,int tier)=>Mathf.Round(MiniLimit(index)*(index==0?(tier==3?.4f:tier==2?.6f:.8f):(tier==3?.6f:tier==2?.8f:.95f))*100)/100;
        public static float CampaignTarget(int tier)=>(tier==3?40:tier==2?60:90)*60;
        public float StakeTarget(int tier)=>StakeTargetSeconds*(tier==3?1:tier==2?1.5f:2);
        public float WoodTarget(int tier)=>Mathf.Max(180,Record.woodTargetSeconds>0?Record.woodTargetSeconds:VillageWoodHunt.Instance?.TargetSeconds??180)*(tier==3?1:tier==2?1.5f:2);
        public bool WoodTimeKnown=>Record.woodStarted&&Record.woodFinished&&FinitePositive(Record.woodTargetSeconds)&&!float.IsNaN(Record.woodSeconds)&&!float.IsInfinity(Record.woodSeconds)&&Record.woodSeconds>=0;
        public int WoodRank{get{if(WoodTimeKnown)for(int tier=3;tier>=1;tier--)if(Record.woodSeconds<=WoodTarget(tier))return tier;return 0;}}
        public string WoodBands=>$"S ≤ {Clock(WoodTarget(3))}  •  A ≤ {Clock(WoodTarget(2))}  •  B ≤ {Clock(WoodTarget(1))}";
        public string WoodResult=>WoodTimeKnown?$"Đủ 5 khúc: {Clock(Record.woodSeconds)} • Hạng {RankName(WoodRank)}":Record.woodStarted?"Đang tìm: "+Clock(Record.woodSeconds):"Chưa ghi thời gian tìm gỗ";
        public int KillRequirement(int tier)=>Mathf.Max(1,Mathf.CeilToInt(KillTarget*(tier==3?1:tier==2?.75f:.5f)));
        static bool FinitePositive(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n)&&n>0;
        public BachDangSaveData.MiniRecord MiniRecord(int index)=>Record.minigames.FirstOrDefault(m=>m!=null&&m.id==MiniId(index)&&!float.IsNaN(m.seconds)&&!float.IsInfinity(m.seconds)&&m.seconds>=0&&m.seconds<MiniLimit(index));
        public bool MeetsMini(int index,int tier){var m=MiniRecord(index);return m!=null&&m.seconds<=MiniTarget(index,tier)+.0001f;}
        public int MiniRank(int index){for(int tier=3;tier>=1;tier--)if(MeetsMini(index,tier))return tier;return 0;}
        public int RecordedMinigames=>Enumerable.Range(0,ScoredMinigames).Count(i=>MiniRecord(i)!=null);
        public string TimingBands(int index)=>$"S ≤ {Clock(MiniTarget(index,3))}  •  A ≤ {Clock(MiniTarget(index,2))}  •  B ≤ {Clock(MiniTarget(index,1))}";
        public string TimingResult(int index){var m=MiniRecord(index);return m==null?"Chưa ghi thời gian":$"Kỷ lục {Clock(m.seconds)} • "+(MiniRank(index)>0?"Hạng "+RankName(MiniRank(index)):"Chưa đạt mốc B");}
        public sealed class RankCondition
        {
            public readonly string name,detail;public readonly bool met;
            public RankCondition(string name,string detail,bool met){this.name=name;this.detail=detail;this.met=met;}
        }
        public RankCondition[] ConditionsFor(int tier)
        {
            var conditions=new RankCondition[9];
            conditions[0]=new RankCondition("Trống trận",TimingResult(0)+$" / cần ≤ {Clock(MiniTarget(0,tier))}",MeetsMini(0,tier));
            for(int work=0;work<4;work++)
            {
                int first=1+work*3;bool met=true;string detail="";
                for(int difficulty=0;difficulty<3;difficulty++)
                {int index=first+difficulty;var m=MiniRecord(index);met&=MeetsMini(index,tier);if(difficulty>0)detail+=" • ";detail+=DifficultyNames[difficulty]+" "+(m==null?"chưa ghi":Clock(m.seconds))+$" / ≤ {Clock(MiniTarget(index,tier))}";}
                conditions[work+1]=new RankCondition(WorkNames[work]+" (đủ 3 mức)",detail,met);
            }
            bool stakeKnown=Record.stakeStarted&&Record.stakeFinished&&!float.IsNaN(Record.stakeSeconds)&&!float.IsInfinity(Record.stakeSeconds)&&Record.stakeSeconds>=0;
            conditions[5]=new RankCondition("Dựng đủ năm cụm cọc",(stakeKnown?Clock(Record.stakeSeconds):Record.stakeFinished?"Thiếu thời gian cũ":Record.stakeStarted?"Đang làm "+Clock(Record.stakeSeconds):"Chưa làm")+$" / cần ≤ {Clock(StakeTarget(tier))}",stakeKnown&&Record.stakeSeconds<=StakeTarget(tier)+.0001f);
            conditions[6]=new RankCondition("Tự hạ địch quest 3",$"{Record.kills.Length}/{KillRequirement(tier)} địch • tính cả cung và kiếm",Record.kills.Length>=KillRequirement(tier));
            bool reported=Record.finished||(Record.completedMask&(1<<(int)BachDangStoryManager.StoryState.Act3_Victory))!=0;
            conditions[7]=new RankCondition("Thời gian chiến dịch",(Record.historyIncomplete?"Thiếu thời gian cũ":(reported?"":"Đang chơi ")+Duration(Record.seconds))+$" / hoàn thành ≤ {Duration(CampaignTarget(tier))}",reported&&!Record.historyIncomplete&&!float.IsNaN(Record.seconds)&&!float.IsInfinity(Record.seconds)&&Record.seconds>=0&&Record.seconds<=CampaignTarget(tier));
            conditions[8]=new RankCondition("Tìm đủ 5 khúc gỗ trong làng",WoodResult+$" / cần ≤ {Clock(WoodTarget(tier))}",WoodTimeKnown&&Record.woodSeconds<=WoodTarget(tier));
            return conditions;
        }
        public bool EligibleFor(int tier)=>tier>=1&&tier<=3&&ConditionsFor(tier).All(c=>c.met);
        public string EarnedRank{get{for(int tier=3;tier>=1;tier--)if(EligibleFor(tier))return RankName(tier);return "C";}}
        public void Finish()
        {
            if(Record.finished)return;Record.completedMask|=1<<(int)BachDangStoryManager.StoryState.Act3_Celebration;
            Record.finalScore=0;Record.finalRank=EarnedRank;Record.rankRulesVersion=RankRulesVersion;Record.finished=true;
        }
        public BachDangSaveData.Campaign Capture()=>JsonUtility.FromJson<BachDangSaveData.Campaign>(JsonUtility.ToJson(Record));
        public void Restore(BachDangSaveData.Campaign data,int state)
        {
            Record=data??new BachDangSaveData.Campaign{historyIncomplete=true};Record.minigames=Record.minigames??Array.Empty<BachDangSaveData.MiniRecord>();Record.kills=Record.kills??Array.Empty<string>();
            if(state!=(int)BachDangStoryManager.StoryState.Act2_CollectWoodInVillage&&(Record.completedMask&(1<<2))!=0&&!WoodTimeKnown)Record.historyIncomplete=true;
            if(data==null)
            {
                int index=Array.IndexOf(StoryOrder,state);for(int i=0;i<index;i++)Record.completedMask|=1<<StoryOrder[i];
                Record.stakeFinished=index>Array.IndexOf(StoryOrder,5);
                foreach(var station in FindObjectsByType<BeachBoatMinigameStation>())if(station.kind==BeachBoatMinigameStation.ActivityKind.WarDrum&&station.HasCompleted&&station.BestTime>0)RecordMinigame("drum",station.BestTime,station.TimeLimit);
                var combat=RiverPlayerCombat.Instance;Record.enemyCount=Mathf.Max(1,combat.guards.Length);
                // Old saves contain a real kill count but no per-enemy identities.
                Record.kills=Enumerable.Range(0,combat.Kills).Select(i=>"legacy:"+i).ToArray();
            }
            // Upgrade the old point-based ending using its real records. Missing
            // timings cannot silently grant a timed achievement or a high rank.
            if(Record.finished&&Record.rankRulesVersion<RankRulesVersion){if(RecordedMinigames<ScoredMinigames)Record.historyIncomplete=true;Record.finalRank=EarnedRank;Record.finalScore=0;Record.rankRulesVersion=RankRulesVersion;}
            Visit(state);if(state==(int)BachDangStoryManager.StoryState.Act3_Celebration&&!Record.finished)Finish();
        }
        public static string Duration(float seconds){var t=TimeSpan.FromSeconds(Mathf.Max(0,seconds));return t.TotalHours>=1?$"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}":$"{t.Minutes:00}:{t.Seconds:00}";}
        public static string Clock(float seconds){long ticks=(long)Math.Round(Mathf.Max(0,seconds)*100.0,MidpointRounding.AwayFromZero);return $"{ticks/6000:00}:{ticks/100%60:00}.{ticks%100:00}";}
        public string RankingRules
        {
            get
            {
                var text=new System.Text.StringBuilder("HẠNG THEO THÀNH TÍCH • ĐỦ TẤT CẢ ĐIỀU KIỆN\nMỗi hạng cần đạt đồng thời 9 điều kiện dưới đây. Lấy hạng cao nhất đủ điều kiện; nếu chưa đủ hạng B thì nhận C sau khi hoàn thành cốt truyện.\n\n");
                for(int tier=3;tier>=1;tier--)
                {
                    var conditions=ConditionsFor(tier);text.Append($"<color=#F0C26D>HẠNG {RankName(tier)} — {conditions.Count(c=>c.met)}/{conditions.Length} ĐIỀU KIỆN</color>\n");
                    foreach(var c in conditions)text.Append(c.met?"<color=#79E9AC>ĐẠT</color> ":"<color=#C0AAA0>CHƯA ĐẠT</color> ").Append(c.name).Append("\n").Append(c.detail).Append("\n");
                    text.Append("\n");
                }
                text.Append("CÁCH TÍNH THỜI GIAN\nTrống trận và từng mức tại bốn sạp: lấy lần thắng nhanh nhất, gồm thời gian phạt lỗi. Mốc trống S/A/B là 40%/60%/80% giới hạn; vật tư là 60%/80%/95% giới hạn của chính mức đó. Thắng đủ Dễ → Vừa → Khó ở mỗi sạp và nhận vật tư để hoàn thành nhiệm vụ.\n\nĐóng cọc: từ bắt đầu cụm đầu đến xong cả năm, gồm chèo và vớt cọc. Mốc S tính theo tuyến đi, tốc độ thuyền, thời gian thao tác và 90 giây dự phòng; tối thiểu 3 phút. A/B cho phép 1,5/2 lần mốc S.\n\nHạ địch: mỗi mã địch chỉ tính một lần. Mốc S là 25% quân địch đầu trận; A/B là 75%/50% mốc S, làm tròn lên. Quân ta hạ không cộng vào số bạn tự hạ.\n\nToàn chiến dịch: gồm thời gian chơi, minigame, chèo và chiến đấu; bỏ qua sảnh, tạm dừng, bảng Tab, dẫn truyện, thoại và màn hình thất bại. Lưu/tải giữ nguyên đồng hồ.\n\nLUYỆN THỂ LỰC — TÙY CHỌN\nBa cấp, mỗi cấp tăng 20 thể lực tối đa, tổng +60. Có thể bỏ qua hoàn toàn; không khóa nhiệm vụ chính và không tham gia xét hạng.\n\nHạng chốt khi thắng và báo công với Ngô Quyền. Có thể chơi lại trống và các sạp để cải thiện kỷ lục trước khi kết thúc; đồng hồ chiến dịch vẫn chạy. Save cũ thiếu thời gian ghi CHƯA GHI, kết quả TẠM TÍNH; thành tích thời gian chỉ được xác nhận bằng kỷ lục thật.");
                text.Append("\n\nTÌM GỖ TRONG LÀNG\nMỗi hành trình có đúng 5 khúc ở vị trí ngẫu nhiên. Đồng hồ từ lúc nhận nhiệm vụ ở làng đến lúc nhặt khúc thứ năm; tạm dừng, Tab, thoại và chuyển cảnh không tính. Mốc S dùng thứ tự ghé 5 khúc ngắn nhất, tốc độ đi bộ, hệ số đi vòng nhà 1,8 và 100 giây tìm kiếm; tối thiểu 3 phút. A/B cho phép 1,5/2 lần mốc S. Lưu/tải giữ vị trí, gỗ đã nhặt và thời gian, không bốc lại để tìm nhanh hơn. Save cũ không có thời gian tìm gỗ không được tự gán thành tích.");
                return text.ToString();
            }
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
