using UnityEngine;

namespace BeachBoatStory
{
    public sealed class CivilianMovementProfile : ScriptableObject
    {
        public Avatar[] avatars;
        public RuntimeAnimatorController controller;
        public AnimationClip walking;
        static CivilianMovementProfile cached;
        static bool loaded;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){cached=null;loaded=false;}
        public static void Apply(Animator rig)
        {
            if(!loaded){cached=Resources.Load<CivilianMovementProfile>("BachDang938/CivilianMovement");loaded=true;}
            if(cached==null||cached.controller==null)return;
            foreach(var avatar in cached.avatars)if(rig.avatar==avatar){rig.runtimeAnimatorController=cached.controller;return;}
        }
    }
}
