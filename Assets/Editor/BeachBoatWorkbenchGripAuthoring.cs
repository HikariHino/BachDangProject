using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;

/// <summary>Separate work-hand poses: three flexion zones plus thumb opposition, without changing the source rig.</summary>
public static class BeachBoatWorkbenchGripAuthoring
{
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Author work grips in Edit Mode.");
        var profile=BowGripProfile.Load();
        var renderer=UnityEngine.Object.FindAnyObjectByType<BeachBoatQuestSession>().player.GetComponentInChildren<SkinnedMeshRenderer>();
        var source=Forearms(profile.source,renderer.bones);
        var result=UnityEngine.Object.Instantiate(source);result.name="Workbench_segmented_finger_grips";result.ClearBlendShapes();
        foreach(bool left in new[]{true,false})foreach(string pose in new[]{"Rod","Pinch","Support"})
            Add(result,source,renderer.bones,profile,left,pose);
        const string path="Assets/Resources/BachDang938/Player/WorkbenchHandGrips.asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null)AssetDatabase.CreateAsset(result,path);
        else {EditorUtility.CopySerialized(result,existing);UnityEngine.Object.DestroyImmediate(result);EditorUtility.SetDirty(existing);}
        AssetDatabase.SaveAssets();UnityEngine.Object.DestroyImmediate(source);
    }
    static Mesh Forearms(Mesh input,Transform[] bones)
    {
        var allowed=new HashSet<int>(Enumerable.Range(0,bones.Length).Where(i=>bones[i]!=null&&new[]{"LeftArm","LeftForeArm","LeftHand","LeftHand_End","RightArm","RightForeArm","RightHand","RightHand_End"}.Contains(bones[i].name)));
        // Use the actual humanoid arm mapping as well, for differently named exports.
        var animator=UnityEngine.Object.FindAnyObjectByType<BeachBoatQuestSession>().player.GetComponentInChildren<Animator>();
        foreach(var part in new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand})allowed.Add(Array.IndexOf(bones,animator.GetBoneTransform(part)));
        var weights=input.boneWeights;bool Arm(int i){var w=weights[i];return (allowed.Contains(w.boneIndex0)?w.weight0:0)+(allowed.Contains(w.boneIndex1)?w.weight1:0)+(allowed.Contains(w.boneIndex2)?w.weight2:0)+(allowed.Contains(w.boneIndex3)?w.weight3:0)>=.65f;}
        var triangles=new List<int>[input.subMeshCount];var used=new SortedSet<int>();
        for(int sub=0;sub<input.subMeshCount;sub++){triangles[sub]=new List<int>();var t=input.GetTriangles(sub);for(int i=0;i<t.Length;i+=3)if(Arm(t[i])&&Arm(t[i+1])&&Arm(t[i+2]))for(int k=0;k<3;k++){triangles[sub].Add(t[i+k]);used.Add(t[i+k]);}}
        var indices=used.ToArray();var map=indices.Select((old,index)=>(old,index)).ToDictionary(p=>p.old,p=>p.index);var source=new Mesh{name="Work forearms base"};
        var vertices=input.vertices;var normals=input.normals;var tangents=input.tangents;var uv=input.uv;
        source.vertices=indices.Select(i=>vertices[i]).ToArray();source.normals=indices.Select(i=>normals[i]).ToArray();source.tangents=indices.Select(i=>tangents[i]).ToArray();source.uv=indices.Select(i=>uv[i]).ToArray();source.boneWeights=indices.Select(i=>weights[i]).ToArray();source.bindposes=input.bindposes;source.subMeshCount=input.subMeshCount;
        for(int sub=0;sub<source.subMeshCount;sub++)source.SetTriangles(triangles[sub].Select(i=>map[i]).ToArray(),sub);source.RecalculateBounds();return source;
    }
    static void Add(Mesh output,Mesh source,Transform[] bones,BowGripProfile profile,bool left,string pose)
    {
        string name=left?"LeftHand":"RightHand";int hand=Array.FindIndex(bones,b=>b.name==name),end=Array.FindIndex(bones,b=>b.name==name+"_End");
        var bind=source.bindposes[hand];var inverse=bind.inverse;var vertices=source.vertices;var bent=(Vector3[])vertices.Clone();var weights=source.boneWeights;
        var along=left?profile.leftForward:profile.rightForward;var wide=left?profile.leftAcross:profile.rightAcross;
        var palm=Vector3.Cross(along,wide)*(left?-1:1);float length=left?.00195941f:.00209216f;int affected=0;
        for(int i=0;i<vertices.Length;i++)
        {
            var w=weights[i];float weight=(w.boneIndex0==hand||w.boneIndex0==end?w.weight0:0)+(w.boneIndex1==hand||w.boneIndex1==end?w.weight1:0)+(w.boneIndex2==hand||w.boneIndex2==end?w.weight2:0)+(w.boneIndex3==hand||w.boneIndex3==end?w.weight3:0);
            if(weight<.5f)continue;
            var p=bind.MultiplyPoint3x4(vertices[i])/length;float s=Vector3.Dot(p,along),u=Vector3.Dot(p,wide),n=Vector3.Dot(p,palm);
            // Wrist/cuff vertices stay untouched. The thumb is on the +wide side in both bind frames.
            if(s<.28f||s>1.12f||Mathf.Abs(u)>.65f||Mathf.Abs(n)>.45f)continue;
            bool thumb=u>.32f&&s<.73f;
            float start=thumb?.32f:.48f;
            if(s<=start)continue;
            float curl=pose=="Rod"?1:pose=="Pinch"?(u>.18f||thumb?1.04f:.48f):.26f;
            float a1=(thumb?32:48)*curl,a2=(thumb?38:65)*curl,a3=(thumb?12:42)*curl;
            float span=s-start,advance=0,inward=0,theta=0;const int steps=24;
            for(int k=0;k<steps;k++)
            {
                float at=start+span*(k+.5f)/steps;
                theta=(a1*Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,start+.07f,at))+
                    a2*Mathf.SmoothStep(0,1,Mathf.InverseLerp(thumb?.46f:.69f,thumb?.51f:.75f,at))+
                    a3*Mathf.SmoothStep(0,1,Mathf.InverseLerp(thumb?.57f:.84f,thumb?.61f:.9f,at)))*Mathf.Deg2Rad;
                advance+=Mathf.Cos(theta)*span/steps;inward+=Mathf.Sin(theta)*span/steps;
            }
            float final=(a1*Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,start+.07f,s))+
                a2*Mathf.SmoothStep(0,1,Mathf.InverseLerp(thumb?.46f:.69f,thumb?.51f:.75f,s))+
                a3*Mathf.SmoothStep(0,1,Mathf.InverseLerp(thumb?.57f:.84f,thumb?.61f:.9f,s)))*Mathf.Deg2Rad;
            float thumbBlend=thumb?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,.5f,u))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,.6f,s)):0;
            float opposed=u-thumbBlend*(pose=="Support"?.04f:pose=="Pinch"?.2f:.14f);
            var q=wide*opposed+along*(start+advance-n*Mathf.Sin(final))+palm*(inward+n*Mathf.Cos(final));
            float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,.85f,weight));bent[i]=Vector3.Lerp(vertices[i],inverse.MultiplyPoint3x4(q*length),blend);affected++;
        }
        if(affected<250)throw new Exception("Work hand has insufficient finger vertices: "+name);
        var temp=UnityEngine.Object.Instantiate(source);temp.vertices=bent;temp.RecalculateNormals();temp.RecalculateTangents();
        var delta=new Vector3[vertices.Length];var normals=new Vector3[vertices.Length];var tangents=new Vector3[vertices.Length];var sn=source.normals;var st=source.tangents;var dn=temp.normals;var dt=temp.tangents;
        for(int i=0;i<vertices.Length;i++)if(vertices[i]!=bent[i]){delta[i]=bent[i]-vertices[i];normals[i]=dn[i]-sn[i];tangents[i]=new Vector3(dt[i].x-st[i].x,dt[i].y-st[i].y,dt[i].z-st[i].z);}
        output.AddBlendShapeFrame((left?"Left":"Right")+pose,100,delta,normals,tangents);UnityEngine.Object.DestroyImmediate(temp);
        Debug.Log("Work fingers "+name+" "+pose+": "+affected+" vertices, three knuckle zones and opposed thumb.");
    }
}
