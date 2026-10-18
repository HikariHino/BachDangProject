using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BeachBoatStoryEditor
{
    public static class BeachBoatToolSafety
    {
        public static UnityEngine.SceneManagement.Scene Begin(string name)
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!="Assets/Scenes/beachBoat.unity")throw new InvalidOperationException("Open beachBoat in Edit Mode. These tools do not run in Play Mode.");
            System.IO.Directory.CreateDirectory(".utmp/October18Repair");
            if(!EditorSceneManager.SaveScene(scene,".utmp/October18Repair/"+name+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".unity",true))throw new Exception("Backup failed.");
            Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName(name);return scene;
        }
        public static void Save(UnityEngine.SceneManagement.Scene scene)
        {EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");AssetDatabase.SaveAssets();}
        public static Material Material(string name,Color color,bool transparent=false,bool emissive=false)
        {
            const string folder="Assets/BeachBoatStory/SceneryMaterials";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/BeachBoatStory","SceneryMaterials");
            string path=folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            Undo.RecordObject(m,"Repair scenery material");
            m.shader=Shader.Find("Universal Render Pipeline/Lit");m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.1f);m.enableInstancing=true;
            if(transparent){m.SetFloat("_Surface",1);m.SetFloat("_ZWrite",0);m.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;m.SetOverrideTag("RenderType","Transparent");}
            if(emissive){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*2);}
            EditorUtility.SetDirty(m);return m;
        }
    }
}
