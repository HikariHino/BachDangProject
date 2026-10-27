using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;

/// <summary>Author grip blend shapes in hand bind space; leave the source model and skin weights intact.</summary>
public static class BeachBoatBowGripAuthoring
{
    const string Root="Assets/Resources/BachDang938/Player";
    [MenuItem("Tools/Bach Dang/Rebuild player bow grips")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Build grips in Edit Mode.");
        var session=UnityEngine.Object.FindFirstObjectByType<BeachBoatQuestSession>();
        var renderer=session.player.GetComponentInChildren<SkinnedMeshRenderer>();var source=renderer.sharedMesh;
        if(!source.isReadable)throw new Exception("The source character mesh must be readable.");
        Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        var profile=AssetDatabase.LoadAssetAtPath<BowGripProfile>(Root+"/BowGripProfile.asset");
        if(profile==null){profile=ScriptableObject.CreateInstance<BowGripProfile>();AssetDatabase.CreateAsset(profile,Root+"/BowGripProfile.asset");}
        var mesh=UnityEngine.Object.Instantiate(source);mesh.name="Player_Bow_Grip_Hands";mesh.ClearBlendShapes();
        profile.leftForward=Vector3.up;profile.leftAcross=new Vector3(-.3539533f,0,.9352631f);
        profile.rightForward=Vector3.up;profile.rightAcross=new Vector3(.3250758f,0,.945688f);
        profile.leftCenter=Morph(mesh,source,renderer.bones,"LeftHand",profile.leftForward,profile.leftAcross,new Vector3(-.9352631f,0,-.3539533f),.00195941f,155,"LeftBowGrip");
        profile.rightCenter=Morph(mesh,source,renderer.bones,"RightHand",profile.rightForward,profile.rightAcross,new Vector3(.945688f,0,-.3250758f),.00209216f,145,"RightBowHook");
        profile.rightRodCenter=profile.rightCenter-profile.rightForward*(.00209216f*.52f/(145*Mathf.Deg2Rad)*.8f);
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/PlayerBowGrips.asset");
        if(existing==null){AssetDatabase.CreateAsset(mesh,Root+"/PlayerBowGrips.asset");existing=mesh;}
        else {EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);}
        profile.source=source;profile.posed=existing;EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
        Debug.Log("Authored two bow hand morphs without altering the source character or its rig.");
    }
    static Vector3 Morph(Mesh result,Mesh source,Transform[] bones,string handName,Vector3 along,Vector3 wide,Vector3 palm,float length,float degrees,string shape)
    {
        int hand=Array.FindIndex(bones,b=>b.name==handName),end=Array.FindIndex(bones,b=>b.name==handName+"_End");
        var toHand=source.bindposes[hand];var fromHand=toHand.inverse;var original=source.vertices;var bent=(Vector3[])original.Clone();var weights=source.boneWeights;
        float start=length*.48f,span=length-start,angle=degrees*Mathf.Deg2Rad,radius=span/angle;int affected=0;
        for(int i=0;i<bent.Length;i++)
        {
            var b=weights[i];float weight=0;
            if(b.boneIndex0==hand||b.boneIndex0==end)weight+=b.weight0;if(b.boneIndex1==hand||b.boneIndex1==end)weight+=b.weight1;
            if(b.boneIndex2==hand||b.boneIndex2==end)weight+=b.weight2;if(b.boneIndex3==hand||b.boneIndex3==end)weight+=b.weight3;
            if(weight<.2f)continue;
            var p=toHand.MultiplyPoint3x4(original[i]);float s=Vector3.Dot(p,along),u=Vector3.Dot(p,wide),n=Vector3.Dot(p,palm);
            // Exclude cuff armour, the sword attachment and unrelated weighted vertices.
            if(s<start||s>length*1.12f||u*u+n*n>1.3e-3f*1.3e-3f)continue;
            float theta=Mathf.Clamp((s-start)/span,0,1.15f)*angle;
            var q=wide*u+along*(start+radius*Mathf.Sin(theta)-n*Mathf.Sin(theta))+palm*(radius*(1-Mathf.Cos(theta))+n*Mathf.Cos(theta));
            bent[i]=Vector3.Lerp(original[i],fromHand.MultiplyPoint3x4(q),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,.8f,weight)));affected++;
        }
        if(affected<100)throw new Exception("Not enough hand vertices: "+handName+" / "+affected);
        var posed=UnityEngine.Object.Instantiate(source);posed.vertices=bent;posed.RecalculateNormals();posed.RecalculateTangents();
        var dn=posed.normals;var dt=posed.tangents;var sn=source.normals;var st=source.tangents;var delta=new Vector3[bent.Length];var normals=new Vector3[bent.Length];var tangents=new Vector3[bent.Length];
        for(int i=0;i<bent.Length;i++){delta[i]=bent[i]-original[i];if(delta[i].sqrMagnitude>0){normals[i]=dn[i]-sn[i];tangents[i]=new Vector3(dt[i].x-st[i].x,dt[i].y-st[i].y,dt[i].z-st[i].z);}}
        result.AddBlendShapeFrame(shape,100,delta,normals,tangents);UnityEngine.Object.DestroyImmediate(posed);
        Debug.Log(shape+": "+affected+" vertices curled in hand bind space.");
        // The bow sits inside the left fist; the thin string meets the inner
        // curve of the drawing fingers rather than floating in its fist centre.
        return along*(start+(handName=="RightHand"?radius*.8f:0))+palm*radius+wide*.0001f;
    }
}
