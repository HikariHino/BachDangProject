using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Title music and immediate UI feedback, including while gameplay is paused.</summary>
    public sealed class BachDangMenuAudio : MonoBehaviour
    {
        public static BachDangMenuAudio Instance {get;private set;}
        public AudioSource Music {get;private set;}
        public int ClicksPlayed {get;private set;}
        public int EntriesPlayed {get;private set;}
        AudioSource feedback;AudioClip click,entry;bool entered;float nextSlider;
        void Awake()
        {
            Instance=this;
            Music=gameObject.AddComponent<AudioSource>();Music.playOnAwake=false;Music.loop=true;Music.spatialBlend=0;Music.priority=180;Music.volume=0;
            Music.clip=Resources.Load<AudioClip>("BachDang938/LobbyAudio/BachDang_Lobby_Pentatonic");
            feedback=gameObject.AddComponent<AudioSource>();feedback.playOnAwake=false;feedback.spatialBlend=0;feedback.priority=40;feedback.volume=1;
            click=Resources.Load<AudioClip>("BachDang938/LobbyAudio/Menu_Timber_Bronze_Click");
            entry=Resources.Load<AudioClip>("BachDang938/LobbyAudio/Lobby_Entry_Seal");
        }
        void Update()
        {
            bool title=BachDangFrontEnd.AtTitle;
            if(title&&!entered)
            {
                entered=true;if(Music.clip!=null)Music.Play();
                if(entry!=null){feedback.PlayOneShot(entry,.38f);EntriesPlayed++;}
            }
            Music.volume=Mathf.MoveTowards(Music.volume,title?.22f:0,Time.unscaledDeltaTime*.35f);
            if(!title&&Music.volume==0&&Music.isPlaying)Music.Stop();
        }
        public static void Click(){if(Instance!=null&&Instance.click!=null){Instance.feedback.PlayOneShot(Instance.click,.62f);Instance.ClicksPlayed++;}}
        public static void SliderChanged(){if(Instance==null||Time.unscaledTime<Instance.nextSlider)return;Instance.nextSlider=Time.unscaledTime+.12f;Click();}
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
