using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Story = BeachBoatStory.BachDangStoryManager;

/// <summary>Batch smoke test for timeout, retry, randomized beats and four physical deliveries.</summary>
public static class BeachBoatMinigamePlaytest
{
    const string Flag = "BeachBoatMinigamePlaytestActive";
    static double started;
    static int phase;
    static string failure;
    static string signFailure;
    static bool timedOut, supplyTimedOut, beatChanged, wrongCargoRejected, allDeliveries, signsLabelled;
    static bool selectionVisible, travelCompact, returnCompact, closedAfterFinish, capturedTravel, capturedReturn;

    [MenuItem("BeachBoat/QA/Run Minigame Smoke Test")]
    public static void RunAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        SessionState.SetBool(Flag, true);
        started = 0; phase = 0; failure = null;
        signFailure = null;
        timedOut = supplyTimedOut = beatChanged = wrongCargoRejected = allDeliveries = false;
        signsLabelled = true;
        selectionVisible=travelCompact=returnCompact=closedAfterFinish=capturedTravel=capturedReturn=false;
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Register() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

    static void Move(GameObject player, Vector3 position)
    {
        player.transform.position = position;
        var body = player.GetComponent<Rigidbody>();
        if (body != null) { body.position = position; body.linearVelocity = Vector3.zero; }
        Physics.SyncTransforms();
    }

