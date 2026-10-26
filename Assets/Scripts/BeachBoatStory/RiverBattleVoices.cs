using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Short recorded film cues on one voice channel, without repeated lines.</summary>
    public sealed class RiverBattleVoices : MonoBehaviour
    {
        AudioSource speech;
        AudioClip[] advances,panics;AudioClip ambushCommand,attackCommand;
        TMP_Text caption;
        bool invasionPlayed,ambushPlayed,counterattackPlayed,active,paused,secondCommand;
        float nextEnemy,commandTime,nextCommand,busy;
        int advanceMask,panicMask;
        public int InvasionCues {get;private set;}
        public int PanicCues {get;private set;}
        public int CommanderCues {get;private set;}
        public AudioSource SpeechSource=>speech;
        public float VoiceBusySeconds=>busy;
        public bool AssetsReady=>ambushCommand!=null&&attackCommand!=null&&
            System.Array.TrueForAll(advances,c=>c!=null)&&System.Array.TrueForAll(panics,c=>c!=null);
        public bool InvasionPlayed=>invasionPlayed;
        public bool AmbushPlayed=>ambushPlayed;
        static AudioClip Cue(string name)=>Resources.Load<AudioClip>("BachDang938/BattleVoices/"+name);
        void Awake()
        {
            var channel=new GameObject("BattleVoice_SingleChannel");channel.transform.SetParent(transform,false);speech=channel.AddComponent<AudioSource>();
            speech.playOnAwake=false;speech.loop=false;speech.dopplerLevel=0;speech.rolloffMode=AudioRolloffMode.Linear;speech.minDistance=250;speech.maxDistance=5000;
            // The old second advance asset repeated the end of the first line.
            // Keep one complete order followed by a distinct crowd response.
            advances=new[]{Cue("Enemy_Advance_1"),Cue("Enemy_Cheer_Crowd")};
            panics=new[]{Cue("Enemy_Panic_1"),Cue("Enemy_Panic_2"),Cue("Enemy_Panic_3")};
            ambushCommand=Cue("NgoQuyen_Ambush");attackCommand=Cue("NgoQuyen_Attack");
            var canvas=new GameObject("BattleCommanderCaption",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=100;
            var scale=canvas.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);
            var go=new GameObject("Caption",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(canvas.transform,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=new Vector2(.27f,.02f);rect.anchorMax=new Vector2(.76f,.075f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            caption=go.GetComponent<TextMeshProUGUI>();StoryFont.Apply(caption);caption.fontSize=17;caption.enableAutoSizing=true;caption.fontSizeMin=13;caption.fontSizeMax=17;
            caption.color=new Color(1,.85f,.57f);caption.alignment=TextAlignmentOptions.Center;caption.raycastTarget=false;caption.gameObject.SetActive(false);
        }
        public void FillSave(BachDangSaveData.Fleet data)
        {
            data.invasionVoiced=invasionPlayed;data.ambushVoiced=ambushPlayed;data.counterattackVoiced=counterattackPlayed;
            data.counterattackDelay=secondCommand?Mathf.Max(.5f,nextCommand):0;data.voiceAdvanceMask=advanceMask;data.voicePanicMask=panicMask;
        }
        public void RestoreSave(BachDangSaveData.Fleet data)
        {
            Silence();invasionPlayed=data.invasionVoiced;ambushPlayed=data.ambushVoiced;counterattackPlayed=data.counterattackVoiced;
            secondCommand=ambushPlayed&&!counterattackPlayed;nextCommand=Mathf.Max(.5f,data.counterattackDelay);nextEnemy=3;
            advanceMask=data.voiceAdvanceMask&3;panicMask=data.voicePanicMask&7;
        }
        public void ResetForNewBattle(){Silence();invasionPlayed=ambushPlayed=counterattackPlayed=false;advanceMask=panicMask=0;InvasionCues=PanicCues=CommanderCues=0;nextEnemy=0;}
        public void Silence(){speech.Stop();caption.gameObject.SetActive(false);commandTime=busy=0;secondCommand=false;active=false;paused=false;}
        public void RequestWreckCry(){nextEnemy=Mathf.Min(nextEnemy,.5f);}
        public void PlayInvasionIntroduction()
        {
            if((advanceMask&1)!=0)return;
            invasionPlayed=true;advanceMask|=1;InvasionCues++;
            Play(advances[0],false,"Giết! Đuổi theo cho ta!");nextEnemy=12;
        }
        void Update()
        {
            bool blocked=Time.timeScale<=0||BachDangFrontEnd.IsBlocking||OpeningEvent.IsOpeningActive||(StoryDialogueUI.Instance?.IsOpen??false);
            if(blocked!=paused){if(blocked){speech.Pause();caption.gameObject.SetActive(false);}else speech.UnPause();paused=blocked;}
            if(!blocked)Step(Time.deltaTime);
        }
        void Play(AudioClip clip,bool command,string subtitle=null)
        {
            if(clip==null)return;
            // Play replaces the channel's clip. There are no overlapping OneShots.
            speech.Stop();speech.clip=clip;speech.pitch=1;speech.volume=command?1:.85f;speech.spatialBlend=command?0:.15f;speech.priority=command?12:55;speech.Play();
            busy=clip.length+.5f;
            if(!string.IsNullOrEmpty(subtitle)){commandTime=clip.length+.25f;caption.text=(command?"Ngô Quyền: ":"Quân Nam Hán: ")+subtitle;}
            if(command)CommanderCues++;
        }
        void EnemyCue(bool panic)
        {
            if(busy>0)return;var clips=panic?panics:advances;int mask=panic?panicMask:advanceMask;
            for(int i=0;i<clips.Length;i++)
            {
                if((mask&(1<<i))!=0)continue;mask|=1<<i;Play(clips[i],false,!panic&&i==0?"Giết! Đuổi theo cho ta!":null);
                if(panic){panicMask=mask;PanicCues++;}else{advanceMask=mask;InvasionCues++;}break;
            }
            nextEnemy=Random.Range(8f,13f);
        }
        public void Step(float dt)
        {
            if(dt<=0)return;var fleet=EnemyFleetDirector.Instance;
            if(RiverInvasionIntro.IsRunning&&!RiverInvasionIntro.Instance.IsPlaying)return;
            bool battle=fleet!=null&&fleet.BattleStarted&&BachDangStoryManager.Instance.currentState==BachDangStoryManager.StoryState.Act3_BattleInProgress&&!(QuestCheckpoint.Instance?.IsDefeated??false);
            if(!battle)
            {
                // Victory stops new calls, but lets the spoken sentence finish.
                // Explicit reset/load/defeat still cancels through Silence().
                if(active)
                {
                    secondCommand=false;busy=Mathf.Max(0,busy-dt);commandTime=Mathf.Max(0,commandTime-dt);
                    caption.gameObject.SetActive(commandTime>0);if(busy<=0)Silence();
                }
                return;
            }
            active=true;
            var player=BeachBoatQuestSession.Instance.player.transform.position;var origin=player+Vector3.forward*180;float closest=float.PositiveInfinity;
            foreach(var ship in fleet.enemyShips)if(ship!=null&&(ship.gameObject.activeInHierarchy||ship.WreckTarget!=null))
            {var p=ship.WreckTarget!=null?ship.WreckTarget.position:ship.transform.position;float d=(p-player).sqrMagnitude;if(d<closest){closest=d;origin=p+Vector3.up*20;}}
            speech.transform.position=origin;
            bool ambush=fleet.AmbushStarted||BachDangBattleTimeline.Instance?.CurrentMoment==BachDangBattleTimeline.BattleMoment.Ambush;
            busy=Mathf.Max(0,busy-dt);commandTime=Mathf.Max(0,commandTime-dt);nextEnemy-=dt;nextCommand-=dt;
            if(ambush&&!ambushPlayed&&busy<=0)
            {
                ambushPlayed=invasionPlayed=true;Play(ambushCommand,true,"Tất cả chuẩn bị! Tổng phản công cho ta!");
                secondCommand=true;nextCommand=busy+2.5f;nextEnemy=busy+1.25f;
            }
            else if(!ambush&&!invasionPlayed){invasionPlayed=true;EnemyCue(false);}
            else if(busy<=0&&secondCommand&&nextCommand<=0)
            {secondCommand=false;counterattackPlayed=true;Play(attackCommand,true,"Hỡi ba quân tướng sĩ! Đánh cho Nam quốc sơn hà rạng danh ngàn đời!");nextEnemy=busy+1.25f;}
            else if(busy<=0&&nextEnemy<=0)EnemyCue(ambush);
            caption.gameObject.SetActive(commandTime>0);
        }
    }
}
