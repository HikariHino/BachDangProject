using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using Story=BeachBoatStory.BachDangStoryManager;
using Object=UnityEngine.Object;

public static class BeachBoatTrapVoicePlaytest
{
    const string Flag="TrapHumanVoiceQA";const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static int phase,frame;static double began;static string failure;static float volume;static bool captured;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=0;frame=-1;began=0;failure=null;captured=false;checks.Clear();hashes.Clear();BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/TrapVoice/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error){if(captured)BachDangPreferences.SetVolume(volume);SessionState.SetBool(Flag,false);File.WriteAllText("Logs/TrapVoicePlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<5)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>90)throw new Exception("Trap voice QA timed out");
            var fleet=EnemyFleetDirector.Instance;var voice=fleet.GetComponent<RiverBattleVoices>();
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);BachDangAutoSave.Instance.enabled=false;volume=BachDangPreferences.Volume;captured=true;BachDangPreferences.SetVolume(0);
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();Story.Instance.TransitionTo(Story.StoryState.Act3_BattleInProgress,true);
                var data=new BachDangSaveData.Fleet{active=true,lureVersion=1,lureStage=2,invasionVoiced=true,pursuitVoiced=true,voiceEntryVersion=1};fleet.RestoreSave(data);fleet.StopAllCoroutines();fleet.Pursuit.enabled=false;fleet.GetComponent<RiverInvasionIntro>()?.Cancel();BachDangBattleTimeline.Instance.BeginLure();RiverBoardingCombat.Instance.enabled=false;RiverPlayerCombat.Instance.enabled=false;voice.enabled=false;
                var clip=Resources.Load<AudioClip>("BachDang938/BattleVoices/Enemy_Trap_Order");clip.LoadAudioData();var samples=new float[clip.samples];Check(clip.channels==1&&clip.frequency==32000&&clip.length>.4f&&clip.length<.9f,"The new trap order imports as one short recorded human shout");Check(clip.GetData(samples,0)&&samples.Max(v=>Mathf.Abs(v))>.2f&&samples.All(v=>Mathf.Abs(v)<.9f),"The shout contains audible unclipped recorded samples");
                var manifest=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Assets/Resources/BachDang938/BattleVoices/manifest.json"));var entry=manifest["entries"].First(e=>(string)e["name"]=="Enemy_Trap_Order");var pursuit=manifest["entries"].First(e=>(string)e["name"]=="Enemy_Advance_2");Check((string)entry["kind"]=="recorded-film-excerpt"&&(string)entry["source"]==(string)pursuit["source"]&&(string)entry["text"]==RiverBattleVoices.TrapEntryLine,"The shout uses the same human source as pursuit and a matching subtitle");Check((double)entry["tempo"]==1,"The human delivery retains its original tempo");
                var human=Resources.Load<AudioClip>("BachDang938/BattleVoices/Enemy_Advance_2");typeof(RiverBattleVoices).GetMethod("Play",Private).Invoke(voice,new object[]{human,false,"Giết! Đuổi theo bọn chúng cho ta!"});voice.Step(100);
                Check(voice.SpeechSource.isPlaying&&voice.SpeechSource.clip==human&&!voice.EntryPlayed,"Trap entry waits until the existing human pursuit sentence finishes");phase=1;return;
            }
            if(phase==1)
            {
                if(voice.SpeechSource.isPlaying)return;voice.Step(.02f);
                Check(voice.EntryPlayed&&voice.SpeechSource.clip.name=="Enemy_Trap_Order"&&voice.SpeechSource.isPlaying,"The recorded trap shout actually starts after the previous sentence");
                var caption=(TMPro.TMP_Text)typeof(RiverBattleVoices).GetField("caption",Private).GetValue(voice);Check(caption.text=="Quân Nam Hán: "+RiverBattleVoices.TrapEntryLine,"On-screen wording matches the shorter human shout");Check(!voice.SpeechSource.loop&&voice.SpeechSource.pitch==1,"Trap playback neither loops nor pitches the human voice");phase=2;return;
            }
            if(phase==2)
            {
                if(voice.SpeechSource.isPlaying)return;var saved=new BachDangSaveData.Fleet();voice.FillSave(saved);Check(saved.voiceEntryVersion==1&&saved.voiceEntryPlayed,"The completed entry shout retains the existing save flag");voice.RestoreSave(saved);voice.Step(.02f);Check(voice.EntryPlayed&&!voice.SpeechSource.isPlaying,"Loading the completed shout does not repeat or layer it");
                voice.RestoreSave(new BachDangSaveData.Fleet{ebb=true,voiceEntryVersion=0});Check(voice.EntryPlayed,"An older save already at low tide does not replay trap-entry speech");
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