    static void Finish(bool success, BeachBoatMinigameStation drum, BeachBoatMinigameStation supply)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BeachBoatMinigamePlaytest.json", JsonConvert.SerializeObject(new {
            success, failure, signFailure, timedOut, supplyTimedOut, beatChanged, wrongCargoRejected, allDeliveries, signsLabelled,
            drumCompleted = drum != null && drum.HasCompleted,
            supplyCompleted = supply != null && supply.HasCompleted,
            supplyTrips = supply?.Progress, selectionVisible, travelCompact, returnCompact, closedAfterFinish
        }, Formatting.Indented));
        SessionState.SetBool(Flag, false);
        BeachBoatQAExit.AfterPlayMode(success ? 0 : 1);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
        if (started == 0) { started = EditorApplication.timeSinceStartup; signsLabelled = true; }
        double elapsed = EditorApplication.timeSinceStartup - started;
        var stations = UnityEngine.Object.FindObjectsByType<BeachBoatMinigameStation>(FindObjectsSortMode.None);
        var drum = stations.FirstOrDefault(s => s.kind == BeachBoatMinigameStation.ActivityKind.WarDrum);
        var supply = stations.FirstOrDefault(s => s.kind == BeachBoatMinigameStation.ActivityKind.SupplyRelay);
        var story = Story.Instance;
        var session = BeachBoatQuestSession.Instance;
        if (elapsed > 45) {
            failure = "Timed out at phase " + phase + "; state=" + story?.currentState +
                "; opening=" + BeachBoatStory.OpeningEvent.IsOpeningActive +
                "; dialogue=" + BeachBoatStory.StoryDialogueUI.Instance?.IsOpen +
                "; fade=" + BeachBoatStory.ScreenFader.Instance?.IsTransitioning +
                "; storyMode=" + session?.storyMode;
            Finish(false, drum, supply); return;
        }
        if (drum == null || supply == null || story == null || session?.player == null) return;

        if (phase == 0 && elapsed > 2)
        {
            foreach (var opening in UnityEngine.Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                opening.SkipOpening();
            BeachBoatStory.StoryDialogueUI.Instance?.Close();
            story.TransitionTo(Story.StoryState.Act1_Training, true);
            Move(session.player, drum.transform.position + Vector3.right * 18);
            phase = 1;
        }
        else if (phase == 1 && elapsed > 3 && !BeachBoatQuestSession.InteractionBlocked)
        {
            drum.BeginActivity();
            if (BeachBoatMinigameStation.Active != drum) { failure = "Could not start drum"; Finish(false, drum, supply); return; }
            int original = drum.GetRhythmBeat(0);
            typeof(BeachBoatMinigameStation).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(drum, drum.TimeLimit + .1f);
            phase = 2;
            SessionState.SetInt("BeachBoatMinigameFirstBeat", original);
        }
        else if (phase == 2 && drum.Failed)
        {
            timedOut = !drum.HasCompleted && story.currentState == Story.StoryState.Act1_Training;
            drum.RetryActivity();
            beatChanged = drum.GetRhythmBeat(0) != SessionState.GetInt("BeachBoatMinigameFirstBeat", 0);
            // One matching first beat is possible by chance; the following beats are also randomized.
            int safety = 0;
            while (BeachBoatMinigameStation.Active == drum && safety++ < 30)
                drum.SubmitRhythm(drum.ExpectedBeat);
            if (!drum.HasCompleted) { failure = "Drum did not complete after retry"; Finish(false, drum, supply); return; }
            phase = 3;
        }
        else if (phase == 3)
        {
            BeachBoatStory.StoryDialogueUI.Instance?.Close();
            story.TransitionTo(Story.StoryState.Act2_SupplyPreparation, true);
            Move(session.player, supply.transform.position + Vector3.right * 15);
            phase = 4;
        }
        else if (phase == 4 && BeachBoatStory.StoryDialogueUI.Instance != null &&
                 BeachBoatStory.StoryDialogueUI.Instance.IsOpen)
        {
            BeachBoatStory.StoryDialogueUI.Instance.Close();
        }
        else if (phase == 4 && !BeachBoatQuestSession.InteractionBlocked)
        {
            supply.BeginActivity();
            if (BeachBoatMinigameStation.Active != supply) { failure = "Could not start supply relay"; Finish(false, drum, supply); return; }
            typeof(BeachBoatMinigameStation).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(supply, supply.TimeLimit + .1f);
            phase = 8;
        }
        else if (phase == 8 && supply.Failed)
        {
            supplyTimedOut = !supply.HasCompleted && story.currentState == Story.StoryState.Act2_SupplyPreparation;
            supply.RetryActivity();
            int wrong = supply.RequestedCargo % 3 + 1;
            wrongCargoRejected = !supply.ChooseCargo(wrong) && supply.Mistakes == 1;
            phase = 5;
        }
        else if (phase == 5)
        {
            var ui=BeachBoatMinigameUI.Instance;
            selectionVisible=ui!=null&&ui.CargoSelectionVisible&&!ui.TravelStatusVisible;
            if(!selectionVisible){failure="Warehouse selection panel missing";Finish(false,drum,supply);return;}
            if (!supply.ChooseCargo(supply.RequestedCargo)) { failure = "Correct cargo rejected"; Finish(false, drum, supply); return; }
            var marker = supply.checkpointMarkers[supply.Progress];
            var sign = marker == null ? null : marker.GetComponent<SupplyDestinationSign>();
            bool labelled = marker != null && marker.activeSelf && sign != null && sign.label != null &&
                sign.label.text.Contains(BeachBoatMinigameStation.CargoName(supply.SelectedCargo));
            if (!labelled) signFailure = $"Trip {supply.Progress}: marker={marker?.name}, active={marker?.activeSelf}, " +
                $"component={sign != null}, label={sign?.label?.text}, cargo={BeachBoatMinigameStation.CargoName(supply.SelectedCargo)}";
            signsLabelled &= labelled;
            phase = 6;
        }
        else if (phase == 6)
        {
            var ui=BeachBoatMinigameUI.Instance;
            travelCompact=ui!=null&&!ui.CargoSelectionVisible&&ui.TravelStatusVisible;
            if(!travelCompact){failure="Large cargo panel remained visible after choosing cargo";Finish(false,drum,supply);return;}
            if(!capturedTravel){CaptureUI(session,"RelayUI_Delivering");capturedTravel=true;}
            int index = supply.Progress;
            Move(session.player, supply.checkpoints[index].position + Vector3.right * 4);
            if (!supply.TryDeliver()) { failure = "Delivery rejected at checkpoint " + index; Finish(false, drum, supply); return; }
            if (supply.HasCompleted) { allDeliveries = supply.Progress == supply.checkpoints.Length;phase=11;return; }
            phase=10;
        }
        else if(phase==10)
        {
            var ui=BeachBoatMinigameUI.Instance;
            returnCompact=ui!=null&&!ui.CargoSelectionVisible&&ui.TravelStatusVisible;
            if(!returnCompact){failure="Return trip reopened the large cargo panel";Finish(false,drum,supply);return;}
            if(!capturedReturn){CaptureUI(session,"RelayUI_Returning");capturedReturn=true;}
            Move(session.player,supply.transform.position+Vector3.right*15);phase=7;
        }
        else if(phase==11)
        {
            var ui=BeachBoatMinigameUI.Instance;closedAfterFinish=ui!=null&&!ui.CargoSelectionVisible&&!ui.TravelStatusVisible;
            if(!closedAfterFinish)failure="Cargo interface remained after completion";
            Finish(timedOut&&supplyTimedOut&&wrongCargoRejected&&allDeliveries&&signsLabelled&&closedAfterFinish,drum,supply);
        }
        else if (phase == 7 && supply.IsSelectingCargo) phase = 5;
    }
    static void CaptureUI(BeachBoatQuestSession session,string name)
    {
        var go=new GameObject("RelayQA_Camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(session.storyCamera);
        camera.transform.SetPositionAndRotation(session.storyCamera.transform.position,session.storyCamera.transform.rotation);camera.enabled=false;
        var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var rt=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);camera.targetTexture=rt;
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=2;}
        Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
        pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",pixels.EncodeToPNG());
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceOverlay;c.worldCamera=null;}
        RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(go);
        Canvas.ForceUpdateCanvases();
    }
}
