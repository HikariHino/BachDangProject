using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BeachBoatStartupAudioPlaytest
{
    const string Flag="StartupAudioQA";
    static double started;
    static double until;
    static int phase;
    static float introWind,introRiver,windRms;
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");EditorUtility.audioMasterMute=false;SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<5)return;
        var sound=BeachBoatSoundscape.Instance;var player=BeachBoatQuestSession.Instance.player;
        if(EditorApplication.timeSinceStartup<until)return;
        try
        {
            if(phase==0)
            {
                introWind=sound.WindSource.volume;introRiver=sound.RiverSource.volume;
                if(!BeachBoatStory.OpeningEvent.IsOpeningActive||introWind>.006f||introRiver>.0001f)throw new Exception("Opening still has loud ambience: "+introWind+" / "+introRiver);
                foreach(var intro in Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))intro.SkipOpening();
                BeachBoatStory.StoryDialogueUI.Instance?.Close();phase=1;until=EditorApplication.timeSinceStartup+4;return;
            }
            if(sound.RiverSource.volume>.0001f||!float.IsPositiveInfinity(sound.DistanceToWater))throw new Exception("Water is audible at the inland spawn");
            if(sound.WindSource.volume<.005f||sound.WindSource.volume>.037f)throw new Exception("Camp wind is silent or too loud: "+sound.WindSource.volume);
            var filter=sound.WindSource.GetComponent<AudioLowPassFilter>();if(filter==null||!filter.enabled||filter.cutoffFrequency>800)throw new Exception("Wind hiss is not filtered");
            var output=new float[2048];sound.WindSource.GetOutputData(output,0);windRms=(float)Math.Sqrt(output.Sum(v=>(double)v*v)/output.Length);
            if(windRms>.008f)throw new Exception("Wind output still masks quiet action sounds: "+windRms);
            Finish(null,sound,player);
        }
        catch(Exception error){Finish(error.ToString(),sound,player);}
    }
    static void Finish(string failure,BeachBoatSoundscape sound,GameObject player)
    {
        var listener=Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).First(l=>l.isActiveAndEnabled);
        var sources=Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Select(s=>new{name=s.name,clip=s.clip?.name,s.isPlaying,s.volume,s.spatialBlend,s.minDistance,s.maxDistance,rolloff=s.rolloffMode.ToString(),distance=Vector3.Distance(s.transform.position,listener.transform.position),playerDistance=Vector3.Distance(s.transform.position,player.transform.position)}).ToArray();
        var clips=new[]{sound.wind,sound.river}.Select(c=>{var samples=new float[c.samples*c.channels];c.GetData(samples,0);return new {c.name,rms=Math.Sqrt(samples.Sum(v=>(double)v*v)/samples.Length),peak=samples.Max(v=>Mathf.Abs(v))};}).ToArray();
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/StartupAudioPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,introWind,introRiver,windRms,player=player.transform.position.ToString(),listener=listener.transform.position.ToString(),sound.DistanceToWater,sources,clips},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
}
