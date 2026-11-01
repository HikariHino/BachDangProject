using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BeachBoatStakeMeshSetup
{
    public static void Build()
    {
        const string path="Assets/Resources/BachDang938/Wrecks/FieldStake.asset";
        var source=AssetDatabase.LoadAllAssetsAtPath("Assets/wood-spike/source/Wood_Spike_02_sculpted.fbx").OfType<Mesh>().First(m=>m.name=="Wood_Spike_02_base");
        var simplify=new UnityMeshSimplifier.MeshSimplifier();var options=UnityMeshSimplifier.SimplificationOptions.Default;
        options.PreserveBorderEdges=true;options.PreserveUVSeamEdges=false;simplify.SimplificationOptions=options;simplify.Initialize(source);simplify.SimplifyMesh(.025f);
        var mesh=simplify.ToMesh();mesh.name="FieldStake";mesh.RecalculateBounds();
        if(mesh.vertexCount<16||mesh.vertexCount>source.vertexCount/8)throw new System.InvalidOperationException("Unexpected stake reduction: "+mesh.vertexCount);
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null){EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);}else AssetDatabase.CreateAsset(mesh,path);
        AssetDatabase.SaveAssets();Debug.Log("Stake mesh prepared: "+source.vertexCount+" -> "+AssetDatabase.LoadAssetAtPath<Mesh>(path).vertexCount);
    }
}
