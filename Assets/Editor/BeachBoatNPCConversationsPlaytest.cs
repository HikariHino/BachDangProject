using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using Newtonsoft.Json.Linq;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;
using Dialogue=BeachBoatStory.StoryDialogueUI;

public static class BeachBoatNPCConversationsPlaytest
{
    const string Flag="NPCConversations01QA";static int phase,frame;static double began,wait;static string failure;static int callbacks;static float previousVolume;static bool capturedVolume;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=callbacks=0;capturedVolume=false;frame=-1;began=wait=0;failure=null;checks.Clear();hashes.Clear();BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/NPCConversations/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error){if(capturedVolume)BachDangPreferences.SetVolume(previousVolume);SessionState.SetBool(Flag,false);File.WriteAllText("Logs/NPCConversationsPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<5||EditorApplication.timeSinceStartup<wait)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>150)throw new Exception("NPC conversation QA timed out");
            var story=Story.Instance;var ui=Dialogue.Instance;var voice=ui.GetComponent<DialogueVoice>();
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);BachDangAutoSave.Instance.enabled=false;previousVolume=BachDangPreferences.Volume;capturedVolume=true;BachDangPreferences.SetVolume(0);
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();ui.DismissForCheckpoint();
                var catalog=JObject.Parse(Resources.Load<TextAsset>("BachDang938/Voices/NPCConversations").text);
                var manifest=JObject.Parse(Resources.Load<TextAsset>("BachDang938/Voices/manifest").text);
                var variants=manifest["entries"].SelectMany(e=>e["variants"]??new JArray()).Where(v=>((string)v["profile"]).StartsWith("npc_")).ToArray();
                Check(variants.Length==180,"All 180 character conversation takes are indexed offline");
                foreach(var variant in variants)
                {
                    var clip=Resources.Load<AudioClip>((string)variant["path"]);Check(clip!=null&&clip.length>.5f&&clip.frequency==24000,"Bundled mono 24 kHz NPC clip: "+variant["profile"]);
                    var samples=new float[Mathf.Min(clip.samples*clip.channels,clip.frequency*2)];Check(clip.GetData(samples,0)&&samples.Any(s=>Mathf.Abs(s)>.005f),"Audible NPC waveform: "+clip.name);
                }
                var actors=Object.FindObjectsByType<BeachBoatStory.NPCDialogue>(FindObjectsInactive.Include,FindObjectsSortMode.None);var unique=new HashSet<string>();
                foreach(var script in catalog["actors"])
                {
                    string key=(string)script["key"];var actor=actors.Single(a=>a.name==key);string sameVoice=null;
                    foreach(var state in new[]{Story.StoryState.Act1_ExploreCamp,Story.StoryState.Act3_PrepareForBattle,Story.StoryState.Act3_Victory})
                    {
                        story.currentState=state;ui.Show(actor.speakerName,actor.dialogueText,null,actor.transform);var saved=ui.CaptureSave();
                        Check(unique.Add(saved.line)&&saved.line==((JArray)script["lines"])[NPCConversationLibrary.StoryStage].Value<string>(),"Distinct authored line at "+key+" / "+state);
                        Check(voice.CurrentClip!=null&&voice.ProfileId==(string)script["profile"]&&(sameVoice==null||sameVoice==voice.VoiceName),"Same character retains its assigned voice: "+key+" / "+state);sameVoice=voice.VoiceName;
                        var oldClip=voice.CurrentClip;ui.Close();ui.RestoreSave(saved);Check(voice.CurrentClip==oldClip&&voice.ProfileId==saved.voiceProfile,"Dialogue restore keeps exact new line and cast: "+key+" / "+state);ui.Close();
                    }
                }
                Check(unique.Count==84,"Every named NPC has three distinct authored story-stage lines");
                foreach(var role in (NPCAmbientDialogue.Role[])Enum.GetValues(typeof(NPCAmbientDialogue.Role)))foreach(bool female in new[]{false,true})
                {
                    var clips=new HashSet<AudioClip>();var profiles=new HashSet<string>();
                    for(int i=0;i<2;i++)
                    {
                        var actor=new GameObject("Conversation_QA_"+role+"_"+i).AddComponent<NPCAmbientDialogue>();actor.role=role;
                        if(female){var marker=new GameObject("Female_avatar_marker");marker.transform.SetParent(actor.transform,false);}
                        story.currentState=Story.StoryState.Act1_ExploreCamp;ui.Show("Người trong làng","old placeholder",null,actor.transform);clips.Add(voice.CurrentClip);profiles.Add(voice.ProfileId);
                        Check(voice.CurrentClip!=null&&voice.Female==female&&voice.ProfileId.StartsWith("npc_"+role.ToString().ToLowerInvariant()+"_"),"Resident role and gender resolve: "+role+" / "+female+" / "+i);ui.Close();Object.DestroyImmediate(actor.gameObject);
                    }
                    Check(clips.Count==2&&profiles.Count==2,"Same-role neighbours have distinct stable cast and lines: "+role+" / "+female);
                }
                story.currentState=Story.StoryState.Act2_CollectWoodInVillage;VillageVendorSpeech.Say(VillageWorkStall.WorkKind.Rice,0);
                Check(!ui.IsOpen&&VillageVendorSpeech.Instance.Voice.CurrentClip!=null&&VillageVendorSpeech.Instance.Voice.Female,"Rice vendor speaks without blocking play or opening a dialogue panel");
                phase=1;wait=EditorApplication.timeSinceStartup+.35;return;
            }
            if(phase==1)
            {
                Check(VillageVendorSpeech.Instance.Voice.SpeechSource.isPlaying,"Vendor voice advances through a real AudioSource");
                var casts=new HashSet<string>();foreach(var kind in (VillageWorkStall.WorkKind[])Enum.GetValues(typeof(VillageWorkStall.WorkKind)))
                {VillageVendorSpeech.Say(kind,0);casts.Add(VillageVendorSpeech.Instance.Voice.VoiceName);Check(VillageVendorSpeech.Instance.Caption.Length>20&&VillageVendorSpeech.Instance.Voice.CurrentClip!=null,"Distinct voiced vendor greeting: "+kind);VillageVendorSpeech.Say(kind,2);Check(VillageVendorSpeech.Instance.Voice.CurrentClip!=null,"Voiced vendor completion: "+kind);}
                Check(casts.Count==4,"The four vendors use four different voice presets");
                const string order="Quân Nam Hán đang lăm le xâm phạm bờ cõi. Hãy đến làng ven sông, cùng bà con chuẩn bị gỗ làm cọc. Ta trông cậy vào ngươi.";
                ui.Show("Ngô Quyền",order,()=>callbacks++);Check(VillageVendorSpeech.Instance.Voice.CurrentClip==null&&voice.ProfileId=="commander"&&ui.CaptureSave().line==order,"Commander dialogue interrupts vendor voice and retains its exact mission text and separate cast");ui.TryAdvance();ui.TryAdvance();Check(callbacks==1,"Mission callback still advances exactly once");
                VillageVendorSpeech.Say(VillageWorkStall.WorkKind.Rope,1);var save=BachDangSaveSystem.Capture("QA new NPC voices");BachDangSaveSystem.Apply(save);Check(VillageVendorSpeech.Instance.Voice.CurrentClip==null,"Checkpoint load stops old vendor speech");
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
