using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using Object=UnityEngine.Object;

public static class BeachBoatContactRegression
{
    const string Flag="BoatContact30QA";static int phase,frame=-1,steps;static double began;static float until,health;static string failure;static Vector3 start;static GameObject wall;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=steps=0;frame=-1;began=0;failure=null;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error){SessionState.SetBool(Flag,false);File.WriteAllText("Logs/BoatContactRegression.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>100)throw new Exception("Contact QA timeout");var boat=QuestRowboat.Instance;
            if(phase==0)
            {
                foreach(var path in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[path]=Hash(path);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/BoatContactQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                var args=new object[]{Path.Combine(Application.persistentDataPath,"campaign938-auto.json"),null};Check((bool)typeof(BachDangSaveSystem).GetMethod("ReadFile",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args),"The reported checkpoint passes its original checksum");BachDangSaveSystem.Apply((BachDangSaveData)args[1]);
                Check(RiverConstructionCamp.Ensure(BeachBoatStory.RiverStakeZone.Instance).Workers>=12,"The older partial checkpoint gains persistent paired construction teams");
                health=boat.Durability;until=Time.unscaledTime+4;phase=1;return;
            }
            if(phase==1)
            {
                boat.Step(0,1,.05f);if(Time.unscaledTime<until)return;
                Check(boat.Durability==health,"Holding steering beside the reported stake for four seconds causes no invented angular-impact damage");start=boat.transform.position;steps=0;phase=2;return;
            }
            if(phase==2)
            {
                boat.Step(-1,0,.05f);if(++steps<180)return;
                Check(QuestRowboat.FlatDistance(start,boat.transform.position)>30,"The reported boat can back away from the completed stake instead of staying wedged");
                foreach(var patrol in Object.FindObjectsByType<AmbientRiverBoat>())patrol.enabled=false;
                bool found=false;for(int x=0;x<8&&!found;x++)for(int z=0;z<5&&!found;z++)
                {
                    var p=new Vector3(-3000+x*180,TideSystem.CurrentWaterHeight,1350+z*200);
                    if(Enumerable.Range(0,13).All(i=>boat.CanOccupy(p+Vector3.forward*i*25,Quaternion.identity))){boat.RestoreSave(true,p,Quaternion.identity);start=boat.transform.position;found=true;}
                }
                Check(found,"A real open-water route is clear of every solid hull and pole");boat.RestoreDurability(100);steps=0;phase=3;return;
            }
            if(phase==3)
            {
                boat.Step(1,0,.05f);if(++steps<180)return;
                Check(boat.Durability==100&&QuestRowboat.FlatDistance(start,boat.transform.position)>200,"Straight rowing on open water travels over two hundred units without losing hull durability");
                var hull=BoatHullCollision.Get(boat.transform);wall=new GameObject("QA_Real_Wood_Obstacle");var box=wall.AddComponent<BoxCollider>();box.size=new Vector3(200,100,4);wall.transform.position=boat.transform.position+Vector3.forward*(hull.SolidHull.bounds.extents.z+2);Physics.SyncTransforms();
                start=boat.transform.position;typeof(QuestRowboat).GetField("<Speed>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(boat,95f);boat.Step(1,0,.05f);
                Check(boat.Durability<100&&QuestRowboat.FlatDistance(start,boat.transform.position)<.01f,"A genuine fast collision still stops at solid wood and damages the hull");health=boat.Durability;until=Time.unscaledTime+4;phase=4;return;
            }
            if(phase==4)
            {
                boat.Step(0,1,.05f);if(Time.unscaledTime<until)return;
                Check(boat.Durability==health,"Continued stationary steering contact does not repeatedly damage the hull");Object.DestroyImmediate(wall);
                var hull=BoatHullCollision.Get(boat.transform);var p=boat.transform.position;boat.transform.rotation=Quaternion.identity;var post=new GameObject("QA_Existing_Post_Overlap");var capsule=post.AddComponent<CapsuleCollider>();capsule.height=35;capsule.radius=1;post.transform.position=p+Vector3.right*(hull.SolidHull.size.x*.5f);Physics.SyncTransforms();
                Check(hull.CanMove(p,Quaternion.identity,p-Vector3.right*2,Quaternion.identity,false),"An already overlapping checkpoint may move out of the pole");
                Check(!hull.CanMove(p,Quaternion.identity,p+Vector3.right*1,Quaternion.identity,false),"The same overlap cannot be pushed deeper through the pole");Object.DestroyImmediate(post);
                var sound=BeachBoatSoundscape.Instance;sound.masterVolume=.8f;sound.SetMuted(false);var foley=new VillageWorkFoley(new GameObject("QA_Foley_Mix").transform);foley.Move(VillageWorkFoley.Cue.Saw,1);foley.Play(VillageWorkFoley.Cue.Knot);foley.Tick(.2f,false);
                var friction=(AudioSource)typeof(VillageWorkFoley).GetField("friction",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(foley);var action=(AudioSource)typeof(VillageWorkFoley).GetField("action",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(foley);
                Check(friction.isPlaying&&friction.volume>.6f&&friction.volume<=.8f,"Recorded tool friction plays clearly at the raised bounded mix level");Check(action.isPlaying&&action.volume>.7f&&action.volume<=.8f,"Grain, knot and cloth accents use the raised bounded action mix");foley.Stop();
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
