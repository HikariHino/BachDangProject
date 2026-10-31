using System.Collections.Generic;
using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>Closed hand surfaces for imported avatars without finger joints.</summary>
    public static class HumanoidGripMesh
    {
        sealed class Entry {public Mesh source,posed;}
        sealed class Hands {public GameObject left,right;}
        static readonly Dictionary<Mesh,Mesh> originals=new Dictionary<Mesh,Mesh>();
        static readonly Dictionary<string,Entry> variants=new Dictionary<string,Entry>();
        static readonly Dictionary<Animator,Hands> hands=new Dictionary<Animator,Hands>();
        static Material material;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){originals.Clear();variants.Clear();hands.Clear();material=null;}
        public static void Apply(Animator rig,HumanoidHandGrip left,HumanoidHandGrip right,float lw,float rw)
        {
            if(rig.GetBoneTransform(HumanBodyBones.RightIndexProximal)!=null)return;
            if(!hands.TryGetValue(rig,out var pair))
            {
                if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Weapon hand skin",hideFlags=HideFlags.DontSave};material.SetColor("_BaseColor",new Color(.39f,.25f,.18f));material.SetFloat("_Smoothness",.24f);}
                pair=new Hands{left=Build(rig.GetBoneTransform(HumanBodyBones.LeftHand),left,"Left_Closed_Weapon_Hand"),right=Build(rig.GetBoneTransform(HumanBodyBones.RightHand),right,"Right_Closed_Weapon_Hand")};hands[rig]=pair;
            }
            bool l=lw>.05f,r=rw>.05f;pair.left.SetActive(l);pair.right.SetActive(r);
            foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var source=skin.sharedMesh;if(source==null||!source.isReadable)continue;
                if(originals.TryGetValue(source,out var original))source=original;
                if(Camera.main!=null&&(rig.transform.position-Camera.main.transform.position).sqrMagnitude<140*140)source=CombatHandSources.Original(source);
                string suffix=l&&r?"Both":l?"Left":r?"Right":"None",key=source.GetEntityId()+"/"+suffix;
                if(!variants.TryGetValue(key,out var entry))
                {
                    var mesh=Object.Instantiate(source);mesh.name=source.name+"_Weapon_"+suffix;mesh.hideFlags=HideFlags.DontSave;var vertices=source.vertices;
                    int li=System.Array.IndexOf(skin.bones,rig.GetBoneTransform(HumanBodyBones.LeftHand)),ri=System.Array.IndexOf(skin.bones,rig.GetBoneTransform(HumanBodyBones.RightHand));var bind=source.bindposes;
                    bool InHand(Vector3 point,int index,HumanoidHandGrip frame)
                    {if(index<0)return false;var p=bind[index].MultiplyPoint3x4(point)/frame.Length;return Vector3.Dot(p,frame.Forward)>.22f&&Vector3.Dot(p,frame.Forward)<1.4f&&Mathf.Abs(Vector3.Dot(p,frame.Across))<.85f&&Mathf.Abs(Vector3.Dot(p,frame.Palm))<.6f;}
                    for(int sub=0;sub<source.subMeshCount;sub++)
                    {
                        var tris=source.GetTriangles(sub);var kept=new List<int>(tris.Length);
                        for(int i=0;i<tris.Length;i+=3)
                        {var c=(vertices[tris[i]]+vertices[tris[i+1]]+vertices[tris[i+2]])/3;if(l&&InHand(c,li,left)||r&&InHand(c,ri,right))continue;kept.Add(tris[i]);kept.Add(tris[i+1]);kept.Add(tris[i+2]);}
                        mesh.SetTriangles(kept,sub,false);
                    }
                    entry=new Entry{source=source,posed=mesh};variants[key]=entry;originals[mesh]=source;
                }
                skin.sharedMesh=entry.posed;
            }
        }
        public static void Restore(Animator rig)
        {
            if(hands.TryGetValue(rig,out var pair)){if(pair.left!=null)pair.left.SetActive(false);if(pair.right!=null)pair.right.SetActive(false);}
            foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(skin.sharedMesh!=null&&originals.TryGetValue(skin.sharedMesh,out var original))skin.sharedMesh=original;
        }
        static GameObject Build(Transform bone,HumanoidHandGrip frame,string name)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(bone,false);go.GetComponent<MeshRenderer>().sharedMaterial=material;
            var vertices=new List<Vector3>();var triangles=new List<int>();var normals=new List<Vector3>();float unit=frame.Length;var f=frame.Forward;var a=frame.Across;var n=frame.Palm;var centre=f*(unit*.64f)+n*(unit*.16f);
            void Tube(List<Vector3> points,List<float> widths)
            {
                const int sides=10;int start=vertices.Count;
                for(int j=0;j<points.Count;j++)
                {
                    var tangent=(points[Mathf.Min(j+1,points.Count-1)]-points[Mathf.Max(0,j-1)]).normalized;var side=Vector3.Cross(tangent,a).normalized;if(side.sqrMagnitude<.1f)side=f;var up=Vector3.Cross(tangent,side).normalized;
                    for(int k=0;k<sides;k++){float angle=k*Mathf.PI*2/sides;var direction=side*Mathf.Cos(angle)+up*Mathf.Sin(angle);vertices.Add(points[j]+direction*widths[j]);normals.Add(direction);}
                }
                for(int j=0;j<points.Count-1;j++)for(int k=0;k<sides;k++){int x=start+j*sides+k,y=start+j*sides+(k+1)%sides;triangles.Add(x);triangles.Add(y);triangles.Add(x+sides);triangles.Add(y);triangles.Add(y+sides);triangles.Add(x+sides);}
                for(int k=1;k<sides-1;k++){triangles.Add(start);triangles.Add(start+k+1);triangles.Add(start+k);int end=start+(points.Count-1)*sides;triangles.Add(end);triangles.Add(end+k);triangles.Add(end+k+1);}
            }
            for(int finger=0;finger<4;finger++)
            {
                var p=new List<Vector3>();var w=new List<float>();float offset=(finger-1.5f)*unit*.145f;
                for(int j=0;j<=18;j++){float t=j/18f,angle=Mathf.Lerp(-130,155,t)*Mathf.Deg2Rad,radius=unit*(finger==3?.135f:.15f);p.Add(centre+a*offset+f*(Mathf.Cos(angle)*radius)+n*(Mathf.Sin(angle)*radius));w.Add(unit*Mathf.Lerp(.073f,.052f,t)*(j==18?.55f:1));}Tube(p,w);
            }
            for(int strip=0;strip<4;strip++)
            {
                var p=new List<Vector3>();var w=new List<float>();float offset=(strip-1.5f)*unit*.145f;
                for(int j=0;j<8;j++){float t=j/7f;p.Add(f*(unit*Mathf.Lerp(-.02f,.58f,t))+a*offset-n*(unit*.015f));w.Add(unit*Mathf.Lerp(.085f,.10f,t));}Tube(p,w);
            }
            var thumb=new List<Vector3>();var widths=new List<float>();
            for(int j=0;j<12;j++){float t=j/11f;thumb.Add(centre+a*(unit*Mathf.Lerp(.36f,.04f,t))+f*(unit*Mathf.Lerp(-.12f,.08f,t))+n*(unit*(.06f+Mathf.Sin(t*Mathf.PI)*.13f)));widths.Add(unit*Mathf.Lerp(.095f,.068f,t));}Tube(thumb,widths);
            var meshResult=new Mesh{name=name,hideFlags=HideFlags.DontSave};meshResult.SetVertices(vertices);meshResult.SetNormals(normals);meshResult.SetTriangles(triangles,0);meshResult.RecalculateBounds();go.GetComponent<MeshFilter>().sharedMesh=meshResult;return go;
        }
    }
}
