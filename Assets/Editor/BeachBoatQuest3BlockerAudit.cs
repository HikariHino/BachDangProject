using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Story = BeachBoatStory.BachDangStoryManager;
using Zone = BeachBoatStory.RiverStakeZone;
using StoryOpening = BeachBoatStory.OpeningEvent;
using StoryDialogue = BeachBoatStory.StoryDialogueUI;

public static class BeachBoatQuest3BlockerAudit
{
    const string Scene = "Assets/Scenes/beachBoat.unity";
    const string Flag = "BeachBoatQuest3BlockerAuditRunning";
    static double started;
    static int phase;

    public static void AuditAndExit()
    {
        EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        var director = UnityEngine.Object.FindFirstObjectByType<EnemyFleetDirector>();
        var rowboat = UnityEngine.Object.FindFirstObjectByType<QuestRowboat>();
        var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var riverDebris = all.FirstOrDefault(t => t.name == "RiverDebris");
        const string RedSailAsset = "Assets/junk/source/Sketchfab_2013_12_04_23_35_26.fbx";
        var redSailInstances = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(r => AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(r.gameObject)) == RedSailAsset)
            .Select(r => PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject))
            .Where(go => go != null).Distinct()
            .Select(go => new { go.name, active = go.activeInHierarchy, position = go.transform.position.ToString("F1"),
                parent = go.transform.parent?.name,
                enemy = go.GetComponentInParent<EnemyShip>(true) != null }).ToArray();
        var shipLike = all.Where(t => t.name.IndexOf("boat", StringComparison.OrdinalIgnoreCase) >= 0 ||
            t.name.IndexOf("thuyen", StringComparison.OrdinalIgnoreCase) >= 0 ||
            t.name.IndexOf("sail", StringComparison.OrdinalIgnoreCase) >= 0)
            .Where(t => t.parent == null || t.GetComponent<AmbientRiverBoat>() != null ||
                t.GetComponent<EnemyShip>() != null || t == rowboat.transform)
            .Select(t => new { t.name, active = t.gameObject.activeSelf, position = t.position.ToString("F1"),
                parent = t.parent == null ? "root" : t.parent.name,
                patrol = t.GetComponent<AmbientRiverBoat>() != null,
                enemy = t.GetComponent<EnemyShip>() != null,
                questBoat = t == rowboat.transform,
                renderers = t.GetComponentsInChildren<Renderer>(true).Length }).ToArray();
        var report = new {
            shipLike, redSailInstances,
            enemies = director.enemyShips.Select(s => new { s.name, position = s.transform.position.ToString("F1"),
                active = s.gameObject.activeSelf }).ToArray(),
            rowboat = new { position = rowboat.transform.position.ToString("F1"), heading = rowboat.transform.eulerAngles.y,
                boarding = rowboat.boardingPoint.position.ToString("F1"), dock = rowboat.dockPoint.position.ToString("F1") },
            debris = riverDebris == null ? null : new { active = riverDebris.gameObject.activeSelf,
                pieces = riverDebris.Cast<Transform>().Select(t => new { t.name, position = t.position.ToString("F1"),
                    mesh = t.GetComponent<MeshFilter>()?.sharedMesh?.name,
                    material = t.GetComponent<Renderer>()?.sharedMaterial?.name }).ToArray() }
        };
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BeachBoatQuest3BlockerAudit.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        EditorApplication.Exit(0);
    }

    public static void CleanLegacySceneAndExit()
    {
        var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        var director = UnityEngine.Object.FindFirstObjectByType<EnemyFleetDirector>();
        var legacy = GameObject.Find("Sketchfab_2013_12_04_23_35_26");
        var debris = GameObject.Find("RiverDebris");
        if (director == null || director.ShipCount != 10 || legacy == null || debris == null ||
            legacy.GetComponentInParent<EnemyShip>(true) != null ||
            director.enemyShips.Any(ship => ship != null &&
                (ship.transform == legacy.transform || ship.transform.IsChildOf(legacy.transform))))
            throw new InvalidOperationException("Legacy ship/debris cleanup failed its safety check");
        UnityEngine.Object.DestroyImmediate(legacy);
        UnityEngine.Object.DestroyImmediate(debris);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorApplication.Exit(0);
    }

    public static void PlaytestAndExit()
    {
        EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        SessionState.SetBool(Flag, true); started = 0; phase = 0;
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Register() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

    static void Tick()
    {
        if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
        if (started == 0) started = EditorApplication.timeSinceStartup;
        double elapsed = EditorApplication.timeSinceStartup - started;
        if (elapsed < 2) return;
        var rowboat = QuestRowboat.Instance;
        var session = BeachBoatQuestSession.Instance;
        var story = Story.Instance;
        var zone = Zone.Instance;
        if (rowboat == null || session == null || story == null || zone == null) return;
        if (phase == 0)
        {
            foreach (var opening in UnityEngine.Object.FindObjectsByType<StoryOpening>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                opening.SkipOpening();
            StoryDialogue.Instance?.Close();
            foreach (var point in zone.placements) point.planted = true;
            foreach (var stake in zone.fieldStakes) if (stake != null) stake.gameObject.SetActive(true);
            typeof(Zone).GetField("<PlantedCount>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(zone, zone.RequiredCount);
            typeof(Zone).GetField("<CompletedClusterCount>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(zone, zone.ClusterCount);
            story.collectedWood = story.totalWoodRequired;
            story.TransitionTo(Story.StoryState.Act3_WaitForLowTide, true);
            session.player.transform.position = rowboat.boardingPoint.position;
            var body = session.player.GetComponent<Rigidbody>();
            body.position = rowboat.boardingPoint.position; body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms(); phase = 1;
            return;
        }
        if (phase == 1 && elapsed > 3)
        {
            if (!rowboat.TryBoard() && elapsed < 7) return;
            var before = rowboat.transform.position;
            var heading = rowboat.transform.rotation;
            var forward = heading * Vector3.forward;
            var hits = new Collider[64];
            int count = Physics.OverlapSphereNonAlloc(before + forward * 26 + Vector3.up * 2, 11, hits,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var blockers = hits.Take(count).Where(h => h != null && !(h is TerrainCollider) &&
                !h.transform.IsChildOf(rowboat.transform) && !h.transform.IsChildOf(session.player.transform))
                .Select(h => new { h.name, parent = h.transform.parent?.name, position = h.transform.position.ToString("F1"),
                    bounds = h.bounds.ToString() }).ToArray();
            bool forwardClear = rowboat.CanOccupy(before + forward * 2, heading);
            bool backwardClear = rowboat.CanOccupy(before - forward * 2, heading);
            var patrol = UnityEngine.Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).First();
            var patrolHome = patrol.transform.position;
            patrol.transform.position = before + forward * 55;
            Physics.SyncTransforms();
            bool shipBlocked = !rowboat.CanOccupy(before + forward * 2, heading);
            patrol.transform.position = patrolHome;
            Physics.SyncTransforms();
            var vitals = session.player.GetComponent<PlayerVitals>();
            for (int i = 0; i < 45; i++) rowboat.Step(1, 0, .05f, true);
            float boostedSpeed = rowboat.Speed;
            float staminaAfterSprint = vitals.Stamina;
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "Rock_TestImpact";
            rock.transform.position = rowboat.transform.position + forward * 100 + Vector3.up * 2;
            rock.transform.localScale = new Vector3(25, 30, 25);
            Physics.SyncTransforms();
            for (int i = 0; i < 45; i++) rowboat.Step(1, 0, .05f, true);
            float travel = QuestRowboat.FlatDistance(before, rowboat.transform.position);
            float healthAfterImpact = vitals.Health;
            string lastImpact = vitals.LastImpact;
            vitals.TakeImpact(vitals.Health, "Test knockout");
            typeof(QuestRowboat).GetField("nextImpactTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(rowboat, 0f);
            typeof(QuestRowboat).GetField("<Speed>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(rowboat, 50f);
            rowboat.Step(1, 0, .05f);
            bool recovered = !rowboat.IsAboard && rowboat.AtDock && vitals.Health >= 60;
            var report = new { boarded = rowboat.IsAboard, state = story.currentState.ToString(),
                blocked = BeachBoatQuestSession.InteractionBlocked, inputBlocked = BeachBoatQuestSession.InputBlocked,
                cursor = Cursor.lockState.ToString(), from = before.ToString("F1"), to = rowboat.transform.position.ToString("F1"),
                speed = rowboat.Speed, boostedSpeed, staminaAfterSprint, healthAfterImpact,
                lastImpact, recovered, recoveredHealth = vitals.Health, travel, forwardClear, backwardClear, shipBlocked, blockers,
                water = TideSystem.CurrentWaterHeight, terrainAtBoat = rowboat.terrain.SampleHeight(before) + rowboat.terrain.transform.position.y,
                heading = rowboat.transform.eulerAngles.y };
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/BeachBoatQuest3RowboatProbe.json", JsonConvert.SerializeObject(report, Formatting.Indented));
            SessionState.SetBool(Flag, false);
            EditorApplication.Exit(travel > 40 && boostedSpeed > rowboat.maxSpeed &&
                staminaAfterSprint < vitals.maxStamina && healthAfterImpact < vitals.maxHealth &&
                lastImpact == "Va vào đá" && shipBlocked && recovered ? 0 : 1);
        }
        if (elapsed > 12) { SessionState.SetBool(Flag, false); EditorApplication.Exit(1); }
    }
}
