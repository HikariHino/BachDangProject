using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Small shared audio bed and audible player actions; distances match beachBoat's world scale.</summary>
    public sealed class BeachBoatSoundscape : MonoBehaviour
    {
        public static BeachBoatSoundscape Instance {get;private set;}
        public AudioClip river,wind,bird,oar,bow,drum,crash,good,bad,hit;
        public AudioClip[] footsteps=new AudioClip[0];
        [Range(0,1)] public float masterVolume=.8f;
        public bool Muted {get;private set;}
        public int FootstepsPlayed {get;private set;}
        public int OarsPlayed {get;private set;}
        public int EffectsPlayed {get;private set;}
        public AudioSource RiverSource {get;private set;}
        public AudioSource WindSource {get;private set;}
        AudioSource foley,effects,ui,birds;
        BeachBoatQuestSession session;
        Vector3 previous;
        float distance,nextStep,nextOar,nextBird,toastUntil;
        int footIndex;
        float savedListenerVolume=1;
        void Awake(){Instance=this;session=GetComponent<BeachBoatQuestSession>();}
        void Start()
        {
            Application.runInBackground=true;
            savedListenerVolume=AudioListener.volume;
            foreach(var old in session.player.GetComponentsInChildren<FootstepSound>(true))old.enabled=false;
            previous=session.player.transform.position;
            RiverSource=Source("RiverAmbience",river,true,.34f);WindSource=Source("WindAndLeaves",wind,true,.20f);
            foley=Source("PlayerFoley",null,false,1);effects=Source("ActionEffects",null,false,1);ui=Source("InteractionFeedback",null,false,1);birds=Source("DistantBirds",null,false,1);
            nextBird=Time.time+4;
        }
        AudioSource Source(string name,AudioClip clip,bool loop,float volume)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var source=go.AddComponent<AudioSource>();
            // The listener follows an orbit camera, not the player's feet. Personal sounds are 2D.
            source.spatialBlend=0;source.playOnAwake=false;source.loop=loop;source.clip=clip;source.volume=loop?volume*masterVolume:1;source.dopplerLevel=0;source.priority=loop?160:64;
            if(loop&&clip!=null)source.Play();return source;
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.F10))SetMuted(!Muted);
            float volume=Muted?0:masterVolume;
            bool dead=QuestCheckpoint.Instance!=null&&QuestCheckpoint.Instance.IsDefeated;
            RiverSource.volume=Mathf.MoveTowards(RiverSource.volume,volume*(dead ? .08f : QuestRowboat.Instance!=null&&QuestRowboat.Instance.IsAboard ? .34f : .19f),Time.unscaledDeltaTime*.3f);
            WindSource.volume=volume*(dead ? .06f : .20f);
            var position=session.player.transform.position;float moved=QuestRowboat.FlatDistance(position,previous);previous=position;
            if(Muted||dead||!session.storyMode)return;
            if(Time.time>=nextBird){nextBird=Time.time+Random.Range(8,15);birds.panStereo=Random.Range(-.6f,.6f);Play(birds,bird,.22f,Random.Range(.94f,1.08f));}
            if(BeachBoatQuestSession.InputBlocked)return;
            bool aboard=QuestRowboat.Instance!=null&&QuestRowboat.Instance.IsAboard;
            if(aboard)
            {
                distance=0;var boat=QuestRowboat.Instance;
                if(Mathf.Abs(boat.Speed)>5&&Time.time>=nextOar&&!(RiverPlayerCombat.Instance?.IsAiming??false))
                {nextOar=Time.time+Mathf.Lerp(1.3f,.7f,Mathf.Abs(boat.Speed)/Mathf.Max(1,boat.maxSpeed));PlayOar();}
            }
            else if(moved<60&&session.player.GetComponent<Character_Movement>().IsGrounded)
            {
                distance+=moved;
                if(distance>=14&&Time.time>=nextStep){distance=0;nextStep=Time.time+.28f;PlayFootstep();}
            }
            else distance=0;
        }
        void Play(AudioSource source,AudioClip clip,float volume,float pitch=1)
        {if(Muted||source==null||clip==null)return;source.pitch=pitch;source.PlayOneShot(clip,volume*masterVolume);EffectsPlayed++;}
        public void PlayFootstep(){if(footsteps.Length==0)return;Play(foley,footsteps[footIndex++%footsteps.Length],.6f,Random.Range(.93f,1.06f));FootstepsPlayed++;}
        public void SetMuted(bool value){Muted=value;AudioListener.volume=value?0:savedListenerVolume;toastUntil=Time.unscaledTime+2.5f;}
        public void PlayOar(){Play(foley,oar,.62f,Random.Range(.94f,1.06f));OarsPlayed++;}
        public void PlayBow(bool hostile=false)=>Play(effects,bow,hostile ? .25f : .65f,Random.Range(.95f,1.05f));
        public void PlayDrum(int beat)=>Play(effects,drum,.8f,.8f+beat*.13f);
        public void PlayCrash()=>Play(effects,crash,.85f);
        public void PlayStake()=>Play(effects,drum,.3f,1.7f);
        public void PlayHit()=>Play(effects,hit,.55f);
        public void PlayResult(bool correct)=>Play(ui,correct?good:bad,.55f);
        void OnGUI()
        {
            if(Time.unscaledTime>=toastUntil)return;
            GUI.Box(new Rect(Screen.width/2-125,Screen.height-110,250,36),Muted?"Âm thanh: TẮT  [F10]":"Âm thanh: BẬT  [F10]");
        }
        void OnDestroy(){if(Muted)AudioListener.volume=savedListenerVolume;if(Instance==this)Instance=null;}
    }
}
