using System.Collections.Generic;
using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>A tapered steel handsaw, individual teeth and a solid wooden handle with a real finger opening.</summary>
    public sealed class VillageHandSaw:MonoBehaviour
    {
        public const int Teeth=22;
        readonly List<Mesh> meshes=new List<Mesh>();
        public Vector3 HandleOffset=>transform.TransformVector(new Vector3(0,.2f,-5.45f));
        public Vector3 HandleContact=>transform.TransformPoint(new Vector3(0,.2f,-5.45f));
        public Vector3 HandleAxis=>transform.up;
        public Mesh BladeMesh {get;private set;}
        public static VillageHandSaw Create(Transform parent,Material wood,Material steel)
        {
            var root=new GameObject("Hand_saw");root.transform.SetParent(parent,false);var saw=root.AddComponent<VillageHandSaw>();saw.Build(wood,steel);return saw;
        }
        void Build(Material wood,Material steel)
        {
            steel.SetFloat("_Metallic",.82f);steel.SetFloat("_Smoothness",.42f);
            var outline=new List<Vector2>{new Vector2(-3.35f,2.2f),new Vector2(5.3f,1.4f),new Vector2(5.5f,-.55f)};
            for(int i=0;i<=Teeth;i++){float z=Mathf.Lerp(5.3f,-3.3f,i/(float)Teeth);outline.Add(new Vector2(z,-.65f));if(i<Teeth)outline.Add(new Vector2(z-.18f,-1.1f));}
            var verts=new List<Vector3>();var tris=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){int n=verts.Count;verts.AddRange(new[]{a,b,c,d});tris.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
            for(int i=0;i<outline.Count;i++)
            {
                var a=outline[i];var b=outline[(i+1)%outline.Count];Quad(new Vector3(-.12f,a.y,a.x),new Vector3(.12f,a.y,a.x),new Vector3(.12f,b.y,b.x),new Vector3(-.12f,b.y,b.x));
                foreach(int side in new[]{-1,1}){int n=verts.Count;verts.AddRange(new[]{new Vector3(side*.12f,.1f,0),new Vector3(side*.12f,a.y,a.x),new Vector3(side*.12f,b.y,b.x)});if(side>0)tris.AddRange(new[]{n,n+1,n+2});else tris.AddRange(new[]{n,n+2,n+1});}
            }
            BladeMesh=Mesh("Tapered serrated steel blade",verts,tris);Part("Steel_blade_with_22_teeth",BladeMesh,steel);
            verts.Clear();tris.Clear();
            Vector3 P(int i,float x,bool inner){float a=i*Mathf.PI/16;return new Vector3(x,.2f+Mathf.Sin(a)*(inner?1.08f:1.7f),-4.35f+Mathf.Cos(a)*(inner?.65f:1.35f));}
            for(int i=0;i<32;i++)
            {
                Quad(P(i,-.45f,false),P(i+1,-.45f,false),P(i+1,.45f,false),P(i,.45f,false));
                Quad(P(i,.45f,true),P(i+1,.45f,true),P(i+1,-.45f,true),P(i,-.45f,true));
                foreach(int side in new[]{-1,1})Quad(P(i,side*.45f,false),P(i,side*.45f,true),P(i+1,side*.45f,true),P(i+1,side*.45f,false));
            }
            Part("Saw_wooden_handle",Mesh("Open wooden D grip",verts,tris),wood);
            var bar=GameObject.CreatePrimitive(PrimitiveType.Cylinder);bar.name="Palm_grip";bar.transform.SetParent(transform,false);bar.transform.localPosition=new Vector3(0,.2f,-5.45f);bar.transform.localScale=new Vector3(1.1f,1.1f,1.1f);bar.GetComponent<Renderer>().sharedMaterial=wood;bar.GetComponent<Collider>().enabled=false;Destroy(bar.GetComponent<Collider>());
            for(int i=0;i<2;i++)foreach(int side in new[]{-1,1}){var pin=GameObject.CreatePrimitive(PrimitiveType.Sphere);pin.name="Handle_rivet";pin.transform.SetParent(transform,false);pin.transform.localPosition=new Vector3(side*.48f,-.4f+i*1.15f,-3.25f);pin.transform.localScale=Vector3.one*.26f;pin.GetComponent<Renderer>().sharedMaterial=steel;pin.GetComponent<Collider>().enabled=false;Destroy(pin.GetComponent<Collider>());}
        }
        Mesh Mesh(string name,List<Vector3> v,List<int> t){var m=new Mesh{name=name};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();meshes.Add(m);return m;}
        void Part(string name,Mesh mesh,Material material){var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(transform,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=material;}
        void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}
