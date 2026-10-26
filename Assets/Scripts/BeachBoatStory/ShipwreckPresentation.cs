using System.Collections;
using UnityEngine;

namespace BeachBoatStory
{
    public sealed class ShipwreckPresentation : MonoBehaviour
    {
        Transform original,broken,bow,stern;MeshCollider[] colliders;ParticleSystem[] fire;
        Material flameMaterial,smokeMaterial;Texture2D texture;float began,nextSurface;
        readonly Vector3[] anchors={new Vector3(-16,17,42),new Vector3(-16,17,42),new Vector3(14,19,-50),new Vector3(14,19,-50)};
        public bool IsBroken=>broken!=null&&broken.gameObject.activeSelf;
        public MeshCollider[] Colliders=>colliders;
        public int FireParticles {get{int n=0;if(fire!=null)foreach(var p in fire)n+=p.particleCount;return n;}}
        public void Prepare()
        {
            if(broken!=null)return;original=transform.Find("RedSailJunkVisual");var prefab=Resources.Load<GameObject>("BachDang938/Wrecks/BrokenWarship");if(original==null||prefab==null)return;
            broken=Instantiate(prefab,transform,false).transform;broken.name="BrokenWarship";broken.localPosition=original.localPosition+original.localRotation*Vector3.Scale(original.localScale,Vector3.up*20);broken.localRotation=original.localRotation;broken.localScale=original.localScale;
            bow=broken.Find("Bow");stern=broken.Find("Stern");colliders=broken.GetComponentsInChildren<MeshCollider>();
            texture=new Texture2D(16,16,TextureFormat.RGBA32,false);
            for(int y=0;y<16;y++)for(int x=0;x<16;x++){float r=new Vector2((x-7.5f)/7.5f,(y-7.5f)/7.5f).magnitude;texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01((1-r)*2)));}texture.Apply();
            flameMaterial=RiverParticleMaterial.Create(texture);smokeMaterial=new Material(flameMaterial);
            fire=new[]{Flame(bow,new Vector3(-16,17,42),false),Flame(bow,new Vector3(-16,17,42),true),Flame(stern,new Vector3(14,19,-50),false),Flame(stern,new Vector3(14,19,-50),true)};
            broken.gameObject.SetActive(false);
        }
        ParticleSystem Flame(Transform parent,Vector3 point,bool smoke)
        {
            var go=new GameObject(smoke?"Light smoke":"Small timber fire");go.transform.SetParent(parent,false);go.transform.localPosition=point;go.transform.rotation=Quaternion.Euler(-90,0,0);
            var p=go.AddComponent<ParticleSystem>();p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=p.main;main.playOnAwake=false;main.loop=true;main.duration=4;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startLifetime=new ParticleSystem.MinMaxCurve(smoke?1.8f:.4f,smoke?3f:1.05f);main.startSpeed=new ParticleSystem.MinMaxCurve(smoke?7:12,smoke?12:22);main.startSize=new ParticleSystem.MinMaxCurve(smoke?5:3,smoke?10:6);main.maxParticles=smoke?24:40;
            main.startColor=smoke?new Color(.16f,.15f,.13f,.28f):new Color(1,.48f,.035f,.9f);var emission=p.emission;emission.rateOverTime=smoke?6:28;var shape=p.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=smoke?17:10;shape.radius=smoke?1.2f:2;
            var color=p.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(smoke?new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)}:new[]{new GradientColorKey(new Color(1,1,.4f),0),new GradientColorKey(new Color(1,.25f,.025f),.5f),new GradientColorKey(new Color(.3f,.05f,.01f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(0,1)});color.color=gradient;
            var size=p.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,smoke?.5f:1,1,smoke?2:.1f));
            var renderer=p.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=smoke?smokeMaterial:flameMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return p;
        }
        public void SetBroken(bool value,float seconds=0)
        {
            Prepare();if(broken==null)return;bool changed=IsBroken!=value;original.gameObject.SetActive(!value);broken.gameObject.SetActive(value);
            if(value){began=Time.time-seconds;broken.localPosition=original.localPosition+original.localRotation*Vector3.Scale(original.localScale,Vector3.up*20);Step(seconds);if(changed)foreach(var p in fire)p.Play();}
            else foreach(var p in fire)p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        public void Step(float seconds)
        {
            if(!IsBroken)return;float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(seconds/3.2f));
            bow.localPosition=new Vector3(3*t,-3*t,13*t);stern.localPosition=new Vector3(-3*t,-2*t,-13*t);bow.localRotation=Quaternion.Euler(-14*t,0,-3*t);stern.localRotation=Quaternion.Euler(13*t,0,4*t);
            bool updateSurface=Time.time>=nextSurface;if(updateSurface)nextSurface=Time.time+.15f;
            for(int i=0;i<fire.Length;i++)
            {
                var p=fire[i];if(updateSurface)
                {
                    var world=p.transform.parent.TransformPoint(anchors[i]);float top=float.NegativeInfinity;
                    foreach(var c in colliders)if(c.Raycast(new Ray(new Vector3(world.x,c.bounds.max.y+2,world.z),Vector3.down),out var hit,500)&&hit.normal.y>.35f)top=Mathf.Max(top,hit.point.y);
                    if(!float.IsNegativeInfinity(top))world.y=top+.6f;p.transform.position=world;p.transform.rotation=Quaternion.Euler(-90,0,0);
                }
                var e=p.emission;e.enabled=seconds<40&&p.transform.position.y>TideSystem.CurrentWaterHeight+.8f;
            }
        }
        void Update(){if(IsBroken&&GetComponent<EnemyShip>().HasSettled)Step(Time.time-began);}
        void OnDestroy(){if(flameMaterial!=null)Destroy(flameMaterial);if(smokeMaterial!=null)Destroy(smokeMaterial);if(texture!=null)Destroy(texture);}
    }
    public sealed class ShipwreckPrewarm : MonoBehaviour
    {
        IEnumerator Start()
        {
            // Spread preparation over title frames; sinking only enables prepared objects.
            foreach(var ship in FindObjectsByType<EnemyShip>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {ship.PrepareWreck();yield return null;}
        }
    }
    public sealed class RiverCorpse : MonoBehaviour
    {
        Vector3 origin;float began,phase;
        public static void Ensure(EnemyDeckGuard guard)
        {
            var corpse=guard.GetComponent<RiverCorpse>();if(corpse==null)corpse=guard.gameObject.AddComponent<RiverCorpse>();
            corpse.enabled=true;corpse.origin=guard.transform.position;corpse.began=Time.time;corpse.phase=Random.value*6;
        }
        void LateUpdate()
        {
            var guard=GetComponent<EnemyDeckGuard>();if(guard.Health>0){enabled=false;return;}
            var p=origin;p.y=TideSystem.CurrentWaterHeight+.4f+Mathf.Sin((Time.time-began)*1.4f+phase)*.25f;transform.position=p;
        }
    }
}
