using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Lightweight period-appropriate combat: boats converge while arrows, spears and stones cross the river.</summary>
    [DisallowMultipleComponent]
    public sealed class RiverBattleChoreography : MonoBehaviour
    {
        sealed class Shot
        {
            public GameObject gameObject;
            public Transform transform;
            public Vector3 start, end;
            public float time, duration, arc;
            public bool impact;
        }

        public AmbientRiverBoat[] friendlyBoats = System.Array.Empty<AmbientRiverBoat>();
        [Min(8)] public int projectilePoolSize = 32;
        [Min(.2f)] public float volleyInterval = .72f;
        [Tooltip("Bac's lightweight sword and impact clips, reused during close river combat.")]
        public AudioClip swordSwing, swordClash, hitImpact;
        readonly List<EnemyShip> enemies = new List<EnemyShip>();
        readonly List<EnemyShip> activeScratch = new List<EnemyShip>();
        Shot[] shots;
        Material woodProjectileMaterial, stoneMaterial;
        float nextVolley, nextAssignment;
        int volley;
        bool fighting;
        AudioSource battleAudio;

        void Awake()
        {
            battleAudio = gameObject.AddComponent<AudioSource>();
            battleAudio.playOnAwake = false; battleAudio.loop = false;
            battleAudio.spatialBlend = 0.15f; battleAudio.volume = .32f;
            BuildPool();
            if(GetComponent<RiverShoreArchers>()==null)gameObject.AddComponent<RiverShoreArchers>();
            if(GetComponent<RiverBattleVoices>()==null)gameObject.AddComponent<RiverBattleVoices>();
        }

        public void BeginAmbush(IEnumerable<EnemyShip> fleet)
        {
            SynchronizeFriendlyFleet();
            enemies.Clear();
            foreach (var ship in fleet) if (ship != null && !enemies.Contains(ship)) enemies.Add(ship);
            if (friendlyBoats == null || friendlyBoats.Length == 0)
                friendlyBoats = FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            fighting = true;
            nextVolley = Time.time + .25f; nextAssignment = 0; volley = 0;
        }

        public void EndBattle(bool retreat=false)
        {
            SynchronizeFriendlyFleet();
            fighting = false;
            for(int i=0;i<(friendlyBoats?.Length??0);i++)if(friendlyBoats[i]!=null)
            {if(retreat)friendlyBoats[i].ReturnToHarbor(i);else friendlyBoats[i].SetBattleTarget(null);}
            if (shots != null) foreach (var shot in shots) if (shot?.gameObject != null) shot.gameObject.SetActive(false);
        }

        void SynchronizeFriendlyFleet()
        {
            var registered=RiverBoardingCombat.Instance?.friendlyBoats;
            if(registered!=null&&registered.Length>0)friendlyBoats=registered;
        }

        void Update()
        {
            UpdateShots(Time.deltaTime);
            if (!fighting||!RiverBattleObjective.Counterattack) return;
            if (Time.time >= nextAssignment) { nextAssignment = Time.time + 1.2f; AssignTargets(); }
            if (Time.time < nextVolley) return;
            nextVolley = Time.time + volleyInterval;
            FireVolley(); volley++;
        }

        void AssignTargets()
        {
            var active = ActiveEnemies();
            if (active.Count == 0) { EndBattle(); return; }
            for (int i = 0; i < friendlyBoats.Length; i++)
                if (friendlyBoats[i] != null){var ship=active[i % active.Count];friendlyBoats[i].SetBattleTarget(ship.WreckTarget!=null?ship.WreckTarget:ship.transform);}
        }

        void FireVolley()
        {
            var active = ActiveEnemies(); if (active.Count == 0) { EndBattle(); return; }
            // Three staggered arrows or light spears make the two-bank ambush readable at a distance.
            for (int i = 0; i < 3; i++)
            {
                var target = active[(volley + i) % active.Count];
                Vector3 source;
                if (friendlyBoats != null && friendlyBoats.Length > 0 && friendlyBoats[(volley + i) % friendlyBoats.Length] != null)
                    source = friendlyBoats[(volley + i) % friendlyBoats.Length].transform.position + Vector3.up * (13 + i * 2);
                else source = target.transform.position + (i % 2 == 0 ? Vector3.left : Vector3.right) * 180 + Vector3.up * 30;
                Launch(source, (target.WreckTarget!=null?target.WreckTarget.position:target.transform.position) + Vector3.up * Random.Range(9f, 22f), 13 + i * 3);
            }
            // The invaders answer with one darker shot toward a Vietnamese boat.
            if (friendlyBoats != null && friendlyBoats.Length > 0)
            {
                var friendly = friendlyBoats[volley % friendlyBoats.Length];
                if (friendly != null)
                {
                    var enemy = active[volley % active.Count];
                    Launch((enemy.WreckTarget!=null?enemy.WreckTarget.position:enemy.transform.position) + Vector3.up * 22, friendly.transform.position + Vector3.up * 8, 18);
                }
            }
            if (battleAudio != null && volley % 2 == 0)
            {
                var clip = volley % 4 == 0 ? swordClash : swordSwing;
                if (clip != null)
                {
                    battleAudio.pitch = Random.Range(.92f, 1.08f);
                    battleAudio.PlayOneShot(clip, .75f);
                }
            }
        }

        public void PlayShipImpact()
        {
            if (battleAudio == null || hitImpact == null) return;
            battleAudio.pitch = Random.Range(.88f, 1.04f);
            battleAudio.PlayOneShot(hitImpact, .9f);
        }

        /// <summary>The player's flag order has an immediate visible consequence.</summary>
        public void FireSignalVolley(IEnumerable<EnemyShip> targets)
        {
            if(shots==null)return;
            fighting=true;
            nextVolley=Time.time+.35f;
            AssignTargets();
            int index=0;
            foreach(var target in targets)
            {
                if(target==null||(!target.gameObject.activeInHierarchy&&target.WreckTarget==null))continue;
                var focus=target.WreckTarget!=null?target.WreckTarget.position:target.transform.position;
                Vector3 source=friendlyBoats!=null&&friendlyBoats.Length>0&&friendlyBoats[index%friendlyBoats.Length]!=null
                    ? friendlyBoats[index%friendlyBoats.Length].transform.position+Vector3.up*17f
                    : focus+Vector3.left*140f+Vector3.up*25f;
                for(int shot=0;shot<3;shot++)
                    Launch(source+Vector3.up*(shot*2f),focus+Vector3.up*(10f+shot*4f),18f+shot*4f);
                index++;
            }
            if(battleAudio!=null&&swordClash!=null)battleAudio.PlayOneShot(swordClash,.9f);
        }

        List<EnemyShip> ActiveEnemies()
        {
            activeScratch.Clear();
            foreach (var enemy in enemies) if (enemy != null &&
                    (enemy.WreckTarget!=null||enemy.gameObject.activeInHierarchy&&!enemy.isDestroyed)&&
                    (enemy.IsReleasedForAmbush||EnemyFleetDirector.Instance.EbbStarted)) activeScratch.Add(enemy);
            return activeScratch;
        }

        void BuildPool()
        {
            woodProjectileMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ArrowAndSpear_Runtime" };
            woodProjectileMaterial.SetColor("_BaseColor", new Color(.28f, .17f, .075f));
            stoneMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "SlingStone_Runtime" };
            stoneMaterial.SetColor("_BaseColor", new Color(.24f, .25f, .22f));
            shots = new Shot[Mathf.Max(8, projectilePoolSize)];
            for (int i = 0; i < shots.Length; i++)
            {
                bool stone = i % 5 == 4;
                var go = GameObject.CreatePrimitive(stone ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
                go.name = (stone ? "DaBan_Pooled_" : "MuiTen_Giao_Pooled_") + (i + 1); go.transform.SetParent(transform, false);
                Destroy(go.GetComponent<Collider>()); var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = stone ? stoneMaterial : woodProjectileMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                go.transform.localScale = stone ? Vector3.one * 1.25f : new Vector3(.28f, 3.8f, .28f); go.SetActive(false);
                shots[i] = new Shot { gameObject = go, transform = go.transform };
            }
        }

        void Launch(Vector3 start, Vector3 end, float arc)
        {
            Shot shot = null;
            foreach (var candidate in shots) if (!candidate.gameObject.activeSelf) { shot = candidate; break; }
            if (shot == null) shot = shots[volley % shots.Length];
            shot.start = start; shot.end = end; shot.time = 0; shot.duration = Mathf.Clamp(Vector3.Distance(start, end) / 230f, .55f, 1.45f); shot.arc = arc; shot.impact = false;
            bool stone = shot.gameObject.name.StartsWith("DaBan");
            shot.transform.localScale = stone ? Vector3.one * 1.25f : new Vector3(.28f, 3.8f, .28f);
            shot.gameObject.SetActive(true); shot.transform.position = start;
        }

        void UpdateShots(float dt)
        {
            if (shots == null) return;
            foreach (var shot in shots)
            {
                if (!shot.gameObject.activeSelf) continue;
                shot.time += dt;
                if (!shot.impact)
                {
                    float t = Mathf.Clamp01(shot.time / shot.duration);
                    Vector3 p = Vector3.Lerp(shot.start, shot.end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * shot.arc);
                    Vector3 next = Vector3.Lerp(shot.start, shot.end, Mathf.Min(1, t + .025f)) + Vector3.up * (Mathf.Sin(Mathf.Min(1, t + .025f) * Mathf.PI) * shot.arc);
                    shot.transform.SetPositionAndRotation(p, Quaternion.FromToRotation(Vector3.up, next - p));
                    if (t >= 1) { shot.impact = true; shot.time = 0; shot.transform.localScale = Vector3.one * 2.2f; }
                }
                else
                {
                    float t = shot.time / .18f; shot.transform.localScale = Vector3.one * Mathf.Lerp(2.2f, .2f, t);
                    if (t >= 1) shot.gameObject.SetActive(false);
                }
            }
        }

        void OnDestroy()
        {
            if (woodProjectileMaterial != null) Destroy(woodProjectileMaterial);
            if (stoneMaterial != null) Destroy(stoneMaterial);
        }
    }
}
