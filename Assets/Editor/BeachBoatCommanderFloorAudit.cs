using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using BeachBoatStory;

public static class BeachBoatCommanderFloorAudit
{
    public static void FixAndExit()
    {
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");Physics.SyncTransforms();
            var actor=UnityEngine.Object.FindAnyObjectByType<BeachBoatStory.NPCQuestGiver>().gameObject;var rig=actor.GetComponent<Animator>();
            var before=Newtonsoft.Json.Linq.JObject.FromObject(Inspect(actor,rig));float sole=(float)before["sole"];
            var hits=new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot}.Select(rig.GetBoneTransform)
                .SelectMany(f=>Physics.RaycastAll(f.position+Vector3.up*5,Vector3.down,30,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                .Where(h=>!h.transform.IsChildOf(actor.transform)&&h.normal.y>.6f&&h.point.y<=sole+1&&!(h.collider is TerrainCollider)).ToArray();
            if(hits.Length==0)throw new InvalidOperationException("No solid command deck below the boots");
            float floor=hits.Max(h=>h.point.y);float correction=floor+.05f-sole;
            if(Mathf.Abs(correction)>8)throw new InvalidOperationException("Unexpected commander height correction: "+correction);
            actor.transform.position+=Vector3.up*correction;Physics.SyncTransforms();EditorUtility.SetDirty(actor.transform);
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save corrected commander position");
            var after=Inspect(actor,rig);File.WriteAllText("Logs/CommanderFloorFix.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{correction,floor,before,after},Newtonsoft.Json.Formatting.Indented));EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText("Logs/CommanderFloorFix.json",e.ToString());EditorApplication.Exit(1);}
    }
    public static void AuditAndExit()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");Physics.SyncTransforms();
            var actor=UnityEngine.Object.FindAnyObjectByType<BeachBoatStory.NPCQuestGiver>().gameObject;var rig=actor.GetComponent<Animator>();
            var before=Inspect(actor,rig);rig.Rebind();rig.Update(.05f);actor.GetComponent<NPCUprightPosture>()?.Balance();
            var evaluated=Inspect(actor,rig);
            File.WriteAllText("Logs/CommanderFloorAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{before,evaluated},Newtonsoft.Json.Formatting.Indented));
            EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText("Logs/CommanderFloorAudit.json",e.ToString());EditorApplication.Exit(1);}
    }
    public static object Inspect(GameObject actor,Animator rig)
    {
        var feet=new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot}.Select(rig.GetBoneTransform).ToArray();
        var footBones=new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.LeftToes,HumanBodyBones.RightToes}.Select(rig.GetBoneTransform).Where(t=>t!=null).ToArray();
        float sole=float.PositiveInfinity;int count=0;
        foreach(var r in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh=new Mesh();r.BakeMesh(mesh,true);var vertices=mesh.vertices;var weights=r.sharedMesh.boneWeights;var bones=r.bones;
            var matrix=Matrix4x4.TRS(r.transform.position,r.transform.rotation,Vector3.one);
            for(int i=0;i<weights.Length;i++)
            {
                var w=weights[i];float Weight(int index,float amount)=>index>=0&&index<bones.Length&&footBones.Contains(bones[index])?amount:0;
                if(Weight(w.boneIndex0,w.weight0)+Weight(w.boneIndex1,w.weight1)+Weight(w.boneIndex2,w.weight2)+Weight(w.boneIndex3,w.weight3)<.5f)continue;
                sole=Mathf.Min(sole,matrix.MultiplyPoint3x4(vertices[i]).y);count++;
            }
            UnityEngine.Object.DestroyImmediate(mesh);
        }
        var floors=feet.Select(f=>new{foot=f.name,position=f.position.ToString("F5"),hits=Physics.RaycastAll(f.position+Vector3.up*60,Vector3.down,150,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
            .Where(h=>!h.transform.IsChildOf(actor.transform)&&h.normal.y>.6f).OrderByDescending(h=>h.point.y).Select(h=>new{h.collider.name,y=h.point.y,parent=h.transform.parent?.name,type=h.collider.GetType().Name}).ToArray()}).ToArray();
        return new{actor.name,position=actor.transform.position.ToString("F5"),scale=actor.transform.lossyScale.ToString("F5"),sole,count,floors,components=actor.GetComponents<Component>().Select(c=>c?.GetType().FullName).ToArray()};
    }
}
