using UnityEngine;
namespace BeachBoatStory
{
    // Conservative FPS quality adjustment. Does not unload assets or promise to reduce RAM.
    public sealed class EnvironmentOptimizer : MonoBehaviour
    {
        public Transform player;
        public float cullDistance=400,importantCullDistance=1000;
        public bool autoAdjustQuality=true;
        public int targetFPS=45;
        int originalQuality,lastApplied,frames,slowWindows;
        float warmup,elapsed;
        void OnEnable(){originalQuality=lastApplied=QualitySettings.GetQualityLevel();warmup=10;elapsed=0;frames=slowWindows=0;}
        void Update()
        {
            if(!autoAdjustQuality||!Application.isFocused||CinematicQuality.High)return;
            if(warmup>0){warmup-=Time.unscaledDeltaTime;return;}
            elapsed+=Time.unscaledDeltaTime;frames++;
            if(elapsed<5)return;
            float fps=frames/elapsed;elapsed=0;frames=0;
            slowWindows=fps<Mathf.Max(15,targetFPS)*.8f?slowWindows+1:0;
            if(slowWindows<2)return;
            slowWindows=0;
            int current=QualitySettings.GetQualityLevel();
            if(current!=lastApplied){originalQuality=current;lastApplied=current;}
            if(current>0){lastApplied=current-1;QualitySettings.SetQualityLevel(lastApplied,true);warmup=10;}
        }
        void OnDisable()
        {
            if(QualitySettings.GetQualityLevel()==lastApplied&&lastApplied!=originalQuality)
                QualitySettings.SetQualityLevel(originalQuality,true);
        }
    }
}
