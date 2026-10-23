using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BeachBoatFrontEndSetup
{
    static float Ground(Vector3 p)
    {
        foreach(var t in Terrain.activeTerrains){var local=p-t.transform.position;var size=t.terrainData.size;
            if(local.x>=0&&local.z>=0&&local.x<=size.x&&local.z<=size.z)return t.SampleHeight(p)+t.transform.position.y;}
        return float.NegativeInfinity;
    }
    static Vector3 Place(Vector3 anchor,GameObject player,Quaternion rotation,bool includeAnchor)
    {
        var radii=includeAnchor?new[]{0f,70f,95f,125f,160f,200f,240f}:new[]{70f,95f,125f,160f,200f,240f};
        foreach(float radius in radii)for(int i=0;i<24;i++)
        {
            var p=anchor+Quaternion.Euler(0,i*15,0)*Vector3.forward*radius;p.y=Ground(p);
            if(float.IsInfinity(p.y)||p.y<Object.FindFirstObjectByType<TideSystem>().highTideY+3)continue;
            if(Mathf.Abs(Ground(p+Vector3.right*20)-p.y)>4||Mathf.Abs(Ground(p+Vector3.forward*29)-p.y)>4)continue;
            bool blocked=Physics.OverlapBox(p+Vector3.up*15,new Vector3(24,14,34),rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                .Any(c=>!(c is TerrainCollider)&&!c.transform.IsChildOf(player.transform));
            if(blocked)continue;
            bool field=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Any(r=>{
                string n=r.transform.root.name+"/"+r.name;var parent=r.transform.parent;if(parent!=null)n+="/"+parent.name;
                return (n.Contains("Field",StringComparison.OrdinalIgnoreCase)||n.Contains("Ruong",StringComparison.OrdinalIgnoreCase)||n.Contains("Crop",StringComparison.OrdinalIgnoreCase))&&r.bounds.Intersects(new Bounds(p,new Vector3(48,40,65)));});
            if(!field)return p;
        }
        throw new Exception("Cannot place a clear save shelter near "+anchor);
    }
    static void Block(string name,Transform parent,Vector3 position,Vector3 size,Material mat,bool solid=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=mat;if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    public static void ApplyAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        var session=Object.FindFirstObjectByType<BeachBoatQuestSession>();var old=GameObject.Find("CampaignSaveShelters");if(old!=null)Object.DestroyImmediate(old);
        var root=new GameObject("CampaignSaveShelters").transform;
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BeachBoatStory/Camp/CanvasFieldTent.asset");
        var canvas=AssetDatabase.LoadAssetAtPath<Material>("Assets/BeachBoatStory/Camp/CanvasFieldTent.mat");
        var timber=AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Buildings/Materials/mat_building_01.mat");
        var parchment=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.80f,.69f,.44f)};
        string materialPath="Assets/Resources/BachDang938/SaveParchment.mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(existing==null)AssetDatabase.CreateAsset(parchment,materialPath);else{Object.DestroyImmediate(parchment);parchment=existing;}
        // Quiet edges of the camp and village, clear of the market and departure lanes.
        var anchors=new[]{new Vector3(-5650,0,1620),new Vector3(-6870,0,-4370),Object.FindFirstObjectByType<QuestRowboat>().boardingPoint.position};
        var rotations=new[]{Quaternion.Euler(0,180,0),Quaternion.Euler(0,120,0),Quaternion.identity};
        var labels=new[]{"DOANH TRẠI","LÀNG VEN SÔNG","BẾN TRẬN ĐỊA CỌC"};
        for(int i=0;i<3;i++)
        {
            Physics.SyncTransforms();var p=Place(anchors[i],session.player,rotations[i],i<2);
            var tent=new GameObject("Leu_Luu_"+i,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));tent.transform.SetParent(root,false);tent.transform.position=p;
            tent.transform.rotation=rotations[i];
            tent.GetComponent<MeshFilter>().sharedMesh=mesh;tent.GetComponent<MeshRenderer>().sharedMaterial=canvas;tent.GetComponent<MeshCollider>().sharedMesh=mesh;
            Block("Ban_Thu_Bo",tent.transform,new Vector3(0,9,-3),new Vector3(16,1,8),timber,true);
            foreach(float x in new[]{-6f,6f})foreach(float z in new[]{-5f,-1f})Block("Chan_Ban",tent.transform,new Vector3(x,4.5f,z),new Vector3(1.2f,9,1.2f),timber);
            Block("So_Luu_Hanh_Trinh",tent.transform,new Vector3(0,9.7f,-2),new Vector3(7,.4f,5),parchment);
            Block("Bien_Luu",tent.transform,new Vector3(0,21,25),new Vector3(30,5,.6f),timber);
            var sign=new GameObject("Luu_Hanh_Trinh",typeof(TextMeshPro));sign.transform.SetParent(tent.transform,false);sign.transform.localPosition=new Vector3(0,21,25.5f);sign.transform.localRotation=Quaternion.Euler(0,180,0);
            var text=sign.GetComponent<TextMeshPro>();StoryFont.Apply(text);text.text="LỀU LƯU HÀNH TRÌNH";text.fontSize=23;text.alignment=TextAlignmentOptions.Center;text.color=new Color(1,.85f,.45f);text.rectTransform.sizeDelta=new Vector2(29,5);text.textWrappingMode=TextWrappingModes.NoWrap;
            var marker=new GameObject("Save_"+i);marker.transform.SetParent(tent.transform,false);marker.transform.localPosition=new Vector3(0,1,17);
            marker.AddComponent<CampaignSavePoint>().displayName=labels[i];
            Debug.Log("SAVE_SHELTER "+labels[i]+" "+p+" mesh="+mesh.bounds.size);
        }
        var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/BachDang938/MainMenu.png");importer.textureType=TextureImporterType.Default;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        foreach(string guid in AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/Resources/BachDang938/RunSteps"}))
        {var audio=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));var s=audio.defaultSampleSettings;s.loadType=AudioClipLoadType.DecompressOnLoad;s.compressionFormat=AudioCompressionFormat.PCM;audio.defaultSampleSettings=s;audio.forceToMono=true;audio.SaveAndReimport();}
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
}
