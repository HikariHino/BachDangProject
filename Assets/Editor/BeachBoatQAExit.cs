using UnityEditor;

/// <summary>Let Unity tear down Play Mode before shutting down the batch editor.</summary>
public static class BeachBoatQAExit
{
    const string Key="BeachBoatQAExitCode";
    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.playModeStateChanged-=OnMode;
        EditorApplication.playModeStateChanged+=OnMode;
    }
    public static void AfterPlayMode(int code)
    {
        SessionState.SetInt(Key,code);
        if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
        else EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
    }
    static void OnMode(PlayModeStateChange state)
    {
        int code=SessionState.GetInt(Key,-1);
        if(state!=PlayModeStateChange.EnteredEditMode||code<0)return;
        SessionState.SetInt(Key,-1);EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
    }
}
