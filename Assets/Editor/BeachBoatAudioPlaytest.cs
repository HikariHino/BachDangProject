using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BeachBoatAudioPlaytest
{
    const string Flag="BeachBoatAudioPlaytest";
    static double started,until;
    static int phase;
    static float outputPeak;
    static bool muteChecked;
    static bool inlandSilent,riverAudible,interactionAudible;
    static bool pickupAudible,gateAudible,questAudible;
    static int interactionBaseline;
    static string failure;
    static readonly float[] Output=new float[1024];
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");EditorUtility.audioMasterMute=false;SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Finish()
    {
        var audio=BeachBoatSoundscape.Instance;Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/AudioPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,outputPeak,muteChecked,inlandSilent,riverAudible,interactionAudible,pickupAudible,gateAudible,questAudible,interactions=audio?.InteractionsPlayed,sampleRate=AudioSettings.outputSampleRate,listenerVolume=AudioListener.volume,listenerPaused=AudioListener.pause,editorMuted=EditorUtility.audioMasterMute,riverPlaying=audio?.RiverSource.isPlaying,windPlaying=audio?.WindSource.isPlaying,effects=audio?.EffectsPlayed,footsteps=audio?.FootstepsPlayed,oars=audio?.OarsPlayed},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<3)return;
        try
        {
            var audio=BeachBoatSoundscape.Instance;if(audio==null)throw new Exception("Scene soundscape missing");
            AudioListener.GetOutputData(Output,0);foreach(float sample in Output)outputPeak=Mathf.Max(outputPeak,Mathf.Abs(sample));
            if(EditorApplication.timeSinceStartup<until)return;
            if(phase==0)
            {
                var listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(l=>l.isActiveAndEnabled).ToArray();
                if(listeners.Length!=1||listeners[0].GetComponent<Camera>()!=BeachBoatQuestSession.Instance.storyCamera)throw new Exception("Wrong or duplicate active audio listener");
                if(AudioListener.pause||AudioListener.volume<=0||EditorUtility.audioMasterMute)throw new Exception("Master audio is muted");
                foreach(var clip in new[]{audio.river,audio.wind,audio.bird,audio.oar,audio.bow,audio.drum,audio.crash,audio.good,audio.bad,audio.hit}.Concat(audio.footsteps))
                {
                    if(clip==null)throw new Exception("Missing sound clip");var samples=new float[clip.samples*clip.channels];
                    if(!clip.GetData(samples,0)||!samples.Any(s=>Mathf.Abs(s)>.01f))throw new Exception("Silent/unreadable clip "+clip.name);
                }
                if(!audio.RiverSource.isPlaying||!audio.WindSource.isPlaying||audio.RiverSource.spatialBlend!=0)throw new Exception("Background bed is not playing audibly");
                audio.PlayFootstep();phase=1;until=EditorApplication.timeSinceStartup+.6;return;
            }
            if(phase==1){audio.PlayOar();phase=2;until=EditorApplication.timeSinceStartup+.6;return;}
            if(phase==2){audio.PlayBow();audio.PlayDrum(1);phase=3;until=EditorApplication.timeSinceStartup+.7;return;}
            if(phase==3){audio.PlayCrash();audio.PlayResult(true);phase=4;until=EditorApplication.timeSinceStartup+1;return;}
            if(phase==4)
            {
                if(outputPeak<.001f)throw new Exception("No PCM signal reached the AudioListener output");
                if(audio.EffectsPlayed<6||audio.FootstepsPlayed!=1||audio.OarsPlayed!=1)throw new Exception("Player action sounds were not emitted");
                audio.SetMuted(true);phase=5;until=EditorApplication.timeSinceStartup+.4;return;
            }
            if(phase==5){muteChecked=AudioListener.volume==0&&Output.All(s=>Mathf.Abs(s)<.0001f);audio.SetMuted(false);if(!muteChecked)throw new Exception("Mute did not silence the master output");phase=6;until=EditorApplication.timeSinceStartup+.4;return;}
            if(phase==6)
            {
                if(AudioListener.volume<=0)throw new Exception("Unmute did not restore master output");
                foreach(var opening in UnityEngine.Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))opening.SkipOpening();
                BeachBoatStory.StoryDialogueUI.Instance?.Close();
                MovePlayer(new Vector3(-6720,124.5f,-4700));phase=7;until=EditorApplication.timeSinceStartup+2;return;
            }
            if(phase==7)
            {
                inlandSilent=audio.RiverSource.volume<.0001f&&float.IsPositiveInfinity(audio.DistanceToWater);
                if(!inlandSilent)throw new Exception("River ambience is audible in the inland village: "+audio.RiverSource.volume);
                MovePlayer(new Vector3(-3000,TideSystem.CurrentWaterHeight+5,2100));phase=8;until=EditorApplication.timeSinceStartup+2;return;
            }
            if(phase==8)
            {
                riverAudible=audio.DistanceToWater<=20&&audio.RiverSource.volume>.2f;
                if(!riverAudible)throw new Exception("River ambience did not fade in beside the water: "+audio.DistanceToWater);
                interactionBaseline=audio.InteractionsPlayed;outputPeak=0;
                audio.RiverSource.Stop();audio.WindSource.Stop();
                foreach(var source in audio.GetComponentsInChildren<AudioSource>())source.Stop();
                phase=9;until=EditorApplication.timeSinceStartup+.5;return;
            }
            if(phase==9)
            {
                outputPeak=0;BeachBoatStory.StoryDialogueUI.Instance.Show("Kiểm tra","Nhận nhiệm vụ");
                phase=10;until=EditorApplication.timeSinceStartup+.3;return;
            }
            if(phase==10)
            {
                interactionAudible=audio.InteractionsPlayed>interactionBaseline&&outputPeak>.001f;
                if(!interactionAudible)throw new Exception("Dialogue interaction did not reach the audio output");
                BeachBoatStory.StoryDialogueUI.Instance.Close();phase=11;until=EditorApplication.timeSinceStartup+.5;return;
            }
            if(phase==11)
            {
                var story=BeachBoatStory.BachDangStoryManager.Instance;story.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act2_CollectWoodInVillage,true);
                var item=BeachBoatQuestSession.Instance.woodItems[0];item.RestoreCollection(false);MovePlayer(item.transform.position+Vector3.up);
                int before=audio.InteractionsPlayed;pickupAudible=item.TryCollect()&&audio.InteractionsPlayed==before+1&&!item.TryCollect();
                if(!pickupAudible)throw new Exception("Successful wood pickup is silent or repeat collection is accepted");phase=12;until=EditorApplication.timeSinceStartup+.5;return;
            }
            if(phase==12)
            {
                BeachBoatStory.BachDangStoryManager.Instance.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act1_GoToCampGate,true);
                var gate=UnityEngine.Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsSortMode.None).First(g=>g.gateType==BeachBoatStory.CampGateTransition.GateType.CampGate);
                MovePlayer(gate.transform.position+Vector3.up);int before=audio.InteractionsPlayed;
                gateAudible=gate.TryInteract()&&audio.InteractionsPlayed==before+1;
                if(!gateAudible)throw new Exception("Successful gate transition is silent");phase=13;until=EditorApplication.timeSinceStartup+5;return;
            }
            if(phase==13)
            {
                if(BeachBoatStory.ScreenFader.Instance.IsTransitioning)return;
                BeachBoatStory.BachDangStoryManager.Instance.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act1_ExploreCamp,true);
                var npc=UnityEngine.Object.FindAnyObjectByType<BeachBoatStory.NPCQuestGiver>();MovePlayer(npc.transform.position+Vector3.forward*50);
                int before=audio.InteractionsPlayed;questAudible=npc.TryTalk()&&audio.InteractionsPlayed==before+1;
                if(!questAudible)throw new Exception("Quest dialogue opening is silent");phase=14;until=EditorApplication.timeSinceStartup+.5;return;
            }
            if(phase==14)
            {
                int before=audio.InteractionsPlayed;BeachBoatStory.StoryDialogueUI.Instance.Close();
                questAudible&=audio.InteractionsPlayed==before+1&&BeachBoatStory.BachDangStoryManager.Instance.currentState==BeachBoatStory.BachDangStoryManager.StoryState.Act1_Training;
                if(!questAudible)throw new Exception("Quest acceptance is silent or did not advance the quest");Finish();
            }
        }
        catch(Exception e){failure=e.ToString();Finish();}
    }
    static void MovePlayer(Vector3 position)
    {
        var player=BeachBoatQuestSession.Instance.player;player.GetComponent<BeachBoatStory.Character_Movement>().enabled=false;
        player.GetComponent<Rigidbody>().isKinematic=true;player.transform.position=position;player.GetComponent<Rigidbody>().position=position;
    }
}
