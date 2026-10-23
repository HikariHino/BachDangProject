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

public static class BeachBoatCampaignRevisionSetup
{
    public static void ApplyAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        BeachBoatToolSafety.Begin("ExpandedAlliedFleet");
        var combat=Object.FindAnyObjectByType<RiverBoardingCombat>();
        var boats=Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsSortMode.None).OrderBy(b=>b.name).ToList();
        var original=boats.ToArray();
        while(boats.Count<10)
        {
            var source=original[(boats.Count-original.Length)%original.Length];
            var copy=Object.Instantiate(source,source.transform.parent);copy.name="Thuyen_Tiep_Vien_Dai_Viet_"+(boats.Count+1).ToString("00");boats.Add(copy);
        }
        var tide=Object.FindAnyObjectByType<TideSystem>();var terrain=Terrain.activeTerrain;
        var root=GameObject.Find("Tuyen_Tuan_Tra_Mo_Rong_938");if(root!=null)Object.DestroyImmediate(root);
        root=new GameObject("Tuyen_Tuan_Tra_Mo_Rong_938");
        var starts=new List<Vector3>();
        foreach(var boat in boats)
        {
            Vector3[] selected=null;float score=float.PositiveInfinity;Vector3 origin=boat.transform.position;
            // Inspect complete long routes, including the turning clearance, at low tide.
            for(int x=-7;x<=7;x++)for(int z=-7;z<=7;z++)for(int angle=0;angle<180;angle+=30)
            {
                var center=origin+new Vector3(x*140,0,z*140);var rotation=Quaternion.Euler(0,angle,0);
                var points=new[]{new Vector3(-360,0,-135),new Vector3(360,0,-135),new Vector3(360,0,135),new Vector3(-360,0,135)}.Select(v=>center+rotation*v).ToArray();
                if(starts.Any(p=>QuestRowboat.FlatDistance(p,points[0])<280))continue;
                float value=(points[0]-origin).sqrMagnitude;if(value>=score)continue;
                bool clear=true;
                for(int i=0;i<4&&clear;i++)for(int sample=0;sample<=24&&clear;sample++)
                {
                    var at=Vector3.Lerp(points[i],points[(i+1)%4],sample/24f);
                    foreach(var offset in new[]{Vector3.zero,Vector3.left*110,Vector3.right*110,Vector3.forward*110,Vector3.back*110})
                    {
                        var p=at+offset;var local=p-terrain.transform.position;var size=terrain.terrainData.size;
                        if(local.x<0||local.z<0||local.x>size.x||local.z>size.z||terrain.SampleHeight(p)+terrain.transform.position.y>tide.lowTideY-18){clear=false;break;}
                    }
                }
                if(clear){score=value;selected=points;}
            }
            if(selected==null)throw new Exception("No broad deep-water route for "+boat.name);
            starts.Add(selected[0]);boat.route=selected.Select((p,i)=>{var t=new GameObject(boat.name+"_"+i).transform;t.SetParent(root.transform,false);p.y=boat.transform.position.y;t.position=p;return t;}).ToArray();
            boat.transform.position=boat.route[0].position;
            boat.transform.rotation=Quaternion.LookRotation(selected[1]-selected[0])*Quaternion.Euler(0,-boat.visualForwardYaw,0);
            boat.cruiseSpeed=24;boat.turnSpeed=32;boat.pauseAtPoint=.6f;
            EditorUtility.SetDirty(boat);
        }
        combat.friendlyBoats=boats.ToArray();EditorUtility.SetDirty(combat);
        var choreography=Object.FindAnyObjectByType<RiverBattleChoreography>();
        choreography.friendlyBoats=combat.friendlyBoats;EditorUtility.SetDirty(choreography);
        BeachBoatToolSafety.Save(scene);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ExpandedFleetLayout.json",Newtonsoft.Json.JsonConvert.SerializeObject(boats.Select(b=>new{b.name,start=b.transform.position.ToString(),route=b.route.Select(p=>p.position.ToString()),span=Vector3.Distance(b.route[0].position,b.route[2].position)}),Newtonsoft.Json.Formatting.Indented));
        EditorApplication.Exit(0);
    }
    public static void UpdateFleetRegistryAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        var combat=Object.FindAnyObjectByType<RiverBoardingCombat>();
        var choreography=Object.FindAnyObjectByType<RiverBattleChoreography>();
        choreography.friendlyBoats=combat.friendlyBoats;EditorUtility.SetDirty(choreography);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorApplication.Exit(0);
    }
}
