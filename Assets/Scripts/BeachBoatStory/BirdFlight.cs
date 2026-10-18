using UnityEngine;
namespace BeachBoatStory
{
    public sealed class BirdFlight : MonoBehaviour
    {
        public float orbitRadius=200,orbitSpeed=.2f,orbitHeight=200;
        Vector3 origin;float angle;
        void Start(){origin=transform.position;}
        void Update()
        {
            angle+=orbitSpeed*Time.deltaTime;
            transform.position=origin+new Vector3((Mathf.Cos(angle)-1)*orbitRadius,Mathf.Sin(angle*2)*10,Mathf.Sin(angle)*orbitRadius);
            transform.rotation=Quaternion.LookRotation(new Vector3(-Mathf.Sin(angle),0,Mathf.Cos(angle)));
        }
    }
}
