using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Checks actual capsule clearance, layout footprints and renders the authored camp.</summary>
public static class BeachBoatCampRedesignQA
{
    public static void InspectAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity",OpenSceneMode.Single);
        var camp=GameObject.Find("TestQuest_Camp").transform;var origin=camp.Find("CampLayoutOrigin").position;
        var root=camp.Find("Compact_Living_Camp");var terrain=Terrain.activeTerrain;
        var session=UnityEngine.Object.FindFirstObjectByType<BeachBoatStory.BeachBoatQuestSession>();
        var tents=root.Find("Barracks_Rows").Cast<Transform>().OrderBy(t=>t.name).ToArray();
        var capsule=session.player.GetComponent<CapsuleCollider>();
        float radius=capsule.radius*Mathf.Max(capsule.transform.lossyScale.x,capsule.transform.lossyScale.z);
        float height=Mathf.Max(radius*2,capsule.height*capsule.transform.lossyScale.y);
        Physics.SyncTransforms();var doorBlocks=new List<object>();var pathBlocks=new List<object>();
        foreach(var t in tents)
        {
            var from=t.TransformPoint(new Vector3(0,0,37));var to=t.TransformPoint(new Vector3(0,0,5));var d=to-from;
            foreach(var h in Physics.CapsuleCastAll(from+Vector3.up*(radius+1),from+Vector3.up*(height-radius+1),radius,d.normalized,d.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                if(h.transform.IsChildOf(t))doorBlocks.Add(new{tent=t.name,obstacle=h.collider.name,h.distance});
        }
        var roads=new[]{(new Vector2(-320,150),new Vector2(0,150)),(new Vector2(0,-250),new Vector2(0,325))};
        foreach(var road in roads)
        for(int i=0;i<=50;i++)
        {
            var at=Vector2.Lerp(road.Item1,road.Item2,i/50f);var p=origin+new Vector3(at.x,0,at.y);p.y=terrain.SampleHeight(p)+terrain.transform.position.y;
            foreach(var c in Physics.OverlapCapsule(p+Vector3.up*(radius+1),p+Vector3.up*(height-radius+1),radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                if(!(c is TerrainCollider)&&c.GetComponentInParent<Animator>()==null)
                    pathBlocks.Add(new{at=at.ToString(),c.name,parent=c.transform.parent?.name});
        }
        Bounds Bounds(Transform t){var rr=t.GetComponentsInChildren<Renderer>().Where(r=>r.GetComponent<TMPro.TMP_Text>()==null).ToArray();var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;}
        var buildings=tents.Concat(root.Find("Supply_And_Work").Cast<Transform>().Where(t=>t.GetComponentsInChildren<Renderer>().Length>0))
            .Concat(new[]{camp.Cast<Transform>().First(t=>t.name.Contains("Rustic_Wooden_Stage"))}).ToArray();
        var overlaps=new List<object>();
        for(int i=0;i<buildings.Length;i++)for(int j=i+1;j<buildings.Length;j++)
        {
            var a=Bounds(buildings[i]);var b=Bounds(buildings[j]);
            float x=Mathf.Min(a.max.x,b.max.x)-Mathf.Max(a.min.x,b.min.x),z=Mathf.Min(a.max.z,b.max.z)-Mathf.Max(a.min.z,b.min.z);
            if(x>2&&z>2)overlaps.Add(new{a=buildings[i].name,b=buildings[j].name,x,z});
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CampRedesignQA.json",JsonConvert.SerializeObject(new{tents=tents.Length,radius,height,doorBlocks,pathBlocks,overlaps,
            yardProps=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>Mathf.Abs(r.bounds.center.x-origin.x)<70&&r.bounds.center.z>origin.z-55&&r.bounds.center.z<origin.z+255&&r.bounds.size.y<20)
                .Select(r=>new{r.name,parent=r.transform.parent?.name,position=r.transform.position.ToString("F1"),bounds=r.bounds.ToString(),mesh=AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>()?.sharedMesh)}).ToArray(),
            buildings=buildings.Select(t=>new{t.name,position=t.position.ToString("F1"),bounds=Bounds(t).ToString()}).ToArray()},Formatting.Indented));
        Capture(session.storyCamera,origin,tents[0]);BeachBoatCampRedesignAudit.PoseAudit(session.storyCamera);
        EditorApplication.Exit(doorBlocks.Count==0&&pathBlocks.Count==0?0:1);
    }
    static void Capture(Camera source,Vector3 origin,Transform tent)
    {
        var go=new GameObject("RedesignQACamera");var c=go.AddComponent<Camera>();c.CopyFrom(source);c.enabled=false;c.farClipPlane=2800;c.fieldOfView=65;
        var light=new GameObject("QADaylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(52,-35,0);
        RenderSettings.ambientLight=new Color(.55f,.55f,.5f);
        var rt=new RenderTexture(1440,1000,24);var image=new Texture2D(1440,1000,TextureFormat.RGB24,false);c.targetTexture=rt;
        void Shot(string name,Vector3 from,Vector3 target,bool ortho=false)
        {c.orthographic=ortho;c.orthographicSize=470;c.transform.position=from;c.transform.LookAt(target);c.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1440,1000),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());}
        Shot("Camp_Redesign_Overview",origin+new Vector3(0,1100,130),origin+Vector3.forward*130,true);
        Shot("Camp_Redesign_Command",origin+new Vector3(-100,23,260),origin+new Vector3(0,42,420));
        Shot("Camp_Redesign_Gate",origin+new Vector3(-285,23,150),origin+new Vector3(0,24,150));
        Shot("Camp_Redesign_OpenTent",tent.TransformPoint(new Vector3(0,15,62)),tent.TransformPoint(new Vector3(0,10,0)));
        RenderTexture.active=null;c.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(light.gameObject);
    }
}
