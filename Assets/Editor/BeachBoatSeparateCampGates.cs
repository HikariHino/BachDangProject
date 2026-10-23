using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using Gate=BeachBoatStory.CampGateTransition;

/// <summary>Two separate, signed departure gates in the west palisade of the camp.</summary>
public static class BeachBoatSeparateCampGates
{
    static float Ground(Vector3 p)=>Terrain.activeTerrain.SampleHeight(p)+Terrain.activeTerrain.transform.position.y;
    static Bounds BoundsOf(Transform root)
    {
        var renderers=root.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;
        foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);return bounds;
    }
    static Transform Empty(string name,Transform parent,Vector3 p)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;return t;}
    static void Board(Transform parent,string name,float y,float width,float height,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
        go.transform.localPosition=new Vector3(0,y,7);go.transform.localScale=new Vector3(width,height,.8f);go.GetComponent<Renderer>().sharedMaterial=material;
    }
    static void Label(Transform parent,string name,string caption,float y,float size,Vector2 area)
    {
        var t=Empty(name,parent,new Vector3(0,y,7.5f));t.localRotation=Quaternion.Euler(0,180,0);
        var text=t.gameObject.AddComponent<TextMeshPro>();StoryFont.Apply(text);text.text=caption;text.fontSize=size;text.alignment=TextAlignmentOptions.Center;
        text.color=new Color(1,.86f,.53f);text.rectTransform.sizeDelta=area;text.textWrappingMode=TextWrappingModes.NoWrap;
    }
    static void BuildGate(Gate gate,Vector3 position,Transform sample,Vector3 visualScale,Material wood,string title,string detail)
    {
        position.y=Ground(position);gate.transform.SetPositionAndRotation(position,Quaternion.Euler(0,90,0));
        var scale=gate.transform.parent.lossyScale;gate.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
        foreach(var collider in gate.GetComponents<Collider>())Object.DestroyImmediate(collider);
        var visual=gate.transform.Find("Visible_Story_Gate");
        if(visual==null){visual=Object.Instantiate(sample.gameObject,gate.transform).transform;visual.name="Visible_Story_Gate";}
        foreach(var collider in visual.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
        visual.SetPositionAndRotation(position,Quaternion.Euler(0,180,0));visual.localScale=visualScale;
        visual.position+=Vector3.up*(Ground(position)-BoundsOf(visual).min.y);
        foreach(var mesh in visual.GetComponentsInChildren<MeshFilter>(true))
            if(mesh.sharedMesh!=null)mesh.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh.sharedMesh;
        var old=gate.transform.Find("Destination_Sign");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var sign=Empty("Destination_Sign",gate.transform,Vector3.zero);
        Board(sign,"Route_Header",45,64,8,wood);Label(sign,"Destination",title,45,27,new Vector2(62,7));
        Board(sign,"Route_Details",36,62,7,wood);Label(sign,"Instructions",detail,36,19,new Vector2(60,6));
        gate.interactDistance=22;EditorUtility.SetDirty(gate);
    }
    static void Barrier(Transform fence,string name,float x,float a,float b)
    {
        var t=fence.Find(name);if(t==null)t=Empty(name,fence,Vector3.zero);
        var p=new Vector3(x,0,(a+b)*.5f);p.y=Ground(p)+22;t.SetPositionAndRotation(p,Quaternion.identity);
        var scale=t.parent.lossyScale;t.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
        var box=t.GetComponent<BoxCollider>();if(box==null)box=t.gameObject.AddComponent<BoxCollider>();box.size=new Vector3(5,44,Mathf.Abs(b-a));
    }
    public static void ApplyAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        var session=Object.FindFirstObjectByType<BeachBoatQuestSession>();var gates=session.GetComponentsInChildren<Gate>(true);
        var village=gates.Single(g=>g.gateType==Gate.GateType.CampGate);var river=gates.Single(g=>g.gateType==Gate.GateType.CampRiverGate);
        var anchor=session.transform.Find("Camp_Departure_Center");
        if(anchor==null){anchor=Empty("Camp_Departure_Center",session.transform,Vector3.zero);anchor.position=village.transform.position;}
        Vector3 center=anchor.position;var sample=village.transform.Find("Visible_Story_Gate");var visualScale=sample.lossyScale;
        var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Buildings/Materials/mat_building_01.mat");
        var fence=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(t=>t.name=="Camp_Palisade_Reused");
        var panel=fence.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name.StartsWith("Camp_Palisade_")).gameObject;
        foreach(var renderer in fence.GetComponentsInChildren<MeshRenderer>(true).ToArray())
        {
            var bounds=renderer.bounds;if(Mathf.Abs(bounds.center.x-center.x)>16)continue;
            bool Intrudes(float z)=>bounds.max.z>z-32&&bounds.min.z<z+32;
            if(Intrudes(center.z-60)||Intrudes(center.z+60))Object.DestroyImmediate(renderer.gameObject);
        }
        var old=fence.Find("Departure_Separator_Panels");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var separator=Empty("Departure_Separator_Panels",fence,Vector3.zero);
        var fenceScale=fence.lossyScale;separator.localScale=new Vector3(1/fenceScale.x,1/fenceScale.y,1/fenceScale.z);
        foreach(float z in new[]{-21f,-7f,7f,21f})
        {
            var copy=Object.Instantiate(panel,separator);copy.name="Departure_Palisade";
            copy.transform.SetPositionAndRotation(new Vector3(center.x,center.y,center.z+z),Quaternion.Euler(0,180,0));
            copy.transform.localScale=panel.transform.lossyScale;
            copy.transform.position+=Vector3.up*(Ground(copy.transform.position)-copy.GetComponent<Renderer>().bounds.min.y);
        }
        var north=fence.Find("Camp_Barrier_3").GetComponent<BoxCollider>().bounds.max.z;
        var south=fence.Find("Camp_Barrier_4").GetComponent<BoxCollider>().bounds.min.z;
        Barrier(fence,"Camp_Barrier_3",center.x,center.z+92,north);
        Barrier(fence,"Camp_Barrier_4",center.x,south,center.z-92);
        Barrier(fence,"Camp_Barrier_Departure_Separator",center.x,center.z-28,center.z+28);
        BuildGate(village,center+Vector3.back*60,sample,visualScale,wood,"LÀNG • NHIỆM VỤ 2","GỖ • DÂY • GẠO • LƯƠNG KHÔ   [E]");
        BuildGate(river,center+Vector3.forward*60,sample,visualScale,wood,"BẾN SÔNG BẠCH ĐẰNG","ĐÓNG CỌC • XUẤT CHIẾN   [E]");
        if(village.destinationTarget==river.destinationTarget||Vector3.Distance(village.destinationTarget.position,river.destinationTarget.position)<1000)throw new Exception("Camp routes do not have distinct destinations");
        Physics.SyncTransforms();
        foreach(var gate in new[]{village,river})
        {
            var p=gate.transform.position+gate.transform.forward*18+Vector3.up*2.4f;
            if(!PlayerArrival.IsClear(session.player,p,gate.transform.rotation)||!PlayerArrival.HasExit(session.player,p,gate.transform.rotation))throw new Exception("Gate approach is blocked: "+gate.name);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("Two signed camp gates saved, 120 world units apart: village quest 2 and river harbor.");EditorApplication.Exit(0);
    }
}
