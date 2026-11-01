using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>Retain original finger topology when nearby actors grip weapons.</summary>
    public sealed class CombatHandSources:ScriptableObject
    {
        [System.Serializable]public sealed class Source {public Mesh original;public Mesh[] crowd;}
        public Source[] sources;
        static CombatHandSources profile;
        public static bool Crowd(Mesh mesh,out Mesh close,out Mesh middle,out Mesh far)
        {
            close=middle=far=null;if(profile==null)profile=Resources.Load<CombatHandSources>("BachDang938/Player/CombatHandSources");
            if(profile==null||mesh==null)return false;mesh=HumanoidGripMesh.SourceMesh(mesh);
            foreach(var source in profile.sources)if(source.crowd!=null&&source.crowd.Length>=3&&(source.original==mesh||System.Array.IndexOf(source.crowd,mesh)>=0))
            {close=source.crowd[0];middle=source.crowd[1];far=source.crowd[2];return close!=null&&middle!=null&&far!=null;}return false;
        }
        public static Mesh Original(Mesh mesh)
        {
            if(profile==null)profile=Resources.Load<CombatHandSources>("BachDang938/Player/CombatHandSources");
            if(profile!=null)foreach(var source in profile.sources)if(source.original!=null&&System.Array.IndexOf(source.crowd,mesh)>=0)return source.original;
            return mesh;
        }
    }
}
