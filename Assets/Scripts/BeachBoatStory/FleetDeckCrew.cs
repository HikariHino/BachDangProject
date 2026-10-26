using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Patrol crew stay on the moving deck before and during the battle.</summary>
    public sealed class FleetDeckCrew : MonoBehaviour
    {
        public Transform[] soldiers=System.Array.Empty<Transform>();
        Renderer[] renderers;
        float nextCheck;
        bool rosterReady;
        public void EnsureRoster()
        {
            if(rosterReady)return;rosterReady=true;
            var people=new System.Collections.Generic.List<Transform>(soldiers);
            foreach(var rig in GetComponentInParent<AmbientRiverBoat>().GetComponentsInChildren<Animator>(true))
                if(rig.isHuman&&!people.Exists(p=>rig.transform.IsChildOf(p)))people.Add(rig.transform);
            soldiers=people.ToArray();var reference=BeachBoatQuestSession.Instance.player.GetComponentInChildren<Animator>();
            var hull=BoatHullCollision.Get(GetComponentInParent<AmbientRiverBoat>().transform);
            foreach(var soldier in soldiers)
            {
                var rig=soldier.GetComponentInChildren<Animator>();if(rig==null||!rig.isHuman)continue;
                rig.enabled=true;rig.applyRootMotion=false;rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;rig.Update(0);
                rig.transform.localScale*=EnemyDeckGuard.RigHeight(reference)/Mathf.Max(.01f,EnemyDeckGuard.RigHeight(rig));rig.Update(0);
                if(hull.TryDeckSurface(soldier.position,out var surface))
                {ActorSolidBody.WalkingDimensions(rig,out _,out float sole,out _);soldier.position+=Vector3.up*(surface.y+.15f-rig.transform.position.y-sole);}
            }
        }
        void Start()
        {
            EnsureRoster();
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
            foreach(var renderer in renderers??System.Array.Empty<Renderer>())if(renderer!=null)renderer.enabled=visible;
        }
    }
}
