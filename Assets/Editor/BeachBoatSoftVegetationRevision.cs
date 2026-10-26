using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using BeachBoatStoryEditor;
using Object=UnityEngine.Object;

public static class BeachBoatSoftVegetationRevision
{
    static bool SoftName(string name)=>System.Text.RegularExpressions.Regex.IsMatch(name,"grass|bush|fern|heather|shrub|plant|co_thap|co_loang|co_bui",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    public static bool SoftObject(GameObject go)
    {
        var meshes=go.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.sharedMesh!=null).ToArray();
        return meshes.Length>0&&meshes.All(m=>SoftName(m.sharedMesh.name));
    }
    public static bool SoftCollider(Collider collider)
    {
        if(collider is TerrainCollider||collider.GetComponentInParent<Animator>()!=null)return false;
        var root=collider.transform;
        for(int i=0;i<3&&root!=null;i++,root=root.parent)
        {if(root.childCount>32)break;if(SoftObject(root.gameObject))return true;}
        return false;
    }
    public static string Apply()
    {
        var scene=BeachBoatToolSafety.Begin("PassableGrassAndBushes");int sceneCount=0,prefabCount=0;
        var paths=new HashSet<string>();
        foreach(var terrain in Terrain.activeTerrains)foreach(var prototype in terrain.terrainData.treePrototypes)
            if(prototype.prefab!=null&&SoftObject(prototype.prefab))paths.Add(AssetDatabase.GetAssetPath(prototype.prefab));
        foreach(var path in paths)
        {
            var prefab=PrefabUtility.LoadPrefabContents(path);bool changed=false;
            foreach(var collider in prefab.GetComponentsInChildren<Collider>(true))if(collider.enabled){collider.enabled=false;changed=true;prefabCount++;}
            if(changed)PrefabUtility.SaveAsPrefabAsset(prefab,path);PrefabUtility.UnloadPrefabContents(prefab);
        }
        foreach(var collider in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(collider.gameObject.scene==scene&&collider.enabled&&SoftCollider(collider)){Undo.RecordObject(collider,"Passable vegetation");collider.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(collider);sceneCount++;}
        BeachBoatToolSafety.Save(scene);
        return $"Disabled {sceneCount} scene vegetation colliders and {prefabCount} terrain prefab colliders; solid trees, rocks, buildings and stakes retained.";
    }
}
