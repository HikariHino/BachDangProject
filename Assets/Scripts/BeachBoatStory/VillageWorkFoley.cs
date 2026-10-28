using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>One friction loop plus one short action channel; tool sounds never accumulate voices.</summary>
    public sealed class VillageWorkFoley
    {
        public enum Cue { Saw, Rope, Cloth, Scoop, Pour, Crease, Knot, SetDown }
        readonly AudioSource friction,action;
        readonly Dictionary<Cue,AudioClip> clips=new Dictionary<Cue,AudioClip>();
        readonly Dictionary<Cue,int> counts=new Dictionary<Cue,int>();
        float lastMotion=-10,target;bool paused;
        public Cue LastCue {get;private set;}
        public bool IsPlaying=>friction!=null&&friction.isPlaying||action!=null&&action.isPlaying;
        public int Count(Cue cue)=>counts.TryGetValue(cue,out int value)?value:0;
        public VillageWorkFoley(Transform parent)
        {
            foreach(Cue cue in System.Enum.GetValues(typeof(Cue)))clips[cue]=Resources.Load<AudioClip>("BachDang938/WorkAudio/Trade_"+cue);
            AudioSource Source(string name,bool loop)
            {
                var go=new GameObject(name);go.transform.SetParent(parent,false);var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=loop;source.spatialBlend=0;source.dopplerLevel=0;source.priority=60;return source;
            }
            friction=Source("ToolFriction",true);action=Source("ToolActions",false);friction.volume=0;
        }
        float Volume=>BeachBoatSoundscape.Instance==null?.8f:BeachBoatSoundscape.Instance.Muted?0:BeachBoatSoundscape.Instance.masterVolume;
        public void Move(Cue cue,float speed)
        {
            if(Volume<=0||!clips.TryGetValue(cue,out var clip)||clip==null)return;
            if(friction.clip!=clip){friction.Stop();friction.clip=clip;friction.volume=0;}
            if(!friction.isPlaying){friction.Play();counts[cue]=Count(cue)+1;}LastCue=cue;lastMotion=Time.unscaledTime;target=Mathf.Lerp(.13f,.42f,Mathf.Clamp01(speed));
        }
        public void Play(Cue cue)
        {
            if(Volume<=0||!clips.TryGetValue(cue,out var clip)||clip==null)return;
            action.Stop();action.clip=clip;action.volume=.55f*Volume;action.pitch=1;action.Play();LastCue=cue;counts[cue]=Count(cue)+1;
        }
        public void Tick(float seconds,bool blocked)
        {
            if(blocked){if(!paused){friction.Pause();action.Pause();paused=true;}return;}
            if(paused){friction.UnPause();action.UnPause();paused=false;}
            float goal=Time.unscaledTime-lastMotion<.09f?target*Volume:0;
            friction.volume=Mathf.MoveTowards(friction.volume,goal,seconds*5);if(goal==0&&friction.volume<.001f)friction.Stop();
        }
        public void Stop(){friction.Stop();action.Stop();}
    }
}
