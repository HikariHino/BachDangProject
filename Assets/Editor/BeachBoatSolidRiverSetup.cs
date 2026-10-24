using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using BeachBoatStoryEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BeachBoatSolidRiverSetup
{
    public static void RebuildEnemyApproachesAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");BeachBoatToolSafety.Begin("DistinctSolidEnemySpawns");RiverWaterNavigation.Reset();RiverWaterNavigation.ReserveInvasionApproach=false;
        var fleet=Object.FindAnyObjectByType<EnemyFleetDirector>();var enemies=fleet.enemyShips;var tide=Object.FindAnyObjectByType<TideSystem>();
        var zone=Object.FindAnyObjectByType<BeachBoatStory.RiverStakeZone>();
        var lanes=fleet.transform.Find("Battle_Lanes_938");if(lanes!=null)Object.DestroyImmediate(lanes.gameObject);
        TideSystem.CurrentWaterHeight=tide.highTideY;RiverFleetLayout.Configure(fleet,zone);
        float north=zone.fieldStakes.Max(s=>s.GetComponentInChildren<Renderer>(true).bounds.max.z);
        foreach(var enemy in enemies)enemy.gameObject.SetActive(false);
        var previous=GameObject.Find("NamHan_Approaches_Solid_938");if(previous!=null)Object.DestroyImmediate(previous);
        var root=new GameObject("NamHan_Approaches_Solid_938");var used=new List<Vector3>();var report=new List<object>();fleet.spawnPoints=new Transform[enemies.Length];
        for(int i=0;i<enemies.Length;i++)
        {
            var ship=enemies[i];var hull=BoatHullCollision.Get(ship.transform);
            var requested=new Vector3(ship.targetPoint.position.x,tide.highTideY,north+650);
            if(!RiverWaterNavigation.NearestClear(requested,hull.Radius,out var spawn))throw new Exception("No enemy spawn: "+ship.name);
            spawn.y=tide.highTideY;used.Add(spawn);ship.spawnPoint=spawn;ship.transform.position=spawn;
            var entry=new Vector3(ship.targetPoint.position.x,spawn.y,Mathf.Min(spawn.z,north+260));
            var route=new List<Vector3>();if(!RiverWaterNavigation.FindPath(spawn,entry,hull.Radius,route))throw new Exception("No connected invasion approach: "+ship.name);
            ship.routeWaypoints=route.Select((p,n)=>{var t=new GameObject(ship.name+"_Approach_"+n).transform;t.SetParent(root.transform,false);p.y=spawn.y;t.position=p;return t;}).ToArray();
            var marker=new GameObject(ship.name+"_Spawn").transform;marker.SetParent(root.transform,false);marker.position=spawn;fleet.spawnPoints[i]=marker;
            if(route.Count>1){var d=route[1]-route[0];d.y=0;ship.transform.rotation=Quaternion.LookRotation(d);}
            EditorUtility.SetDirty(ship);report.Add(new{ship.name,spawn=spawn.ToString(),entry=entry.ToString(),points=route.Count});
        }
        EditorUtility.SetDirty(fleet);BeachBoatToolSafety.Save(scene);File.WriteAllText("Logs/SolidEnemyApproaches.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));EditorApplication.Exit(0);
    }
    public static void RefreshSolidsAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");BeachBoatToolSafety.Begin("RestoreSolidProps");
        foreach(var hull in Object.FindObjectsByType<BoatHullCollision>(FindObjectsInactive.Include,FindObjectsSortMode.None))hull.Initialize();
        int restored=0,added=0,treePrototypes=0;
        foreach(var terrain in Terrain.activeTerrains)
        {
            foreach(var prototype in terrain.terrainData.treePrototypes)
            {
                if(prototype.prefab==null)continue;var path=AssetDatabase.GetAssetPath(prototype.prefab);if(!path.StartsWith("Assets/"))continue;
                var prefab=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if(prefab.GetComponentsInChildren<Collider>(true).Any(c=>!c.isTrigger&&c.enabled))continue;
                    var renderers=prefab.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)continue;var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
                    var scale=prefab.transform.lossyScale;bool rock=(path+prefab.name).ToLowerInvariant().Contains("rock");
                    var source=PrefabUtility.GetCorrespondingObjectFromOriginalSource(prototype.prefab);if(source!=null)rock|=AssetDatabase.GetAssetPath(source).ToLowerInvariant().Contains("rock");
                    if(rock)
                    {var box=prefab.AddComponent<BoxCollider>();box.center=prefab.transform.InverseTransformPoint(b.center);box.size=Vector3.Scale(b.size,new Vector3(1/Mathf.Abs(scale.x),1/Mathf.Abs(scale.y),1/Mathf.Abs(scale.z)));}
                    else
                    {var capsule=prefab.AddComponent<CapsuleCollider>();float height=b.size.y*.65f,radius=Mathf.Min(b.size.x,b.size.z)*.045f;capsule.height=height/Mathf.Abs(scale.y);capsule.radius=Mathf.Max(radius,b.size.y*.012f)/Mathf.Abs(scale.x);capsule.center=prefab.transform.InverseTransformPoint(new Vector3(prefab.transform.position.x,b.min.y+height*.5f,prefab.transform.position.z));}
                    PrefabUtility.SaveAsPrefabAsset(prefab,path);treePrototypes++;
                }
                finally{PrefabUtility.UnloadPrefabContents(prefab);}
            }
            terrain.terrainData.RefreshPrototypes();var collider=terrain.GetComponent<TerrainCollider>();if(collider!=null){var serialized=new SerializedObject(collider);serialized.FindProperty("m_EnableTreeColliders").boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(collider);}
        }
        var sceneMeshes=Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        foreach(var mesh in sceneMeshes)
        {
            if(mesh.sharedMesh==null||mesh.GetComponentInParent<Animator>()!=null||mesh.GetComponentInParent<BoatHullCollision>()!=null||mesh.GetComponent<TideSystem>()!=null)continue;
            string names="";for(var t=mesh.transform;t!=null;t=t.parent)names+="/"+t.name.ToLowerInvariant();
            bool substantial=new[]{"rock","stone","boulder","house","hut","tent","stall","barrel","crate","fence","wall","gate","bridge","dock","woodpile","cart","basket","nha_","leu_","sap_","thung_","cocgo","coc_go","tree","pine","mangrove","cay_"}.Any(names.Contains);
            if(!substantial||mesh.GetComponentInParent<ParticleSystem>()!=null)continue;
            bool existing=false;
            for(var t=mesh.transform;t!=null;t=t.parent)
            {
                foreach(var c in t.GetComponents<Collider>())
                    if(!c.isTrigger){existing=true;if(!c.enabled){c.enabled=true;restored++;EditorUtility.SetDirty(c);}}
                if(existing)break;
            }
            if(existing)continue;
            var collider=mesh.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;collider.convex=false;collider.isTrigger=false;added++;
        }
        BeachBoatToolSafety.Save(scene);File.WriteAllText("Logs/SolidSceneryRestoration.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{restored,added,treePrototypes}));EditorApplication.Exit(0);
    }
    public static void ApplyAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");BeachBoatToolSafety.Begin("SolidRiverRoutes");RiverWaterNavigation.Reset();RiverWaterNavigation.ReserveInvasionApproach=true;
        var allies=Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsSortMode.None).OrderBy(b=>b.name).ToArray();
        var enemies=Object.FindObjectsByType<EnemyShip>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        foreach(var enemy in enemies)BoatHullCollision.Get(enemy.transform).Initialize();
        foreach(var ally in allies){BoatHullCollision.Get(ally.transform).Initialize();ally.gameObject.SetActive(false);}
        var previous=GameObject.Find("Tuyen_An_Toan_Ngoai_Bai_Coc");if(previous!=null)Object.DestroyImmediate(previous);
        var root=new GameObject("Tuyen_An_Toan_Ngoai_Bai_Coc");var used=new List<Vector3>();var report=new List<object>();
        foreach(var ally in allies)
        {
            var hull=BoatHullCollision.Get(ally.transform);var wanted=ally.transform.position;
            if(!RiverWaterNavigation.NearestClear(wanted,hull.Radius,out var spawn,used,hull.Radius*2+40))throw new Exception("No clear ship spawn: "+ally.name);
            spawn.y=wanted.y;ally.transform.position=spawn;ally.gameObject.SetActive(true);Physics.SyncTransforms();
            if(!hull.CanOccupy(spawn,ally.transform.rotation))throw new Exception("Solid hull spawn collides: "+ally.name+" at "+spawn+" radius "+hull.Radius);
            used.Add(spawn);
            var goals=new List<Vector3>{spawn};var scratch=new List<Vector3>();
            for(int angle=0;angle<360&&goals.Count<4;angle+=45)
            {
                var requested=spawn+Quaternion.Euler(0,angle,0)*Vector3.forward*800;
                if(!RiverWaterNavigation.NearestClear(requested,hull.Radius,out var point)||goals.Any(g=>QuestRowboat.FlatDistance(g,point)<300))continue;
                if(!RiverWaterNavigation.FindPath(goals[goals.Count-1],point,hull.Radius,scratch))continue;goals.Add(point);
            }
            if(goals.Count<2)throw new Exception("No broad connected route: "+ally.name);
            ally.route=goals.Select((p,i)=>{var t=new GameObject(ally.name+"_"+i).transform;t.SetParent(root.transform,false);p.y=spawn.y;t.position=p;return t;}).ToArray();
            report.Add(new{ally.name,radius=hull.Radius,size=hull.SolidHull.bounds.size.ToString(),position=spawn.ToString(),route=goals.Select(p=>p.ToString())});
            EditorUtility.SetDirty(ally);
        }
        BeachBoatToolSafety.Save(scene);File.WriteAllText("Logs/SolidRiverLayout.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));EditorApplication.Exit(0);
    }
}
