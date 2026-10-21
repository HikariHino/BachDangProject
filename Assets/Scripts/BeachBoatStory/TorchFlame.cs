using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Shared torch effects with a scene-wide budget of eight nearby, shadowless lights.</summary>
    [DisallowMultipleComponent]
    public sealed class TorchFlame : MonoBehaviour
    {
        public ParticleSystem flame, embers;
        public Light fireLight;
        public float baseIntensity=80;
        public float flameDistance=850, lightDistance=180;
        const int LightBudget=8;
        static readonly List<TorchFlame> All=new List<TorchFlame>();
        static readonly List<TorchFlame> Nearest=new List<TorchFlame>(LightBudget);
        static readonly List<float> Distances=new List<float>(LightBudget);
        static float nextRefresh;
        bool emitting, initialized;
        float phase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){All.Clear();Nearest.Clear();Distances.Clear();nextRefresh=0;}
        void OnEnable()
        {
            if(!All.Contains(this))All.Add(this);
            phase=Mathf.Repeat(transform.position.x*.17f+transform.position.z*.13f,100);
            initialized=false;nextRefresh=0;
        }
        void OnDisable(){All.Remove(this);if(fireLight!=null)fireLight.enabled=false;}
        void Update()
        {
            if(Time.unscaledTime>=nextRefresh)RefreshForCamera(Camera.main);
            if(fireLight!=null&&fireLight.enabled)
                fireLight.intensity=baseIntensity*(.88f+.24f*Mathf.PerlinNoise(phase,Time.time*3));
        }
        void SetEmission(bool visible)
        {
            if(initialized&&visible==emitting)return;
            initialized=true;emitting=visible;
            if(visible){flame.Play();if(embers!=null)embers.Play();}
            else
            {
                flame.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                if(embers!=null)embers.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        public static void RefreshForCamera(Camera view)
        {
            nextRefresh=Time.unscaledTime+.5f;Nearest.Clear();Distances.Clear();
            foreach(var torch in All)
            {
                if(torch==null||torch.flame==null)continue;
                float distance=view!=null?(view.transform.position-torch.transform.position).sqrMagnitude:float.PositiveInfinity;
                torch.SetEmission(distance<torch.flameDistance*torch.flameDistance);
                if(torch.fireLight==null)continue;
                torch.fireLight.enabled=false;
                if(distance>=torch.lightDistance*torch.lightDistance)continue;
                int slot=0;while(slot<Distances.Count&&Distances[slot]<=distance)slot++;
                if(slot>=LightBudget)continue;
                Nearest.Insert(slot,torch);Distances.Insert(slot,distance);
                if(Nearest.Count>LightBudget){Nearest.RemoveAt(LightBudget);Distances.RemoveAt(LightBudget);}
            }
            foreach(var torch in Nearest)torch.fireLight.enabled=true;
        }
    }
}
