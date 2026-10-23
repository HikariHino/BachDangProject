using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using BeachBoatStory;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using Dialogue=BeachBoatStory.StoryDialogueUI;

public static class BeachBoatDialogueVoicePlaytest
{
    const string Flag="DialogueVoice938QA";
    static readonly List<string> checks=new List<string>();
    static int phase,callbacks;
    static double started,until;
    static float previousVolume;
    static bool capturedVolume;
    static string runtimeError;
    static GameObject first,second;
    const string Line="Quân Nam Hán đang lăm le xâm phạm bờ cõi. Hãy đến làng ven sông, cùng bà con chuẩn bị gỗ làm cọc. Ta trông cậy vào ngươi.";
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string m,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))runtimeError=m+"\n"+stack;}
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);}
    static TMP_Text Body=>Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include).First(t=>t.name=="Body"&&t.GetComponentInParent<Dialogue>()!=null);
    static void Finish(string failure)
    {
        if(capturedVolume)BachDangPreferences.SetVolume(previousVolume);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DialogueVoicePlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,phase,checks},Formatting.Indented));SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<4||EditorApplication.timeSinceStartup<until)return;
        if(EditorApplication.timeSinceStartup-started>70){Finish("Timeout");return;}
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);
            var ui=Dialogue.Instance;var voice=ui.GetComponent<DialogueVoice>();var menu=BachDangFrontEnd.Instance;
            if(phase==0)
            {
                menu.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();ui.Close();
                previousVolume=BachDangPreferences.Volume;capturedVolume=true;BachDangPreferences.SetVolume(1);
                ui.Show("Ngô Quyền",Line,()=>callbacks++);
                Check(voice.CurrentClip!=null&&voice.CurrentClip.length>4&&voice.ProfileId=="commander"&&Mathf.Abs(voice.VoicePitch-1)<.001f,"Ngo Quyen uses a separately recorded commander take at normal playback speed");
                var manifest=JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(Resources.Load<TextAsset>("BachDang938/Voices/manifest").text);
                var variants=manifest["entries"].SelectMany(e=>e["variants"]).ToArray();
                Check(variants.Length>=75&&variants.All(v=>Resources.Load<AudioClip>((string)v["path"])!=null),"Every cast recording is bundled and resolves offline");
                foreach(var variant in variants)
                {
                    var take=Resources.Load<AudioClip>((string)variant["path"]);var waveform=new float[Mathf.Min(take.samples*take.channels,take.frequency*2)];
                    Check(take.GetData(waveform,0)&&waveform.Any(s=>Mathf.Abs(s)>.005f),"Audible cast waveform: "+take.name);
                }
                Check(Body.maxVisibleCharacters==0,"Subtitle starts hidden for progressive appearance");
                phase=1;until=EditorApplication.timeSinceStartup+.7;return;
            }
            if(phase==1)
            {
                Check(voice.SpeechSource.isPlaying&&voice.SpeechSource.time>.2f,"Dialogue speech plays through a real AudioSource");
                var clip=voice.CurrentClip;var samples=new float[Mathf.Min(clip.samples*clip.channels,22050*4)];clip.GetData(samples,0);
                Check(samples.Any(v=>Mathf.Abs(v)>.01f),"Generated speech contains an audible waveform");
                Check(Body.maxVisibleCharacters>0&&Body.maxVisibleCharacters<Body.textInfo.characterCount&&voice.SubtitleSounds>0,"Subtitle reveals gradually with quiet typing feedback");
                menu.OpenPause();phase=2;until=EditorApplication.timeSinceStartup+.25;return;
            }
            if(phase==2)
            {
                Check(!voice.SpeechSource.isPlaying&&ui.IsOpen,"Pause suspends dialogue speech without closing the dialogue");
                menu.Resume();phase=3;until=EditorApplication.timeSinceStartup+.2;return;
            }
            if(phase==3)
            {
                Check(voice.SpeechSource.isPlaying,"Resume continues the same voice clip");
                Check(ui.TryAdvance()&&ui.IsOpen&&callbacks==0&&Body.maxVisibleCharacters>=Body.textInfo.characterCount,"First advance reveals text without advancing the quest");
                Check(ui.TryAdvance()&&!ui.IsOpen&&callbacks==1&&!voice.SpeechSource.isPlaying,"Second advance closes dialogue, stops speech and advances once");
                first=new GameObject("Villager_A");second=new GameObject("Villager_B");
                ui.Show("Dân làng","Đang đi làm. Làng ven sông hôm nay đông vui hơn mọi ngày.",null,first.transform);
                Check(voice.CurrentClip!=null,"Activity prefix resolves the full spoken villager line");
                float pitch=voice.VoicePitch;string identity=voice.Identity;string source=voice.VoiceName;var firstClip=voice.CurrentClip;
                ui.Show("Dân làng","Làng ven sông hôm nay đông vui hơn mọi ngày.",null,second.transform);
                Check(identity!=voice.Identity&&source!=voice.VoiceName&&firstClip!=voice.CurrentClip,"Two villagers with the same display name use different recorded voice actors");
                ui.Show("Bà con làng ven sông","Gỗ, dây buộc, gạo và lương khô đã chuẩn bị đủ. Hãy về doanh trại báo cáo Ngô Quyền.");
                Check(voice.Female&&voice.CurrentClip.name.EndsWith("_f"),"Female speaker uses a real Vietnamese female recording");
                var saved=ui.CaptureSave();var savedIdentity=voice.Identity;var savedProfile=voice.ProfileId;var savedClip=voice.CurrentClip;ui.Close();ui.RestoreSave(saved);
                Check(voice.Identity==savedIdentity&&voice.Female&&voice.ProfileId==savedProfile&&voice.CurrentClip==savedClip,"Saved dialogue restores the exact recorded cast profile");
                saved.voiceProfile=null;ui.Close();ui.RestoreSave(saved);
                Check(voice.CurrentClip!=null&&voice.Identity==savedIdentity&&voice.Female,"Older saves without a cast-profile field still restore speech");
                var actors=Object.FindObjectsByType<NPCAmbientDialogue>(FindObjectsInactive.Include);
                foreach(var role in new[]{NPCAmbientDialogue.Role.Farmer,NPCAmbientDialogue.Role.Fisher,NPCAmbientDialogue.Role.Merchant,NPCAmbientDialogue.Role.Porter,NPCAmbientDialogue.Role.Smith})
                {
                    var actor=actors.FirstOrDefault(a=>a.role==role);
                    if(actor==null){actor=new GameObject("Voice_QA_"+role).AddComponent<NPCAmbientDialogue>();actor.role=role;}
                    string line=role==NPCAmbientDialogue.Role.Farmer?"Ruộng ngoài bãi phù sa tốt lắm, mùa này lúa lên đều.":role==NPCAmbientDialogue.Role.Fisher?"Khúc sông này phải thuộc con nước mới chèo thuyền an toàn.":role==NPCAmbientDialogue.Role.Merchant?"Chợ họp từ sáng, bà con đổi cá lấy thóc và củi.":role==NPCAmbientDialogue.Role.Porter?"Tôi đang chuyển bó củi này sang bếp trại.":"Lò rèn phải đỏ lửa cả ngày để sửa giáo và nông cụ.";
                    ui.Show(actor.displayName,line,null,actor.transform);
                    Check(voice.CurrentClip!=null&&voice.ProfileId.StartsWith(role.ToString().ToLowerInvariant()+"_"),"Role-specific recorded cast: "+role);
                }
                ui.Show("Quản quân","Ngươi đã thuộc hiệu lệnh. Hãy qua cổng sang làng, cùng dân binh chuẩn bị cọc gỗ.");
                Check(voice.ProfileId=="veteran","Quartermaster uses the veteran voice rather than the commander voice");
                ui.Show("Lính gác doanh trại","Doanh trại đã sẵn sàng. Quân sĩ đang chờ lệnh của Chủ tướng.");
                Check(voice.ProfileId=="soldier"||voice.ProfileId=="veteran","Guard uses a military voice cast");
                ui.Show("Luyện quân","Hoàn thành hiệu lệnh trống trong 12.0 giây, sai 1 lần.");Check(voice.CurrentClip!=null,"Dynamic training result has a voiced completion line");
                ui.Close();BachDangPreferences.SetVolume(0);int ticks=voice.SubtitleSounds;
                ui.Show("Ngô Quyền",Line);voice.SubtitleTick();Check(AudioListener.volume==0&&voice.SubtitleSounds==ticks,"Volume setting mutes dialogue and subtitle ticks");
                ui.Close();Finish(null);
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
}
