using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Small, closed plank hull authored in world units for the existing scaled player.
// Generates two shared meshes; no textures, runtime mesh generation or MeshColliders.
public static class BeachBoatRowboatPolish
{
    const string Folder = "Assets/BeachBoatStory/Rowboat";
    static readonly float[] Z = {-40,-36,-29,-20,0,20,29,36,40};
    static readonly float[] W = {0.4f,4.5f,9,12.5f,13.5f,12.5f,9,4.5f,0.4f};
    [MenuItem("Bach Dang/Polish Quest Rowboat")]
    public static void Apply()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(Application.isPlaying || scene.path!="Assets/Scenes/beachBoat.unity" || scene.isDirty)
            throw new InvalidOperationException("Open and save beachBoat in Edit Mode first.");
        var boat=UnityEngine.Object.FindAnyObjectByType<BeachBoatStory.QuestRowboat>();
        if(boat==null)throw new InvalidOperationException("Quest boat not installed.");
        System.IO.Directory.CreateDirectory(".utmp/RowingPolish");
        if(!EditorSceneManager.SaveScene(scene,".utmp/RowingPolish/before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true))throw new Exception("Backup failed.");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/BeachBoatStory","Rowboat");
        var wood=MaterialAsset("Song_Wood",new Color(1f,.86f,.65f));
        var trim=MaterialAsset("Song_DarkTrim",new Color(.44f,.32f,.22f));
        var light=MaterialAsset("Song_EndGrain",new Color(.8f,.67f,.5f));
        var materials=new[]{wood,trim,light};
        var mesh=new Builder();
        // Outside and inside planks with a capped gunwale and closed keel.
        for(int i=0;i<Z.Length-1;i++)
        {
            for(int side=-1;side<=1;side+=2)
            {
                for(int band=0;band<4;band++)
                {
                    var a=Hull(i,band/4f,side,false);var b=Hull(i+1,band/4f,side,false);
                    var c=Hull(i+1,(band+1)/4f,side,false);var d=Hull(i,(band+1)/4f,side,false);
                    mesh.Quad(a,b,c,d,band%3==0?2:0,true);
                    mesh.Quad(Hull(i,band/4f,side,true),Hull(i+1,band/4f,side,true),Hull(i+1,(band+1)/4f,side,true),Hull(i,(band+1)/4f,side,true),0,true);
                    if(band>0)mesh.Beam(a,b,.13f,.13f,1);
                }
                mesh.Beam(Hull(i,1,side,false),Hull(i+1,1,side,false),.95f,.8f,1);
            }
            mesh.Quad(Hull(i,0,-1,false),Hull(i+1,0,-1,false),Hull(i+1,0,1,false),Hull(i,0,1,false),1,true);
            mesh.Quad(Floor(i,-1),Floor(i+1,-1),Floor(i+1,1),Floor(i,1),1,true);
        }
        // Narrow end decks, deck boards, benches and internal ribs.
        for(int i=0;i<15;i++)
        {
            float z=-33+i*4.7f;float width=WidthAt(z)*1.45f;
            float y=2.5f+Mathf.Pow(Mathf.Abs(z)/40,4)*5;
            mesh.Box(new Vector3(0,y,z),new Vector3(width,.6f,4.48f),Quaternion.identity,i%3==0?2:0);
        }
        foreach(float z in new[]{-25f,-12f,7f,25f})
        {
            float width=WidthAt(z)*1.8f;
            mesh.Box(new Vector3(0,5.8f,z),new Vector3(width,1,3.8f),Quaternion.identity,0);
            for(int side=-1;side<=1;side+=2)
                mesh.Box(new Vector3(side*(width/2-1.5f),2.6f,z),new Vector3(1.1f,5.4f,1.5f),Quaternion.identity,1);
        }
        for(int side=-1;side<=1;side+=2)
            foreach(float z in new[]{-29f,-20f,0f,20f,29f})
                mesh.Beam(new Vector3(side*WidthAt(z)*.45f,-2,z),new Vector3(side*(WidthAt(z)-.8f),5.6f,z),.65f,.7f,1);
        var hull=SaveMesh(mesh.Make("Thuyen_Song_Closed_Planks"),"RiverHull.asset");
        var paddleMesh=new Builder();
        paddleMesh.Box(new Vector3(0,-10.2f,0),new Vector3(.52f,20.4f,.52f),Quaternion.identity,0);
        // Tapered blade, broad below the waterline, narrow at its neck.
        paddleMesh.Quad(new Vector3(-.3f,-19,0),new Vector3(.3f,-19,0),new Vector3(1.6f,-25.8f,0),new Vector3(-1.6f,-25.8f,0),0,true);
        paddleMesh.Box(new Vector3(0,-25.5f,0),new Vector3(3.1f,.65f,.22f),Quaternion.identity,1);
        paddleMesh.Box(Vector3.zero,new Vector3(2,.5f,.6f),Quaternion.identity,1);
        var paddleAsset=SaveMesh(paddleMesh.Make("Mai_Cheo_Go"),"RiverPaddle.asset");
        Undo.RecordObject(boat,"Polish rowing quest");
        foreach(string name in new[]{"Thuyen_Nhe_12k_Vertices","Ghe_Ngoi_Cheo","Thuyen_Go_Song"})
        {var t=boat.transform.Find(name);if(t!=null)Undo.DestroyObjectImmediate(t.gameObject);}
        if(boat.paddle!=null)Undo.DestroyObjectImmediate(boat.paddle.gameObject);
        var model=MeshObject("Thuyen_Go_Song",boat.transform,hull,materials);
        boat.paddle=MeshObject("Mai_Cheo",boat.transform,paddleAsset,materials).transform;
        boat.paddle.localPosition=new Vector3(9,7,-12);boat.paddle.localRotation=Quaternion.Euler(0,0,-65);
        boat.tide=UnityEngine.Object.FindAnyObjectByType<TideSystem>();
        Undo.RecordObject(boat.seat,"Match seating to bench");boat.seat.localPosition=new Vector3(7,0,-12);
        var body=boat.GetComponent<Rigidbody>();Undo.RecordObject(body,"Avoid competing boat interpolation");body.interpolation=RigidbodyInterpolation.None;
        EditorUtility.SetDirty(boat);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed.");
        Debug.Log($"River boat: {hull.vertexCount} hull vertices, {paddleAsset.vertexCount} paddle vertices; shared wood texture.");
    }
    static Vector3 Hull(int i,float t,int side,bool inner)
    {
        float rise=Mathf.Pow(Mathf.Abs(Z[i])/40,4)*5;
        float width=W[i]*Mathf.Lerp(.42f,1,t)-(inner?.65f:0);
        return new Vector3(side*Mathf.Max(.05f,width),Mathf.Lerp(-5,6,t)+rise+(inner?.6f:0),Z[i]);
    }
    static float WidthAt(float z)
    {for(int i=0;i<Z.Length-1;i++)if(z<=Z[i+1])return Mathf.Lerp(W[i],W[i+1],Mathf.InverseLerp(Z[i],Z[i+1],z));return W[W.Length-1];}
    static Vector3 Floor(int i,int side) => new Vector3(side*W[i]*.72f,2.1f+Mathf.Pow(Mathf.Abs(Z[i])/40,4)*5,Z[i]);
    static Material MaterialAsset(string name,Color color)
    {
        string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Props/Materials/wood_tiled_02_a.mat");
        mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.22f);mat.enableInstancing=true;
        if(source!=null){var texture=source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):source.mainTexture;mat.SetTexture("_BaseMap",texture);}
        EditorUtility.SetDirty(mat);return mat;
    }
    static Mesh SaveMesh(Mesh mesh,string file)
    {string path=Folder+"/"+file;var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;}
    static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material[] materials)
    {var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Replace quest boat visuals");go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=materials;return go;}
    sealed class Builder
    {
        readonly List<Vector3> vertices=new List<Vector3>();readonly List<Vector2> uv=new List<Vector2>();
        readonly List<int>[] triangles={new List<int>(),new List<int>(),new List<int>()};
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material,bool twoSided=false)
        {
            int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});float u=Vector3.Distance(a,b)/8,v=Vector3.Distance(b,c)/8;
            uv.AddRange(new[]{Vector2.zero,new Vector2(u,0),new Vector2(u,v),new Vector2(0,v)});
            triangles[material].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            if(twoSided)Quad(d,c,b,a,material);
        }
        public void Box(Vector3 center,Vector3 size,Quaternion rotation,int material)
        {
            var p=new Vector3[8];for(int i=0;i<8;i++)p[i]=center+rotation*Vector3.Scale(size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            Quad(p[0],p[2],p[3],p[1],material);Quad(p[4],p[5],p[7],p[6],material);Quad(p[0],p[4],p[6],p[2],material);
            Quad(p[1],p[3],p[7],p[5],material);Quad(p[2],p[6],p[7],p[3],material);Quad(p[0],p[1],p[5],p[4],material);
        }
        public void Beam(Vector3 a,Vector3 b,float width,float depth,int material)
        {Box((a+b)*.5f,new Vector3(width,depth,Vector3.Distance(a,b)),Quaternion.LookRotation(b-a),material);}
        public Mesh Make(string name)
        {var m=new Mesh{name=name,subMeshCount=3};m.SetVertices(vertices);m.SetUVs(0,uv);for(int i=0;i<3;i++)m.SetTriangles(triangles[i],i);m.RecalculateNormals();m.RecalculateBounds();return m;}
    }
}
