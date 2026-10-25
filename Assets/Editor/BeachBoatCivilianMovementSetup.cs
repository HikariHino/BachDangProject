using System;
using System.Linq;
using BeachBoatStory;
using UnityEditor;
using UnityEngine;

public static class BeachBoatCivilianMovementSetup
{
    public static void Apply()
    {
        const string folder="Assets/Model_NPC/";
        const string walkPath=folder+"FemalePea/FemalePea/Meshy_AI_Brown_Tunic_Figure_biped_Animation_Walking_withSkin.fbx";
        string[] models={folder+"FemalePea/FemalePea/Meshy_AI_Brown_Tunic_Figure_biped_Character_output.fbx",folder+"MalePea/MalePea/Meshy_AI_Blue_Scrub_Figure_biped_Character_output.fbx",folder+"DanThuongLonTuoi/Meshy_AI_Wandering_Peasant_biped_Character_output.fbx"};
        var avatars=models.Select(p=>AssetDatabase.LoadAllAssetsAtPath(p).OfType<Avatar>().First(a=>a.isHuman&&a.isValid)).ToArray();
        var importer=(ModelImporter)AssetImporter.GetAtPath(walkPath);
        importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;importer.sourceAvatar=avatars[0];
        var settings=importer.clipAnimations.Length>0?importer.clipAnimations:importer.defaultClipAnimations;
        foreach(var clip in settings){clip.loopTime=true;clip.lockRootPositionXZ=true;clip.lockRootHeightY=true;clip.lockRootRotation=true;}
        importer.clipAnimations=settings;importer.SaveAndReimport();
        var walking=AssetDatabase.LoadAllAssetsAtPath(walkPath).OfType<AnimationClip>().First(c=>c.isHumanMotion&&!c.name.StartsWith("__preview__"));
        if(!walking.isHumanMotion)throw new InvalidOperationException("Civilian walk did not import as a humanoid clip");
        const string root="Assets/Resources/BachDang938/";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(root+"CivilianMovement.overrideController");
        if(controller==null){controller=new AnimatorOverrideController();AssetDatabase.CreateAsset(controller,root+"CivilianMovement.overrideController");}
        controller.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Resources/Player_Neutral.controller");
        var overrides=new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip,AnimationClip>>();controller.GetOverrides(overrides);
        for(int i=0;i<overrides.Count;i++)if(overrides[i].Key.name.IndexOf("walk",StringComparison.OrdinalIgnoreCase)>=0)overrides[i]=new System.Collections.Generic.KeyValuePair<AnimationClip,AnimationClip>(overrides[i].Key,walking);
        controller.ApplyOverrides(overrides);EditorUtility.SetDirty(controller);
        var profile=AssetDatabase.LoadAssetAtPath<CivilianMovementProfile>(root+"CivilianMovement.asset");
        if(profile==null){profile=ScriptableObject.CreateInstance<CivilianMovementProfile>();AssetDatabase.CreateAsset(profile,root+"CivilianMovement.asset");}
        profile.avatars=avatars;profile.controller=controller;profile.walking=walking;EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
    }
}
