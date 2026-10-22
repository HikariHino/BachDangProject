using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Patrol crew stay on the moving deck before and during the battle.</summary>
    public sealed class FleetDeckCrew : MonoBehaviour
    {
        public Transform[] soldiers=System.Array.Empty<Transform>();
        Renderer[] renderers;
        float nextCheck;
        void Start()
        {
            renderers=GetComponentsInChildren<Renderer>();
            foreach(var soldier in soldiers)
            {
                var animator=soldier.GetComponentInChildren<Animator>();
                if(animator==null)continue;
                animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                animator.Update(0);animator.enabled=false;
            }
        }
        void Update()
        {
            if(Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+.5f;
            var player=BeachBoatQuestSession.Instance?.player;if(player==null)return;
            bool visible=(player.transform.position-transform.position).sqrMagnitude<1800f*1800;
            foreach(var renderer in renderers)renderer.enabled=visible;
        }
    }
}
