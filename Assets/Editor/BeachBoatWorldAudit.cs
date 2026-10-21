using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BeachBoatWorldAudit
{
    public static Bounds BoundsOf(Transform t)
    {var rr=t.GetComponentsInChildren<Renderer>(true).Where(r=>!(r is ParticleSystemRenderer)&&r.GetComponent<TMPro.TMP_Text>()==null).ToArray();var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;}
    public static void RunAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");Directory.CreateDirectory("Logs");
        var camp=GameObject.Find("TestQuest_Camp").transform;var origin=camp.Find("CampLayoutOrigin").position;
        var houses=camp.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Nha_Nghi")||t.name=="Lo_Ren_Quan_Do"||t.name=="Kho_Khi_Gioi_Nho"||t.name=="Central_Supply_Store").ToArray();
        var settlements=GameObject.Find("BachDang_Living_Settlements").transform;
        File.WriteAllText("Logs/WorldDensityAudit.json",JsonConvert.SerializeObject(new{
            houses=houses.Select(t=>new{t.name,bounds=BoundsOf(t).ToString(),position=t.position.ToString("F2"),rotation=t.eulerAngles.ToString(),scale=t.lossyScale.ToString(),parts=t.GetComponentsInChildren<MeshFilter>().Select(f=>new{f.name,position=f.transform.localPosition.ToString(),rotation=f.transform.localEulerAngles.ToString(),scale=f.transform.localScale.ToString(),mesh=AssetDatabase.GetAssetPath(f.sharedMesh)}).ToArray()}).ToArray(),
            districts=settlements.Cast<Transform>().Select(t=>new{t.name,position=t.position.ToString("F1"),scale=t.lossyScale.ToString(),people=t.GetComponentsInChildren<NPCWander>(true).Length,children=t.Cast<Transform>().Take(45).Select(c=>new{c.name,position=c.position.ToString("F1"),bounds=c.GetComponentsInChildren<Renderer>().Length>0?BoundsOf(c).ToString():""}).ToArray()}).ToArray(),
            actors=UnityEngine.Object.FindObjectsByType<NPCWander>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(w=>new{w.name,path=AnimationUtility.CalculateTransformPath(w.transform,null),position=w.transform.position.ToString("F1"),avatar=AssetDatabase.GetAssetPath(w.GetComponent<Animator>()?.avatar)}).ToArray()
        },Formatting.Indented));
        var sourceCamera=UnityEngine.Object.FindFirstObjectByType<BeachBoatQuestSession>().storyCamera;
        var camera=new GameObject("AuditCamera").AddComponent<Camera>();camera.CopyFrom(sourceCamera);camera.enabled=false;camera.fieldOfView=55;camera.farClipPlane=3000;
        var light=new GameObject("AuditLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(50,-30,0);
        var rt=new RenderTexture(900,800,24);var image=new Texture2D(900,800,TextureFormat.RGB24,false);camera.targetTexture=rt;
        void Shot(string name,Vector3 at,Vector3 target){camera.transform.position=at;camera.transform.LookAt(target);camera.ResetWorldToCameraMatrix();camera.ResetProjectionMatrix();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,900,800),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());}
        foreach(var h in houses){var b=BoundsOf(h);Shot("HouseAudit_"+h.name,b.center+new Vector3(70,20,100),b.center);}
        RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(light.gameObject);
        EditorApplication.Exit(0);
    }
}
