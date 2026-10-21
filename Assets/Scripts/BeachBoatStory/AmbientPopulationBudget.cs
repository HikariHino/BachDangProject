using System;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Only nearby extra civilians animate; story and quest actors are never managed here.</summary>
    public sealed class AmbientPopulationBudget : MonoBehaviour
    {
        public NPCWander[] citizens=Array.Empty<NPCWander>();
        public int maximumAnimated=28;
        public float activeDistance=550,visibleDistance=850;
        Animator[] animators;
        Renderer[][] renderers;
        NPCAmbientDialogue[] talkers;
        float[] distances;
        int[] order;
        float nextCheck;
        public int ActiveCount {get;private set;}
        void Start()
        {
            int n=citizens.Length;animators=new Animator[n];renderers=new Renderer[n][];talkers=new NPCAmbientDialogue[n];distances=new float[n];order=new int[n];
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
                if(active)ActiveCount++;
                citizens[i].enabled=active;talkers[i].enabled=active;animators[i].enabled=active;
                foreach(var renderer in renderers[i])renderer.enabled=visible;
                order[rank]=rank;
            }
        }
    }
}
