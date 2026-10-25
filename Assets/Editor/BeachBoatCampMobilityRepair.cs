using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BeachBoatCampMobilityRepair
{
    [MenuItem("BeachBoat/Repair Camp Guard Walking Routes")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/beachBoat.unity"||scene.isDirty)throw new InvalidOperationException("Open and save beachBoat first.");
        var terrain=Terrain.activeTerrain;float scale=BachDangWorldScale.ForScene(scene);
        var plans=new Dictionary<string,Vector2[]>{
            ["Camp_Guard_Front_Left"]=new[]{new Vector2(-5550,1440),new Vector2(-5550,1500),new Vector2(-5580,1500),new Vector2(-5580,1440)},
            ["Camp_Guard_Back_1"]=new[]{new Vector2(-5470,1475),new Vector2(-5435,1475),new Vector2(-5435,1530),new Vector2(-5470,1530)},
            ["Camp_Guard_Front_Right"]=new[]{new Vector2(-5445,1185),new Vector2(-5445,1260),new Vector2(-5410,1260),new Vector2(-5410,1185)},
            ["Camp_Guard_Mid_Right"]=new[]{new Vector2(-5550,1190),new Vector2(-5550,1280),new Vector2(-5580,1280),new Vector2(-5580,1190)},
            ["Camp_Guard_Back_3"]=new[]{new Vector2(-5523,1562),new Vector2(-5523,1574),new Vector2(-5523,1586),new Vector2(-5523,1574)},
            ["Camp_Guard_Back_2"]=new[]{new Vector2(-5500,1586),new Vector2(-5500,1575),new Vector2(-5500,1565),new Vector2(-5500,1575)}
        };
        var reports=new List<object>();
        foreach(var entry in plans)
        {
            var w=GameObject.Find(entry.Key).GetComponent<NPCWander>();var a=w.GetComponent<Animator>();
            bool raised=entry.Key=="Camp_Guard_Back_3"||entry.Key=="Camp_Guard_Back_2";
            Undo.RecordObject(w.transform,"Move guard into clear patrol lane");Undo.RecordObject(w,"Extend guard patrol");
            if(!raised){var p=w.transform.position;p.y=terrain.SampleHeight(p)+terrain.transform.position.y+3.2f;w.transform.position=p;}
            a.Update(0);Physics.SyncTransforms();
            var nav=new NPCWalkNavigation(w,terrain,scale,raised);
            var points=new List<Vector3>();
            foreach(var xy in entry.Value)
            {
                var p=new Vector3(xy.x,w.transform.position.y,xy.y);
                if(!nav.FindSpawn(p,out p))throw new InvalidOperationException("No clear patrol stop for "+w.name+" at "+xy);
                points.Add(p);
            }
            var route=new List<Vector3>();
            for(int i=1;i<points.Count;i++)if(!nav.Route(points[i-1],points[i],route))throw new InvalidOperationException("Disconnected patrol stops for "+w.name);
            for(int i=0;i<4;i++)
            {
                if(w.waypoints==null||w.waypoints.Length!=4||w.waypoints[i]==null)throw new InvalidOperationException("Expected four authored stops for "+w.name);
                Undo.RecordObject(w.waypoints[i],"Reposition clear guard patrol stop");w.waypoints[i].position=points[i];
            }
            w.transform.position=points[0];w.autoGenerateWaypoints=false;w.followWaypointsInOrder=true;
            w.waypointWaitSeconds=new[]{1.4f,.6f,2f,.6f};
            w.activityLabels=new[]{"Đang quan sát doanh trại","Đang tuần tra","Đang kiểm tra lối đi","Đang đổi ca gác"};
            w.minIdleTime=.3f;w.maxIdleTime=1.8f;
            EditorUtility.SetDirty(w);reports.Add(new{w.name,raised,stops=points.Select(p=>p.ToString("F2"))});
        }
        EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save repaired guard routes.");
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CampMobilityRepair.json",JsonConvert.SerializeObject(reports,Formatting.Indented));
    }
}
