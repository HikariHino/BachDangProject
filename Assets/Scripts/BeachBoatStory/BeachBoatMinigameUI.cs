using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Contextual presentation for the two playable camp activities.</summary>
    [DisallowMultipleComponent]
    public sealed class BeachBoatMinigameUI : MonoBehaviour
    {
        public static BeachBoatMinigameUI Instance { get; private set; }

        GameObject drumRoot, relayRoot, relayTravelRoot, failureRoot, choicesRoot;
        TMP_Text drumRound, expectedBeat, feedback, timer, relayTitle, relayHint, relayOrder, relayTime, failureTitle,drumTimingGoals,commandName;
        static readonly string[] Commands={"TẬP HỢP","TIẾN QUÂN","GIỮ TRẬN"};
        TMP_Text relayTravelTitle, relayTravelHint, relayTravelTime;
        RectTransform drumProgress, relayProgress;
        Image[] beatCards;
        TMP_Text[] beatLabels;
        BeachBoatMinigameStation shown;
        GameObject objectivePanel;
        int lastFeedback = -1;
        int lastRelayFeedback = -1;
        float relayErrorUntil;
        public bool CargoSelectionVisible => relayRoot != null && relayRoot.activeSelf;
        public bool TravelStatusVisible => relayTravelRoot != null && relayTravelRoot.activeSelf;

        static readonly Color Ink = new Color(.025f, .032f, .038f, .97f);
        static readonly Color Paper = new Color(.12f, .105f, .075f, .97f);
        static readonly Color Gold = new Color(1f, .72f, .2f);
        static readonly Color Green = new Color(.27f, .78f, .43f);
        static readonly Color Red = new Color(.95f, .31f, .23f);

        public void Initialize()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            var objective = transform.Find("Objective");
            objectivePanel = objective != null ? objective.gameObject : null;
            BuildDrumUI();
            BuildRelayUI();
            BuildFailureUI();
            HideAll();
        }

        void Update()
        {
            var active = BeachBoatMinigameStation.Active;
            if (active != shown)
            {
                shown = active;
                lastFeedback = -1;
                lastRelayFeedback = -1;
                relayErrorUntil = 0;
                if (objectivePanel != null) objectivePanel.SetActive(active == null);
                drumRoot.SetActive(active != null && active.kind == BeachBoatMinigameStation.ActivityKind.WarDrum);
                relayRoot.SetActive(active != null && active.kind == BeachBoatMinigameStation.ActivityKind.SupplyRelay);
                relayTravelRoot.SetActive(false);
            }
            failureRoot.SetActive(active != null && active.Failed);
            if (active == null) return;
            if (active.Failed)
            {
                drumRoot.SetActive(false); relayRoot.SetActive(false); relayTravelRoot.SetActive(false);
                failureTitle.text = active.kind == BeachBoatMinigameStation.ActivityKind.WarDrum
                    ? "HẾT GIỜ — HIỆU LỆNH CHƯA HOÀN THÀNH"
                    : "HẾT GIỜ — VẬT TƯ CHƯA TẬP KẾT ĐỦ";
                return;
            }
            drumRoot.SetActive(active.kind == BeachBoatMinigameStation.ActivityKind.WarDrum);
            if (active.kind == BeachBoatMinigameStation.ActivityKind.WarDrum) RefreshDrum(active);
            else RefreshRelay(active);
        }

        void RefreshDrum(BeachBoatMinigameStation game)
        {
            drumTimingGoals.text=CampaignProgress.Instance.TimingBands(0);
            commandName.text=game.ExpectedBeat>=1&&game.ExpectedBeat<=3?Commands[game.ExpectedBeat-1]:"HOÀN THÀNH";
            drumRound.text = $"LƯỢT {game.Round + 1}/2";
            expectedBeat.text = game.ExpectedBeat.ToString();
            timer.text = $"CÒN {game.TimeRemaining:0}s   •   Sai {game.Mistakes}";
            timer.color = game.TimeRemaining < 10f ? Red : Color.white;
            SetProgress(drumProgress, (game.Round * game.RhythmLength + game.Progress) / (game.RhythmLength * 2f));

            for (int i = 0; i < beatCards.Length; i++)
            {
                beatLabels[i].text = i < game.Progress ? "OK" : i == game.Progress ? game.GetRhythmBeat(i).ToString() : "·";
                beatCards[i].color = i < game.Progress ? Green : i == game.Progress ? Gold : new Color(.24f, .23f, .2f, 1f);
                beatLabels[i].color = i == game.Progress ? Color.black : Color.white;
            }

            if (lastFeedback == game.FeedbackVersion) return;
            lastFeedback = game.FeedbackVersion;
            if (lastFeedback == 0)
            {
                feedback.text = "Đọc hiệu lệnh rồi đánh trống trước khi hết giờ";
                feedback.color = new Color(.83f, .84f, .8f);
            }
            else
            {
                feedback.text = game.LastInputCorrect ? "ĐÚNG NHỊP!" : "SAI NHỊP — GIỮ BÌNH TĨNH";
                feedback.color = game.LastInputCorrect ? Green : Red;
            }
        }

        void RefreshRelay(BeachBoatMinigameStation game)
        {
            bool selecting = game.IsSelectingCargo;
            relayRoot.SetActive(selecting);
            relayTravelRoot.SetActive(!selecting);
            if (lastRelayFeedback != game.FeedbackVersion)
            {
                lastRelayFeedback = game.FeedbackVersion;
                if (lastRelayFeedback > 0 && !game.LastInputCorrect) relayErrorUntil = Time.unscaledTime + 2.5f;
            }
            int total = game.checkpoints != null ? game.checkpoints.Length : 0;
            relayTitle.text = $"VẬT TƯ RA TRẬN ĐỊA   {game.Progress}/{total}";
            relayTime.text = $"CÒN {game.TimeRemaining:0}s  •  Chọn sai {game.Mistakes}";
            relayTime.color = game.TimeRemaining < 20f ? Red : Color.white;
            relayOrder.text = game.ReturningToWarehouse
                ? "QUAY LẠI KHO NHẬN ĐƠN TIẾP"
                : game.IsSelectingCargo
                ? $"ĐƠN HÀNG {game.Progress + 1}: {BeachBoatMinigameStation.CargoName(game.RequestedCargo)}"
                : $"ĐANG CHỞ: {BeachBoatMinigameStation.CargoName(game.SelectedCargo)}";
            relayHint.text = game.ReturningToWarehouse
                ? $"Về kho lấy vật tư • {WorldDistance.Format(game.CurrentTargetDistance)}"
                : game.IsSelectingCargo
                ? "Đọc phiếu, chọn vật tư cùng tên ở kho (phím 1–3 hoặc bấm ô). Chọn sai mất 8 giây."
                : game.CurrentTargetDistance <= game.interactDistance
                    ? $"Đã tới biển GIAO {BeachBoatMinigameStation.CargoName(game.SelectedCargo)} — bấm E để giao"
                    : $"Theo biển GIAO {BeachBoatMinigameStation.CargoName(game.SelectedCargo)} • {WorldDistance.Format(game.CurrentTargetDistance)}";
            if (Time.unscaledTime < relayErrorUntil)
                relayHint.text = "SAI VẬT TƯ: xem tên trên phiếu và thùng hàng. Bị trừ 8 giây.";
            relayHint.color = Time.unscaledTime < relayErrorUntil ? Red : Color.white;
            choicesRoot.SetActive(game.IsSelectingCargo);
            SetProgress(relayProgress, total > 0 ? game.Progress / (float)total : 0f);
            relayTravelTitle.text = game.ReturningToWarehouse
                ? $"VỀ KHO NHẬN VẬT TƯ   •   {game.Progress}/{total}"
                : $"GIAO {BeachBoatMinigameStation.CargoName(game.SelectedCargo)}   •   {game.Progress}/{total}";
            relayTravelHint.text = game.ReturningToWarehouse
                ? $"Về kho • {WorldDistance.Format(game.CurrentTargetDistance)}"
                : game.CurrentTargetDistance <= game.interactDistance
                    ? "Đã tới điểm giao • Bấm E để giao vật tư"
                    : $"Theo biển giao hàng • {WorldDistance.Format(game.CurrentTargetDistance)}";
            relayTravelTime.text = $"CÒN {game.TimeRemaining:0}s";
            relayTravelTime.color = game.TimeRemaining < 20f ? Red : Color.white;
        }

        void BuildDrumUI()
        {
            drumRoot = Panel("DrumMinigame", transform, Vector2.zero, Vector2.one, new Color(.012f,.025f,.022f,.78f));
            var card = Panel("DrumBoard", drumRoot.transform, new Vector2(.08f, .08f), new Vector2(.92f, .92f), Ink);
            Outline(card, new Color(.72f, .49f, .17f, .9f), 2f);
            Panel("HeaderRule",card.transform,new Vector2(.04f,.80f),new Vector2(.96f,.804f),Gold);
            var title = Text("Title", card.transform, new Vector2(.04f, .87f), new Vector2(.74f, .97f), 34, FontStyles.Bold);
            title.text = "LUYỆN HIỆU LỆNH"; title.color = Gold;
            var subtitle=Text("CampCaption",card.transform,new Vector2(.04f,.82f),new Vector2(.74f,.88f),16);subtitle.text="SÂN LUYỆN QUÂN • DOANH TRẠI NGÔ QUYỀN";subtitle.color=new Color(.65f,.73f,.66f);
            drumRound = Text("Round", card.transform, new Vector2(.74f, .87f), new Vector2(.96f, .96f), 20, FontStyles.Bold);
            drumRound.alignment = TextAlignmentOptions.Right;
            Panel("CommandLedger",card.transform,new Vector2(.04f,.33f),new Vector2(.32f,.76f),new Color(.10f,.14f,.12f));
            var legend=Text("LegendTitle",card.transform,new Vector2(.06f,.68f),new Vector2(.30f,.74f),20,FontStyles.Bold);legend.text="QUÂN LỆNH";legend.color=Gold;
            for(int i=0;i<3;i++)
            {var row=Text("Command"+i,card.transform,new Vector2(.06f,.59f-i*.085f),new Vector2(.30f,.67f-i*.085f),20,FontStyles.Bold);row.text=$"{i+1}  /  {Commands[i]}";}
            var rule = Text("Rule", card.transform, new Vector2(.05f,.275f),new Vector2(.95f,.325f),16);
            rule.text="Đánh hiệu lệnh đang sáng • 2 lượt × 8 nhịp • Bấm 1 / 2 / 3 hoặc nút bên dưới • Sai: cộng 1 giây";rule.color=new Color(.73f,.78f,.71f);
            var face=new GameObject("WarDrumFace",typeof(RectTransform),typeof(CanvasRenderer),typeof(DrumFaceGraphic));face.transform.SetParent(card.transform,false);var rect=(RectTransform)face.transform;rect.anchorMin=new Vector2(.48f,.36f);rect.anchorMax=new Vector2(.83f,.69f);rect.offsetMin=rect.offsetMax=Vector2.zero;face.GetComponent<DrumFaceGraphic>().raycastTarget=false;
            var cue = Text("Cue", card.transform, new Vector2(.48f,.43f),new Vector2(.83f,.63f),72,FontStyles.Bold);
            expectedBeat = cue; cue.alignment = TextAlignmentOptions.Center; cue.color = Gold;
            commandName=Text("ActiveCommand",card.transform,new Vector2(.38f,.33f),new Vector2(.96f,.39f),22,FontStyles.Bold);commandName.alignment=TextAlignmentOptions.Center;commandName.color=Gold;
            beatCards = new Image[BeachBoatMinigameStation.DrumBeats]; beatLabels = new TMP_Text[BeachBoatMinigameStation.DrumBeats];
            for (int i = 0; i < beatCards.Length; i++)
            {
                float slot=.58f/beatCards.Length;float left = .38f + i * slot;
                var beat = Panel("Beat_" + (i + 1), card.transform, new Vector2(left,.70f),new Vector2(left+slot*.84f,.77f),new Color(.15f,.19f,.16f));
                beatCards[i] = beat.GetComponent<Image>();
                beatLabels[i] = Text("Number", beat.transform, Vector2.zero, Vector2.one, 18, FontStyles.Bold);
                beatLabels[i].alignment = TextAlignmentOptions.Center;
            }

            var track = Panel("ProgressTrack", card.transform, new Vector2(.04f,.25f),new Vector2(.96f,.263f),new Color(.20f,.20f,.18f));
            drumProgress = Panel("Fill", track.transform, Vector2.zero, new Vector2(0,1), Green).GetComponent<RectTransform>();
            feedback = Text("Feedback",card.transform,new Vector2(.04f,.17f),new Vector2(.65f,.23f),17,FontStyles.Bold);
            timer = Text("Timer",card.transform,new Vector2(.73f,.17f),new Vector2(.96f,.23f),20,FontStyles.Bold);
            timer.alignment = TextAlignmentOptions.Right;

            for (int key = 1; key <= 3; key++)
            {
                int value = key;
                float left = .04f + (key - 1) * .312f;
                var button = Button("Drum" + key, card.transform, new Vector2(left,.065f),new Vector2(left+.296f,.16f));
                var label = Text("Label",button.transform,Vector2.zero,Vector2.one,28,FontStyles.Bold);
                label.text = key + "  <size=65%>"+Commands[key-1]+"</size>"; label.alignment = TextAlignmentOptions.Center;
                button.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => BeachBoatMinigameStation.Active?.SubmitRhythm(value));
            }
            var exit = Text("Exit",card.transform,new Vector2(.76f,.012f),new Vector2(.96f,.05f),14);
            exit.text = "ESC  Thoát"; exit.alignment = TextAlignmentOptions.Right; exit.color = new Color(.7f,.7f,.66f);
            drumTimingGoals=Text("TimingGoals",card.transform,new Vector2(.04f,.012f),new Vector2(.75f,.05f),14);drumTimingGoals.color=Gold;
        }

        void BuildRelayUI()
        {
            relayRoot = Panel("RelayMinigame", transform, new Vector2(.23f,.53f), new Vector2(.77f,.94f), Ink);
            Outline(relayRoot, new Color(.72f,.49f,.17f,.75f), 1.5f);
            relayTitle = Text("Title",relayRoot.transform,new Vector2(.05f,.83f),new Vector2(.95f,.97f),23,FontStyles.Bold);
            relayTitle.color = Gold;
            relayTime = Text("Time",relayRoot.transform,new Vector2(.05f,.72f),new Vector2(.95f,.82f),19,FontStyles.Bold);
            relayOrder = Text("Order",relayRoot.transform,new Vector2(.05f,.57f),new Vector2(.95f,.72f),24,FontStyles.Bold);
            relayOrder.color = Gold;
            relayHint = Text("Hint",relayRoot.transform,new Vector2(.05f,.44f),new Vector2(.95f,.57f),16);
            choicesRoot = Panel("Choices",relayRoot.transform,new Vector2(.04f,.15f),new Vector2(.96f,.43f),Color.clear);
            choicesRoot.GetComponent<Image>().raycastTarget = false;
            for (int key = 1; key <= 3; key++)
            {
                int cargo = key;
                float left = .02f + (key-1) * .33f;
                var button = Button("Cargo" + key, choicesRoot.transform, new Vector2(left,.05f),new Vector2(left+.30f,.95f));
                var label = Text("Label",button.transform,Vector2.zero,Vector2.one,19,FontStyles.Bold);
                label.text = key + "  " + BeachBoatMinigameStation.CargoName(key);
                label.alignment = TextAlignmentOptions.Center;
                button.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => BeachBoatMinigameStation.Active?.ChooseCargo(cargo));
            }
            var track = Panel("ProgressTrack",relayRoot.transform,new Vector2(.05f,.07f),new Vector2(.95f,.12f),new Color(.20f,.20f,.18f));
            relayProgress = Panel("Fill",track.transform,Vector2.zero,new Vector2(0,1),Green).GetComponent<RectTransform>();
            // While walking, leave the center of the view clear. Only the warehouse
            // selection uses the large panel; the return trip uses this same compact HUD.
            relayTravelRoot = Panel("RelayTravelStatus",transform,new Vector2(0,1),new Vector2(0,1),Ink);
            var travelRect=relayTravelRoot.GetComponent<RectTransform>();
            travelRect.pivot=new Vector2(0,1);travelRect.anchoredPosition=new Vector2(24,-155);travelRect.sizeDelta=new Vector2(350,90);
            relayTravelRoot.GetComponent<Image>().raycastTarget=false;
            relayTravelTitle=Text("Title",relayTravelRoot.transform,new Vector2(.045f,.61f),new Vector2(.95f,.94f),18,FontStyles.Bold);
            relayTravelTitle.color=Gold;
            relayTravelHint=Text("Hint",relayTravelRoot.transform,new Vector2(.045f,.30f),new Vector2(.95f,.60f),16);
            relayTravelTime=Text("Time",relayTravelRoot.transform,new Vector2(.045f,.02f),new Vector2(.95f,.31f),16,FontStyles.Bold);
        }

        void BuildFailureUI()
        {
            failureRoot = Panel("MinigameFailed", transform, new Vector2(.24f,.33f), new Vector2(.76f,.68f), Ink);
            Outline(failureRoot, Red, 2f);
            failureTitle = Text("Title",failureRoot.transform,new Vector2(.06f,.66f),new Vector2(.94f,.90f),24,FontStyles.Bold);
            failureTitle.color = Red;
            failureTitle.alignment = TextAlignmentOptions.Center;
            var reason = Text("Reason",failureRoot.transform,new Vector2(.08f,.43f),new Vector2(.92f,.65f),18);
            reason.text = "Nhiệm vụ chưa hoàn thành. Hãy thử lại với hiệu lệnh mới.";
            reason.alignment = TextAlignmentOptions.Center;
            var retry = Button("Retry",failureRoot.transform,new Vector2(.23f,.13f),new Vector2(.77f,.38f));
            var label = Text("Label",retry.transform,Vector2.zero,Vector2.one,22,FontStyles.Bold);
            label.text = "CHƠI LẠI  [R]"; label.alignment = TextAlignmentOptions.Center;
            retry.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => BeachBoatMinigameStation.Active?.RetryActivity());
        }

        static GameObject Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color; return go;
        }

        static GameObject Button(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = Panel(name,parent,min,max,Paper); var button = go.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = go.GetComponent<Image>(); button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1f,.78f,.35f);
            colors.pressedColor = new Color(.7f,.46f,.15f); button.colors = colors;
            Outline(go,new Color(.75f,.52f,.2f,.8f),1.5f); return go;
        }

        static TMP_Text Text(string name, Transform parent, Vector2 min, Vector2 max, float size, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
            var text = go.GetComponent<TextMeshProUGUI>(); StoryFont.Apply(text); text.fontSize=size; text.fontStyle=style;
            text.color=Color.white; text.textWrappingMode=TextWrappingModes.Normal;
            var rect=text.rectTransform; rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=rect.offsetMax=Vector2.zero; return text;
        }

        static void Outline(GameObject target, Color color, float distance)
        {
            var outline = target.AddComponent<Outline>(); outline.effectColor=color; outline.effectDistance=new Vector2(distance,-distance);
        }

        static void SetProgress(RectTransform fill, float value)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }

        void HideAll()
        {
            drumRoot.SetActive(false); relayRoot.SetActive(false); relayTravelRoot.SetActive(false); failureRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
    public sealed class DrumFaceGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();var center=r.center;float radius=Mathf.Min(r.width,r.height)*.48f;
            void Disc(Vector2 c,float outer,Color tint)
            {
                int start=vh.currentVertCount;vh.AddVert(c,tint,Vector2.zero);
                for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;vh.AddVert(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*outer,tint,Vector2.zero);if(i>0)vh.AddTriangle(start,start+i,start+i+1);}
            }
            Disc(center,radius,new Color(.36f,.21f,.09f));Disc(center,radius*.94f,new Color(.83f,.61f,.29f));Disc(center,radius*.88f,new Color(.21f,.15f,.075f));Disc(center,radius*.82f,new Color(.56f,.39f,.19f));Disc(center,radius*.74f,new Color(.29f,.23f,.13f));
            for(int i=0;i<16;i++){float angle=i*Mathf.PI/8;Disc(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius*.85f,radius*.025f,new Color(.96f,.79f,.43f));}
        }
    }
}
