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

        GameObject drumRoot, relayRoot;
        TMP_Text drumRound, expectedBeat, feedback, timer, relayTitle, relayHint;
        RectTransform drumProgress, relayProgress;
        Image[] beatCards;
        TMP_Text[] beatLabels;
        BeachBoatMinigameStation shown;
        GameObject objectivePanel;
        int lastFeedback = -1;

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
            HideAll();
        }

        void Update()
        {
            var active = BeachBoatMinigameStation.Active;
            if (active != shown)
            {
                shown = active;
                lastFeedback = -1;
                if (objectivePanel != null) objectivePanel.SetActive(active == null);
                drumRoot.SetActive(active != null && active.kind == BeachBoatMinigameStation.ActivityKind.WarDrum);
                relayRoot.SetActive(active != null && active.kind == BeachBoatMinigameStation.ActivityKind.SupplyRelay);
            }
            if (active == null) return;
            if (active.kind == BeachBoatMinigameStation.ActivityKind.WarDrum) RefreshDrum(active);
            else RefreshRelay(active);
        }

        void RefreshDrum(BeachBoatMinigameStation game)
        {
            drumRound.text = $"LƯỢT {game.Round + 1}/2";
            expectedBeat.text = game.ExpectedBeat.ToString();
            timer.text = $"{game.Elapsed:0.0}s   •   Sai {game.Mistakes}";
            SetProgress(drumProgress, (game.Round * game.RhythmLength + game.Progress) / (game.RhythmLength * 2f));

            for (int i = 0; i < beatCards.Length; i++)
            {
                beatLabels[i].text = game.GetRhythmBeat(i).ToString();
                beatCards[i].color = i < game.Progress ? Green : i == game.Progress ? Gold : new Color(.24f, .23f, .2f, 1f);
                beatLabels[i].color = i == game.Progress ? Color.black : Color.white;
            }

            if (lastFeedback == game.FeedbackVersion) return;
            lastFeedback = game.FeedbackVersion;
            if (lastFeedback == 0)
            {
                feedback.text = "Nhìn nhịp sáng và chọn đúng tiếng trống";
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
            int total = game.checkpoints != null ? game.checkpoints.Length : 0;
            relayTitle.text = $"TIẾP TẾ DOANH TRẠI   {game.Progress}/{total}";
            relayHint.text = game.CurrentTargetDistance <= game.interactDistance
                ? "Đã tới điểm tập kết — bấm E để giao vật tư"
                : $"Theo cột sáng phía trước   •   Còn {Mathf.RoundToInt(game.CurrentTargetDistance)} m";
            SetProgress(relayProgress, total > 0 ? game.Progress / (float)total : 0f);
        }

        void BuildDrumUI()
        {
            drumRoot = Panel("DrumMinigame", transform, Vector2.zero, Vector2.one, new Color(0, 0, 0, .48f));
            var card = Panel("DrumBoard", drumRoot.transform, new Vector2(.18f, .13f), new Vector2(.82f, .86f), Ink);
            Outline(card, new Color(.72f, .49f, .17f, .9f), 2f);

            var title = Text("Title", card.transform, new Vector2(.05f, .86f), new Vector2(.72f, .96f), 30, FontStyles.Bold);
            title.text = "HIỆU LỆNH TRỐNG TRẬN"; title.color = Gold;
            drumRound = Text("Round", card.transform, new Vector2(.72f, .87f), new Vector2(.95f, .95f), 20, FontStyles.Bold);
            drumRound.alignment = TextAlignmentOptions.Right;
            var rule = Text("Rule", card.transform, new Vector2(.05f, .76f), new Vector2(.95f, .85f), 18);
            rule.text = "Đánh theo dãy hiệu lệnh. Sai nhịp sẽ lùi một bước.";
            rule.color = new Color(.82f, .82f, .77f);

            var cue = Text("Cue", card.transform, new Vector2(.35f, .54f), new Vector2(.65f, .76f), 72, FontStyles.Bold);
            expectedBeat = cue; cue.alignment = TextAlignmentOptions.Center; cue.color = Gold;

            beatCards = new Image[12]; beatLabels = new TMP_Text[12];
            for (int i = 0; i < beatCards.Length; i++)
            {
                float left = .055f + i * .074f;
                var beat = Panel("Beat_" + (i + 1), card.transform, new Vector2(left, .43f), new Vector2(left + .059f, .51f), new Color(.24f,.23f,.2f));
                beatCards[i] = beat.GetComponent<Image>();
                beatLabels[i] = Text("Number", beat.transform, Vector2.zero, Vector2.one, 18, FontStyles.Bold);
                beatLabels[i].alignment = TextAlignmentOptions.Center;
            }

            var track = Panel("ProgressTrack", card.transform, new Vector2(.055f,.365f), new Vector2(.945f,.39f), new Color(.20f,.20f,.18f));
            drumProgress = Panel("Fill", track.transform, Vector2.zero, new Vector2(0,1), Green).GetComponent<RectTransform>();
            feedback = Text("Feedback",card.transform,new Vector2(.05f,.28f),new Vector2(.70f,.35f),18,FontStyles.Bold);
            timer = Text("Timer",card.transform,new Vector2(.70f,.28f),new Vector2(.95f,.35f),17);
            timer.alignment = TextAlignmentOptions.Right;

            for (int key = 1; key <= 3; key++)
            {
                int value = key;
                float left = .17f + (key - 1) * .23f;
                var button = Button("Drum" + key, card.transform, new Vector2(left,.085f),new Vector2(left+.19f,.245f));
                var label = Text("Label",button.transform,Vector2.zero,Vector2.one,28,FontStyles.Bold);
                label.text = key + "\n<size=55%>ĐÁNH TRỐNG</size>"; label.alignment = TextAlignmentOptions.Center;
                button.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => BeachBoatMinigameStation.Active?.SubmitRhythm(value));
            }
            var exit = Text("Exit",card.transform,new Vector2(.72f,.015f),new Vector2(.95f,.07f),14);
            exit.text = "ESC  Thoát"; exit.alignment = TextAlignmentOptions.Right; exit.color = new Color(.7f,.7f,.66f);
        }

        void BuildRelayUI()
        {
            relayRoot = Panel("RelayMinigame", transform, new Vector2(.32f,.70f), new Vector2(.68f,.84f), Ink);
            Outline(relayRoot, new Color(.72f,.49f,.17f,.75f), 1.5f);
            relayTitle = Text("Title",relayRoot.transform,new Vector2(.05f,.60f),new Vector2(.95f,.92f),20,FontStyles.Bold);
            relayTitle.color = Gold;
            relayHint = Text("Hint",relayRoot.transform,new Vector2(.05f,.26f),new Vector2(.95f,.60f),16);
            var track = Panel("ProgressTrack",relayRoot.transform,new Vector2(.05f,.10f),new Vector2(.95f,.20f),new Color(.20f,.20f,.18f));
            relayProgress = Panel("Fill",track.transform,Vector2.zero,new Vector2(0,1),Green).GetComponent<RectTransform>();
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
            drumRoot.SetActive(false); relayRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
