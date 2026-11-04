using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>Reused drill arrows and targets; no combat damage is sent to camp actors.</summary>
    public sealed class DrumVolleyDisplay
    {
        sealed class Shot{public Transform arrow,target;public Vector3 from,to;public float age;public bool flying,hit;}
        readonly Shot[] shots=new Shot[2];readonly GameObject root;readonly Material wood,cloth;
        public int Fired {get;private set;}public int Landed {get;private set;}
        public DrumVolleyDisplay(DrumTrainingGround ground)
        {
            root=new GameObject("Bia_tap_lenh_cung_thu");root.transform.SetParent(ground.transform,false);wood=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.32f,.19f,.08f)};cloth=new Material(wood){color=new Color(.62f,.43f,.22f)};
            for(int i=0;i<2;i++)
            {
                var p=ground.transform.TransformPoint(new Vector3(i==0?-45:45,0,100));if(Terrain.activeTerrain!=null)p.y=Terrain.activeTerrain.SampleHeight(p)+Terrain.activeTerrain.transform.position.y;
                var target=new GameObject("Bia_rom_lenh_"+i).transform;target.SetParent(root.transform,true);target.position=p;
                void Part(Transform parent,PrimitiveType type,Vector3 at,Vector3 size,Material material,Quaternion rotation){var g=GameObject.CreatePrimitive(type);g.transform.SetParent(parent,false);g.transform.localPosition=at;g.transform.localScale=size;g.transform.localRotation=rotation;g.GetComponent<Collider>().enabled=false;Object.Destroy(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=material;}
                Part(target,PrimitiveType.Cylinder,Vector3.up*8,new Vector3(.8f,8,.8f),wood,Quaternion.identity);Part(target,PrimitiveType.Cylinder,Vector3.up*15,new Vector3(10,.5f,10),cloth,Quaternion.Euler(90,0,0));
                var arrow=new GameObject("Mui_ten_tap_"+i).transform;arrow.SetParent(root.transform,false);Part(arrow,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.12f,2,.12f),wood,Quaternion.Euler(90,0,0));arrow.gameObject.SetActive(false);shots[i]=new Shot{arrow=arrow,target=target};
            }
        }
        public Vector3 Target(int index)=>shots[index].target.position+Vector3.up*15;
        public void Fire(int index,Vector3 from){var s=shots[index];s.from=from;s.to=Target(index);s.age=0;s.flying=true;s.hit=false;s.arrow.gameObject.SetActive(true);Fired++;BeachBoatSoundscape.Instance?.PlayBow();}
        public void Tick(float dt)
        {
            foreach(var s in shots)
            {
                if(s.flying){s.age+=dt;float t=Mathf.Clamp01(s.age/.65f);var p=Vector3.Lerp(s.from,s.to,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*9;s.arrow.SetPositionAndRotation(p,Quaternion.LookRotation((s.to-s.from).normalized));if(t>=1){s.flying=false;s.hit=true;s.age=0;Landed++;BeachBoatSoundscape.Instance?.PlayHit();}}
                if(s.hit){s.age+=dt;s.target.localRotation=Quaternion.Euler(Mathf.Sin(s.age*24)*Mathf.Exp(-s.age*6)*9,0,0);}
            }
        }
        public void Dispose(){Object.Destroy(root);Object.Destroy(wood);Object.Destroy(cloth);}
    }
}
