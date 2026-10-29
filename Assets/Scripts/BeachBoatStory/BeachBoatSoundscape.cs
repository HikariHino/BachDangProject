using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Small shared audio bed and audible player actions; distances match beachBoat's world scale.</summary>
    public sealed class BeachBoatSoundscape : MonoBehaviour
    {
        public static BeachBoatSoundscape Instance {get;private set;}
        public AudioClip river,wind,bird,oar,bow,drum,crash,good,bad,hit;
        public AudioClip[] footsteps=new AudioClip[0];
        AudioClip[] runningFootsteps;
        AudioClip[] recordedOars,recordedBows,recordedSwordSwings,deckFootsteps;
        int oarIndex,bowIndex,swordIndex;
        Transform previousCarrier;Vector3 previousDeckPosition;
        public AudioClip LastOar {get;private set;}
        public AudioClip LastBow {get;private set;}
        public AudioClip LastSwordSwing {get;private set;}
        public AudioClip LastCombatImpact {get;private set;}
        public int SwordSwingsPlayed {get;private set;}
        public int RunningFootstepsPlayed {get;private set;}
        public AudioClip LastFootstep {get;private set;}
        [Range(0,1)] public float masterVolume=.8f;
        public bool Muted {get;private set;}
        public int FootstepsPlayed {get;private set;}
        public int OarsPlayed {get;private set;}
        public int SwimStrokesPlayed {get;private set;}
        public int EffectsPlayed {get;private set;}
        public int InteractionsPlayed {get;private set;}
        public float DistanceToWater {get;private set;}=float.PositiveInfinity;
        public AudioSource RiverSource {get;private set;}
        public AudioSource WindSource {get;private set;}
        AudioSource foley,effects,ui,birds;
        AudioSource wreckChannel;int cryIndex;public int WreckCriesPlayed {get;private set;}
        public AudioClip StakeImpact {get;private set;}
        AudioClip arrowImpact;AudioClip[] wreckCries;float nextDistantHit,nextWreckSound;
        public int WreckSoundsPlayed {get;private set;}
        public int CombatHitsPlayed {get;private set;}
        BeachBoatQuestSession session;
        Vector3 previous;
        float distance,nextStep,nextOar,nextBird,toastUntil;
        int footIndex;
        int runStepIndex;
        float savedListenerVolume=1;
        Terrain[] terrains;
        float nextShoreCheck;
        int interactionFrame=-1;
        void Awake(){Instance=this;session=GetComponent<BeachBoatQuestSession>();}
        void Start()
        {
            Application.runInBackground=true;
            savedListenerVolume=AudioListener.volume;
            if(GetComponent<CombatHitFeedback>()==null)gameObject.AddComponent<CombatHitFeedback>();
            if(GetComponent<ShipwreckPrewarm>()==null)gameObject.AddComponent<ShipwreckPrewarm>();
            wreckCries=Resources.LoadAll<AudioClip>("BachDang938/WreckAudio");
            runningFootsteps=Resources.LoadAll<AudioClip>("BachDang938/RunSteps");
            arrowImpact=Resources.Load<AudioClip>("BachDang938/LobbyAudio/Arrow_Flesh_Impact");
            StakeImpact=Resources.Load<AudioClip>("BachDang938/WorkAudio/Trade_SetDown");
            var actionClips=Resources.LoadAll<AudioClip>("BachDang938/ActionAudio");
            AudioClip[] Group(string prefix)=>System.Array.FindAll(actionClips,c=>c.name.StartsWith(prefix,System.StringComparison.Ordinal));
            var walking=Group("Walk_");if(walking.Length>0)footsteps=walking;
            var running=Group("Run_");if(running.Length>0)runningFootsteps=running;
            recordedOars=Group("Oar_");recordedBows=Group("Bow_");recordedSwordSwings=Group("Sword_");deckFootsteps=Group("Deck_");
            foreach(var clips in new[]{footsteps,runningFootsteps,recordedOars,recordedBows,recordedSwordSwings,deckFootsteps})System.Array.Sort(clips,(a,b)=>string.CompareOrdinal(a.name,b.name));
            var impacts=Group("Arrow_Flesh_");if(impacts.Length>0)arrowImpact=impacts[0];
            System.Array.Sort(runningFootsteps,(a,b)=>string.CompareOrdinal(a.name,b.name));
            foreach(var old in session.player.GetComponentsInChildren<FootstepSound>(true))old.enabled=false;
            previous=session.player.transform.position;
            terrains=Terrain.activeTerrains;
            RiverSource=Source("RiverAmbience",river,true,0);WindSource=Source("WindAndLeaves",wind,true,0);
            // The procedural leaf loop contains broadband hiss. Keep it a soft bed,
            // rather than a full-volume rush that sounds like water throughout camp.
            var windFilter=WindSource.gameObject.AddComponent<AudioLowPassFilter>();
            windFilter.cutoffFrequency=700;windFilter.lowpassResonanceQ=1;
            foley=Source("PlayerFoley",null,false,1);effects=Source("ActionEffects",null,false,1);ui=Source("InteractionFeedback",null,false,1);birds=Source("DistantBirds",null,false,1);
            wreckChannel=Source("Shipwreck_Scream_Channel",null,false,1);
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
            if(OpeningEvent.IsOpeningActive){RiverSource.volume=WindSource.volume=0;birds.Stop();nextBird=Time.time+6;previous=session.player.transform.position;return;}
            bool dead=QuestCheckpoint.Instance!=null&&QuestCheckpoint.Instance.IsDefeated;
            if(Time.unscaledTime>=nextShoreCheck)
            {nextShoreCheck=Time.unscaledTime+.5f;DistanceToWater=MeasureWaterDistance(session.player.transform.position);}
            float shore=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(20,200,DistanceToWater));
            float intro=OpeningEvent.IsOpeningActive?.15f:StakeConstruction.Active!=null?.25f:1f;
            RiverSource.volume=Mathf.MoveTowards(RiverSource.volume,volume*(dead?.08f:.34f)*shore*intro,Time.unscaledDeltaTime*.3f);
            float breeze=.035f+.01f*Mathf.Sin(Time.unscaledTime*.23f);
            WindSource.volume=Mathf.MoveTowards(WindSource.volume,volume*(dead?.01f:breeze)*intro,Time.unscaledDeltaTime*.012f);
            var position=session.player.transform.position;float moved=QuestRowboat.FlatDistance(position,previous);previous=position;
            var carrier=RiverBoardingCombat.Instance?.PlayerDeckCarrier;
            if(carrier!=null)
            {
                var local=carrier.InverseTransformPoint(position);
                moved=previousCarrier==carrier?carrier.TransformVector(local-previousDeckPosition).magnitude:0;
                previousDeckPosition=local;
            }
            previousCarrier=carrier;
            if(Muted||dead||!session.storyMode)return;
            if(Time.time>=nextBird){nextBird=Time.time+Random.Range(8,15);birds.panStereo=Random.Range(-.6f,.6f);Play(birds,bird,.22f,Random.Range(.94f,1.08f));}
            if(BeachBoatQuestSession.InputBlocked)return;
            bool aboard=QuestRowboat.Instance!=null&&QuestRowboat.Instance.IsAboard&&!(RiverBoardingCombat.Instance?.State==RiverBoardingCombat.PlayerState.Land);
            if(RiverBoardingCombat.Instance!=null&&RiverBoardingCombat.Instance.PlayerAway&&RiverBoardingCombat.Instance.State!=RiverBoardingCombat.PlayerState.Land&&carrier==null){distance=0;return;}
            if(carrier!=null)aboard=false;
            if(aboard)
            {
                distance=0;var boat=QuestRowboat.Instance;
                if(Mathf.Abs(boat.Speed)>5&&Time.time>=nextOar&&!(RiverPlayerCombat.Instance?.IsAiming??false))
                {nextOar=Time.time+Mathf.Lerp(1.3f,.7f,Mathf.Abs(boat.Speed)/Mathf.Max(1,boat.maxSpeed));PlayOar();}
            }
            else if(moved<60&&(carrier!=null||session.player.GetComponent<Character_Movement>().IsGrounded))
            {
                distance+=moved;
                bool running=carrier==null&&session.player.GetComponent<Character_Movement>().IsRunning;
                if(moved>.001f&&distance>=(running?12:14)&&Time.time>=nextStep){distance=0;nextStep=Time.time+(running?.23f:.42f);PlayFootstep(running);}
            }
            else distance=0;
        }
        void Play(AudioSource source,AudioClip clip,float volume,float pitch=1)
        {if(Muted||source==null||clip==null)return;source.pitch=pitch;source.PlayOneShot(clip,volume*masterVolume);EffectsPlayed++;}
        public void PlayFootstep(bool running=false)
        {
            bool deck=RiverBoardingCombat.Instance?.PlayerDeckCarrier!=null&&deckFootsteps!=null&&deckFootsteps.Length>0;
            var clips=deck?deckFootsteps:running&&runningFootsteps!=null&&runningFootsteps.Length>0?runningFootsteps:footsteps;
            if(clips.Length==0)return;
            // Recorded pairs alternate left/right, with varied takes of each foot.
            int index=running?(runStepIndex++%2)+2*Random.Range(0,Mathf.Max(1,clips.Length/2)):footIndex++%clips.Length;
            LastFootstep=clips[Mathf.Min(index,clips.Length-1)];
            Play(foley,LastFootstep,running?.62f:.52f,Random.Range(.98f,1.02f));FootstepsPlayed++;
            if(running)RunningFootstepsPlayed++;
        }
        public void SetMuted(bool value){Muted=value;AudioListener.volume=value?0:BachDangPreferences.Volume;toastUntil=Time.unscaledTime+2.5f;}
        static AudioClip Next(AudioClip[] clips,ref int index,AudioClip fallback)=>clips!=null&&clips.Length>0?clips[index++%clips.Length]:fallback;
        public void PlayOar(){LastOar=Next(recordedOars,ref oarIndex,oar);Play(foley,LastOar,.65f,Random.Range(.98f,1.02f));OarsPlayed++;}
        public void PlaySwimStroke(bool moving=true){Play(foley,oar,moving?.48f:.22f,Random.Range(1.10f,1.25f));SwimStrokesPlayed++;}
        public void PlayWaterEntry()=>Play(foley,oar,.72f,.8f);
        public void PlayClimbGrip()=>Play(foley,footsteps.Length>0?footsteps[0]:oar,.38f,.92f);
        public void PlayBow(bool hostile=false){LastBow=Next(recordedBows,ref bowIndex,bow);Play(effects,LastBow,hostile ? .23f : .65f,Random.Range(.98f,1.02f));}
        public void PlaySwordSwing()
        {
            LastSwordSwing=Next(recordedSwordSwings,ref swordIndex,GetComponent<RiverBattleChoreography>()?.swordSwing);
            Play(effects,LastSwordSwing,.65f,Random.Range(.97f,1.03f));SwordSwingsPlayed++;
        }
        public void PlayDrum(int beat)=>Play(effects,drum,.8f,.8f+beat*.13f);
        public void PlayCrash()=>Play(effects,crash,.85f);
        public void PlayShipwreck(Vector3 point)
        {
            if(Time.time<nextWreckSound)return;nextWreckSound=Time.time+.22f;
            float distance=Vector3.Distance(point,session.player.transform.position);float level=Mathf.Lerp(.72f,.12f,Mathf.Clamp01(distance/900));
            Play(effects,crash,level,.9f);
            var voices=EnemyFleetDirector.Instance?.GetComponent<RiverBattleVoices>();
            if(!Muted&&wreckChannel!=null&&wreckCries!=null&&wreckCries.Length>0){var cry=wreckCries[cryIndex++%wreckCries.Length];if(voices?.QueueWreckCry(cry)==true)WreckCriesPlayed++;else if(!wreckChannel.isPlaying){wreckChannel.clip=cry;wreckChannel.pitch=1;wreckChannel.volume=Mathf.Max(.5f,level*.95f)*masterVolume;wreckChannel.Play();WreckCriesPlayed++;}}
            WreckSoundsPlayed++;
        }
        public int StakeHitsPlayed {get;private set;}
        public void PlayStake(){Play(effects,StakeImpact!=null?StakeImpact:drum,1f,1);StakeHitsPlayed++;}
        public void PlayHit()=>Play(effects,hit,.55f);
        public void PlayCombatHit(Vector3 point,CombatImpactKind kind,bool foreground)
        {
            float distance=Vector3.Distance(point,session.player.transform.position);
            if(!foreground&&(distance>180||Time.unscaledTime<nextDistantHit))return;
            var clip=kind==CombatImpactKind.Arrow?arrowImpact:GetComponent<RiverBattleChoreography>()?.hitImpact;
            if(clip==null)clip=hit;
            if(Muted||effects==null||clip==null)return;
            LastCombatImpact=clip;
            if(!foreground)nextDistantHit=Time.unscaledTime+.08f;
            Play(effects,clip,foreground?.72f:Mathf.Lerp(.34f,.035f,distance/180),Random.Range(.97f,1.04f));CombatHitsPlayed++;
        }
        public void PlayResult(bool correct)=>Play(ui,correct?good:bad,.55f);
        public void PlayInteraction()
        {
            if(interactionFrame==Time.frameCount)return;
            interactionFrame=Time.frameCount;InteractionsPlayed++;Play(ui,good,.65f,1.08f);
        }
        // Terrain height is sampled in the horizontal plane. Being at river height in
        // the inland village must not make the entire water plane audible there.
        public float MeasureWaterDistance(Vector3 position)
        {
            if(IsWater(position))return 0;
            for(float radius=20;radius<=200;radius+=20)
                for(int i=0;i<32;i++)
                {
                    float angle=i*Mathf.PI/16;
                    if(IsWater(position+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius)))return radius;
                }
            return float.PositiveInfinity;
        }
        bool IsWater(Vector3 p)
        {
            foreach(var terrain in terrains??System.Array.Empty<Terrain>())
            {
                if(terrain==null||terrain.terrainData==null)continue;
                var local=p-terrain.transform.position;var size=terrain.terrainData.size;
                if(local.x>=0&&local.z>=0&&local.x<=size.x&&local.z<=size.z)
                    return terrain.SampleHeight(p)+terrain.transform.position.y<TideSystem.CurrentWaterHeight-.5f;
            }
            return false;
        }
        void OnGUI()
        {
            if(Time.unscaledTime>=toastUntil)return;
            GUI.Box(new Rect(Screen.width/2-125,Screen.height-110,250,36),Muted?"Âm thanh: TẮT  [F10]":"Âm thanh: BẬT  [F10]");
        }
        void OnDestroy(){if(Muted)AudioListener.volume=BachDangPreferences.Volume;if(Instance==this)Instance=null;}
    }
}
