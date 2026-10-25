using UnityEngine;
using System.Collections.Generic;

namespace BeachBoatStory
{
    [DefaultExecutionOrder(-50)]
    public sealed class VillageWorkStall : MonoBehaviour
    {
        public enum WorkKind { Timber, Rope, Rice, Provisions }
        public enum WorkPhase { Instructions, Playing, Won, Lost }
        public VillagePreparationQuest quest;
        public WorkKind kind;
        public Transform interactionPoint;
        public float interactionDistance=25;
        public string stallName;
        public static VillageWorkStall Active {get;private set;}
        public WorkPhase Phase {get;private set;}
        public bool Completed {get;private set;}
        public int Difficulty {get;private set;}
        public int CompletedLevels {get;private set;}
        public int UnlockedDifficulty=>Mathf.Min(2,CompletedLevels);
        public int Progress {get;private set;}
        public int Mistakes {get;private set;}
        public float Elapsed {get;private set;}
        readonly Queue<string> recentRice=new Queue<string>();
        public int RiceLoad {get;private set;}
        public int RiceTarget {get;private set;}
        public int TimberTarget {get;private set;}
        public int TimberTotal {get;private set;}
        public int TimberOptions=>6;
        public int TimberRequiredPieces=>Difficulty==2?3:2;
        public bool TimberNeedsStrength=>Difficulty==2;
        public int TimberSelectedPieces=>timberPieces;
        public int TimberStrengthTarget {get;private set;}
        public int TimberBudget {get;private set;}
        public int TimberStrengthTotal {get;private set;}
        public int TimberCostTotal {get;private set;}
        public string TimberQuestionKey {get;private set;}
        public int Moves {get;private set;}
        public int RiceSmall {get;private set;}=3;
        public int RiceLarge {get;private set;}=5;
        public int RicePerPerson {get;private set;}=2;
        public int FishPerPerson {get;private set;}=1;
        public int SaltPerBundle {get;private set;}=1;
        public int MoveLimit {get;private set;}
        public int People {get;private set;}
        public string Feedback {get;private set;}="";
        public bool LastCorrect {get;private set;}
        public int FeedbackVersion {get;private set;}
        public Vector3 InteractionPosition=>interactionPoint!=null?interactionPoint.position:transform.position;
        public float TimeLimit=>kind==WorkKind.Rice?(Difficulty==0?100:Difficulty==1?100:110):kind==WorkKind.Timber?(Difficulty==0?100:Difficulty==1?135:180):kind==WorkKind.Rope?(Difficulty==0?100:Difficulty==1?75:55):(Difficulty==0?90:Difficulty==1?70:50);
        public int Goal=>kind==WorkKind.Rope?1:kind==WorkKind.Rice?Mathf.Min(3,2+Difficulty):2+Difficulty;
        public int MistakeLimit=>kind==WorkKind.Timber||kind==WorkKind.Rice?3:Difficulty==2?2:3;
        public float TimeRemaining=>Mathf.Max(0,TimeLimit-Elapsed);
        public string Instructions=>kind==WorkKind.Timber?"Dễ và Vừa: chọn 2 thanh đủ chiều dài. Khó: chọn 3 thanh đủ chiều dài và sức chịu lực. Chọn trong 6 thanh, loại gỗ nứt. Bấm vào thanh để chọn hoặc bỏ chọn, rồi GIAO GỖ để kiểm tra. Các số đều nhỏ để dễ nhẩm. Mỗi đơn chỉ có một tổ hợp đúng; câu mới không lặp câu đã chơi.":
            kind==WorkKind.Rope?"Bấm từng ô để xoay đoạn dây 90 độ. Nối một đường liên tục từ đầu VÀO bên trái hàng giữa sang đầu RA bên phải hàng giữa. Dây không được đứt hoặc tạo vòng kín. Số lượt xoay có hạn.":
            kind==WorkKind.Rice?"Đong đúng lượng gạo yêu cầu bằng hai gáo khác cỡ. Chỉ được thêm một gáo nhỏ, thêm một gáo lớn hoặc bớt một gáo nhỏ. Hãy tính cách đong trong số lượt cho phép rồi đóng túi.":
            "Đọc công thức mỗi đơn hàng: số người, gạo/cá cho mỗi người và muối cho cả gói đều có thể thay đổi. Tính đủ nguyên liệu, sửa bằng Hoàn tác nếu nhầm rồi kiểm tra gói.";
        public static string IngredientName(int n)=>n==0?"GẠO":n==1?"CÁ KHÔ":"MUỐI";
        readonly bool[] soundTimber=new bool[8];
        readonly int[] timberLengths=new int[8],timberStrengths=new int[8],timberCosts=new int[8],ropePorts=new int[9],ingredients=new int[3];
        readonly bool[] timberUsed=new bool[8];
        static readonly HashSet<string> TimberQuestions=new HashSet<string>();
        static readonly Queue<string> RecentTimberTargets=new Queue<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetQuestionHistory(){TimberQuestions.Clear();RecentTimberTargets.Clear();}
        int timberPieces;
        sealed class WorkSnapshot
        {
            public int rice,moves,total,pieces,strength,cost;
            public int[] rope,food;
            public bool[] timber;
        }
        readonly List<WorkSnapshot> history=new List<WorkSnapshot>();
        WorkSnapshot initialWork;
        public bool CanUndo=>Active==this&&Phase==WorkPhase.Playing&&history.Count>0;
        public bool CanRestore=>Active==this&&Phase==WorkPhase.Playing&&initialWork!=null&&history.Count>0;
        WorkSnapshot CaptureWork()=>new WorkSnapshot{rice=RiceLoad,moves=Moves,total=TimberTotal,pieces=timberPieces,strength=TimberStrengthTotal,cost=TimberCostTotal,rope=(int[])ropePorts.Clone(),food=(int[])ingredients.Clone(),timber=(bool[])timberUsed.Clone()};
        void RememberWork(){if(history.Count>=128)history.RemoveAt(0);history.Add(CaptureWork());}
        void RestoreWork(WorkSnapshot saved)
        {
            RiceLoad=saved.rice;Moves=saved.moves;TimberTotal=saved.total;timberPieces=saved.pieces;TimberStrengthTotal=saved.strength;TimberCostTotal=saved.cost;
            System.Array.Copy(saved.rope,ropePorts,9);System.Array.Copy(saved.food,ingredients,3);System.Array.Copy(saved.timber,timberUsed,8);
        }
        public bool Undo()
        {
            if(!CanUndo)return false;
            int last=history.Count-1;RestoreWork(history[last]);history.RemoveAt(last);
            Feedback="Đã hoàn tác thao tác cuối. Thời gian và số lỗi vẫn giữ nguyên.";LastCorrect=true;FeedbackVersion++;BeachBoatSoundscape.Instance?.PlayInteraction();return true;
        }
        public bool RestoreCurrentTask()
        {
            if(!CanRestore)return false;
            RestoreWork(initialWork);history.Clear();
            Feedback="Đã làm lại phần này; giữ đơn hàng, tiến độ, thời gian và số lỗi.";LastCorrect=true;FeedbackVersion++;BeachBoatSoundscape.Instance?.PlayInteraction();return true;
        }
        Transform player;
        CursorLockMode savedLock;
        bool savedCursor,prompt;
        void Update()
        {
            if(BachDangFrontEnd.IsBlocking||StoryDialogueUI.InputConsumedThisFrame)return;
            if(Active==this)
            {
                if(Input.GetKeyDown(KeyCode.X)){Close();return;}
                if(Phase==WorkPhase.Instructions){if(Input.GetKeyDown(KeyCode.Return))StartAttempt(Difficulty);return;}
                if(Phase==WorkPhase.Lost){if(Input.GetKeyDown(KeyCode.R))StartAttempt(Difficulty);return;}
                if(Phase==WorkPhase.Won){if(Input.GetKeyDown(KeyCode.Return))ClaimReward();return;}
                AdvanceTime(Time.unscaledDeltaTime);
                if(Input.GetKeyDown(KeyCode.Z)){Undo();return;}
                if(Input.GetKeyDown(KeyCode.C)){RestoreCurrentTask();return;}
                if(kind==WorkKind.Rope){if(Input.GetKeyDown(KeyCode.Space))SubmitOption(9);}
                else if(kind==WorkKind.Timber)
                {for(int i=0;i<TimberOptions;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i)))SubmitOption(i);if(Input.GetKeyDown(KeyCode.Return))SubmitOption(8);}
                else for(int i=0;i<4;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i)))SubmitOption(i);
                return;
            }
            if(player==null&&BeachBoatQuestSession.Instance!=null)player=BeachBoatQuestSession.Instance.player.transform;
            if(Active!=null||quest==null||!quest.CanWork||player==null||QuestRowboat.FlatDistance(player.position,InteractionPosition)>interactionDistance){HidePrompt();return;}
            BachDangStoryManager.Instance.SetInteractPrompt(true,Completed?$"[E] {stallName} • đã chuẩn bị xong, có thể chơi lại":$"[E] Giúp việc tại {stallName}",this);prompt=true;
            if(Input.GetKeyDown(KeyCode.E)&&StoryDialogueUI.TryConsumeInput())TryOpen();
        }
        public bool TryOpen()
        {
            if(player==null&&BeachBoatQuestSession.Instance!=null)player=BeachBoatQuestSession.Instance.player.transform;
            if(Active!=null||quest==null||!quest.CanWork||player==null||QuestRowboat.FlatDistance(player.position,InteractionPosition)>interactionDistance)return false;
            savedLock=Cursor.lockState;savedCursor=Cursor.visible;Active=this;Phase=WorkPhase.Instructions;HidePrompt();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;VillageWorkUI.EnsureExists();BeachBoatSoundscape.Instance?.PlayInteraction();return true;
        }
        public void SelectDifficulty(int difficulty)
        {
            if(Active==this&&Phase==WorkPhase.Instructions)Difficulty=Mathf.Clamp(difficulty,0,UnlockedDifficulty);
        }
        public void StartAttempt(int difficulty)
        {
            if(Active!=this||Phase==WorkPhase.Playing)return;
            Difficulty=Mathf.Clamp(difficulty,0,UnlockedDifficulty);Progress=Mistakes=RiceLoad=0;Elapsed=0;Feedback="Hãy xem yêu cầu và tính trước khi làm.";FeedbackVersion=0;
            Phase=WorkPhase.Playing;NewTask();
        }
        public void AdvanceTime(float dt)
        {
            if(Active!=this||Phase!=WorkPhase.Playing)return;
            Elapsed+=Mathf.Max(0,dt);
            if(Elapsed>=TimeLimit)Lose("Hết giờ. Vật tư chưa chuẩn bị đủ.");
        }
        public bool IsSoundTimber(int index)=>index>=0&&index<TimberOptions&&soundTimber[index];
        public int TimberLength(int i)=>timberLengths[i];
        public int TimberStrength(int i)=>timberStrengths[i];
        public int TimberCost(int i)=>timberCosts[i];
        public bool TimberUsed(int i)=>timberUsed[i];
        public int RopePorts(int i)=>ropePorts[i];
        public int IngredientCount(int i)=>ingredients[i];
        public int RequiredIngredient(int i)=>i==0?People*RicePerPerson:i==1?People*FishPerPerson:SaltPerBundle;
        public static int RotatePorts(int ports)=>((ports<<1)&15)|((ports>>3)&1);
        public bool RopeConnected()
        {
            int cell=3,entry=8,visited=0;
            for(int n=0;n<9;n++)
            {
                if((visited&(1<<cell))!=0||(ropePorts[cell]&entry)==0)return false;
                visited|=1<<cell;int exit=ropePorts[cell]^entry;
                if(cell==5&&exit==2)return true;
                int row=cell/3,col=cell%3;
                if(exit==1){if(row==0)return false;cell-=3;entry=4;}
                else if(exit==2){if(col==2)return false;cell++;entry=8;}
                else if(exit==4){if(row==2)return false;cell+=3;entry=1;}
                else if(exit==8){if(col==0)return false;cell--;entry=2;}
                else return false;
            }
            return false;
        }
        public bool SubmitOption(int option)
        {
            if(Active!=this||Phase!=WorkPhase.Playing||option<0||option>(kind==WorkKind.Rope?9:kind==WorkKind.Timber?8:3))return false;
            if(kind==WorkKind.Rope)
            {
                if(option<9){RememberWork();ropePorts[option]=RotatePorts(ropePorts[option]);Moves++;if(Moves>MoveLimit)Lose("Đã hết lượt xoay. Quan sát đường dây rồi thử lại.");return true;}
                if(RopeConnected()){Score("Dây đã nối liền từ đầu vào tới đầu ra.");return true;}
                Fault("Đường dây còn đứt hoặc đi ra ngoài khung. Kiểm tra các đầu nối.");return false;
            }
            if(kind==WorkKind.Timber)
            {
                if(option==8)
                {
                    int mask=0;for(int i=0;i<TimberOptions;i++)if(timberUsed[i])mask|=1<<i;
                    if(TimberSelectionValid(mask)){Score("Đơn gỗ đáp ứng đủ các điều kiện.");return true;}
                    Fault(TimberNeedsStrength?"Kiểm tra lại số thanh, chiều dài và sức chịu lực. Bấm thanh đã chọn để bỏ.":"Kiểm tra lại số thanh và chiều dài. Bấm thanh đã chọn để bỏ.");return false;
                }
                if(option>=TimberOptions)return false;
                if(!soundTimber[option]){Fault("Gỗ nứt không chịu được tải. Hãy chọn thanh nguyên vẹn.");return false;}
                RememberWork();int sign=timberUsed[option]?-1:1;timberUsed[option]=!timberUsed[option];
                TimberTotal+=sign*timberLengths[option];TimberStrengthTotal+=sign*timberStrengths[option];TimberCostTotal+=sign*timberCosts[option];timberPieces+=sign;
                return true;
            }
            if(kind==WorkKind.Rice)
            {
                if(option<3&&(option!=2||RiceLoad>=RiceSmall))RememberWork();
                if(option==0)RiceLoad+=RiceSmall;
                else if(option==1)RiceLoad+=RiceLarge;
                else if(option==2){if(RiceLoad<RiceSmall){Fault("Không đủ gạo để bớt một gáo nhỏ.");return false;}RiceLoad-=RiceSmall;}
                else if(RiceLoad==RiceTarget){Score("Đã đong chính xác và đóng một túi gạo.");return true;}
                else {Fault("Lượng gạo chưa đúng. Đong thêm hoặc bớt trước khi đóng túi.");return false;}
                Moves++;if(Moves>MoveLimit){RiceLoad=Moves=0;Fault("Quá số lượt đong. Hãy tính đường đi ngắn hơn.");return false;}
                return true;
            }
            if(option<3){RememberWork();ingredients[option]++;return true;}
            bool correct=true;for(int i=0;i<3;i++)correct&=ingredients[i]==RequiredIngredient(i);
            if(correct)Score("Khẩu phần đã đủ cho cả nhóm quân sĩ.");
            else
            {
                Fault($"Chưa đúng: mỗi người {RicePerPerson} gạo + {FishPerPerson} cá; cả gói {SaltPerBundle} muối.");
                // Keep the unsubmitted ration visible so the player can undo excess ingredients.
            }
            return correct;
        }
        void Score(string message)
        {
            BeachBoatSoundscape.Instance?.PlayResult(true);
            Progress++;Feedback=message;LastCorrect=true;FeedbackVersion++;
            if(Progress>=Goal){Phase=WorkPhase.Won;CampaignProgress.Instance?.RecordMinigame($"village:{(int)kind}:{Difficulty}",Elapsed,TimeLimit);return;}NewTask();
        }
        void Fault(string message)
        {
            BeachBoatSoundscape.Instance?.PlayResult(false);
            Mistakes++;Elapsed+=2;Feedback=message;LastCorrect=false;FeedbackVersion++;
            if(Mistakes>=MistakeLimit)Lose("Sai quá số lần cho phép. Chơi lại để làm chắc tay hơn.");
            else if(Elapsed>=TimeLimit)Lose("Hết giờ sau lần thao tác sai.");
        }
        void NewTask()
        {
            Moves=TimberTotal=timberPieces=RiceLoad=TimberStrengthTotal=TimberCostTotal=0;
            if(kind==WorkKind.Timber)
            {
                GenerateTimber();
            }
            else if(kind==WorkKind.Rope)
            {
                var paths=new List<int[]>();BuildRopePaths(3,new List<int>(),0,paths);
                int minimum=Difficulty==0?3:Difficulty==1?5:7;
                var eligible=paths.FindAll(p=>p.Length>=minimum&&p.Length<=minimum+2);
                var path=eligible[Random.Range(0,eligible.Count)];var solved=new int[9];
                for(int i=0;i<9;i++)solved[i]=new[]{3,5,6,9,10,12}[Random.Range(0,6)];
                int Direction(int from,int to)=>to==from-3?1:to==from+1?2:to==from+3?4:8;
                for(int i=0;i<path.Length;i++)solved[path[i]]=(i==0?8:Direction(path[i],path[i-1]))|(i==path.Length-1?2:Direction(path[i],path[i+1]));
                int rotations=0;
                for(int i=0;i<9;i++){ropePorts[i]=solved[i];int turns=Random.Range(0,4);for(int n=0;n<turns;n++)ropePorts[i]=RotatePorts(ropePorts[i]);}
                if(RopeConnected())ropePorts[3]=RotatePorts(ropePorts[3]);
                foreach(int i in path){int ports=ropePorts[i],turns=0;while(ports!=solved[i]&&turns<4){ports=RotatePorts(ports);turns++;}rotations+=turns;}
                MoveLimit=rotations+(Difficulty==0?12:Difficulty==1?7:3);
            }
            else if(kind==WorkKind.Rice)
            {
                int[,] pairs={{2,3},{3,5},{4,7}};int choice=Random.Range(0,Difficulty==0?2:3);
                RiceSmall=pairs[choice,0];RiceLarge=pairs[choice,1];
                var targets=new List<int>();
                for(int amount=6;amount<=(Difficulty==2?18:14);amount++)
                {
                    int steps=MinimumScoops(amount,RiceSmall,RiceLarge);
                    string key=$"{RiceSmall}/{RiceLarge}/{amount}";
                    if(steps>=2&&steps<=(Difficulty==2?4:3)&&!recentRice.Contains(key))targets.Add(amount);
                }
                if(targets.Count==0){recentRice.Clear();for(int amount=6;amount<=14;amount++)if(MinimumScoops(amount,RiceSmall,RiceLarge)<=4)targets.Add(amount);}
                RiceTarget=targets[Random.Range(0,targets.Count)];
                recentRice.Enqueue($"{RiceSmall}/{RiceLarge}/{RiceTarget}");if(recentRice.Count>12)recentRice.Dequeue();
                MoveLimit=MinimumScoops(RiceTarget,RiceSmall,RiceLarge)+3;
            }
            else {People=Random.Range(1+Difficulty,3+Difficulty);RicePerPerson=Random.Range(1,3+Difficulty);FishPerPerson=Random.Range(1,2+Difficulty);SaltPerBundle=Random.Range(1,3);for(int i=0;i<3;i++)ingredients[i]=0;}
            history.Clear();initialWork=CaptureWork();
        }
        public bool TimberSelectionValid(int mask)
        {
            int count=0,length=0,strength=0;
            if(mask<0||(mask>>TimberOptions)!=0)return false;
            for(int i=0;i<TimberOptions;i++)if((mask&(1<<i))!=0)
            {if(!soundTimber[i])return false;count++;length+=timberLengths[i];strength+=timberStrengths[i];}
            return count==TimberRequiredPieces&&length==TimberTarget&&(!TimberNeedsStrength||strength>=TimberStrengthTarget);
        }
        void GenerateTimber()
        {
            for(int attempt=0;attempt<4096;attempt++)
            {
                int bad=Random.Range(0,TimberOptions);var good=new List<int>();var lengths=new HashSet<int>();
                for(int i=0;i<8;i++)
                {
                    timberUsed[i]=false;soundTimber[i]=i<TimberOptions&&i!=bad;
                    int length;do{length=Random.Range(3,13+Difficulty*3);}while(!lengths.Add(length));
                    timberLengths[i]=length;timberStrengths[i]=Random.Range(2,9);timberCosts[i]=0;
                    if(soundTimber[i])good.Add(i);
                }
                for(int i=good.Count-1;i>0;i--){int j=Random.Range(0,i+1);int swap=good[i];good[i]=good[j];good[j]=swap;}
                TimberTarget=TimberStrengthTarget=TimberBudget=0;
                for(int i=0;i<TimberRequiredPieces;i++){int n=good[i];TimberTarget+=timberLengths[n];TimberStrengthTarget+=timberStrengths[n];}
                TimberStrengthTarget=TimberNeedsStrength?TimberStrengthTarget-Random.Range(0,3):0;
                int solutions=0,lengthOnly=0;
                for(int mask=1;mask<(1<<TimberOptions);mask++)
                {
                    int count=0,length=0;bool sound=true;
                    for(int i=0;i<TimberOptions;i++)if((mask&(1<<i))!=0){count++;length+=timberLengths[i];sound&=soundTimber[i];}
                    if(sound&&count==TimberRequiredPieces&&length==TimberTarget)lengthOnly++;
                    if(TimberSelectionValid(mask))solutions++;
                }
                // Only hard orders add a load comparison to the length calculation.
                if(solutions!=1||(TimberNeedsStrength&&lengthOnly<2))continue;
                string target=$"{Difficulty}:{TimberTarget}:{TimberStrengthTarget}:{TimberBudget}";
                if(RecentTimberTargets.Contains(target))continue;
                var cards=new List<string>();for(int i=0;i<TimberOptions;i++)cards.Add($"{soundTimber[i]}:{timberLengths[i]}:{(TimberNeedsStrength?timberStrengths[i]:0)}");
                cards.Sort(System.StringComparer.Ordinal);string key=target+"|"+string.Join(";",cards);
                if(!TimberQuestions.Add(key))continue;
                TimberQuestionKey=key;RecentTimberTargets.Enqueue(target);while(RecentTimberTargets.Count>12)RecentTimberTargets.Dequeue();return;
            }
            throw new System.InvalidOperationException("Could not generate a fresh, uniquely solvable timber order.");
        }
        static void BuildRopePaths(int cell,List<int> path,int visited,List<int[]> result)
        {
            path.Add(cell);visited|=1<<cell;
            if(cell==5)result.Add(path.ToArray());
            else foreach(int next in new[]{cell-3,cell+1,cell+3,cell-1})
                if(next>=0&&next<9&&(visited&(1<<next))==0&&Mathf.Abs(next%3-cell%3)+Mathf.Abs(next/3-cell/3)==1)BuildRopePaths(next,path,visited,result);
            path.RemoveAt(path.Count-1);
        }
        static int MinimumScoops(int target,int small,int large)
        {
            int[] distance=new int[64],queue=new int[64];for(int i=1;i<64;i++)distance[i]=-1;int read=0,write=1;
            while(read<write)
            {
                int at=queue[read++];if(at==target)return distance[at];
                foreach(int next in new[]{at+small,at+large,at-small})
                    if(next>=0&&next<64&&distance[next]<0){distance[next]=distance[at]+1;queue[write++]=next;}
            }
            return 8;
        }
        void Lose(string message){Phase=WorkPhase.Lost;Feedback=message;LastCorrect=false;}
        public bool ClaimReward()
        {
            if(Active!=this||Phase!=WorkPhase.Won)return false;
            if(!Completed&&Difficulty<CompletedLevels){Difficulty=CompletedLevels;Phase=WorkPhase.Instructions;return true;}
            if(!Completed&&Difficulty==CompletedLevels)
            {
                CompletedLevels++;QuestCheckpoint.Instance?.RefreshVillageProgress();
                if(CompletedLevels<3)
                {Difficulty=CompletedLevels;Phase=WorkPhase.Instructions;history.Clear();initialWork=null;Feedback="Đã mở mức tiếp theo.";return true;}
            }
            if(CompletedLevels<3)return false;
            Close();quest.Collect(this);return true;
        }
        public void MarkCollected(){Completed=true;CompletedLevels=3;}
        public void ResetCollection(){RestoreLearningProgress(0);}
        public void RestoreLearningProgress(int levels){CompletedLevels=Mathf.Clamp(levels,0,3);Completed=CompletedLevels==3;Difficulty=UnlockedDifficulty;history.Clear();initialWork=null;}
        public void Close()
        {
            if(Active!=this)return;Active=null;history.Clear();initialWork=null;HidePrompt();Cursor.lockState=savedLock;Cursor.visible=savedCursor;
        }
        void HidePrompt(){if(prompt){BachDangStoryManager.Instance?.SetInteractPrompt(false,owner:this);prompt=false;}}
        void OnDisable(){Close();HidePrompt();}
    }
}
