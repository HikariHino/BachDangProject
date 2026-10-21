using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>A small yard animal; no Animator, rigidbody, navigation mesh or per-frame allocation.</summary>
    public sealed class VillageChicken : MonoBehaviour
    {
        public Transform head,leftLeg,rightLeg;
        public float radius=8,speed=2.4f;
        Vector3 home,target;
        Terrain terrain;
        Quaternion headRest;
        Vector3 headPosition;
        VillageChicken[] neighbors;
        float pause,nextProbe,phase;
        void Start(){home=transform.position;terrain=Terrain.activeTerrain;headRest=head.localRotation;headPosition=head.localPosition;neighbors=transform.parent.GetComponentsInChildren<VillageChicken>(true);phase=Random.value*6;Choose();}
        void Choose(){var offset=Random.insideUnitCircle*radius;target=home+new Vector3(offset.x,0,offset.y);pause=Random.Range(.4f,2.5f);}
        void Update()
        {
            if(QuestCheckpoint.Instance!=null&&QuestCheckpoint.Instance.IsDefeated)return;
            var camera=Camera.main;if(camera==null||(camera.transform.position-transform.position).sqrMagnitude>450*450)return;
            float dt=Time.deltaTime;if(dt<=0)return;
            var separation=Vector3.zero;
            foreach(var neighbor in neighbors)
            {
                if(neighbor==this||!neighbor.gameObject.activeInHierarchy)continue;var delta=transform.position-neighbor.transform.position;delta.y=0;
                float distance=delta.magnitude;if(distance>.01f&&distance<3.8f)separation+=delta/distance*(1-distance/3.8f);
                if(distance<2.8f)pause=0;
            }
            pause-=dt;float peck=pause>0?Mathf.Max(0,Mathf.Sin(Time.time*7+phase)):0;
            head.localRotation=headRest*Quaternion.Euler(pause>0?peck*38:Mathf.Sin(Time.time*9+phase)*5,0,0);
            head.localPosition=headPosition+new Vector3(0,-peck*1.2f,peck*.4f);
            float stride=pause>0?0:Mathf.Sin(Time.time*9+phase)*24;
            if(leftLeg!=null)leftLeg.localRotation=Quaternion.Euler(stride,0,0);
            if(rightLeg!=null)rightLeg.localRotation=Quaternion.Euler(-stride,0,0);
            if(pause>0)return;
            var direction=target-transform.position;direction.y=0;
            if(direction.sqrMagnitude<1){Choose();return;}
            direction=(direction.normalized+separation*1.8f).normalized;
            var next=transform.position+direction*speed*dt;
            if((next-home).sqrMagnitude>(radius+1.5f)*(radius+1.5f)){target=home;return;}
            if(Time.time>=nextProbe)
            {
                nextProbe=Time.time+.3f;
                if(terrain.SampleHeight(next)+terrain.transform.position.y<TideSystem.CurrentWaterHeight+3||
                    Physics.CheckSphere(next+Vector3.up*2,1.3f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {Choose();return;}
            }
            float ground=terrain.SampleHeight(next)+terrain.transform.position.y;
            if(Mathf.Abs(ground-transform.position.y)>.8f){Choose();return;}
            next.y=ground+.1f;transform.position=next;
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),140*dt);
        }
    }
}
