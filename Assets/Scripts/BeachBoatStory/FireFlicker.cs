using UnityEngine;
namespace BeachBoatStory
{
    [RequireComponent(typeof(Light))]
    public sealed class FireFlicker : MonoBehaviour
    {
        Light source;float intensity,phase;
        void Start(){source=GetComponent<Light>();intensity=source.intensity;phase=Random.value*100;}
        void Update(){source.intensity=intensity*(.8f+.4f*Mathf.PerlinNoise(phase,Time.time*3));}
        void OnDisable(){if(source!=null)source.intensity=intensity;}
    }
}
