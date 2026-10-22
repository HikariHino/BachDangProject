using System.IO;
using System.Linq;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BeachBoatSpawnAudit
{
    public static void FixAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        var terrain=Terrain.activeTerrain;
        var gates=Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var gate=gates.First(g=>g.gateType==BeachBoatStory.CampGateTransition.GateType.RiverGate);
        var guard=gate.transform.Find("Riverside_Guard_Post");
        var arrival=gates.First(g=>g.gateType==BeachBoatStory.CampGateTransition.GateType.CampRiverGate).destinationTarget;
        var point=gate.transform.position+guard.forward*30;point.y=terrain.SampleHeight(point)+terrain.transform.position.y+3.2f;
        arrival.SetPositionAndRotation(point,Quaternion.LookRotation(guard.forward));
        var session=Object.FindAnyObjectByType<BeachBoatQuestSession>();Physics.SyncTransforms();
        if(!PlayerArrival.IsClear(session.player,point,arrival.rotation)||!PlayerArrival.HasExit(session.player,point,arrival.rotation))throw new System.Exception("Repaired river arrival is obstructed");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
    public static void RunAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        Physics.SyncTransforms();
        var session=Object.FindAnyObjectByType<BeachBoatQuestSession>();
        var story=Object.FindAnyObjectByType<BeachBoatStory.BachDangStoryManager>();
        var capsule=session.player.GetComponent<CapsuleCollider>();
        var points=new[]{session.playerSpawn,story.campSpawnPoint,story.villageSpawnPoint}
            .Concat(Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(g=>g.destinationTarget)).Where(t=>t!=null).Distinct();
        var entries=points.Select(t=>new {name=t.name,position=t.position.ToString("F2"),
            nearby=Physics.OverlapSphere(t.position,30,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                .Where(c=>!(c is TerrainCollider)&&!c.transform.IsChildOf(session.player.transform))
                .Select(c=>new{name=c.name,parent=c.transform.parent?.name,bounds=c.bounds.ToString()}).ToArray()}).ToArray();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/SpawnAudit.json",JsonConvert.SerializeObject(new{player=session.player.name,scale=session.player.transform.lossyScale.ToString(),center=capsule.center.ToString(),capsule.radius,capsule.height,entries},Formatting.Indented));
        EditorApplication.Exit(0);
    }
}
