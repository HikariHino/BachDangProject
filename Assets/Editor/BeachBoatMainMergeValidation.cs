using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

public static class BeachBoatMainMergeValidation
{
    public static void AuditAndExit()
    {
        var buildScenes = EditorBuildSettings.scenes;
        if (buildScenes.Length == 0 || !buildScenes[0].enabled ||
            buildScenes[0].path != "Assets/Scenes/beachBoat.unity")
            throw new InvalidOperationException("beachBoat is not the first enabled build scene");

        var beachBoat = EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        if (!beachBoat.IsValid() || !beachBoat.isLoaded || beachBoat.rootCount < 1)
            throw new InvalidOperationException("beachBoat scene did not load after merging main");
        int beachBoatRootCount = beachBoat.rootCount;

        var testQuest = EditorSceneManager.OpenScene("Assets/Scenes/TestQuest.unity");
        if (!testQuest.IsValid() || !testQuest.isLoaded || testQuest.rootCount < 1)
            throw new InvalidOperationException("TestQuest scene did not load after merging main");

        var character = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Main_Character/Animation/Main_Character_Animation.controller");
        var sword = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Main_Character/Animation/Sword_Anima.controller");
        if (character == null || sword == null) throw new InvalidOperationException("Merged controllers missing");
        string[] states = character.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
        string[] parameters = character.parameters.Select(p => p.name).ToArray();
        string[] swordParameters = sword.parameters.Select(p => p.name).ToArray();
        foreach (string name in new[] { "Sitting", "Laying", "Attack" })
            if (!states.Contains(name)) throw new InvalidOperationException("Missing merged animation state: " + name);
        foreach (string name in new[] { "Pose", "Attack" })
            if (!parameters.Contains(name) || !swordParameters.Contains(name))
                throw new InvalidOperationException("Missing merged animator parameter: " + name);

        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BeachBoatMainMergeValidation.json", JsonConvert.SerializeObject(new {
            primaryBuildScene = buildScenes[0].path, beachBoatRootCount,
            testQuestLoaded = true, testQuestRootCount = testQuest.rootCount,
            states, parameters, swordParameters
        }, Formatting.Indented));
        EditorApplication.Exit(0);
    }
}
