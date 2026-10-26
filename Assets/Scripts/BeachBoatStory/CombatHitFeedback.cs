using UnityEngine;

namespace BeachBoatStory
{
    public enum CombatImpactKind { Other, Sword, Arrow }

    /// <summary>Bounded, reusable blood bursts. Call only after health was actually removed.</summary>
    public sealed class CombatHitFeedback : MonoBehaviour
    {
        public static CombatHitFeedback Instance {get;private set;}
        public int ConfirmedHits {get;private set;}
        public int SwordHits {get;private set;}
        public int ArrowHits {get;private set;}
        public int PoolSize=>bursts.Length;
        public int LivingParticles {get{int n=0;foreach(var p in bursts)n+=p.particleCount;return n;}}
        readonly ParticleSystem[] bursts=new ParticleSystem[12];int nextPlayer,nextDistant;Material material;Texture2D texture;
        void Awake()
        {
            Instance=this;
            texture=new Texture2D(16,16,TextureFormat.RGBA32,false);texture.name="Blood droplet alpha";
            for(int y=0;y<16;y++)for(int x=0;x<16;x++){float r=new Vector2((x-7.5f)/7.5f,(y-7.5f)/7.5f).magnitude;texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01((1-r)*5)));}texture.Apply();
            material=RiverParticleMaterial.Create(texture);
            for(int i=0;i<bursts.Length;i++)
            {
                var go=new GameObject("Blood burst "+i);go.transform.SetParent(transform,false);var p=go.AddComponent<ParticleSystem>();p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=p.main;main.playOnAwake=false;main.loop=false;main.duration=.6f;main.startLifetime=new ParticleSystem.MinMaxCurve(.18f,.48f);main.startSpeed=new ParticleSystem.MinMaxCurve(8,19);main.startSize=new ParticleSystem.MinMaxCurve(.35f,1.05f);main.startColor=new ParticleSystem.MinMaxGradient(new Color(.48f,.015f,.012f,.95f),new Color(.75f,.025f,.012f,.9f));main.gravityModifier=2;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=32;
                var emission=p.emission;emission.enabled=false;var shape=p.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=38;shape.radius=.45f;
                var fade=p.colorOverLifetime;fade.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});fade.color=g;
                var renderer=p.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Billboard;bursts[i]=p;
            }
        }
        public static void Show(Vector3 point,CombatImpactKind kind,bool foreground=false,Vector3 direction=default)
        {
            var s=BeachBoatQuestSession.Instance;if(s==null)return;
            var fx=Instance!=null?Instance:s.gameObject.AddComponent<CombatHitFeedback>();
            fx.ConfirmedHits++;if(kind==CombatImpactKind.Sword)fx.SwordHits++;if(kind==CombatImpactKind.Arrow)fx.ArrowHits++;
            var p=fx.bursts[foreground?fx.nextPlayer++%3:3+fx.nextDistant++%9];p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(direction.sqrMagnitude<.01f)direction=s.player.transform.position-point;
            if(direction.sqrMagnitude>.01f)point+=direction.normalized*2.2f;
            direction.y=Mathf.Max(direction.y,direction.magnitude*.18f);if(direction.sqrMagnitude<.01f)direction=Vector3.up;
            p.transform.SetPositionAndRotation(point,Quaternion.LookRotation(direction));p.Play();p.Emit(kind==CombatImpactKind.Arrow?12:18);
            BeachBoatSoundscape.Instance?.PlayCombatHit(point,kind,foreground);
        }
        void OnDestroy(){if(Instance==this)Instance=null;if(material!=null)Destroy(material);if(texture!=null)Destroy(texture);}
    }
}
