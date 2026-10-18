using UnityEngine;
namespace BeachBoatStory
{
    public sealed class FloatDebris : MonoBehaviour
    {
        public float orbitRadius=2,orbitSpeed=.1f,baseHeight=.3f;
        Vector3 origin;Quaternion rotation;float phase;
        void Start(){origin=transform.position;rotation=transform.rotation;}
        void Update()
        {
            phase+=orbitSpeed*Time.deltaTime;
            var p=origin+new Vector3(Mathf.Sin(phase)*Mathf.Min(orbitRadius,3),0,Mathf.Cos(phase)*Mathf.Min(orbitRadius,3));
            p.y=TideSystem.CurrentWaterHeight+baseHeight;
            transform.SetPositionAndRotation(p,rotation*Quaternion.Euler(0,0,Mathf.Sin(phase*3)*3));
        }
    }
}
