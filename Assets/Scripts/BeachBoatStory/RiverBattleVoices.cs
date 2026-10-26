using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Phase-driven offline battle voices, with separate invader and commander casts.</summary>
    public sealed class RiverBattleVoices : MonoBehaviour
    {
        AudioSource enemy,crowd,commander;
        AudioClip[] advances,panics,cries;AudioClip ambushCommand,attackCommand,advanceCrowd;
        TMP_Text caption;
        bool invasionPlayed,ambushPlayed,counterattackPlayed,active,paused,secondCommand;
        float nextEnemy,nextCry,commandTime,nextCommand;
        public int InvasionCues {get;private set;}
        public int PanicCues {get;private set;}
        public int CommanderCues {get;private set;}
        public bool AssetsReady=>advances.Length==2&&panics.Length==3&&ambushCommand!=null&&attackCommand!=null&&advanceCrowd!=null&&
            System.Array.TrueForAll(advances,c=>c!=null)&&System.Array.TrueForAll(panics,c=>c!=null)&&System.Array.TrueForAll(cries,c=>c!=null);
        public bool InvasionPlayed=>invasionPlayed;
        public bool AmbushPlayed=>ambushPlayed;
        AudioSource Source(string label,float volume,float spatial,int priority)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);var source=go.AddComponent<AudioSource>();
            source.playOnAwake=false;source.loop=false;source.volume=volume;source.spatialBlend=spatial;source.priority=priority;
            source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=250;source.maxDistance=5000;source.dopplerLevel=0;return source;
        }
        static AudioClip Cue(string name)=>Resources.Load<AudioClip>("BachDang938/BattleVoices/"+name);
        void Awake()
        {
            enemy=Source("EnemyBattleSpeech",.85f,.15f,55);crowd=Source("EnemyPanicCrowd",.40f,.15f,75);commander=Source("NgoQuyenBattleCommand",1,0,12);
            advances=new[]{Cue("Enemy_Advance_1"),Cue("Enemy_Advance_2")};panics=new[]{Cue("Enemy_Panic_1"),Cue("Enemy_Panic_2"),Cue("Enemy_Panic_3")};
            cries=new[]{Resources.Load<AudioClip>("BachDang938/WreckAudio/Shipwreck_Cry_1"),Resources.Load<AudioClip>("BachDang938/WreckAudio/Shipwreck_Cry_2"),Resources.Load<AudioClip>("BachDang938/WreckAudio/Shipwreck_Cry_3")};
            ambushCommand=Cue("NgoQuyen_Ambush");attackCommand=Cue("NgoQuyen_Attack");
            advanceCrowd=Cue("Enemy_Cheer_Crowd");
            var canvas=new GameObject("BattleCommanderCaption",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=100;
            var scale=canvas.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);
            var go=new GameObject("Caption",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(canvas.transform,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=new Vector2(.27f,.02f);rect.anchorMax=new Vector2(.76f,.075f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            caption=go.GetComponent<TextMeshProUGUI>();StoryFont.Apply(caption);caption.fontSize=17;caption.enableAutoSizing=true;caption.fontSizeMin=13;caption.fontSizeMax=17;
            caption.color=new Color(1,.85f,.57f);caption.alignment=TextAlignmentOptions.Center;caption.raycastTarget=false;caption.gameObject.SetActive(false);
        }
        public void FillSave(BachDangSaveData.Fleet data){data.invasionVoiced=invasionPlayed;data.ambushVoiced=ambushPlayed;data.counterattackVoiced=counterattackPlayed;data.counterattackDelay=secondCommand?Mathf.Max(.5f,nextCommand):0;}
        public void RestoreSave(BachDangSaveData.Fleet data){Silence();invasionPlayed=data.invasionVoiced;ambushPlayed=data.ambushVoiced;counterattackPlayed=data.counterattackVoiced;secondCommand=ambushPlayed&&!counterattackPlayed;nextCommand=Mathf.Max(.5f,data.counterattackDelay);nextEnemy=3;nextCry=4;}
        public void ResetForNewBattle(){Silence();invasionPlayed=ambushPlayed=counterattackPlayed=false;nextEnemy=nextCry=0;}
        public void Silence()
        {
            enemy.Stop();crowd.Stop();commander.Stop();caption.gameObject.SetActive(false);commandTime=0;secondCommand=false;active=false;paused=false;
        }
        void Update()
        {
            bool blocked=Time.timeScale<=0||BachDangFrontEnd.IsBlocking||OpeningEvent.IsOpeningActive||(StoryDialogueUI.Instance?.IsOpen??false);
            if(blocked!=paused)
            {
                if(blocked){enemy.Pause();crowd.Pause();commander.Pause();caption.gameObject.SetActive(false);}
                else{enemy.UnPause();crowd.UnPause();commander.UnPause();}paused=blocked;
            }
            if(!blocked)Step(Time.deltaTime);
        }
        void PlayEnemy(AudioClip clip,bool panic)
        {
            if(clip==null)return;enemy.clip=clip;enemy.pitch=Random.Range(.97f,1.03f);enemy.Play();
            if(!panic&&advanceCrowd!=null){crowd.pitch=Random.Range(.98f,1.02f);crowd.PlayOneShot(advanceCrowd);}
            if(panic)PanicCues++;else InvasionCues++;
        }
        void Command(AudioClip clip,string text)
        {
            if(clip==null)return;commander.clip=clip;commander.pitch=1;commander.Play();commandTime=clip.length+.4f;
            caption.text="Ngô Quyền: "+text;caption.gameObject.SetActive(true);CommanderCues++;
        }
        public void Step(float dt)
        {
            if(dt<=0)return;var fleet=EnemyFleetDirector.Instance;
            bool battle=fleet!=null&&fleet.BattleStarted&&BachDangStoryManager.Instance.currentState==BachDangStoryManager.StoryState.Act3_BattleInProgress&&!(QuestCheckpoint.Instance?.IsDefeated??false);
            if(!battle){if(active)Silence();return;}active=true;
            var player=BeachBoatQuestSession.Instance.player.transform.position;Vector3 origin=player+Vector3.forward*180;
            float closest=float.PositiveInfinity;foreach(var ship in fleet.enemyShips)if(ship!=null&&(ship.gameObject.activeInHierarchy||ship.WreckTarget!=null))
            {var p=ship.WreckTarget!=null?ship.WreckTarget.position:ship.transform.position;float d=(p-player).sqrMagnitude;if(d<closest){closest=d;origin=p+Vector3.up*20;}}
            enemy.transform.position=crowd.transform.position=origin;
            bool ambush=fleet.AmbushStarted||BachDangBattleTimeline.Instance?.CurrentMoment==BachDangBattleTimeline.BattleMoment.Ambush;
            commandTime=Mathf.Max(0,commandTime-dt);nextEnemy-=dt;nextCry-=dt;nextCommand-=dt;
            if(ambush&&!ambushPlayed)
            {
                ambushPlayed=invasionPlayed=true;enemy.Stop();nextEnemy=1.8f;nextCry=3;
                crowd.Stop();Command(ambushCommand,"Thời cơ đã đến! Bắn! Toàn quân, đánh!");
                secondCommand=true;nextCommand=commandTime+2.5f;
            }
            else if(!ambush&&!invasionPlayed)
            {invasionPlayed=true;PlayEnemy(advances[0],false);nextEnemy=7;}
            if(secondCommand&&nextCommand<=0)
            {secondCommand=false;counterattackPlayed=true;Command(attackCommand,"Đánh! Quyết không để giặc thoát!");}
            if(nextEnemy<=0)
            {PlayEnemy(ambush?panics[Random.Range(0,panics.Length)]:advances[Random.Range(0,advances.Length)],ambush);nextEnemy=Random.Range(6f,10f);}
            if(ambush&&nextCry<=0)
            {var clip=cries[Random.Range(0,cries.Length)];if(clip!=null){crowd.pitch=Random.Range(.92f,1.07f);crowd.PlayOneShot(clip);}nextCry=Random.Range(7f,12f);}
            enemy.volume=commandTime>0?.34f:.85f;crowd.volume=commandTime>0?.12f:.40f;
            caption.gameObject.SetActive(commandTime>0);
        }
    }
}
