using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using StoryOpening = BeachBoatStory.OpeningEvent;
using StoryDialogue = BeachBoatStory.StoryDialogueUI;
using StoryFader = BeachBoatStory.ScreenFader;
using StakeZone = BeachBoatStory.RiverStakeZone;

/// <summary>Headless Play Mode check that every new camp service changes gameplay state.</summary>
public static class BeachBoatCampFacilityPlaytest
{
    const string Flag = "BeachBoatCampFacilityPlaytest";
    const string PendingExit = "BeachBoatCampFacilityPlaytestExit";
    static double started;

    public static void RunAndExit()
    {
        SessionState.SetInt(PendingExit, -1);
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        SessionState.SetBool(Flag, true);
        started = 0;
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        int code = SessionState.GetInt(PendingExit, -1);
        if (state != PlayModeStateChange.EnteredEditMode || code < 0) return;
        SessionState.SetInt(PendingExit, -1);
        EditorApplication.delayCall += () => EditorApplication.Exit(code);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
        if (started == 0) started = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - started < 3) return;
        string failure = null;
        bool rested = false, armored = false, trained = false, supplied = false;
        int facilityCount = 0;
        try
        {
            foreach (var opening in UnityEngine.Object.FindObjectsByType<StoryOpening>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                opening.SkipOpening();
            StoryDialogue.Instance?.Close();
            var session = BeachBoatQuestSession.Instance;
            var story = session != null ? session.GetComponent<BeachBoatStory.BachDangStoryManager>() : null;
            var vitals = session?.player?.GetComponent<PlayerVitals>();
            var facilities = UnityEngine.Object.FindObjectsByType<CampFacility>(FindObjectsSortMode.None);
            facilityCount = facilities.Length;
            if (session == null || story == null || vitals == null || facilityCount != 13)
            {
                if (EditorApplication.timeSinceStartup - started < 35) return;
                var allStories = UnityEngine.Object.FindObjectsByType<BeachBoatStory.BachDangStoryManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                throw new InvalidOperationException($"Missing scene bindings: session={session != null}({session?.name}), " +
                    $"story={story != null}, player={session != null && session.player != null}, " +
                    $"vitals={vitals != null}, facilities={facilityCount}, allStories={allStories.Length} " +
                    string.Join(",", allStories.Select(s => s.name + ":" + s.gameObject.activeInHierarchy)));
            }
            if (BeachBoatQuestSession.InteractionBlocked)
            {
                if (EditorApplication.timeSinceStartup - started < 35) return;
                throw new InvalidOperationException($"Interactions stayed blocked: opening={StoryOpening.IsOpeningActive}, " +
                    $"planting={StakeZone.Instance != null && StakeZone.Instance.IsPlanting}, " +
                    $"storyMode={session.storyMode}, dialogue={StoryDialogue.Instance != null && StoryDialogue.Instance.IsOpen}, " +
                    $"fader={StoryFader.Instance != null && StoryFader.Instance.IsTransitioning}.");
            }

            bool Use(CampFacility.FacilityKind kind)
            {
                StoryDialogue.Instance?.Close();
                var facility = facilities.First(f => f.kind == kind);
                session.player.transform.position = facility.transform.position;
                var body = session.player.GetComponent<Rigidbody>();
                if (body != null) { body.position = facility.transform.position; body.linearVelocity = Vector3.zero; }
                Physics.SyncTransforms();
                return facility.TryUse();
            }

            vitals.TakeImpact(40, "QA");
            vitals.TryExert(25, 1);
            rested = Use(CampFacility.FacilityKind.Barracks) && vitals.Health > 90 &&
                     Mathf.Approximately(vitals.Stamina, vitals.maxStamina);
            armored = Use(CampFacility.FacilityKind.Forge) && vitals.ArmorCharges == 3;
            float beforeHit = vitals.Health;
            vitals.TakeImpact(20, "QA");
            armored &= Mathf.Abs((beforeHit - vitals.Health) - 14) < .1f && vitals.ArmorCharges == 2;
            trained = Use(CampFacility.FacilityKind.Armory) && vitals.maxStamina >= 120 &&
                      Mathf.Approximately(vitals.Stamina, vitals.maxStamina);
            float beforeFood = vitals.Health;
            supplied = Use(CampFacility.FacilityKind.Provisions) && vitals.Health > beforeFood;
            if (!rested || !armored || !trained || !supplied)
                failure = "A facility did not change health, armor or stamina as expected.";
        }
        catch (Exception error) { failure = error.ToString(); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BeachBoatCampFacilityPlaytest.json", JsonConvert.SerializeObject(new
        { success = failure == null, failure, facilityCount, rested, armored, trained, supplied }, Formatting.Indented));
        SessionState.SetBool(Flag, false);
        SessionState.SetInt(PendingExit, failure == null ? 0 : 1);
        EditorApplication.isPlaying = false;
    }
}
