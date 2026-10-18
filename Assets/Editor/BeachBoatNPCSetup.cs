using System.Linq;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;

namespace BeachBoatStoryEditor
{
    public static class BeachBoatNPCSetup
    {
        [MenuItem("BeachBoat/Setup All NPC Wanders")]
        public static void SetupAllNPCWanders()=>Setup(0);
        [MenuItem("BeachBoat/Setup Camp Guards Wander")]
        public static void SetupCampGuards()=>Setup(1);
        [MenuItem("BeachBoat/Setup Villagers Wander")]
        public static void SetupVillagers()=>Setup(2);
        static void Setup(int mode)
        {
            var scene=BeachBoatToolSafety.Begin("NPCWander");int added=0;
            var session=Object.FindAnyObjectByType<BeachBoatQuestSession>();
            var walkingController=session?.player?.GetComponentInChildren<Animator>()?.runtimeAnimatorController;
            foreach(var a in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(a.gameObject.scene!=scene||!NPCWander.IsEligible(a))continue;
                string path=AnimationUtility.CalculateTransformPath(a.transform,null).ToLowerInvariant();
                if(mode==1&&!path.Contains("guard")&&!path.Contains("soldier"))continue;
                if(mode==2&&!path.Contains("villag")&&!path.Contains("lang")&&!path.Contains("dan"))continue;
                if(a.GetComponentInParent<NPCWander>()!=null)continue;
                if(!a.runtimeAnimatorController.animationClips.Any(c=>c.name=="Walking"))
                {
                    if(walkingController==null)continue;
                    Undo.RecordObject(a,"Assign walking animation");a.runtimeAnimatorController=walkingController;a.applyRootMotion=false;
                }
                var wander=Undo.AddComponent<NPCWander>(a.gameObject);wander.animator=a;wander.wanderRadius=20;wander.walkSpeed=6;
                foreach(var t in a.GetComponentsInChildren<Transform>(true)){Undo.RecordObject(t.gameObject,"Allow NPC movement");GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);}
                EditorUtility.SetDirty(wander);added++;
            }
            BeachBoatToolSafety.Save(scene);Debug.Log($"NPC wander: {added} humanoid actors added; player, commander and group roots excluded.");
        }
    }
}
