using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>Brief ally instructions share the gaps in the recorded battle speech, never a wall of overlapping voices.</summary>
    public sealed class CampaignFieldCalls:MonoBehaviour
    {
        public static CampaignFieldCalls Instance {get;private set;}
        public static bool IsSpeaking=>Instance!=null&&Instance.source!=null&&Instance.source.isPlaying;
        AudioSource source;string pending;float expires;bool paused;
        public static void Say(string id)
        {
            if(Instance==null){var go=new GameObject("CampaignFieldCalls");go.transform.SetParent(BeachBoatQuestSession.Instance.transform,false);go.AddComponent<CampaignFieldCalls>();}
            if(IsSpeaking||Instance.pending==id)return;Instance.pending=id;Instance.expires=Time.unscaledTime+6;
        }
        void Awake(){Instance=this;source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.volume=.85f;source.priority=22;}
        void Update()
        {
            bool block=Time.timeScale<=0||BachDangFrontEnd.IsBlocking||StoryDialogueUI.Instance?.IsOpen==true;
            if(block&&!paused){source.Pause();paused=true;}else if(!block&&paused){source.UnPause();paused=false;}if(block)return;
            if(pending==null)return;if(Time.unscaledTime>expires){pending=null;return;}
            if(source.isPlaying||DialogueVoice.AnySpeaking||EnemyFleetDirector.Instance?.GetComponent<RiverBattleVoices>()?.Speaking==true||VillageVendorSpeech.Instance?.Voice?.SpeechSource?.isPlaying==true)return;
            var clip=Resources.Load<AudioClip>(pending=="brace"?"BachDang938/Voices/Preparation/crew_hold":"BachDang938/Voices/FieldCalls/"+pending);pending=null;if(clip==null)return;source.clip=clip;source.volume=.85f*BachDangPreferences.Volume;source.Play();
        }
        public static void Silence(){if(Instance==null)return;Instance.pending=null;Instance.source.Stop();}
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
