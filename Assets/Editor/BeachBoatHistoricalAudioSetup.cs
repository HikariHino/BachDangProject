using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BeachBoatStory;

namespace BeachBoatStoryEditor
{
    public static class BeachBoatHistoricalAudioSetup
    {
        [MenuItem("BeachBoat/Setup Historical Combat Audio")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != "Assets/Scenes/beachBoat.unity")
                throw new System.InvalidOperationException("Open Assets/Scenes/beachBoat.unity first.");
            var battle = Object.FindAnyObjectByType<RiverBattleChoreography>();
            if (battle == null) throw new System.InvalidOperationException("RiverBattleChoreography is missing.");
            Undo.RecordObject(battle, "Assign historical combat audio");
            battle.swordSwing = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Vung Kiem.mp3");
            battle.swordClash = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/kiemvskiem.mp3");
            battle.hitImpact = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/chem trung.mp3");
            EditorUtility.SetDirty(battle);
            var session = Object.FindAnyObjectByType<BeachBoatQuestSession>();
            var animator = session != null && session.player != null ? session.player.GetComponentInChildren<Animator>(true) : null;
            if (animator != null)
            {
                var source = animator.GetComponent<AudioSource>();
                if (source == null) source = Undo.AddComponent<AudioSource>(animator.gameObject);
                var footsteps = animator.GetComponent<FootstepSound>();
                if (footsteps == null) footsteps = Undo.AddComponent<FootstepSound>(animator.gameObject);
                footsteps.footstepClips = new[]
                {
                    AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Action/Walk1.mp3"),
                    AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Action/Walk2.mp3")
                };
                footsteps.walkStepDistance = 7f;
                footsteps.runStepDistance = 5f;
                footsteps.walkVolume = .28f;
                footsteps.runVolume = .42f;
                EditorUtility.SetDirty(source); EditorUtility.SetDirty(footsteps);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Assigned Bac's sword swing, sword clash and hit clips to beachBoat river combat.");
        }
    }
}
