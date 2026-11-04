using UnityEditor;
using UnityEngine;

/// <summary>Silence native CLI playtests without changing game preferences or the user's system audio.</summary>
public static class BeachBoatSilentQA
{
    [InitializeOnLoadMethod]static void Register()
    {
        if(!Application.isBatchMode)return;
        Mute();EditorApplication.update-=Mute;EditorApplication.update+=Mute;
        EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void BeforeScene()=>Mute();
    static void Changed(PlayModeStateChange change)=>Mute();
    static void Mute(){if(Application.isBatchMode)AudioListener.volume=0;}
}
