using System;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Newtonsoft.Json.Linq;

/// <summary>Runs existing play-mode scenarios from the native Unity batch CLI, without desktop control.</summary>
public static class BeachBoatBatchRevision31
{
    const string Active="Revision31BatchActive",Suites="Revision31BatchSuites",Index="Revision31BatchIndex",Stamp="Revision31BatchStamp",Started="Revision31BatchStarted";
    public static bool IsRunning=>SessionState.GetBool(Active,false);
    public static void Run()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("This entry point requires CLI batch mode");
        var args=Environment.GetCommandLineArgs();string list="Revision31";for(int i=0;i<args.Length-1;i++)if(args[i]=="-qaSuites")list=args[i+1];
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Active,true);SessionState.SetBool("Revision31BatchWaiting",true);SessionState.SetString(Suites,list);SessionState.SetInt(Index,0);EditorApplication.delayCall+=Next;
    }
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Next()
    {
        var list=SessionState.GetString(Suites,"").Split(',');int index=SessionState.GetInt(Index,0);
        if(index>=list.Length){SessionState.SetBool(Active,false);Debug.Log("[Revision31 QA] All requested suites passed");BeachBoatQAExit.AfterPlayMode(0);return;}
        string name=list[index],path="Logs/"+name+"Playtest.json";SessionState.SetString(Stamp,File.Exists(path)?File.GetLastWriteTimeUtc(path).Ticks.ToString():"0");SessionState.SetFloat(Started,(float)EditorApplication.timeSinceStartup);
        var type=typeof(BeachBoatBatchRevision31).Assembly.GetType("BeachBoat"+name+"Playtest");if(type==null)throw new InvalidOperationException("Unknown QA suite "+name);
        SessionState.SetBool("Revision31BatchWaiting",false);Debug.Log("[Revision31 QA] Starting "+name);type.GetMethod("RunInEditor",BindingFlags.Static|BindingFlags.Public).Invoke(null,null);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Active,false)||SessionState.GetBool("Revision31BatchWaiting",false))return;
        var suiteNames=SessionState.GetString(Suites,"").Split(',');int suiteIndex=SessionState.GetInt(Index,0);float timeout=suiteIndex<suiteNames.Length&&suiteNames[suiteIndex]=="ConcurrentBattle"?370:190;
        if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Started,0)>timeout){SessionState.SetBool(Active,false);Debug.LogError("[Revision31 QA] Suite timeout");BeachBoatQAExit.AfterPlayMode(1);return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)return;
        var list=SessionState.GetString(Suites,"").Split(',');int index=SessionState.GetInt(Index,0);if(index>=list.Length)return;
        string path="Logs/"+list[index]+"Playtest.json";long stamp=long.Parse(SessionState.GetString(Stamp,"0"));if(!File.Exists(path)||File.GetLastWriteTimeUtc(path).Ticks<=stamp)return;
        var report=JObject.Parse(File.ReadAllText(path));bool pass=(bool?)report["success"]==true;Debug.Log("[Revision31 QA] "+list[index]+" "+(pass?"PASS":"FAIL")+" "+report["checks"]?.Count()+" "+report["error"]);
        if(!pass){SessionState.SetBool(Active,false);BeachBoatQAExit.AfterPlayMode(1);return;}
        SessionState.SetBool("Revision31BatchWaiting",true);SessionState.SetInt(Index,index+1);EditorApplication.delayCall+=Next;
    }
}
