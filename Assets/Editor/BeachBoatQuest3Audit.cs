using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BeachBoatQuest3Audit
{
    public static void AuditAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        var director = UnityEngine.Object.FindFirstObjectByType<EnemyFleetDirector>();
        var zone = UnityEngine.Object.FindFirstObjectByType<BeachBoatStory.RiverStakeZone>();
        var boat = UnityEngine.Object.FindFirstObjectByType<QuestRowboat>();
        var terrain = Terrain.activeTerrain;
        var tide = UnityEngine.Object.FindFirstObjectByType<TideSystem>();
        if (director == null || zone == null || boat == null || terrain == null || tide == null)
            throw new InvalidOperationException("Quest 3 scene bindings missing");
        var targets = director.enemyShips.Select(s => s == null ? null : new {
            name = s.name, active = s.gameObject.activeSelf,
            spawn = s.spawnPoint.ToString("F1"), target = s.targetPoint?.position.ToString("F1"),
            speed = s.moveSpeed
        }).ToArray();
        var channel = Enumerable.Range(0, 15).Select(zi => {
            float z = 1200 + zi * 200;
            return new { z, heights = Enumerable.Range(0, 10).Select(xi => {
                float x = -2400 + xi * 120;
                return new { x, y = (float)Math.Round(terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y, 1) };
            }).ToArray() };
        }).ToArray();
        var playerWater = Enumerable.Range(0, 7).Select(zi => {
            float z = 1500 + zi * 250;
            return new { z, depths = Enumerable.Range(0, 8).Select(xi => {
                float x = -3500 + xi * 170;
                return new { x, depth = (float)Math.Round(tide.lowTideY -
                    (terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y), 1) };
            }).ToArray() };
        }).ToArray();
        var report = new {
            scene = "Assets/Scenes/beachBoat.unity",
            terrain = new { position = terrain.transform.position.ToString("F1"), size = terrain.terrainData.size.ToString("F1") },
            tide = new { low = tide.lowTideY, high = tide.highTideY },
            boat = boat.transform.position.ToString("F1"),
            clusters = zone.placements.Select((p, i) => new { index = i, approach = p.approach?.position.ToString("F1"), stake = p.stake?.position.ToString("F1") }).ToArray(),
            ships = targets,
            channel,
            playerWater,
            friendly = UnityEngine.Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Select(b => new { name = b.name, position = b.transform.position.ToString("F1") }).ToArray()
        };
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BeachBoatQuest3Audit.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        EditorApplication.Exit(0);
    }
}
