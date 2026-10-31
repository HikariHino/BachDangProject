using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
public static class BeachBoatBowLocomotionSetup
{
    public static void Build()
    {
        const string path="Assets/Resources/Player_Bow_Locomotion.controller",maskPath="Assets/Resources/Player_Bow_UpperBody.mask";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if(controller!=null)return;
        AnimationClip Clip(string file)=>AssetDatabase.LoadAllAssetsAtPath(file).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
        var idle=Clip("Assets/Main_Character/Animation/Crossbow_Animation/BowIdle.fbx");
        var walk=Clip("Assets/Main_Character/Animation/Crossbow_Animation/Walking.fbx");
        controller=AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("isWalking",AnimatorControllerParameterType.Bool);controller.AddParameter("isRunning",AnimatorControllerParameterType.Bool);controller.AddParameter("jump",AnimatorControllerParameterType.Trigger);controller.AddParameter("AimMove",AnimatorControllerParameterType.Float);
        var tree=new BlendTree{name="Aim feet locomotion",blendType=BlendTreeType.Simple1D,blendParameter="AimMove",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,controller);tree.AddChild(idle,0);tree.AddChild(walk,1);
        var feet=controller.layers[0].stateMachine.AddState("Bow_Feet");feet.motion=tree;controller.layers[0].stateMachine.defaultState=feet;
        var mask=new AvatarMask{name="Bow upper body"};for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
        foreach(var part in new[]{AvatarMaskBodyPart.Root,AvatarMaskBodyPart.Body,AvatarMaskBodyPart.Head,AvatarMaskBodyPart.LeftArm,AvatarMaskBodyPart.RightArm,AvatarMaskBodyPart.LeftFingers,AvatarMaskBodyPart.RightFingers,AvatarMaskBodyPart.LeftHandIK,AvatarMaskBodyPart.RightHandIK})mask.SetHumanoidBodyPartActive(part,true);
        AssetDatabase.CreateAsset(mask,maskPath);controller.AddLayer("Bow_UpperBody");var layers=controller.layers;layers[1].avatarMask=mask;layers[1].defaultWeight=1;layers[1].blendingMode=AnimatorLayerBlendingMode.Override;controller.layers=layers;
        var aim=layers[1].stateMachine.AddState("Bow_Aim");aim.motion=idle;layers[1].stateMachine.defaultState=aim;
        EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
    }
}
