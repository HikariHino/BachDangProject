using System;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Only nearby extra civilians animate; story and quest actors are never managed here.</summary>
    public sealed class AmbientPopulationBudget : MonoBehaviour
    {
        public static AmbientPopulationBudget Instance {get;private set;}
        public NPCWander[] citizens=Array.Empty<NPCWander>();
        public int maximumAnimated=28;
        public float activeDistance=550,visibleDistance=850;
        Animator[] animators;
        Renderer[][] renderers;
        NPCAmbientDialogue[] talkers;
        float[] distances;
        int[] order;
        int[] activeActors;
        float nextCheck;
        public int ActiveCount {get;private set;}
        void Awake(){Instance=this;}
        void OnDestroy(){if(Instance==this)Instance=null;}
        void Start()
        {
            int n=citizens.Length;animators=new Animator[n];renderers=new Renderer[n][];talkers=new NPCAmbientDialogue[n];distances=new float[n];order=new int[n];activeActors=new int[n];
            for(int i=0;i<n;i++){animators[i]=citizens[i].GetComponent<Animator>();renderers[i]=citizens[i].GetComponentsInChildren<Renderer>();talkers[i]=citizens[i].GetComponent<NPCAmbientDialogue>();order[i]=i;}
            Refresh();
        }
        void Update(){if(Time.unscaledTime>=nextCheck)Refresh();}
        public void Refresh()
        {
            if(animators==null)return;nextCheck=Time.unscaledTime+.6f;var camera=Camera.main;if(camera==null)return;
            for(int i=0;i<citizens.Length;i++)distances[i]=(citizens[i].transform.position-camera.transform.position).sqrMagnitude;
            Array.Sort(distances,order);ActiveCount=0;
            // Restore the distances by actor after sorting indices; no scene queries or allocations.
            for(int rank=0;rank<order.Length;rank++)
            {
                int i=order[rank];bool visible=distances[rank]<visibleDistance*visibleDistance;
                bool active=visible&&distances[rank]<activeDistance*activeDistance&&ActiveCount<maximumAnimated;
                if(active)activeActors[ActiveCount++]=i;
                citizens[i].enabled=active;talkers[i].enabled=active;animators[i].enabled=active;
                // Never leave an unanimated walking pose visible beyond the simulation budget.
                foreach(var renderer in renderers[i])renderer.enabled=active;
                order[rank]=rank;
            }
        }
        public Vector3 Separate(NPCWander actor,Vector3 heading)
        {
            var p=actor.transform.position;var separation=Vector3.zero;
            for(int n=0;n<ActiveCount;n++)
            {
                var other=citizens[activeActors[n]];if(other==actor||!other.isActiveAndEnabled)continue;
                var delta=p-other.transform.position;delta.y=0;float d=delta.sqrMagnitude;
                if(d>.01f&&d<49)separation+=delta.normalized*(1-Mathf.Sqrt(d)/7);
            }
            return (heading+separation*1.6f).normalized;
        }
    }
}
