using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Small skinned boot envelopes; avoids baking an entire character mesh every frame.</summary>
    public static class HumanoidFootSupport
    {
        sealed class Profile
        {
            public Vector3[] points;public BoneWeight[] weights;public Matrix4x4[] binds,world;public Transform[] bones;public int[] used;
        }
        static readonly Dictionary<SkinnedMeshRenderer,Dictionary<Mesh,Profile>> profiles=new Dictionary<SkinnedMeshRenderer,Dictionary<Mesh,Profile>>();
        static readonly Dictionary<Animator,SkinnedMeshRenderer[]> skins=new Dictionary<Animator,SkinnedMeshRenderer[]>();
        static readonly Vector3[] directions=Directions();
        static Vector3[] Directions(){var result=new List<Vector3>();for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(x!=0||y!=0||z!=0)result.Add(new Vector3(x,y,z));return result.ToArray();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){profiles.Clear();skins.Clear();}
        static SkinnedMeshRenderer[] Skins(Animator rig){if(!skins.TryGetValue(rig,out var result))skins[rig]=result=rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);return result;}
        static Profile Get(Animator rig,SkinnedMeshRenderer skin,Mesh mesh)
        {
            if(mesh==null||!mesh.isReadable)return null;mesh=HumanoidGripMesh.SourceMesh(mesh);
            if(!profiles.TryGetValue(skin,out var entries))profiles[skin]=entries=new Dictionary<Mesh,Profile>();
            if(entries.TryGetValue(mesh,out var profile))return profile;
            var bones=skin.bones;var binds=mesh.bindposes;var vertices=mesh.vertices;var weights=mesh.boneWeights;
            if(weights.Length!=vertices.Length||binds.Length!=bones.Length)return null;
            var eligible=new bool[bones.Length];
            for(int i=0;i<bones.Length;i++)foreach(var id in new[]{HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
            {var foot=rig.GetBoneTransform(id);if(foot!=null&&bones[i]!=null&&(bones[i]==foot||bones[i].IsChildOf(foot)))eligible[i]=true;}
            var best=new Dictionary<int,float[]>();var indices=new Dictionary<int,int[]>();
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];int bone=-1;float weight=.15f;
                void Consider(int b,float value){if(b>=0&&b<eligible.Length&&eligible[b]&&value>weight){bone=b;weight=value;}}
                Consider(w.boneIndex0,w.weight0);Consider(w.boneIndex1,w.weight1);Consider(w.boneIndex2,w.weight2);Consider(w.boneIndex3,w.weight3);if(bone<0)continue;
                if(!best.TryGetValue(bone,out var values)){best[bone]=values=new float[directions.Length];indices[bone]=new int[directions.Length];for(int j=0;j<values.Length;j++)values[j]=float.NegativeInfinity;}
                var local=binds[bone].MultiplyPoint3x4(vertices[i]);for(int j=0;j<directions.Length;j++){float value=Vector3.Dot(local,directions[j]);if(value>values[j]){values[j]=value;indices[bone][j]=i;}}
            }
            var selected=new HashSet<int>();foreach(var list in indices.Values)foreach(int i in list)selected.Add(i);
            var points=new List<Vector3>();var selectedWeights=new List<BoneWeight>();var used=new HashSet<int>();
            foreach(int i in selected){points.Add(vertices[i]);var w=weights[i];selectedWeights.Add(w);if(w.weight0>0)used.Add(w.boneIndex0);if(w.weight1>0)used.Add(w.boneIndex1);if(w.weight2>0)used.Add(w.boneIndex2);if(w.weight3>0)used.Add(w.boneIndex3);}
            var ids=new int[used.Count];used.CopyTo(ids);profile=new Profile{points=points.ToArray(),weights=selectedWeights.ToArray(),binds=binds,bones=bones,world=new Matrix4x4[bones.Length],used=ids};entries[mesh]=profile;return profile;
        }
        public static void Prepare(Animator rig)
        {
            foreach(var skin in Skins(rig))
            {
                Get(rig,skin,skin.sharedMesh);var lod=skin.GetComponentInParent<CinematicCharacterLOD>();if(lod!=null){Get(rig,skin,lod.closeMesh);Get(rig,skin,lod.middleMesh);Get(rig,skin,lod.farMesh);}
                Get(rig,skin,CombatHandSources.Original(HumanoidGripMesh.SourceMesh(skin.sharedMesh)));
            }
        }
        public static bool Lowest(Animator rig,out float lowest)
        {
            lowest=float.PositiveInfinity;
            foreach(var skin in Skins(rig))
            {
                var p=Get(rig,skin,skin.sharedMesh);if(p==null)continue;
                foreach(int i in p.used)if(p.bones[i]!=null)p.world[i]=p.bones[i].localToWorldMatrix*p.binds[i];
                for(int i=0;i<p.points.Length;i++)
                {
                    var v=p.points[i];var w=p.weights[i];float y=0;
                    if(w.weight0>0)y+=p.world[w.boneIndex0].MultiplyPoint3x4(v).y*w.weight0;if(w.weight1>0)y+=p.world[w.boneIndex1].MultiplyPoint3x4(v).y*w.weight1;
                    if(w.weight2>0)y+=p.world[w.boneIndex2].MultiplyPoint3x4(v).y*w.weight2;if(w.weight3>0)y+=p.world[w.boneIndex3].MultiplyPoint3x4(v).y*w.weight3;
                    lowest=Mathf.Min(lowest,y);
                }
            }
            return !float.IsPositiveInfinity(lowest);
        }
    }
}
