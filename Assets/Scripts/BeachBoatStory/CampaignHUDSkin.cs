using TMPro;
using UnityEngine;
namespace BeachBoatStory
{
    public sealed class CampaignHUDSkin:MonoBehaviour
    {
        public static bool Ready {get;private set;}
        GameObject chronicle;TMP_Text timeLabel,tideLabel;UnityEngine.UI.Image health;
        public void Build()
        {
            var story=BeachBoatQuestSession.Instance.GetComponent<BachDangStoryManager>();var objective=story.objectiveUIPanel;
            CampaignHUDTheme.Dock(objective,new Vector2(1,1),new Vector2(1,1),new Vector2(-22,-24),new Vector2(292,174));CampaignHUDTheme.Plate(objective,true);
            CampaignHUDTheme.Dock(story.objectiveTitleText.gameObject,Vector2.up,Vector2.up,new Vector2(17,-15),new Vector2(258,30));story.objectiveTitleText.fontSize=19;story.objectiveTitleText.color=CampaignHUDTheme.Gold;
            CampaignHUDTheme.Dock(story.objectiveDescText.gameObject,Vector2.up,Vector2.up,new Vector2(17,-49),new Vector2(258,108));story.objectiveDescText.fontSize=15;story.objectiveDescText.fontSizeMax=15;story.objectiveDescText.fontSizeMin=12;story.objectiveDescText.color=CampaignHUDTheme.Paper;
            foreach(string name in new[]{"Vitals","RowboatDurability","ControlsBar","Interaction","RouteGuide"}){var child=transform.Find(name);if(child!=null)CampaignHUDTheme.Plate(child.gameObject,name=="Vitals"||name=="Interaction");}
            foreach(var text in GetComponentsInChildren<TMP_Text>(true)){text.raycastTarget=false;if(text.name=="Controls")text.color=CampaignHUDTheme.Muted;}
            var vital=transform.Find("Vitals");
            void Anchor(string path,Vector2 min,Vector2 max){var r=(RectTransform)vital.Find(path);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
            Anchor("HealthLabel",new Vector2(.055f,.72f),new Vector2(.95f,.95f));Anchor("StaminaLabel",new Vector2(.055f,.28f),new Vector2(.95f,.50f));
            Anchor("HealthTrack",new Vector2(.055f,.57f),new Vector2(.945f,.67f));Anchor("StaminaTrack",new Vector2(.055f,.13f),new Vector2(.945f,.20f));
            health=vital.Find("HealthTrack/HealthFill").GetComponent<UnityEngine.UI.Image>();
            foreach(string track in new[]{"HealthTrack","StaminaTrack"})for(int i=1;i<4;i++)
            {var go=new GameObject("QuarterMark",typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(vital.Find(track),false);var r=(RectTransform)go.transform;r.anchorMin=new Vector2(i*.25f,0);r.anchorMax=new Vector2(i*.25f,1);r.sizeDelta=new Vector2(1,0);r.anchoredPosition=Vector2.zero;go.GetComponent<UnityEngine.UI.Image>().color=new Color(.04f,.08f,.07f,.45f);go.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;}
            var route=transform.Find("RouteGuide");if(route!=null)CampaignHUDTheme.Dock(route.gameObject,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-24),new Vector2(338,60));
            chronicle=new GameObject("CampaignChronicle",typeof(RectTransform));chronicle.transform.SetParent(transform,false);CampaignHUDTheme.Dock(chronicle,Vector2.up,Vector2.up,new Vector2(22,-24),new Vector2(232,74));CampaignHUDTheme.Plate(chronicle);
            timeLabel=CampaignHUDTheme.Label("Year",chronicle.transform,new Vector2(14,-11),new Vector2(210,23),15,"BẠCH ĐẰNG  /  938");timeLabel.color=CampaignHUDTheme.Gold;
            tideLabel=CampaignHUDTheme.Label("Tide",chronicle.transform,new Vector2(14,-37),new Vector2(210,27),12);Ready=true;
        }
        void Update()
        {
            if(chronicle==null)return;var vitals=BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>();health.color=vitals.HealthFraction<=.25f?new Color(.96f,.32f,.23f):new Color(.72f,.20f,.16f);bool show=BeachBoatQuestSession.Instance.storyMode&&!BeachBoatQuestSession.InteractionBlocked&&!OpeningEvent.IsOpeningActive&&BeachBoatMinigameStation.Active==null;
            chronicle.SetActive(show);if(!show)return;var timeline=BachDangBattleTimeline.Instance;var tide=TideSystem.Instance;
            tideLabel.text=timeline==null?"HÀNH TRÌNH GIỮ NƯỚC":timeline.CurrentMoment==BachDangBattleTimeline.BattleMoment.Ambush?"CỌC ĐÃ LỘ • PHẢN CÔNG":tide!=null&&tide.IsControlledEbb?"THỦY TRIỀU ĐANG RÚT":timeline.TideLabel;
        }
        void OnDestroy(){Ready=false;}
    }
}
