using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Offline Vietnamese voice clips and quiet character-specific subtitle feedback.</summary>
    public sealed class DialogueVoice : MonoBehaviour
    {
        [Serializable] public sealed class Entry {public string line,male,female;}
        [Serializable] sealed class Manifest {public Entry[] entries;}
        static Entry[] entries;
        static readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        AudioSource speech,letters;
        AudioClip tick;
        bool paused;
        float nextTick;
        public AudioClip CurrentClip=>speech!=null?speech.clip:null;
        public AudioSource SpeechSource=>speech;
        public string Identity {get;private set;}
        public bool Female {get;private set;}
        public float VoicePitch {get;private set;}=1;
        public int SubtitleSounds {get;private set;}
        public int SpokenLines {get;private set;}
        public float Duration=>CurrentClip!=null?CurrentClip.length/VoicePitch:0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){entries=null;clips.Clear();}
        void Awake()
        {
            speech=gameObject.AddComponent<AudioSource>();speech.playOnAwake=false;speech.spatialBlend=0;speech.priority=16;speech.volume=.9f;
            letters=gameObject.AddComponent<AudioSource>();letters.playOnAwake=false;letters.spatialBlend=0;letters.priority=100;letters.volume=.055f;
            const int rate=22050;var samples=new float[(int)(rate*.018f)];
            for(int i=0;i<samples.Length;i++)
            {float t=i/(float)rate;float envelope=Mathf.Sin(Mathf.PI*i/(samples.Length-1));samples[i]=Mathf.Sin(2*Mathf.PI*760*t)*envelope*.22f+Mathf.Sin(2*Mathf.PI*1330*t)*envelope*.06f;}
            tick=AudioClip.Create("Subtitle_Soft_Tick",samples.Length,1,rate,false);tick.SetData(samples,0);
            if(entries==null)
            {
                var file=Resources.Load<TextAsset>("BachDang938/Voices/manifest");
                entries=file!=null?JsonUtility.FromJson<Manifest>(file.text).entries:Array.Empty<Entry>();
            }
        }
        static uint Hash(string text){uint hash=2166136261;foreach(char c in text??""){hash^=c;hash*=16777619;}return hash;}
        public static string ActorIdentity(string speaker,Transform actor)
        {
            if(actor==null)return speaker;
            string path=actor.name;
            for(var t=actor.parent;t!=null;t=t.parent)path=t.name+"/"+path;
            return speaker+"/"+path;
        }
        public static bool ActorIsFemale(string speaker,Transform actor)
        {
            if(speaker=="Ngô Quyền")return false;
            if(actor!=null)foreach(var child in actor.GetComponentsInChildren<Transform>(true))
                if(child.name.IndexOf("female",StringComparison.OrdinalIgnoreCase)>=0||child.name.IndexOf("woman",StringComparison.OrdinalIgnoreCase)>=0)return true;
            return speaker.Contains("Bà")||speaker.Contains("Cô")||speaker.Contains("Chị");
        }
        public static Entry FindLine(string line)
        {
            if(entries==null)return null;
            foreach(var entry in entries)if(entry.line==line)return entry;
            foreach(var entry in entries)if(line.EndsWith(entry.line,StringComparison.Ordinal))return entry;
            string generic=line.StartsWith("Hoàn thành hiệu lệnh trống",StringComparison.Ordinal)?"Ngươi đã hoàn thành bài luyện hiệu lệnh trống.":
                line.StartsWith("Chọn đúng vật tư và giao đủ",StringComparison.Ordinal)?"Vật tư đã được tập kết đầy đủ.":null;
            if(generic!=null)foreach(var entry in entries)if(entry.line==generic)return entry;
            return null;
        }
        public void Play(string speaker,string line,string identity,bool female)
        {
            Stop();Identity=string.IsNullOrEmpty(identity)?speaker:identity;Female=female;uint seed=Hash(Identity);
            VoicePitch=speaker=="Ngô Quyền"?.86f:Mathf.Lerp(.92f,1.09f,(seed%1001)/1000f);
            letters.pitch=Mathf.Lerp(.83f,1.2f,((seed>>8)%1001)/1000f);nextTick=0;
            var entry=FindLine(line);if(entry==null)return;
            string path=female?entry.female:entry.male;
            if(!clips.TryGetValue(path,out var clip)){clip=Resources.Load<AudioClip>(path);clips[path]=clip;}
            if(clip==null)return;speech.clip=clip;speech.pitch=VoicePitch;speech.Play();SpokenLines++;
        }
        public void SubtitleTick()
        {
            if(paused||Time.unscaledTime<nextTick||BachDangPreferences.Volume<=0||(BeachBoatSoundscape.Instance?.Muted??false))return;
            nextTick=Time.unscaledTime+.085f;letters.PlayOneShot(tick);SubtitleSounds++;
        }
        void Update()
        {
            bool shouldPause=BachDangFrontEnd.IsBlocking;
            if(shouldPause&&!paused){speech.Pause();letters.Pause();paused=true;}
            else if(!shouldPause&&paused){speech.UnPause();letters.UnPause();paused=false;}
        }
        public void Stop(){if(speech!=null){speech.Stop();speech.clip=null;}if(letters!=null)letters.Stop();paused=false;}
        void OnDisable()=>Stop();
        void OnDestroy(){if(tick!=null)Destroy(tick);}
    }
}
