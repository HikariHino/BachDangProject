using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BeachBoatFleetRevision
{
    const string Folder="Assets/BeachBoatStory/WarFleet/";
    public static void AuditAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");Directory.CreateDirectory("Logs");AuditSword();
        var patrols=Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        File.WriteAllText("Logs/PatrolDeckAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(patrols.Select(p=>new{p.name,visualPosition=p.transform.Find("DaiVietPatrolVisual_Rebuilt").localPosition.ToString(),meshes=p.transform.Find("DaiVietPatrolVisual_Rebuilt").GetComponentsInChildren<MeshFilter>(true).Select(f=>new{f.name,bounds=f.sharedMesh.bounds.ToString(),local=p.transform.InverseTransformPoint(f.transform.position).ToString(),scale=f.transform.lossyScale.ToString(),vertices=f.sharedMesh.vertices.Where((v,i)=>i<12).Select(v=>p.transform.InverseTransformPoint(f.transform.TransformPoint(v)).ToString()).ToArray()})}),Newtonsoft.Json.Formatting.Indented));EditorApplication.Exit(0);
    }
    public static void ApplyAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        var template=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"NamHan_Indigo_Command_Junk.prefab");
        var archery=Object.FindAnyObjectByType<RiverPlayerCombat>();
        var guards=new List<EnemyDeckGuard>();
        foreach(var ship in Object.FindObjectsByType<EnemyShip>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var previous=ship.transform.Find("RedSailJunkVisual");Object.DestroyImmediate(previous.gameObject);
            var visual=((GameObject)PrefabUtility.InstantiatePrefab(template)).transform;visual.SetParent(ship.transform,false);visual.name="RedSailJunkVisual";
            ship.hullHalfLength=145;ship.stakeHitDistance=43;
            var hull=ship.transform.Find("Warship_Hull_Collision").GetComponent<BoxCollider>();hull.size=new Vector3(82,26,275);
            var crew=ship.GetComponentsInChildren<EnemyDeckGuard>(true).OrderBy(g=>g.name).ToList();
            while(crew.Count<6){var copy=Object.Instantiate(crew[0].gameObject,ship.transform).GetComponent<EnemyDeckGuard>();copy.name="Xa_Thu_Boong_"+(crew.Count+1);crew.Add(copy);}
            for(int i=0;i<crew.Count;i++)
            {
                var guard=crew[i];guard.ship=ship;
                var p=new Vector3(i%2==0?-28:28,0,52-(i/2)*45);
                p.y=BeachBoatFleetAndStallUpgrade.Deck(visual,ship.transform,p);
                guard.transform.localPosition=p;guard.transform.localRotation=Quaternion.Euler(0,i%2==0?-70:70,0);
                guards.Add(guard);
            }
            foreach(int side in new[]{-1,1})
            {
                var anchor=ship.transform.Find(side<0?"Boarding_Port":"Boarding_Starboard");
                var p=new Vector3(side*21.5f,0,0);p.y=BeachBoatFleetAndStallUpgrade.Deck(visual,ship.transform,p)+.8f;anchor.localPosition=p;
            }
        }
        archery.guards=guards.ToArray();
        var ally=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"DaiViet_Boarding_Soldier.prefab");
        var neutral=Resources.Load<RuntimeAnimatorController>("Player_Neutral");
        foreach(var patrol in Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var old=patrol.transform.Find("Quan_Si_Tren_Boong");if(old!=null)Object.DestroyImmediate(old.gameObject);
            var root=new GameObject("Quan_Si_Tren_Boong").transform;root.SetParent(patrol.transform,false);
            var manifest=root.gameObject.AddComponent<FleetDeckCrew>();var crew=new List<Transform>();
            var visual=patrol.transform.Find("DaiVietPatrolVisual_Rebuilt");
            // The native junk's long axis can be X. Fit all stations to its actual deck triangles.
            var q=patrol.transform.rotation;patrol.transform.rotation=Quaternion.identity;var bounds=BeachBoatWorldAudit.BoundsOf(visual);patrol.transform.rotation=q;
            bool alongX=bounds.size.x>bounds.size.z;
            for(int i=0;i<6;i++)
            {
                var p=alongX?new Vector3((i/2-1)*38,0,(i%2==0?-1:1)*10):new Vector3((i%2==0?-1:1)*10,0,(i/2-1)*38);
                p+=new Vector3(bounds.center.x-patrol.transform.position.x,0,bounds.center.z-patrol.transform.position.z);
                var parentScale=patrol.transform.lossyScale;
                p=Vector3.Scale(p,new Vector3(1/parentScale.x,1/parentScale.y,1/parentScale.z));
                p.y=BeachBoatFleetAndStallUpgrade.Deck(visual,patrol.transform,p,-100);
                var soldier=Object.Instantiate(ally,root).transform;soldier.name="Linh_Tuan_Tra_"+(i+1);soldier.localPosition=p;soldier.localRotation=Quaternion.Euler(0,patrol.visualForwardYaw,0);
                soldier.localScale=Vector3.Scale(ally.transform.localScale,new Vector3(1/parentScale.x,1/parentScale.y,1/parentScale.z));
                var animator=soldier.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=neutral;animator.applyRootMotion=false;crew.Add(soldier);
            }
            manifest.soldiers=crew.ToArray();
        }
        ExtendStakes();
        AuditSword();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
    static Bounds BoundsOf(Transform t)=>BeachBoatWorldAudit.BoundsOf(t);
    static void ExtendStakes()
    {
        var zone=Object.FindAnyObjectByType<BeachBoatStory.RiverStakeZone>();
        var tide=Object.FindAnyObjectByType<TideSystem>();var terrain=Terrain.activeTerrain;
        var stakes=zone.fieldStakes.ToList();int original=stakes.Count;
        if(stakes.Any(s=>s.name.StartsWith("CocGo_DaiRong_")))return;
        // Continue five long barriers across the enemy lanes. Keep a broad navigable west corridor.
        var random=new System.Random(938);
        for(int band=0;band<5;band++)for(int row=0;row<3;row++)for(int col=0;col<17;col++)
        {
            var p=new Vector3(-1670+col*29+(row%2)*12,0,1330+band*320+row*32+(float)random.NextDouble()*8);
            if(zone.placements.Any(a=>QuestRowboat.FlatDistance(a.approach.position,p)<105)||
                stakes.Any(s=>QuestRowboat.FlatDistance(BoundsOf(s).center,p)<20))continue;
            float ground=terrain.SampleHeight(p)+terrain.transform.position.y;if(ground>tide.lowTideY-6)continue;
            var stake=Object.Instantiate(stakes[0].gameObject,stakes[0].parent).transform;
            stake.name="CocGo_DaiRong_"+(stakes.Count-original+1).ToString("000");stake.gameObject.SetActive(true);
            var b=BoundsOf(stake);p.y=b.center.y;stake.position+=p-b.center;
            float bottom=ground-8,top=Mathf.Lerp(tide.lowTideY,tide.highTideY,.50f+(stakes.Count%7)*.02f);
            b=BoundsOf(stake);stake.localScale*=(top-bottom)/b.size.y;b=BoundsOf(stake);stake.position+=Vector3.up*(bottom-b.min.y);
            stake.gameObject.SetActive(false);stakes.Add(stake);
        }
        zone.fieldStakes=stakes.ToArray();
        // Assign by contiguous river bands, keeping the five existing boat approaches.
        var indices=Enumerable.Range(0,stakes.Count).OrderBy(i=>BoundsOf(stakes[i]).center.z).ToArray();int offset=0;
        for(int i=0;i<zone.placements.Length;i++)
        {
            var point=zone.placements[i];int count=stakes.Count/5+(i<stakes.Count%5?1:0);
            point.stakeIndices=indices.Skip(offset).Take(count).ToArray();offset+=count;point.stake=stakes[point.stakeIndices[0]];
            point.marker.GetComponentInChildren<TMPro.TMP_Text>(true).text=$"CỤM {i+1} • {count} CỌC";
        }
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/FleetRevisionStakes.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{original,total=stakes.Count,added=stakes.Count-original},Newtonsoft.Json.Formatting.Indented));
    }
    static void AuditSword()
    {
        var sword=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/pf_sword_01.prefab");var mesh=sword.GetComponent<MeshFilter>().sharedMesh;var b=mesh.bounds;
        var bins=Enumerable.Range(0,16).Select(i=>new{y=b.min.y+b.size.y*(i+.5f)/16,span=mesh.vertices.Where(v=>v.y>=b.min.y+b.size.y*i/16&&v.y<=b.min.y+b.size.y*(i+1)/16).Select(v=>Mathf.Abs(v.x-b.center.x)).DefaultIfEmpty(0).Max()*2}).ToArray();
        File.WriteAllText("Logs/SwordMeshAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{bounds=b.ToString("F6"),bins},Newtonsoft.Json.Formatting.Indented));
    }
}
