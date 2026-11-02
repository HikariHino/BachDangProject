using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Cached convex skin envelopes fitted to the real armor, animated by the same bones.</summary>
    public sealed class DuelArmorContact
    {
        sealed class Part
        {
            public Transform bone;public Bounds bounds;public readonly float[] support=new float[Directions.Length];public Matrix4x4 previous;public bool hasPoint;
            public Part(Transform bone){this.bone=bone;for(int i=0;i<support.Length;i++)support[i]=float.NegativeInfinity;}
            public void Add(Vector3 p){if(!hasPoint){bounds=new Bounds(p,Vector3.zero);hasPoint=true;}else bounds.Encapsulate(p);for(int i=0;i<support.Length;i++)support[i]=Mathf.Max(support[i],Vector3.Dot(Directions[i],p));}
        }
        static readonly Vector3[] Directions=MakeDirections();
        static Vector3[] MakeDirections(){var result=new List<Vector3>();for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(x!=0||y!=0||z!=0)result.Add(new Vector3(x,y,z).normalized);return result.ToArray();}
        readonly Part[] parts;readonly Vector3[] scratchA=new Vector3[64],scratchB=new Vector3[64];
        public bool Ready=>parts.Length>0;
        public DuelArmorContact(Animator rig)
        {
            var groups=new Dictionary<Transform,Part>();
            foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=skin.sharedMesh;var lod=skin.GetComponentInParent<CinematicCharacterLOD>();if(lod!=null&&lod.target==skin&&lod.closeMesh!=null)mesh=lod.closeMesh;
                mesh=HumanoidGripMesh.SourceMesh(mesh);if(mesh==null||!mesh.isReadable)continue;
                var vertices=mesh.vertices;var weights=mesh.boneWeights;var binds=mesh.bindposes;var bones=skin.bones;
                if(weights.Length!=vertices.Length||binds.Length!=bones.Length)continue;
                for(int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];int index=w.boneIndex0;float weight=w.weight0;
                    if(w.weight1>weight){weight=w.weight1;index=w.boneIndex1;}if(w.weight2>weight){weight=w.weight2;index=w.boneIndex2;}if(w.weight3>weight){weight=w.weight3;index=w.boneIndex3;}
                    if(weight<=0||index>=bones.Length||bones[index]==null)continue;var bone=bones[index];
                    if(!groups.TryGetValue(bone,out var part))groups[bone]=part=new Part(bone);
                    part.Add(binds[index].MultiplyPoint3x4(vertices[i]));
                }
            }
            parts=new List<Part>(groups.Values).ToArray();CapturePose();
        }
        public void CapturePose(){foreach(var part in parts)if(part.bone!=null)part.previous=part.bone.worldToLocalMatrix;}
        bool ClipTriangle(Vector3 a,Vector3 b,Vector3 c,Part part,float padding,out Vector3 contact)
        {
            var from=scratchA;var to=scratchB;from[0]=a;from[1]=b;from[2]=c;int count=3;
            for(int plane=0;plane<Directions.Length;plane++)
            {
                var normal=Directions[plane];float limit=part.support[plane]+padding;int output=0;var previous=from[count-1];float pd=Vector3.Dot(normal,previous)-limit;
                for(int i=0;i<count;i++)
                {
                    var current=from[i];float cd=Vector3.Dot(normal,current)-limit;
                    if((pd<=0)!=(cd<=0))to[output++]=Vector3.LerpUnclamped(previous,current,pd/(pd-cd));
                    if(cd<=0)to[output++]=current;previous=current;pd=cd;
                }
                if(output==0){contact=default;return false;}var swap=from;from=to;to=swap;count=output;
            }
            contact=Vector3.zero;for(int i=0;i<count;i++)contact+=from[i];contact/=count;return true;
        }
        public bool Touches(Vector3 oldRoot,Vector3 oldTip,Vector3 root,Vector3 tip,float radius,bool movingTarget,out Vector3 contact,float targetFrom=0,float targetTo=1)
        {
            foreach(var part in parts)
            {
                var inverse=part.bone.worldToLocalMatrix;var previous=movingTarget?part.previous:inverse;
                var a=Vector3.Lerp(previous.MultiplyPoint3x4(oldRoot),inverse.MultiplyPoint3x4(oldRoot),targetFrom);var b=Vector3.Lerp(previous.MultiplyPoint3x4(oldTip),inverse.MultiplyPoint3x4(oldTip),targetFrom);var c=Vector3.Lerp(previous.MultiplyPoint3x4(tip),inverse.MultiplyPoint3x4(tip),targetTo);var d=Vector3.Lerp(previous.MultiplyPoint3x4(root),inverse.MultiplyPoint3x4(root),targetTo);
                var scale=part.bone.lossyScale;float padding=(radius+.2f)/Mathf.Max(.000001f,Mathf.Min(Mathf.Abs(scale.x),Mathf.Min(Mathf.Abs(scale.y),Mathf.Abs(scale.z))));
                var sweep=new Bounds(a,Vector3.zero);sweep.Encapsulate(b);sweep.Encapsulate(c);sweep.Encapsulate(d);sweep.Expand(padding*2);if(!sweep.Intersects(part.bounds))continue;
                if(ClipTriangle(a,b,c,part,padding,out var local)||ClipTriangle(a,c,d,part,padding,out local)){contact=part.bone.TransformPoint(local);return true;}
            }
            contact=default;return false;
        }
    }
}
