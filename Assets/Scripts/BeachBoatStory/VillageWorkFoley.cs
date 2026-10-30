using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Recorded friction plus a bounded, fading action pair; tool sounds never accumulate voices.</summary>
    public sealed class VillageWorkFoley
    {
        public enum Cue { Saw, Rope, Cloth, Scoop, Pour, Crease, Knot, SetDown }
        readonly AudioSource friction;AudioSource action,tail;
        readonly Dictionary<Cue,AudioClip> clips=new Dictionary<Cue,AudioClip>();
        readonly Dictionary<Cue,int> counts=new Dictionary<Cue,int>();
        float lastMotion=-10,target;bool paused;
        public Cue LastCue {get;private set;}
        public bool IsPlaying=>friction!=null&&friction.isPlaying||action!=null&&action.isPlaying||tail!=null&&tail.isPlaying;
        public int Count(Cue cue)=>counts.TryGetValue(cue,out int value)?value:0;
        public VillageWorkFoley(Transform parent)
        {
            foreach(Cue cue in System.Enum.GetValues(typeof(Cue)))clips[cue]=Resources.Load<AudioClip>("BachDang938/WorkAudio/Trade_"+cue);
            AudioSource Source(string name,bool loop)
            {
                var go=new GameObject(name);go.transform.SetParent(parent,false);var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=loop;source.spatialBlend=0;source.dopplerLevel=0;source.priority=60;return source;
            }
            friction=Source("ToolFriction",true);action=Source("ToolActions",false);tail=Source("ToolActionFade",false);friction.volume=0;
        }
        float Volume=>BeachBoatSoundscape.Instance==null?.8f:BeachBoatSoundscape.Instance.Muted?0:BeachBoatSoundscape.Instance.masterVolume;
        public void Move(Cue cue,float speed)
        {
            if(Volume<=0||!clips.TryGetValue(cue,out var clip)||clip==null)return;
            if(friction.clip!=clip){friction.Stop();friction.clip=clip;friction.volume=0;}
            if(!friction.isPlaying){friction.Play();counts[cue]=Count(cue)+1;}LastCue=cue;lastMotion=Time.unscaledTime;target=Mathf.Lerp(.5f,.85f,Mathf.Clamp01(speed));
        }
        public void Play(Cue cue)
        {
            if(Volume<=0||!clips.TryGetValue(cue,out var clip)||clip==null)return;
            tail.Stop();var swap=tail;tail=action;action=swap;action.clip=clip;action.volume=0;action.pitch=1;action.Play();LastCue=cue;counts[cue]=Count(cue)+1;
        }
        public void Tick(float seconds,bool blocked)
        {
            if(blocked){if(!paused){friction.Pause();action.Pause();tail.Pause();paused=true;}return;}
            if(paused){friction.UnPause();action.UnPause();tail.UnPause();paused=false;}
            action.volume=Mathf.MoveTowards(action.volume,.9f*Volume,seconds*12);tail.volume=Mathf.MoveTowards(tail.volume,0,seconds*12);if(tail.volume<.001f)tail.Stop();
            float goal=Time.unscaledTime-lastMotion<.09f?target*Volume:0;
            friction.volume=Mathf.MoveTowards(friction.volume,goal,seconds*5);if(goal==0&&friction.volume<.001f)friction.Stop();
        }
        public void Stop(){if(friction!=null)friction.Stop();if(action!=null)action.Stop();if(tail!=null)tail.Stop();}
    }
}
