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
    static string failure;
    static readonly float[] Output=new float[1024];
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");EditorUtility.audioMasterMute=false;SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Finish()
    {
        var audio=BeachBoatSoundscape.Instance;Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/AudioPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,outputPeak,muteChecked,sampleRate=AudioSettings.outputSampleRate,listenerVolume=AudioListener.volume,listenerPaused=AudioListener.pause,editorMuted=EditorUtility.audioMasterMute,riverPlaying=audio?.RiverSource.isPlaying,windPlaying=audio?.WindSource.isPlaying,effects=audio?.EffectsPlayed,footsteps=audio?.FootstepsPlayed,oars=audio?.OarsPlayed},Formatting.Indented));
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
            if(phase==6){if(AudioListener.volume<=0)throw new Exception("Unmute did not restore master output");Finish();}
        }
        catch(Exception e){failure=e.ToString();Finish();}
    }
}
