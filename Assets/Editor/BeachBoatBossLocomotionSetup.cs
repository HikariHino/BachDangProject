using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class BeachBoatBossLocomotionSetup
{
    public static void Build()
    {
        var jump=AssetDatabase.LoadAllAssetsAtPath("Assets/Animation/Meshy_AI_Character_output@Jump.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
        foreach(var pair in new[]{("River_Sword_Combat","Boss_Duel_Sword"),("Player_Neutral","Boss_Duel_Foot"),("Player_Bow_Locomotion","Boss_Duel_Bow")})
        {
            string path="Assets/Resources/"+pair.Item2+".controller";
            if(AssetDatabase.LoadAssetAtPath<AnimatorController>(path)==null)AssetDatabase.CopyAsset("Assets/Resources/"+pair.Item1+".controller",path);
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);var machine=controller.layers[0].stateMachine;
            if(!controller.parameters.Any(p=>p.name=="DuelMelee"))controller.AddParameter("DuelMelee",AnimatorControllerParameterType.Bool);
            if(!controller.parameters.Any(p=>p.name=="DuelAirborne"))controller.AddParameter("DuelAirborne",AnimatorControllerParameterType.Bool);
            if(!controller.parameters.Any(p=>p.name=="DuelJumpTime"))controller.AddParameter("DuelJumpTime",AnimatorControllerParameterType.Float);
            var state=machine.states.FirstOrDefault(s=>s.state.name=="Duel_Jump").state;
            if(state==null)
            {
                state=machine.AddState("Duel_Jump");var enter=machine.AddAnyStateTransition(state);enter.hasExitTime=false;enter.duration=.05f;enter.canTransitionToSelf=false;enter.AddCondition(AnimatorConditionMode.If,0,"DuelAirborne");enter.AddCondition(AnimatorConditionMode.IfNot,0,"DuelMelee");
                var exit=state.AddTransition(machine.defaultState);exit.hasExitTime=false;exit.duration=.08f;exit.AddCondition(AnimatorConditionMode.IfNot,0,"DuelAirborne");
            }
            state.motion=jump;state.timeParameterActive=true;state.timeParameter="DuelJumpTime";state.writeDefaultValues=true;EditorUtility.SetDirty(controller);
        }
        AssetDatabase.SaveAssets();Debug.Log("Duel sword, bow and unarmed jump locomotion prepared");
    }
}
