using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>Retain original finger topology when nearby actors grip weapons.</summary>
    public sealed class CombatHandSources:ScriptableObject
    {
        [System.Serializable]public sealed class Source {public Mesh original;public Mesh[] crowd;}
        public Source[] sources;
        static CombatHandSources profile;
        public static Mesh Original(Mesh mesh)
        {
            if(profile==null)profile=Resources.Load<CombatHandSources>("BachDang938/Player/CombatHandSources");
            if(profile!=null)foreach(var source in profile.sources)if(source.original!=null&&System.Array.IndexOf(source.crowd,mesh)>=0)return source.original;
            return mesh;
        }
    }
}
