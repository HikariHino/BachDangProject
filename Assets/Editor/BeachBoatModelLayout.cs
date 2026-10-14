using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Keep enlarged roofs apart and retain their attached yards on dry ground.</summary>
public static class BeachBoatModelLayout
{
    [MenuItem("Bach Dang/Clear Trees Inside Fitted Houses")]
    public static void ClearTrees()
    {
        if(Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/beachBoat.unity" || GameObject.Find("BeachBoat_Models_Fitted_2x")==null)throw new Exception("Requires fitted beachBoat in Edit Mode");
        var terrain=Terrain.activeTerrain;var data=terrain.terrainData;
        var houses=GameObject.Find("BachDang_Living_Settlements").GetComponentsInChildren<Transform>(true)
            .Where(t=>t.name.StartsWith("Nha_Dan_")||t.name.StartsWith("Lan_Quan_")||t.name.StartsWith("Leu_Linh_")||t.name.StartsWith("Meshy_AI_Nomad"))
            .Select(t=>{var b=t.GetComponentsInChildren<Renderer>(true)[0].bounds;b.Expand(new Vector3(20,0,20));return b;}).ToArray();
        var old=data.treeInstances;
        var keep=old.Where(t=>{var p=terrain.transform.position+Vector3.Scale(t.position,data.size);return !houses.Any(b=>p.x>b.min.x&&p.x<b.max.x&&p.z>b.min.z&&p.z<b.max.z);}).ToArray();
        if(keep.Length==old.Length)return;
        const string backup=".utmp/ModelFit/terrain-before-tree-clear.asset";
        System.IO.Directory.CreateDirectory(".utmp/ModelFit");
        if(!System.IO.File.Exists(backup))System.IO.File.Copy(AssetDatabase.GetAssetPath(data),backup);
        Undo.RecordObject(data,"Clear trees inside enlarged houses");data.treeInstances=keep;EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();terrain.Flush();
        Debug.Log("Cleared "+(old.Length-keep.Length)+" trees intersecting enlarged roofs. Heightmap and splatmaps untouched.");
    }
    sealed class House
    {
        public Transform t; public Bounds b; public Vector3 position;
    }
    static bool Overlap(Bounds a, Bounds b, float gap) =>
        Mathf.Abs(a.center.x-b.center.x) < a.extents.x+b.extents.x+gap &&
        Mathf.Abs(a.center.z-b.center.z) < a.extents.z+b.extents.z+gap;
    static float Segment(Vector3 p, Vector3 a, Vector3 b)
    {
        p.y=a.y=b.y=0; var d=b-a;
        return Vector3.Distance(p,a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(1,d.sqrMagnitude)));
    }
    [MenuItem("Bach Dang/Separate Fitted Houses")]
    public static void Apply()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(Application.isPlaying || scene.path!="Assets/Scenes/beachBoat.unity" || GameObject.Find("BeachBoat_Models_Fitted_2x")==null)throw new Exception("Fit beachBoat models in Edit Mode first");
        System.IO.Directory.CreateDirectory(".utmp/ModelFit");
        var settlement=GameObject.Find("BachDang_Living_Settlements").transform;
        var houses=settlement.GetComponentsInChildren<Transform>(true)
            .Where(t=>t.name.StartsWith("Nha_Dan_")||t.name.StartsWith("Lan_Quan_")||t.name.StartsWith("Leu_Linh_")||t.name.StartsWith("Meshy_AI_Nomad"))
            .Select(t=>new House{t=t,b=t.GetComponentsInChildren<Renderer>(true)[0].bounds,position=t.position}).ToArray();
        var terrain=Terrain.activeTerrain; float high=UnityEngine.Object.FindFirstObjectByType<TideSystem>().highTideY;
        Func<Vector3,float> floor=p=>terrain.SampleHeight(p)+terrain.transform.position.y;
        var session=UnityEngine.Object.FindFirstObjectByType<BeachBoatStory.BeachBoatQuestSession>();
        var anchors=session.GetComponentsInChildren<Transform>(true).Where(t=>t.name.EndsWith("Arrival")||t.GetComponent<BeachBoatStory.CampGateTransition>()!=null||t.GetComponent<BeachBoatStory.NPCDialogue>()!=null||t.GetComponent<BeachBoatStory.WoodCollectible>()!=null).Select(t=>t.position).ToArray();
        var spawn=session.playerSpawn.position; var gate=session.transform.Find("CampToVillage").position;
        var yards=settlement.Find("11_San_Nha_Va_Cho").GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("San_Nha_")).ToArray();
        var fixedBlocks=settlement.GetComponentsInChildren<Renderer>(true).Where(r=>r.bounds.size.y>18 && r.bounds.size.x>12 && r.bounds.size.z>12 && !houses.Any(h=>r.transform.IsChildOf(h.t)) && !yards.Any(t=>r.transform.IsChildOf(t)))
            .Where(r=>!r.name.StartsWith("Laptop_LOD") && !r.name.Contains("HangRao") && !r.name.StartsWith("SharedFence"))
            .Select(r=>r.bounds).ToArray();
        var fence=GameObject.Find("Island_Fence_Collisions").GetComponentsInChildren<Collider>().Select(c=>c.bounds).ToArray();
        var occupied=new List<Bounds>(); var moves=new List<object>();
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();
        try
        {
            foreach(var h in houses.OrderByDescending(h=>h.b.size.x*h.b.size.z))
            {
                bool camp=h.t.IsChildOf(GameObject.Find("TestQuest_Camp").transform);
                Vector3 chosen=Vector3.zero; bool found=false;
                for(int ring=0;ring<=24&&!found;ring++)
                {
                    int count=ring==0?1:Mathf.Max(12,ring*8);
                    for(int i=0;i<count&&!found;i++)
                    {
                        float angle=i*Mathf.PI*2/count;
                        var delta=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(ring*18);
                        var b=h.b;b.center+=delta;
                        if(occupied.Any(o=>Overlap(b,o,14))||fixedBlocks.Any(o=>Overlap(b,o,5)))continue;
                        if(anchors.Any(a=>Mathf.Abs(a.x-b.center.x)<b.extents.x+22 && Mathf.Abs(a.z-b.center.z)<b.extents.z+22))continue;
                        if(camp && (Segment(b.center,spawn,gate)<Mathf.Max(b.extents.x,b.extents.z)+25 || fence.Any(o=>Overlap(b,o,12))))continue;
                        var corners=new[]{b.min,b.max,new Vector3(b.min.x,0,b.max.z),new Vector3(b.max.x,0,b.min.z)};
                        if(corners.Any(p=>floor(p)<high+8) || corners.Max(p=>floor(p))-corners.Min(p=>floor(p))>7)continue;
                        chosen=delta;occupied.Add(b);found=true;
                    }
                }
                if(!found)throw new Exception("No clear dry plot for "+h.t.name);
                chosen.y=floor(h.position+chosen)-floor(h.position);
                if(chosen.sqrMagnitude<.01f)continue;
                var attached=new List<Transform>{h.t};
                if(h.t.name.StartsWith("Nha_Dan_"))
                {
                    string village=h.t.parent.name;
                    var yard=settlement.Find("11_San_Nha_Va_Cho/"+village+"/San_"+h.t.name); if(yard!=null)attached.Add(yard);
                    var nature=settlement.Find("10_Lang_Tu_Nhien/"+village+"_Canh_Quan");
                    foreach(string prefix in new[]{"San_Truoc_","Loi_Cua_","Rao_San_"}){var t=nature?.Find(prefix+h.t.name);if(t!=null)attached.Add(t);}
                }
                foreach(var t in attached){Undo.RecordObject(t,"Separate enlarged building");t.position+=chosen;if(PrefabUtility.IsPartOfPrefabInstance(t))PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
                moves.Add(new{path=AnimationUtility.CalculateTransformPath(h.t,null),offset=chosen.ToString()});
            }
            Physics.SyncTransforms();
            System.IO.File.WriteAllText(".utmp/ModelFit/layout-moves.json",Newtonsoft.Json.JsonConvert.SerializeObject(moves,Newtonsoft.Json.Formatting.Indented));
            var sky=UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();if(sky!=null)RenderSettings.skybox=sky.skyboxMaterial;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(undo);
            Debug.Log("Separated "+houses.Length+" houses/tents; moved "+moves.Count+" plots.");
        }
        catch{Undo.RevertAllDownToGroup(undo);throw;}
    }
}
