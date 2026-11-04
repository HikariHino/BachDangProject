using UnityEditor.SceneManagement;
public static class BeachBoatPreparationBatch
{
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");BeachBoatWorkbenchGripAuthoring.Build();BeachBoatBatchRevision31.Run();}
}
