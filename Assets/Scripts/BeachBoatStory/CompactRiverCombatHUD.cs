using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Edge-mounted icon prompts; the center is reserved for aiming and the world.</summary>
    public sealed class CompactRiverCombatHUD : MonoBehaviour
    {
        RiverPlayerCombat combat;
        TMP_Text enemy,kills,allied,ammo,notice,tideLabel;
        Image tideFill;
        CombatHudIcon[] actions;
        public RectTransform Cluster {get;private set;}
        public int ActionIconCount=>actions?.Length??0;
        static readonly Color Gold=new Color(1,.80f,.43f),White=new Color(.92f,.96f,.94f),Panel=new Color(.025f,.045f,.055f,.80f);
        static GameObject Rect(string name,Transform parent,float x,float y,float width,float height)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=Vector2.zero;r.pivot=Vector2.zero;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,height);return go;
        }
        static Image Back(GameObject go,Color color){var i=go.AddComponent<Image>();i.color=color;i.raycastTarget=false;return i;}
        static TMP_Text Text(string name,Transform parent,float x,float y,float width,float height,float size,string value="")
        {
            var t=Rect(name,parent,x,y,width,height).AddComponent<TextMeshProUGUI>();StoryFont.Apply(t);t.fontSize=size;t.text=value;t.color=White;t.alignment=TextAlignmentOptions.MidlineLeft;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;return t;
        }
        static CombatHudIcon Icon(string name,Transform parent,float x,float y,float size,CombatHudIcon.Symbol symbol,Color color)
        {
            var go=Rect(name,parent,x,y,size,size);go.AddComponent<CanvasRenderer>();var icon=go.AddComponent<CombatHudIcon>();icon.symbol=symbol;icon.color=color;icon.raycastTarget=false;return icon;
        }
        public void Initialize(RiverPlayerCombat owner)
        {
            combat=owner;var group=Rect("CompactBattleCluster",transform,0,0,224,198);Cluster=(RectTransform)group.transform;
            Cluster.anchorMin=Cluster.anchorMax=new Vector2(1,0);Cluster.pivot=new Vector2(1,0);Cluster.anchoredPosition=new Vector2(-14,14);
            var stats=Rect("CombatCounters",group.transform,0,104,224,36);Back(stats,Panel);
            Icon("EnemyIcon",stats.transform,7,8,21,CombatHudIcon.Symbol.Enemy,new Color(1,.56f,.44f));enemy=Text("EnemyRemaining",stats.transform,32,2,39,32,20);
            Icon("PlayerKillsIcon",stats.transform,77,8,21,CombatHudIcon.Symbol.Sword,Gold);kills=Text("PlayerKills",stats.transform,103,2,30,32,18);
            Icon("AlliedKillsIcon",stats.transform,147,8,21,CombatHudIcon.Symbol.Allies,new Color(.56f,.80f,1));allied=Text("AlliedKills",stats.transform,176,2,40,32,18);
            var weapon=Rect("ArrowSupply",group.transform,0,64,224,36);Back(weapon,Panel);Icon("BowIcon",weapon.transform,10,2,32,CombatHudIcon.Symbol.Bow,White);ammo=Text("ArrowCount",weapon.transform,53,0,165,36,26);
            actions=new CombatHudIcon[4];var symbols=new[]{CombatHudIcon.Symbol.Sword,CombatHudIcon.Symbol.Swim,CombatHudIcon.Symbol.Climb,CombatHudIcon.Symbol.Reload};
            string[] keys={"Q","F","SPACE","R"},labels={"KIẾM","BƠI","LEO TÀU","NẠP TÊN"};
            for(int i=0;i<4;i++)
            {
                var tile=Rect("Action_"+keys[i],group.transform,i*57,0,53,58);Back(tile,Panel);
                actions[i]=Icon("Icon",tile.transform,12,22,29,symbols[i],White);
                var key=Text("Key",tile.transform,1,1,51,17,11,keys[i]);key.alignment=TextAlignmentOptions.Center;key.color=Gold;
                var caption=Text("Caption",tile.transform,0,49,53,9,8,labels[i]);caption.alignment=TextAlignmentOptions.Center;
            }
            var water=Rect("TideProgress",group.transform,0,147,224,51);Back(water,Panel);Icon("TideIcon",water.transform,8,15,26,CombatHudIcon.Symbol.Swim,new Color(.50f,.83f,.95f));
            tideLabel=Text("TideState",water.transform,43,11,174,37,13);var track=Rect("TideTrack",water.transform,8,6,208,3);Back(track,new Color(.23f,.30f,.31f));
            var fill=Rect("TideFill",track.transform,0,0,208,3);tideFill=Back(fill,Gold);
            notice=Text("CombatNotice",group.transform,-70,202,294,42,13);notice.alignment=TextAlignmentOptions.BottomRight;
            notice.enableAutoSizing=true;notice.fontSizeMin=11;notice.fontSizeMax=13;notice.maxVisibleLines=2;notice.overflowMode=TextOverflowModes.Ellipsis;
        }
        public void Refresh(string message,bool threat)
        {
            enemy.text=combat.RemainingEnemies.ToString();kills.text=combat.Kills.ToString();allied.text=combat.AlliedKills.ToString();
            ammo.text=combat.IsReloading?"ĐANG NẠP…":$"{combat.Arrows} <size=17><color=#9AAEAB>/ 6</color></size>";
            var boarding=RiverBoardingCombat.Instance;
            actions[0].color=boarding!=null&&boarding.SwordDrawn?Gold:White;
            actions[1].color=boarding!=null&&boarding.State==RiverBoardingCombat.PlayerState.Swimming?Gold:White;
            actions[2].color=boarding!=null&&boarding.State==RiverBoardingCombat.PlayerState.Climbing?Gold:White;
            actions[3].color=combat.IsReloading?Gold:combat.Arrows==6?new Color(.45f,.53f,.52f):White;
            notice.text=message;notice.color=threat?new Color(1,.44f,.33f):White;
            var fleet=EnemyFleetDirector.Instance;var tide=TideSystem.Instance;float progress=0;
            if(fleet==null)return;
            if(fleet.AmbushStarted){tideLabel.text=$"CỌC ĐÃ LỘ\nThuyền đắm {BachDangStoryManager.Instance.destroyedEnemyShips}/{fleet.ShipCount}";progress=1;tideFill.color=new Color(.42f,.88f,.65f);}
            else if(fleet.EbbStarted&&tide!=null){progress=tide.EbbProgress;tideLabel.text=$"THỦY TRIỀU ĐANG RÚT\nCòn {tide.EbbSecondsRemaining:0}s • Cọc dần lộ";tideFill.color=new Color(.50f,.83f,.95f);}
            else{tideLabel.text=$"ĐANG NHỬ ĐỊCH\nĐã vào bãi cọc {fleet.ShipsInside}/{fleet.ShipCount}";progress=fleet.ShipsInside/(float)Mathf.Max(1,fleet.ShipCount);tideFill.color=Gold;}
            tideFill.rectTransform.sizeDelta=new Vector2(208*Mathf.Clamp01(progress),3);
        }
    }
}
