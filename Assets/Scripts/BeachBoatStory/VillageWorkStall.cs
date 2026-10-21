using UnityEngine;

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
        public int Difficulty {get;private set;}=1;
        public int Progress {get;private set;}
        public int Mistakes {get;private set;}
        public float Elapsed {get;private set;}
        public int RiceLoad {get;private set;}
        public int RiceTarget {get;private set;}
        public int TimberTarget {get;private set;}
        public int TimberTotal {get;private set;}
        public int Moves {get;private set;}
        public int RiceSmall=>Difficulty==2?4:3;
        public int RiceLarge=>Difficulty==2?7:5;
        public int MoveLimit {get;private set;}
        public int People {get;private set;}
        public string Feedback {get;private set;}="";
        public bool LastCorrect {get;private set;}
        public int FeedbackVersion {get;private set;}
        public Vector3 InteractionPosition=>interactionPoint!=null?interactionPoint.position:transform.position;
        public float TimeLimit=>kind==WorkKind.Rope?(Difficulty==0?100:Difficulty==1?75:55):(Difficulty==0?90:Difficulty==1?70:50);
        public int Goal=>kind==WorkKind.Rope?1:2+Difficulty;
        public int MistakeLimit=>Difficulty==2?2:3;
        public float TimeRemaining=>Mathf.Max(0,TimeLimit-Elapsed);
        public string Instructions=>kind==WorkKind.Timber?"Chọn HAI thanh gỗ có tổng chiều dài đúng đơn hàng. Mỗi thanh chỉ dùng một lần. Gỗ có vết nứt không dùng được; hãy tính tổng trước khi chọn.":
            kind==WorkKind.Rope?"Bấm từng ô để xoay đoạn dây 90 độ. Nối một đường liên tục từ đầu VÀO bên trái hàng giữa sang đầu RA bên phải hàng giữa. Dây không được đứt hoặc tạo vòng kín. Số lượt xoay có hạn.":
            kind==WorkKind.Rice?"Đong đúng lượng gạo yêu cầu bằng hai gáo khác cỡ. Chỉ được thêm một gáo nhỏ, thêm một gáo lớn hoặc bớt một gáo nhỏ. Hãy tính cách đong trong số lượt cho phép rồi đóng túi.":
            "Gói lương theo số quân sĩ: MỖI NGƯỜI cần 2 phần gạo và 1 phần cá khô. CẢ GÓI cần 1 phần muối. Tính số phần cần cho cả nhóm, chọn nguyên liệu rồi kiểm tra gói.";
        public static string IngredientName(int n)=>n==0?"GẠO":n==1?"CÁ KHÔ":"MUỐI";
        readonly bool[] soundTimber=new bool[4];
        readonly int[] timberLengths=new int[4],ropePorts=new int[9],ingredients=new int[3];
        readonly bool[] timberUsed=new bool[4];
        int timberPieces;
        Transform player;
        CursorLockMode savedLock;
        bool savedCursor,prompt;
        void Update()
        {
            if(Active==this)
            {
                if(Input.GetKeyDown(KeyCode.Escape)){Close();return;}
                if(Phase==WorkPhase.Instructions){if(Input.GetKeyDown(KeyCode.Return))StartAttempt(Difficulty);return;}
                if(Phase==WorkPhase.Lost){if(Input.GetKeyDown(KeyCode.R))StartAttempt(Difficulty);return;}
                if(Phase==WorkPhase.Won){if(Input.GetKeyDown(KeyCode.Return))ClaimReward();return;}
                AdvanceTime(Time.unscaledDeltaTime);
                if(kind==WorkKind.Rope){if(Input.GetKeyDown(KeyCode.Space))SubmitOption(9);}
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
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;VillageWorkUI.EnsureExists();return true;
        }
        public void SelectDifficulty(int difficulty)
        {
            if(Active==this&&Phase==WorkPhase.Instructions)Difficulty=Mathf.Clamp(difficulty,0,2);
        }
        public void StartAttempt(int difficulty)
        {
            if(Active!=this||Phase==WorkPhase.Playing)return;
            Difficulty=Mathf.Clamp(difficulty,0,2);Progress=Mistakes=RiceLoad=0;Elapsed=0;Feedback="Hãy xem yêu cầu và tính trước khi làm.";FeedbackVersion=0;
            Phase=WorkPhase.Playing;NewTask();
        }
        public void AdvanceTime(float dt)
        {
            if(Active!=this||Phase!=WorkPhase.Playing)return;
            Elapsed+=Mathf.Max(0,dt);
            if(Elapsed>=TimeLimit)Lose("Hết giờ. Vật tư chưa chuẩn bị đủ.");
        }
        public bool IsSoundTimber(int index)=>index>=0&&index<4&&soundTimber[index];
        public int TimberLength(int i)=>timberLengths[i];
        public bool TimberUsed(int i)=>timberUsed[i];
        public int RopePorts(int i)=>ropePorts[i];
        public int IngredientCount(int i)=>ingredients[i];
        public int RequiredIngredient(int i)=>i==0?People*2:i==1?People:1;
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
            if(Active!=this||Phase!=WorkPhase.Playing||option<0||option>(kind==WorkKind.Rope?9:3))return false;
            if(kind==WorkKind.Rope)
            {
                if(option<9){ropePorts[option]=RotatePorts(ropePorts[option]);Moves++;if(Moves>MoveLimit)Lose("Đã hết lượt xoay. Quan sát đường dây rồi thử lại.");return true;}
                if(RopeConnected()){Score("Dây đã nối liền từ đầu vào tới đầu ra.");return true;}
                Fault("Đường dây còn đứt hoặc đi ra ngoài khung. Kiểm tra các đầu nối.");return false;
            }
            if(kind==WorkKind.Timber)
            {
                if(timberUsed[option])return false;
                if(!soundTimber[option]){Fault("Thanh này bị nứt. Hãy chọn hai thanh liền thân.");return false;}
                timberUsed[option]=true;TimberTotal+=timberLengths[option];timberPieces++;
                if(timberPieces==2&&TimberTotal==TimberTarget){Score("Hai thanh vừa đúng chiều dài đơn hàng.");return true;}
                if(TimberTotal>=TimberTarget||timberPieces>=2)
                {Fault("Tổng chiều dài không đúng. Tính lại cặp gỗ rồi chọn lại.");for(int i=0;i<4;i++)timberUsed[i]=false;TimberTotal=timberPieces=0;return false;}
                return true;
            }
            if(kind==WorkKind.Rice)
            {
                if(option==0)RiceLoad+=RiceSmall;
                else if(option==1)RiceLoad+=RiceLarge;
                else if(option==2){if(RiceLoad<RiceSmall){Fault("Không đủ gạo để bớt một gáo nhỏ.");return false;}RiceLoad-=RiceSmall;}
                else if(RiceLoad==RiceTarget){Score("Đã đong chính xác và đóng một túi gạo.");return true;}
                else {Fault("Lượng gạo chưa đúng. Đong thêm hoặc bớt trước khi đóng túi.");return false;}
                Moves++;if(Moves>MoveLimit){RiceLoad=Moves=0;Fault("Quá số lượt đong. Hãy tính đường đi ngắn hơn.");return false;}
                return true;
            }
            if(option<3){ingredients[option]++;return true;}
            bool correct=true;for(int i=0;i<3;i++)correct&=ingredients[i]==RequiredIngredient(i);
            if(correct)Score("Khẩu phần đã đủ cho cả nhóm quân sĩ.");
            else
            {
                Fault("Khẩu phần chưa đúng công thức. Gói lại: mỗi người 2 gạo + 1 cá, cả gói 1 muối.");
                for(int i=0;i<3;i++)ingredients[i]=0;
            }
            return correct;
        }
        void Score(string message)
        {
            Progress++;Feedback=message;LastCorrect=true;FeedbackVersion++;
            if(Progress>=Goal){Phase=WorkPhase.Won;return;}NewTask();
        }
        void Fault(string message)
        {
            Mistakes++;Elapsed+=2;Feedback=message;LastCorrect=false;FeedbackVersion++;
            if(Mistakes>=MistakeLimit)Lose("Sai quá số lần cho phép. Chơi lại để làm chắc tay hơn.");
            else if(Elapsed>=TimeLimit)Lose("Hết giờ sau lần thao tác sai.");
        }
        void NewTask()
        {
            Moves=TimberTotal=timberPieces=RiceLoad=0;
            if(kind==WorkKind.Timber)
            {
                int bad=Random.Range(0,4);var good=new int[3];int next=0;
                for(int i=0;i<4;i++){soundTimber[i]=i!=bad;timberUsed[i]=false;timberLengths[i]=2+i*2+Random.Range(0,2);if(i!=bad)good[next++]=i;}
                int excluded=Random.Range(0,3);TimberTarget=0;for(int i=0;i<3;i++)if(i!=excluded)TimberTarget+=timberLengths[good[i]];
            }
            else if(kind==WorkKind.Rope)
            {
                bool upper=Random.value>.5f;
                int[] solved=upper?new[]{6,6,12,10,9,3,3,10,9}:new[]{6,10,12,10,12,6,3,3,9};
                for(int i=0;i<9;i++){ropePorts[i]=solved[i];for(int n=Random.Range(0,4);n>0;n--)ropePorts[i]=RotatePorts(ropePorts[i]);}
                if(RopeConnected())ropePorts[3]=RotatePorts(ropePorts[3]);
                MoveLimit=Difficulty==0?26:Difficulty==1?22:18;
            }
            else if(kind==WorkKind.Rice)
            {
                RiceTarget=Difficulty==2?Random.Range(9,21):Random.Range(7,15);
                MoveLimit=MinimumScoops(RiceTarget,RiceSmall,RiceLarge)+(Difficulty==0?2:Difficulty==1?1:0);
            }
            else {People=Random.Range(1+Difficulty,3+Difficulty);for(int i=0;i<3;i++)ingredients[i]=0;}
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
            Close();quest.Collect(this);return true;
        }
        public void MarkCollected(){Completed=true;}
        public void ResetCollection(){Completed=false;}
        public void Close()
        {
            if(Active!=this)return;Active=null;HidePrompt();Cursor.lockState=savedLock;Cursor.visible=savedCursor;
        }
        void HidePrompt(){if(prompt){BachDangStoryManager.Instance?.SetInteractPrompt(false,owner:this);prompt=false;}}
        void OnDisable(){Close();HidePrompt();}
    }
}
