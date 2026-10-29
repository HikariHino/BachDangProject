using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Camp recovery is earned by a timed activity, never by merely opening a tent.</summary>
    public sealed class CampRecoveryGame : MonoBehaviour
    {
        public enum GamePhase { Instructions, Playing, Won, Lost }
        public static CampRecoveryGame Active {get;private set;}
        public GamePhase Phase {get;private set;}
        public CampFacility Facility {get;private set;}
        public int Progress {get;private set;}
        public int Mistakes {get;private set;}
        public float Elapsed {get;private set;}
        public float TimeLimit=>Training?65:Sequence?22:20;
        public PhysicalTrainingCourse Course {get;private set;}
        public Vector3 TrainingHome {get;private set;}
        public bool TrainingDelivered {get;private set;}
        public int TrainingLevel {get;private set;}=1;
        public int Goal=>Training?4+TrainingLevel*2:Sequence?3:4;
        public float CrossingSeconds=>Training?(TrainingLevel==1?1.4f:TrainingLevel==2?.95f:.6f):2f;
        public float GreenWidth=>Training?(TrainingLevel==1?.30f:TrainingLevel==2?.18f:.09f):.24f;
        public float GreenMin=>.7f-GreenWidth*.5f;
        public float GreenMax=>.7f+GreenWidth*.5f;
        public float RhythmPosition=>Mathf.PingPong(Elapsed/CrossingSeconds,1);
        public bool Training=>Facility.kind==CampFacility.FacilityKind.Armory;
        public bool Sequence=>Facility.kind==CampFacility.FacilityKind.Medical||Facility.kind==CampFacility.FacilityKind.Provisions;
        public int ExpectedAction=>Sequence?System.Array.IndexOf(order,Mathf.Min(Progress,2)):0;
        public string Feedback {get;private set;}="";
        readonly int[] order={0,1,2};
        GameObject overlay,instructions,playing,result,choices,rhythm;
        TMP_Text title,rules,stats,task,feedback,resultText,actionLabel;
        readonly TMP_Text[] optionLabels=new TMP_Text[3];
        RectTransform cursor,clockFill,progressFill,goodZone;
        CursorLockMode savedLock;
        bool savedVisible;
        float lastBeat=-100;
        static readonly Color Gold=new Color(1,.77f,.34f),Green=new Color(.23f,.65f,.43f);
        public static bool Open(CampFacility facility)
        {
            if(Active!=null||facility==null)return false;
            var session=BeachBoatQuestSession.Instance;
            var game=session.GetComponent<CampRecoveryGame>()??session.gameObject.AddComponent<CampRecoveryGame>();
            game.Facility=facility;game.savedLock=Cursor.lockState;game.savedVisible=Cursor.visible;
            Active=game;game.Phase=GamePhase.Instructions;game.Feedback="";
            game.TrainingLevel=Mathf.Min(3,session.player.GetComponent<PlayerVitals>().CompletedTrainingLevels+1);
            game.TrainingHome=session.player.transform.position;
            if(game.overlay==null)game.BuildUI();
            game.overlay.SetActive(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            BachDangStoryManager.Instance?.SetInteractPrompt(false);BeachBoatSoundscape.Instance?.PlayInteraction();game.Refresh();if(game.Training)game.StartAttempt();return true;
        }
        public void StartAttempt()
        {
            if(Active!=this||Phase==GamePhase.Playing||Phase==GamePhase.Won)return;
            Progress=Mistakes=0;Elapsed=0;lastBeat=-100;Feedback="";Phase=GamePhase.Playing;
            TrainingDelivered=false;
            if(Training){Course?.Dispose();Course=new PhysicalTrainingCourse(this);Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            Shuffle();Refresh();
        }
        void Shuffle()
        {
            for(int i=0;i<3;i++)order[i]=i;
            for(int i=2;i>0;i--){int j=Random.Range(0,i+1);int v=order[i];order[i]=order[j];order[j]=v;}
        }
        string Item(int semantic)
        {
            if(Facility.kind==CampFacility.FacilityKind.Medical)return new[]{"LÀM SẠCH","ĐẶT BĂNG","CỐ ĐỊNH BĂNG"}[semantic];
            return new[]{"NƯỚC UỐNG","CƠM","CÁ KHÔ"}[semantic];
        }
        string Reward()=>Facility.kind==CampFacility.FacilityKind.Medical?"Thưởng: hồi đầy máu và tiếp thuốc lên ít nhất 3 item.":
            Facility.kind==CampFacility.FacilityKind.Provisions?"Thưởng: hồi 25 máu và 35 thể lực.":
            Training?(BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>().CompletedTrainingLevels<TrainingLevel?
                "Thưởng cấp này: +20 thể lực tối đa và hồi đầy thể lực.":"Bạn đã nhận +20 của cấp này. Chơi lại chỉ hồi đầy thể lực."):"Thưởng: hồi 35 máu và đầy thể lực.";
        string Rules()=>Sequence?(Facility.kind==CampFacility.FacilityKind.Medical?
            "Băng bó theo đúng thứ tự: làm sạch → đặt băng → cố định băng. Vị trí dụng cụ sẽ đổi sau mỗi bước.":
            "Chuẩn bị bữa hồi sức: nước uống → cơm → cá khô. Vị trí món sẽ đổi sau mỗi bước.")+
            "\nNhấn nút có tên phù hợp hoặc phím 1–3. Hoàn thành 3 bước trong 22 giây; sai 3 lần sẽ thua.":
            Training?$"E nhấc bao cát tại điểm xuất phát. WASD mang bao tới cổng vàng, SPACE nhảy qua thanh gỗ.\nCấp {TrainingLevel}/3: vượt {Goal-1} cổng rồi mang bao về, E đặt xuống trong {TimeLimit:0} giây.\nMỗi cấp hoàn thành lần đầu thưởng +20 thể lực tối đa. ESC rời bài tập.":
            "Điều hòa nhịp thở. Đợi con trỏ vào vùng xanh rồi nhấn Space hoặc nút THỞ RA. Đạt 4 nhịp trong 20 giây; sai 3 lần sẽ thua.";
        public void AdvanceTime(float dt)
        {
            if(Active!=this||Phase!=GamePhase.Playing)return;
            Elapsed+=Mathf.Max(0,dt);
            if(Elapsed>=TimeLimit){Phase=GamePhase.Lost;Feedback="Hết giờ. Chưa nhận hồi phục; bạn có thể thử lại.";BeachBoatSoundscape.Instance?.PlayResult(false);if(Training){Course?.Dispose();Course=null;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
            Refresh();
        }
        public bool Submit(int option)
        {
            if(Training)return false;
            if(Active!=this||Phase!=GamePhase.Playing)return false;
            bool valid=Sequence?option>=0&&option<3&&order[option]==Progress:
                option==0&&RhythmPosition>=GreenMin&&RhythmPosition<=GreenMax&&Elapsed-lastBeat>=.45f;
            if(valid)
            {
                Progress++;lastBeat=Elapsed;Feedback="Đúng nhịp / đúng bước!";
                if(Progress>=Goal){Phase=GamePhase.Won;Feedback="Hoàn thành! Nhận hồi phục để áp dụng phần thưởng.";}
                else if(Sequence)Shuffle();
            }
            else {Mistakes++;Feedback=Sequence?"Sai thứ tự. Đọc bước cần làm rồi chọn lại.":"Lệch nhịp. Đợi vùng xanh rồi thử lại.";if(Mistakes>=3){Phase=GamePhase.Lost;Feedback="Sai 3 lần. Chưa nhận hồi phục; hãy thử lại.";}}
            BeachBoatSoundscape.Instance?.PlayResult(valid);Refresh();return valid;
        }
        public bool ClaimReward()
        {
            if(Active!=this||Phase!=GamePhase.Won||Facility==null)return false;
            if(!Facility.CompleteRecovery(this))return false;
            Close();return true;
        }
        public void TrainingCheckpoint(){if(Active!=this||!Training||Phase!=GamePhase.Playing)return;Progress++;if(Progress>=Goal){TrainingDelivered=true;Phase=GamePhase.Won;Feedback="Đã mang bao qua đủ cổng và trả về điểm xuất phát.";Course?.Dispose();Course=null;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}Refresh();}
        public void Close()
        {
            if(Active!=this)return;
            Active=null;if(overlay!=null)overlay.SetActive(false);
            Course?.Dispose();Course=null;
            Cursor.lockState=savedLock;Cursor.visible=savedVisible;StoryDialogueUI.TryConsumeInput();
        }
        void Update()
        {
            if(BachDangFrontEnd.IsBlocking||StoryDialogueUI.InputConsumedThisFrame)return;
            if(Active!=this)return;
            if(Facility==null||!Facility.isActiveAndEnabled){Close();return;}
            if(Input.GetKeyDown(KeyCode.X)){Close();return;}
            if(Phase==GamePhase.Instructions){if(Input.GetKeyDown(KeyCode.Return))StartAttempt();return;}
            if(Phase==GamePhase.Lost){if(Input.GetKeyDown(KeyCode.R))StartAttempt();return;}
            if(Phase==GamePhase.Won){if(Input.GetKeyDown(KeyCode.Return))ClaimReward();return;}
            AdvanceTime(Time.unscaledDeltaTime);
            if(Training){Course?.Tick();return;}
            if(Sequence){for(int i=0;i<3;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i)))Submit(i);}
            else if(Input.GetKeyDown(KeyCode.Space))Submit(0);
        }
        void OnDisable(){Close();}
        void LateUpdate(){if(Active==this)Course?.Pose();}
        void OnGUI(){if(Active==this)Course?.Draw();}
        void OnDestroy(){Close();}
        void BuildUI()
        {
            var canvas=new GameObject("CampRecoveryCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=260;
            var scale=canvas.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);scale.matchWidthOrHeight=.5f;
            overlay=Box("RecoveryOverlay",canvas.transform,Vector2.zero,Vector2.one,new Color(0,0,0,.68f));
            var card=Box("Card",overlay.transform,new Vector2(.18f,.14f),new Vector2(.82f,.86f),new Color(.045f,.065f,.07f,.99f)).transform;
            title=Text("Title",card,new Vector2(.05f,.86f),new Vector2(.87f,.97f),30,Gold);
            Button("Close",card,new Vector2(.9f,.87f),new Vector2(.98f,.97f),"X",Close);
            instructions=Box("Instructions",card,new Vector2(.06f,.12f),new Vector2(.94f,.84f),Color.clear);
            rules=Text("Rules",instructions.transform,new Vector2(0,.24f),Vector2.one,23,Color.white);
            Button("Start",instructions.transform,new Vector2(.17f,0),new Vector2(.83f,.18f),"BẮT ĐẦU  [ENTER]",StartAttempt);
            playing=Box("Activity",card,new Vector2(.06f,.1f),new Vector2(.94f,.84f),Color.clear);
            stats=Text("Stats",playing.transform,new Vector2(0,.83f),Vector2.one,21,Color.white);
            var track=Box("TimeTrack",playing.transform,new Vector2(0,.78f),new Vector2(1,.8f),new Color(.2f,.23f,.24f));clockFill=(RectTransform)Box("TimeFill",track.transform,Vector2.zero,Vector2.one,Gold).transform;
            task=Text("Task",playing.transform,new Vector2(0,.59f),new Vector2(1,.77f),25,Gold);
            choices=Box("Choices",playing.transform,new Vector2(0,.28f),new Vector2(1,.55f),Color.clear);
            for(int i=0;i<3;i++){int n=i;optionLabels[i]=Button("Option"+i,choices.transform,new Vector2(i/3f+.01f,0),new Vector2((i+1)/3f-.01f,1),"",()=>Submit(n));}
            rhythm=Box("Rhythm",playing.transform,new Vector2(0,.29f),new Vector2(1,.57f),Color.clear);
            var bar=Box("BreathTrack",rhythm.transform,new Vector2(0,.62f),new Vector2(1,.92f),new Color(.16f,.21f,.24f));
            goodZone=(RectTransform)Box("GoodZone",bar.transform,new Vector2(GreenMin,0),new Vector2(GreenMax,1),Green).transform;
            cursor=(RectTransform)Box("RhythmCursor",bar.transform,new Vector2(0,0),new Vector2(.012f,1),Color.white).transform;
            actionLabel=Button("Action",rhythm.transform,new Vector2(.15f,0),new Vector2(.85f,.46f),"",()=>Submit(0));
            feedback=Text("Feedback",playing.transform,new Vector2(0,.06f),new Vector2(1,.24f),21,Color.white);
            var progress=Box("ProgressTrack",playing.transform,new Vector2(0,0),new Vector2(1,.025f),new Color(.2f,.23f,.24f));progressFill=(RectTransform)Box("ProgressFill",progress.transform,Vector2.zero,Vector2.one,Green).transform;
            result=Box("Result",card,new Vector2(.06f,.12f),new Vector2(.94f,.83f),Color.clear);
            resultText=Text("ResultText",result.transform,new Vector2(0,.3f),Vector2.one,26,Color.white);
            Button("RetryOrClaim",result.transform,new Vector2(.1f,0),new Vector2(.9f,.22f),"",()=>{if(Phase==GamePhase.Won)ClaimReward();else StartAttempt();});
        }
        void Refresh()
        {
            if(overlay==null||Active!=this)return;
            overlay.SetActive(!Training||Phase!=GamePhase.Playing);
            title.text=Facility.kind==CampFacility.FacilityKind.Medical?"QUÂN Y — BĂNG BÓ":Facility.kind==CampFacility.FacilityKind.Provisions?"BẾP TRẠI — BỮA HỒI SỨC":Training?"LUYỆN THỂ LỰC":"LỀU NGHỈ — ĐIỀU HÒA NHỊP THỞ";
            if(Training)title.text+=$" • CẤP {TrainingLevel}/3 — "+new[]{"DỄ","VỪA","KHÓ"}[TrainingLevel-1];
            instructions.SetActive(Phase==GamePhase.Instructions);playing.SetActive(Phase==GamePhase.Playing);result.SetActive(Phase==GamePhase.Won||Phase==GamePhase.Lost);
            rules.text=Rules()+"\n\n"+Reward()+"\nChỉ nhận thưởng sau khi thắng. ESC / X: rời minigame, giữ phần thưởng đã nhận; lượt đang chơi chưa nhận thưởng.";
            stats.text=$"CÒN {Mathf.Max(0,TimeLimit-Elapsed):0.0}s    •    Hoàn thành {Progress}/{Goal}    •    Sai {Mistakes}/3";
            clockFill.anchorMax=new Vector2(Mathf.Clamp01(1-Elapsed/TimeLimit),1);progressFill.anchorMax=new Vector2((float)Progress/Goal,1);
            choices.SetActive(Sequence);rhythm.SetActive(!Sequence);
            task.text=Sequence?"BƯỚC CẦN LÀM: "+Item(Mathf.Min(Progress,2)):"NHẤN SPACE KHI VẠCH TRẮNG VÀO VÙNG XANH";
            for(int i=0;i<3;i++)optionLabels[i].text=$"[{i+1}]\n{Item(order[i])}";
            cursor.anchorMin=new Vector2(RhythmPosition*.988f,0);cursor.anchorMax=new Vector2(RhythmPosition*.988f+.012f,1);
            goodZone.anchorMin=new Vector2(GreenMin,0);goodZone.anchorMax=new Vector2(GreenMax,1);
            actionLabel.text=Training?"ĐÚNG NHỊP [SPACE]":"THỞ RA [SPACE]";feedback.text=Feedback;
            resultText.text=(Phase==GamePhase.Won?"HOÀN THÀNH\n\n"+Reward():"CHƯA HOÀN THÀNH\n\n"+Feedback)+"\n\nESC / X: rời minigame, giữ phần thưởng đã nhận";
            result.transform.Find("RetryOrClaim/Label").GetComponent<TMP_Text>().text=Phase==GamePhase.Won?"NHẬN THƯỞNG [ENTER]":"THỬ LẠI [R]";
        }
        static GameObject Box(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;go.GetComponent<Image>().color=color;return go;}
        static TMP_Text Text(string name,Transform parent,Vector2 min,Vector2 max,float size,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;var text=go.GetComponent<TextMeshProUGUI>();StoryFont.Apply(text);text.fontSize=size;text.alignment=TextAlignmentOptions.Center;text.color=color;text.raycastTarget=false;return text;}
        static TMP_Text Button(string name,Transform parent,Vector2 min,Vector2 max,string label,UnityEngine.Events.UnityAction action)
        {var go=Box(name,parent,min,max,new Color(.18f,.32f,.29f));var button=go.AddComponent<Button>();button.targetGraphic=go.GetComponent<Image>();button.onClick.AddListener(action);var text=Text("Label",go.transform,new Vector2(.03f,.03f),new Vector2(.97f,.97f),21,Color.white);text.text=label;return text;}
    }
}
