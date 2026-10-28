using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Dedicated range scoreboard, leaving the centre of the firing lane clear.</summary>
    public sealed class ArcheryTrainingHUD
    {
        readonly ArcheryTraining range;
        readonly GameObject root,result;
        readonly TMP_Text score,hits,target,feedback,summary,grade,verdict,best;
        readonly Image progress;
        readonly TMP_Text[] shotText=new TMP_Text[6];
        readonly Image[] shotBox=new Image[6];
        static readonly Color Gold=new Color(.94f,.72f,.34f),Green=new Color(.38f,.86f,.62f),Muted=new Color(.65f,.71f,.69f),Red=new Color(.95f,.48f,.36f),Dark=new Color(.035f,.09f,.085f,.93f);
        public Canvas Canvas {get;}
        public ArcheryTrainingHUD(ArcheryTraining owner)
        {
            range=owner;root=new GameObject("ArcheryTrainingHUD");root.transform.SetParent(owner.transform,false);
            Canvas=root.AddComponent<Canvas>();Canvas.renderMode=RenderMode.ScreenSpaceOverlay;Canvas.sortingOrder=160;
            var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;root.AddComponent<GraphicRaycaster>();
            var header=Panel("Range_scoreboard",root.transform,new Vector2(.22f,.79f),new Vector2(.78f,.985f),Dark);
            Panel("Gold_rule",header.transform,new Vector2(0,.97f),Vector2.one,Gold);
            Text("Title",header.transform,new Vector2(.035f,.68f),new Vector2(.65f,.95f),23,Gold,"THAO TRƯỜNG  /  LUYỆN CUNG",TextAlignmentOptions.MidlineLeft);
            best=Text("Best",header.transform,new Vector2(.68f,.69f),new Vector2(.97f,.95f),15,Muted,"",TextAlignmentOptions.MidlineRight);
            score=Text("Score",header.transform,new Vector2(.035f,.22f),new Vector2(.25f,.68f),29,Color.white,"",TextAlignmentOptions.MidlineLeft);
            hits=Text("Goal",header.transform,new Vector2(.28f,.26f),new Vector2(.59f,.65f),18,Muted,"",TextAlignmentOptions.MidlineLeft);
            target=Text("Next",header.transform,new Vector2(.61f,.26f),new Vector2(.97f,.65f),18,Gold,"",TextAlignmentOptions.MidlineRight);
            var bar=Panel("Goal_background",header.transform,new Vector2(.035f,.08f),new Vector2(.97f,.135f),new Color(.2f,.28f,.25f));
            progress=Panel("Four_hits_goal",bar.transform,Vector2.zero,new Vector2(0,1),Green).GetComponent<Image>();
            var shots=Panel("Six_arrows",root.transform,new Vector2(.32f,.715f),new Vector2(.68f,.775f),Color.clear);
            for(int i=0;i<6;i++)
            {
                float x=i/6f;var box=Panel("Arrow_"+(i+1),shots.transform,new Vector2(x+.008f,0),new Vector2(x+1/6f-.008f,1),Dark);shotBox[i]=box.GetComponent<Image>();
                shotText[i]=Text("Points",box.transform,Vector2.zero,Vector2.one,18,Muted,(i+1).ToString(),TextAlignmentOptions.Center);
            }
            feedback=Text("Hit_feedback",root.transform,new Vector2(.25f,.19f),new Vector2(.75f,.245f),23,Gold,"",TextAlignmentOptions.Center);
            var controls=Panel("Range_controls",root.transform,new Vector2(.14f,.035f),new Vector2(.86f,.145f),Dark);
            Text("Aim_and_release",controls.transform,new Vector2(.025f,.49f),new Vector2(.975f,.95f),19,Color.white,"CHUỘT PHẢI: NGẮM    •    GIỮ CHUỘT TRÁI: KÉO DÂY    •    THẢ: BẮN",TextAlignmentOptions.Center);
            Text("Scoring_rules",controls.transform,new Vector2(.025f,.05f),new Vector2(.975f,.46f),16,Muted,"Tâm 10  /  Vòng giữa 7  /  Vòng ngoài 4  •  Chỉ tính bia vòng vàng  •  ESC rời bãi",TextAlignmentOptions.Center);
            result=Panel("Round_results",root.transform,new Vector2(.26f,.25f),new Vector2(.74f,.70f),Dark);
            Panel("Result_rule",result.transform,new Vector2(0,.985f),Vector2.one,Gold);
            Text("Result_title",result.transform,new Vector2(.05f,.82f),new Vector2(.95f,.96f),22,Gold,"KẾT QUẢ LUYỆN CUNG",TextAlignmentOptions.Center);
            grade=Text("Practice_grade",result.transform,new Vector2(.06f,.39f),new Vector2(.31f,.81f),86,Gold,"",TextAlignmentOptions.Center);
            Text("Grade_rules",result.transform,new Vector2(.04f,.245f),new Vector2(.96f,.29f),12,Muted,"Hạng luyện tập: S ≥ 50  •  A ≥ 40  •  B ≥ 25  •  C < 25 điểm",TextAlignmentOptions.Center);
            summary=Text("Round_stats",result.transform,new Vector2(.36f,.44f),new Vector2(.94f,.8f),23,Color.white,"",TextAlignmentOptions.MidlineLeft);
            verdict=Text("Pass_requirement",result.transform,new Vector2(.06f,.27f),new Vector2(.94f,.41f),18,Green,"",TextAlignmentOptions.Center);
            Button("Replay",result.transform,new Vector2(.06f,.07f),new Vector2(.48f,.22f),"TẬP LẠI  [R]",()=>range.Replay());
            Button("Return",result.transform,new Vector2(.52f,.07f),new Vector2(.94f,.22f),"RỜI BÃI  [ENTER]",()=>range.Close());
            root.SetActive(false);
        }
        public void Refresh()
        {
            bool active=ArcheryTraining.Active==range&&!BachDangFrontEnd.IsBlocking&&!CampaignJournalUI.IsOpen;root.SetActive(active);if(!active)return;
            bool done=range.Finished;result.SetActive(done);
            score.text=$"{range.Score}<size=17> / 60 ĐIỂM</size>";hits.text=$"TRÚNG {range.Hits}/6  •  CẦN 4";hits.color=range.Hits>=4?Green:Muted;
            best.text=$"KỶ LỤC {range.BestScore}/60";
            target.text=done?"ĐÃ HẾT LƯỢT":range.Shots>=6?"TÊN ĐANG BAY…":range.CanFire?$"MŨI {range.Shots+1}/6  •  BIA {range.NextTarget.index+1}":"BIA ĐANG DỰNG LẠI…";
            progress.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(range.Hits/4f),1);
            for(int i=0;i<6;i++)
            {
                int points=range.ShotScore(i);shotText[i].text=points==-1?(i+1).ToString():points==-2?"…":points==0?"×":"+"+points;
                shotBox[i].color=points>0?new Color(.08f,.31f,.2f,.96f):points==0?new Color(.35f,.12f,.09f,.96f):Dark;
                shotText[i].color=points>0?Green:points==0?Red:i==range.Shots?Gold:Muted;
            }
            feedback.text=done?"":range.LastFeedback;
            if(!done)return;
            grade.text=range.Grade;summary.text=$"{range.Score}/60 điểm\nTrúng {range.Hits}/6  •  {(range.RoundSeconds):0.0}s";
            verdict.color=range.Hits>=4?Green:Red;verdict.text=range.Hits>=4?"ĐẠT • Hoàn thành bài tập bắt buộc trước trận":"CHƯA ĐẠT • Cần trúng ít nhất 4/6 bia";
        }
        static GameObject Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;var image=go.AddComponent<Image>();image.color=color;image.raycastTarget=false;return go;
        }
        static TMP_Text Text(string name,Transform parent,Vector2 min,Vector2 max,float size,Color color,string value,TextAlignmentOptions alignment)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var text=go.AddComponent<TextMeshProUGUI>();StoryFont.Apply(text);text.text=value;text.fontSize=size;text.color=color;text.alignment=alignment;text.textWrappingMode=TextWrappingModes.Normal;text.raycastTarget=false;return text;
        }
        static void Button(string name,Transform parent,Vector2 min,Vector2 max,string label,UnityEngine.Events.UnityAction click)
        {
            var go=Panel(name,parent,min,max,new Color(.22f,.3f,.27f));var image=go.GetComponent<Image>();image.raycastTarget=true;var button=go.AddComponent<Button>();button.targetGraphic=image;var colors=button.colors;colors.highlightedColor=new Color(1,.86f,.62f);button.colors=colors;button.onClick.AddListener(click);Text("Label",go.transform,Vector2.zero,Vector2.one,18,Color.white,label,TextAlignmentOptions.Center);
        }
        public void Dispose(){if(root!=null)Object.Destroy(root);}
    }
}
