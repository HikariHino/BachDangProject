using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Offline video frames and synchronized audio; no platform codec dependency.</summary>
    public sealed class RationsTutorialVideo : MonoBehaviour
    {
        public const float Duration=15;
        static RationsTutorialVideo instance;
        Texture2D[] sheets;AudioSource audioSource;RawImage screen;TMP_Text status;
        bool userPaused,menuPaused,failed,ended;float elapsed;int lastFrame=-1;
        public static bool IsWatching=>instance!=null&&instance.isActiveAndEnabled;
        public bool IsReady=>sheets!=null&&sheets.Length==2&&sheets[0]!=null&&sheets[1]!=null&&audioSource.clip!=null;
        public bool IsPlaying=>isActiveAndEnabled&&IsReady&&!failed&&!ended&&!userPaused&&!menuPaused;
        public bool HasFailed=>failed;
        public double Position=>elapsed;
        public int CurrentFrame=>Mathf.Clamp(Mathf.FloorToInt(elapsed*8),0,119);
        public void Initialize(RawImage image,TMP_Text caption)
        {
            instance=this;screen=image;status=caption;
            sheets=new[]{Resources.Load<Texture2D>("BachDang938/Tutorials/RationsFrames0"),Resources.Load<Texture2D>("BachDang938/Tutorials/RationsFrames1")};
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;audioSource.volume=.5f;
            audioSource.clip=Resources.Load<AudioClip>("BachDang938/Tutorials/RationsHowToCues");
            Replay();
        }
        void OnEnable(){if(screen!=null)Replay();}
        public void Replay()
        {
            if(screen==null)return;elapsed=0;lastFrame=-1;userPaused=menuPaused=ended=false;failed=!IsReady;
            if(failed){status.text="Vàng: thiếu • Xanh: đủ • Đỏ: dư • Muối cho cả nhóm";return;}
            status.text="Xem thao tác mẫu • BẮT ĐẦU để vào chơi";DrawFrame();
            audioSource.Stop();if(isActiveAndEnabled)audioSource.Play();
        }
        public void TogglePause()
        {
            if(failed||ended)return;userPaused=!userPaused;
            if(userPaused){audioSource.Pause();status.text="Đã dừng video • Bấm DỪNG / TIẾP để xem tiếp";}
            else {if(!menuPaused)audioSource.UnPause();status.text="Xem thao tác mẫu • BẮT ĐẦU để vào chơi";}
        }
        void Update()
        {
            if(failed||screen==null||ended)return;
            bool blocked=BachDangFrontEnd.IsBlocking;
            if(blocked&&!menuPaused){menuPaused=true;audioSource.Pause();}
            else if(!blocked&&menuPaused){menuPaused=false;if(!userPaused)audioSource.UnPause();}
            if(userPaused||menuPaused)return;
            elapsed=audioSource.isPlaying?audioSource.time:elapsed+Time.unscaledDeltaTime;
            if(elapsed>=Duration){elapsed=Duration;ended=true;audioSource.Stop();status.text="Đến lượt bạn • Bấm BẮT ĐẦU hoặc xem lại";}
            DrawFrame();
        }
        void DrawFrame()
        {
            int frame=CurrentFrame;if(frame==lastFrame)return;lastFrame=frame;
            int sheet=frame/60,tile=frame%60;screen.texture=sheets[sheet];
            // Half-pixel inset avoids neighboring tiles bleeding into the picture.
            screen.uvRect=new Rect(((tile%6)*640+.5f)/3840,(2800-(tile/6+1)*280+.5f)/2800,639f/3840,279f/2800);
        }
        void OnDisable(){if(audioSource!=null)audioSource.Stop();}
        void OnDestroy(){if(instance==this)instance=null;}
    }
}