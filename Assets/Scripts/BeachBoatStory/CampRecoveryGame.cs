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
        public float TimeLimit=>Training?24:Sequence?22:20;
        public int Goal=>Training?6:Sequence?3:4;
        public float RhythmPosition=>Mathf.PingPong(Elapsed/(Training?1.25f:2f),1);
        public bool Training=>Facility.kind==CampFacility.FacilityKind.Armory;
        public bool Sequence=>Facility.kind==CampFacility.FacilityKind.Medical||Facility.kind==CampFacility.FacilityKind.Provisions;
        public int ExpectedAction=>Sequence?System.Array.IndexOf(order,Mathf.Min(Progress,2)):Training?Progress%2:0;
        public string Feedback {get;private set;}="";
        readonly int[] order={0,1,2};
        GameObject overlay,instructions,playing,result,choices,rhythm;
        TMP_Text title,rules,stats,task,feedback,resultText,actionLabel;
        readonly TMP_Text[] optionLabels=new TMP_Text[3];
        RectTransform cursor,clockFill,progressFill;
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
            if(game.overlay==null)game.BuildUI();
            game.overlay.SetActive(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            BachDangStoryManager.Instance?.SetInteractPrompt(false);BeachBoatSoundscape.Instance?.PlayInteraction();game.Refresh();return true;
        }
        public void StartAttempt()
        {
            if(Active!=this||Phase==GamePhase.Playing||Phase==GamePhase.Won)return;
            Progress=Mistakes=0;Elapsed=0;lastBeat=-100;Feedback="";Phase=GamePhase.Playing;
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
        string Reward()=>Facility.kind==CampFacility.FacilityKind.Medical?"Thưởng: hồi đầy máu.":
            Facility.kind==CampFacility.FacilityKind.Provisions?"Thưởng: hồi 25 máu và 35 thể lực.":
            Training?"Thưởng: thể lực tối đa ít nhất 120 và hồi đầy thể lực.":"Thưởng: hồi 35 máu và đầy thể lực.";
        string Rules()=>Sequence?(Facility.kind==CampFacility.FacilityKind.Medical?
            "Băng bó theo đúng thứ tự: làm sạch → đặt băng → cố định băng. Vị trí dụng cụ sẽ đổi sau mỗi bước.":
            "Chuẩn bị bữa hồi sức: nước uống → cơm → cá khô. Vị trí món sẽ đổi sau mỗi bước.")+
            "\nNhấn nút có tên phù hợp hoặc phím 1–3. Hoàn thành 3 bước trong 22 giây; sai 3 lần sẽ thua.":
            Training?"Tập bước trái/phải luân phiên: A rồi D. Chỉ nhấn khi con trỏ ở vùng xanh, thả tay giữa hai nhịp. Đạt 6 nhịp đúng trong 24 giây; sai 3 lần sẽ thua.":
            "Điều hòa nhịp thở. Đợi con trỏ vào vùng xanh rồi nhấn Space hoặc nút THỞ RA. Đạt 4 nhịp trong 20 giây; sai 3 lần sẽ thua.";
        public void AdvanceTime(float dt)
        {
            if(Active!=this||Phase!=GamePhase.Playing)return;
            Elapsed+=Mathf.Max(0,dt);
            if(Elapsed>=TimeLimit){Phase=GamePhase.Lost;Feedback="Hết giờ. Chưa nhận hồi phục; bạn có thể thử lại.";BeachBoatSoundscape.Instance?.PlayResult(false);}
            Refresh();
        }
        public bool Submit(int option)
        {
            if(Active!=this||Phase!=GamePhase.Playing)return false;
            bool valid=Sequence?option>=0&&option<3&&order[option]==Progress:
                option==(Training?Progress%2:0)&&RhythmPosition>=.58f&&RhythmPosition<=.82f&&Elapsed-lastBeat>=.65f;
            if(valid)
            {
                Progress++;lastBeat=Elapsed;Feedback="Đúng nhịp / đúng bước!";
                if(Progress>=Goal){Phase=GamePhase.Won;Feedback="Hoàn thành! Nhận hồi phục để áp dụng phần thưởng.";}
                else if(Sequence)Shuffle();
            }
            else {Mistakes++;Feedback=Sequence?"Sai thứ tự. Đọc bước cần làm rồi chọn lại.":"Lệch nhịp hoặc sai bên. Đợi vùng xanh rồi thử lại.";if(Mistakes>=3){Phase=GamePhase.Lost;Feedback="Sai 3 lần. Chưa nhận hồi phục; hãy thử lại.";}}
            BeachBoatSoundscape.Instance?.PlayResult(valid);Refresh();return valid;
        }
        public bool ClaimReward()
        {
            if(Active!=this||Phase!=GamePhase.Won||Facility==null)return false;
            if(!Facility.CompleteRecovery(this))return false;
            Close();return true;
        }
        public void Close()
        {
            if(Active!=this)return;
            Active=null;if(overlay!=null)overlay.SetActive(false);
            Cursor.lockState=savedLock;Cursor.visible=savedVisible;StoryDialogueUI.TryConsumeInput();
        }
        void Update()
        {
            if(Active!=this)return;
            if(Facility==null||!Facility.isActiveAndEnabled){Close();return;}
            if(Input.GetKeyDown(KeyCode.Escape)){Close();return;}
            if(Phase==GamePhase.Instructions){if(Input.GetKeyDown(KeyCode.Return))StartAttempt();return;}
            if(Phase==GamePhase.Lost){if(Input.GetKeyDown(KeyCode.R))StartAttempt();return;}
            if(Phase==GamePhase.Won){if(Input.GetKeyDown(KeyCode.Return))ClaimReward();return;}
            AdvanceTime(Time.unscaledDeltaTime);
            if(Sequence){for(int i=0;i<3;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i)))Submit(i);}
            else if(Training){if(Input.GetKeyDown(KeyCode.A))Submit(0);else if(Input.GetKeyDown(KeyCode.D))Submit(1);}
            else if(Input.GetKeyDown(KeyCode.Space))Submit(0);
        }
        void OnDisable(){Close();}
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
            Box("GoodZone",bar.transform,new Vector2(.58f,0),new Vector2(.82f,1),Green);
            cursor=(RectTransform)Box("RhythmCursor",bar.transform,new Vector2(0,0),new Vector2(.012f,1),Color.white).transform;
            actionLabel=Button("Action",rhythm.transform,new Vector2(.15f,0),new Vector2(.85f,.46f),"",()=>Submit(Training?Progress%2:0));
            feedback=Text("Feedback",playing.transform,new Vector2(0,.06f),new Vector2(1,.24f),21,Color.white);
            var progress=Box("ProgressTrack",playing.transform,new Vector2(0,0),new Vector2(1,.025f),new Color(.2f,.23f,.24f));progressFill=(RectTransform)Box("ProgressFill",progress.transform,Vector2.zero,Vector2.one,Green).transform;
            result=Box("Result",card,new Vector2(.06f,.12f),new Vector2(.94f,.83f),Color.clear);
            resultText=Text("ResultText",result.transform,new Vector2(0,.3f),Vector2.one,26,Color.white);
            Button("RetryOrClaim",result.transform,new Vector2(.1f,0),new Vector2(.9f,.22f),"",()=>{if(Phase==GamePhase.Won)ClaimReward();else StartAttempt();});
        }
        void Refresh()
        {
            if(overlay==null||Active!=this)return;
            title.text=Facility.kind==CampFacility.FacilityKind.Medical?"QUÂN Y — BĂNG BÓ":Facility.kind==CampFacility.FacilityKind.Provisions?"BẾP TRẠI — BỮA HỒI SỨC":Training?"LUYỆN THỂ LỰC":"LỀU NGHỈ — ĐIỀU HÒA NHỊP THỞ";
            instructions.SetActive(Phase==GamePhase.Instructions);playing.SetActive(Phase==GamePhase.Playing);result.SetActive(Phase==GamePhase.Won||Phase==GamePhase.Lost);
            rules.text=Rules()+"\n\n"+Reward()+"\nChỉ nhận thưởng sau khi thắng. Esc: rời lều, không nhận thưởng.";
            stats.text=$"CÒN {Mathf.Max(0,TimeLimit-Elapsed):0.0}s    •    Hoàn thành {Progress}/{Goal}    •    Sai {Mistakes}/3";
            clockFill.anchorMax=new Vector2(Mathf.Clamp01(1-Elapsed/TimeLimit),1);progressFill.anchorMax=new Vector2((float)Progress/Goal,1);
            choices.SetActive(Sequence);rhythm.SetActive(!Sequence);
            task.text=Sequence?"BƯỚC CẦN LÀM: "+Item(Mathf.Min(Progress,2)):Training?"BƯỚC "+(Progress%2==0?"TRÁI [A]":"PHẢI [D]")+" KHI CON TRỎ VÀO VÙNG XANH":"NHẤN SPACE KHI CON TRỎ VÀO VÙNG XANH";
            for(int i=0;i<3;i++)optionLabels[i].text=$"[{i+1}]\n{Item(order[i])}";
            cursor.anchorMin=new Vector2(RhythmPosition*.988f,0);cursor.anchorMax=new Vector2(RhythmPosition*.988f+.012f,1);
            actionLabel.text=Training?(Progress%2==0?"BƯỚC TRÁI [A]":"BƯỚC PHẢI [D]"):"THỞ RA [SPACE]";feedback.text=Feedback;
            resultText.text=(Phase==GamePhase.Won?"HOÀN THÀNH\n\n"+Reward():"CHƯA HOÀN THÀNH\n\n"+Feedback)+"\n\nEsc: rời lều";
            result.transform.Find("RetryOrClaim/Label").GetComponent<TMP_Text>().text=Phase==GamePhase.Won?"NHẬN HỒI PHỤC [ENTER]":"THỬ LẠI [R]";
        }
        static GameObject Box(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;go.GetComponent<Image>().color=color;return go;}
        static TMP_Text Text(string name,Transform parent,Vector2 min,Vector2 max,float size,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;var text=go.GetComponent<TextMeshProUGUI>();StoryFont.Apply(text);text.fontSize=size;text.alignment=TextAlignmentOptions.Center;text.color=color;text.raycastTarget=false;return text;}
        static TMP_Text Button(string name,Transform parent,Vector2 min,Vector2 max,string label,UnityEngine.Events.UnityAction action)
        {var go=Box(name,parent,min,max,new Color(.18f,.32f,.29f));var button=go.AddComponent<Button>();button.targetGraphic=go.GetComponent<Image>();button.onClick.AddListener(action);var text=Text("Label",go.transform,new Vector2(.03f,.03f),new Vector2(.97f,.97f),21,Color.white);text.text=label;return text;}
    }
}
