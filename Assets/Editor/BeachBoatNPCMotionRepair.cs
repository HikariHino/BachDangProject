using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>NPC-only clips keep the armored veteran's torso balanced without changing player motion.</summary>
public static class BeachBoatNPCMotionRepair
{
    const string Folder="Assets/BeachBoatStory/Camp";
    static AnimationClip Make(string sourcePath,string name)
    {
        var source=AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
        var clip=UnityEngine.Object.Instantiate(source);clip.name=name;
        var bindings=AnimationUtility.GetCurveBindings(clip);
        foreach(var binding in bindings.Where(b=>b.type==typeof(Animator)))
        {
            float factor=1;
            if(binding.propertyName.EndsWith("Left-Right") && !binding.propertyName.Contains("Arm") &&
                (binding.propertyName.Contains("Chest")||binding.propertyName.Contains("Spine"))) factor=.25f;
            if(binding.propertyName=="Head Tilt Left-Right"||binding.propertyName=="Neck Tilt Left-Right")factor=.2f;
            if(factor==1)continue;
            var curve=AnimationUtility.GetEditorCurve(clip,binding);
            curve.keys=curve.keys.Select(k=>new Keyframe(k.time,k.value*factor,k.inTangent*factor,k.outTangent*factor)
                {weightedMode=k.weightedMode,inWeight=k.inWeight,outWeight=k.outWeight}).ToArray();
            AnimationUtility.SetEditorCurve(clip,binding,curve);
        }
        var root=bindings.Where(b=>b.propertyName.StartsWith("RootQ."))
            .OrderBy(b=>"xyzw".IndexOf(b.propertyName[b.propertyName.Length-1])).ToArray();
        if(root.Length==4)
        {
            var curves=root.Select(b=>AnimationUtility.GetEditorCurve(clip,b)).ToArray();
            var times=curves.SelectMany(c=>c.keys.Select(k=>k.time)).Distinct().OrderBy(t=>t).ToArray();
            var rebuilt=Enumerable.Range(0,4).Select(_=>new AnimationCurve()).ToArray();
            Quaternion previous=Quaternion.identity;
            foreach(float time in times)
            {
                var q=new Quaternion(curves[0].Evaluate(time),curves[1].Evaluate(time),curves[2].Evaluate(time),curves[3].Evaluate(time));
                var e=q.eulerAngles;
                q=Quaternion.Euler(Mathf.DeltaAngle(0,e.x)*.35f,e.y,0);
                if(Quaternion.Dot(previous,q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                previous=q;
                rebuilt[0].AddKey(time,q.x);rebuilt[1].AddKey(time,q.y);rebuilt[2].AddKey(time,q.z);rebuilt[3].AddKey(time,q.w);
            }
            for(int i=0;i<4;i++)AnimationUtility.SetEditorCurve(clip,root[i],rebuilt[i]);
            clip.EnsureQuaternionContinuity();
        }
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;
        AnimationUtility.SetAnimationClipSettings(clip,settings);
        string path=Folder+"/"+name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(existing==null){AssetDatabase.CreateAsset(clip,path);return clip;}
        EditorUtility.CopySerialized(clip,existing);UnityEngine.Object.DestroyImmediate(clip);EditorUtility.SetDirty(existing);return existing;
    }
    public static void Apply()
    {
        var idle=Make("Assets/Animation/Meshy_AI_Character_output@Idle.fbx","Camp_Balanced_Idle");
        var walk=Make("Assets/Animation/Meshy_AI_Character_output@Walking (1).fbx","Camp_Balanced_Walk");
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/ThaiCampPoses.controller");
        if(controller==null)throw new InvalidOperationException("Camp NPC controller missing.");
        foreach(var child in controller.layers[0].stateMachine.states)
        {
            if(child.state.name=="Idle")child.state.motion=idle;
            if(child.state.name=="Walking"){child.state.motion=walk;child.state.iKOnFeet=true;}
        }
        EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
    }
    public static void ApplyAndExit(){Apply();EditorApplication.Exit(0);}
}
