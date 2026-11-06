using System;
using System.IO;
using BeachBoatStory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Scenario = BeachBoatStory.CampaignQuickPlay.Scenario;

/// <summary>Starts a manual play session at a chosen objective in the real campaign scene.</summary>
public static class BeachBoatQuickPlay
{
    const string Menu = "BeachBoat/Test nhanh/";
    const string ScenePath = "Assets/Scenes/beachBoat.unity";
    const string ScenarioKey = "BeachBoatQuickPlay.Scenario";
    const string PreviousSceneKey = "BeachBoatQuickPlay.PreviousScene";
    const string PreviousDirectoryKey = "BeachBoatQuickPlay.PreviousDirectory";
    const string PreviousSlotKey = "BeachBoatQuickPlay.PreviousSlot";

    [MenuItem(Menu + "1. Hieu lenh trong")]
    static void Drum() => Launch(Scenario.Drum);
    [MenuItem(Menu + "2. Tiep te/Cua go")]
    static void Timber() => Launch(Scenario.Timber);
    [MenuItem(Menu + "2. Tiep te/Buoc day")]
    static void Rope() => Launch(Scenario.Rope);
    [MenuItem(Menu + "2. Tiep te/Dong gao")]
    static void Rice() => Launch(Scenario.Rice);
    [MenuItem(Menu + "2. Tiep te/Goi luong kho")]
    static void Provisions() => Launch(Scenario.Provisions);
    [MenuItem(Menu + "3. Dong coc")]
    static void Stakes() => Launch(Scenario.Stakes);
    [MenuItem(Menu + "4. Nhu dich")]
    static void Lure() => Launch(Scenario.Lure);
    [MenuItem(Menu + "5. Tap ban cung")]
    static void Archery() => Launch(Scenario.Archery);

    static int Selected => SessionState.GetInt(ScenarioKey, -1);

    static void Launch(Scenario scenario)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("Hãy dừng Play Mode và đợi Unity biên dịch xong trước khi chọn đoạn test.");
            return;
        }

        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (scene == null)
        {
            Debug.LogError("Không tìm thấy cảnh chiến dịch " + ScenePath);
            return;
        }

        SessionState.SetString(PreviousSceneKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(PreviousDirectoryKey, BachDangSaveSystem.TestDirectory ?? "");
        SessionState.SetInt(PreviousSlotKey, BachDangSaveSystem.ActiveSlot);
        SessionState.SetInt(ScenarioKey, (int)scenario);
        IsolateSaves();
        EditorSceneManager.playModeStartScene = scene;
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        IsolateSaves();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void IsolateSaves()
    {
        if (Selected < 0) return;
        BachDangSaveSystem.TestDirectory = Path.GetFullPath(
            Path.Combine("Library", "BachDangQuickTest", ((Scenario)Selected).ToString()));
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (Selected < 0) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            EditorApplication.update += StartWhenReady;
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= StartWhenReady;
            var path = SessionState.GetString(PreviousSceneKey, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            BachDangSaveSystem.TestDirectory = SessionState.GetString(PreviousDirectoryKey, "");
            BachDangSaveSystem.SelectSlot(SessionState.GetInt(PreviousSlotKey, 1));
            SessionState.EraseInt(ScenarioKey);
        }
    }

    static void StartWhenReady()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 3) return;
        var scenario = (Scenario)Selected;
        if (scenario == Scenario.Archery && !CampaignQuickPlay.ArcheryReady && Time.frameCount < 120) return;
        EditorApplication.update -= StartWhenReady;
        try
        {
            var menu = BachDangFrontEnd.Instance;
            if (menu == null)
                throw new InvalidOperationException("Cảnh chiến dịch chưa khởi tạo xong.");

            IsolateSaves();
            menu.Play();
            CampaignQuickPlay.SetUp(scenario);
        }
        catch (Exception error)
        {
            Debug.LogError("Không thể vào đoạn test nhanh. " + error);
            EditorApplication.ExitPlaymode();
        }
    }
}
