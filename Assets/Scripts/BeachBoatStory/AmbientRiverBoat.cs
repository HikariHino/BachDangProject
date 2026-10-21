using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Low-cost authored patrol for background river boats.</summary>
    [DisallowMultipleComponent]
    public sealed class AmbientRiverBoat : MonoBehaviour
    {
        public TideSystem tide;
        public Transform[] route = System.Array.Empty<Transform>();
        public Animator[] rowers = System.Array.Empty<Animator>();
        [Min(.1f)] public float cruiseSpeed = 12f;
        [Min(1f)] public float turnSpeed = 22f;
        [Tooltip("Yaw from the root's blue Z axis to the model's bow. Use 90 when the hull is authored along local X.")]
        public float visualForwardYaw;
        [Min(0f)] public float pauseAtPoint = 2f;
        public float waterlineOffset;
        [Min(0f)] public float bobAmplitude = .18f;
        int target;
        float pause, phase;
        bool ready;
        Transform battleTarget;
        Vector3 checkpointHome;
        Quaternion checkpointRotation;

        public void SetBattleTarget(Transform value) { battleTarget = value; pause = 0; }

        void Start()
        {
            if (tide == null) tide = FindAnyObjectByType<TideSystem>();
            if (tide == null || route == null || route.Length < 2) { enabled = false; return; }
            // The authored patrol roots can sit below the tide plane. Calibrate from the
            // visible hull once, then keep that depth as the tide moves. Using the root's
            // original Y made only the sails visible on the water.
            var hull = transform.Find("DaiVietPatrolVisual_Rebuilt");
            if (hull != null)
            {
                var renderers = hull.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    transform.position += Vector3.up * (tide.transform.position.y - 3.2f - bounds.min.y);
                }
            }
            waterlineOffset = transform.position.y - tide.transform.position.y;
            phase = Mathf.Repeat(transform.position.x * .013f + transform.position.z * .009f, Mathf.PI * 2f);
            target = NearestNextPoint(); ready = true;
            checkpointHome=transform.position;checkpointRotation=transform.rotation;
            SetRowing(true);
        }
        public void ResetForCheckpoint()
        {
            if(!ready)return;
            battleTarget=null;pause=0;var position=checkpointHome;position.y=TideSystem.CurrentWaterHeight+waterlineOffset;
            transform.SetPositionAndRotation(position,checkpointRotation);target=NearestNextPoint();SetRowing(true);
        }

        int NearestNextPoint()
        {
            int nearest = 0; float best = float.PositiveInfinity;
            for (int i = 0; i < route.Length; i++)
            {
                if (route[i] == null) continue;
                Vector3 d = route[i].position - transform.position; d.y = 0;
                if (d.sqrMagnitude < best) { best = d.sqrMagnitude; nearest = i; }
            }
            return (nearest + 1) % route.Length;
        }

        void Update()
        {
            if (!ready) return;
            float dt = Time.deltaTime;
            if (battleTarget != null && battleTarget.gameObject.activeInHierarchy)
            {
                Vector3 battleDirection = battleTarget.position - transform.position; battleDirection.y = 0;
                if (battleDirection.sqrMagnitude > 90f * 90f) MoveAlongBow(battleDirection, dt, cruiseSpeed * 1.28f);
                else
                {
                    Quaternion broadside = Quaternion.LookRotation(battleDirection.normalized, Vector3.up) * Quaternion.Euler(0, -visualForwardYaw - 18f, 0);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, broadside, turnSpeed * dt);
                    FloatOnly(); SetRowing(false);
                }
                return;
            }
            if (pause > 0) { pause -= dt; SetRowing(false); FloatOnly(); return; }
            var point = route[target];
            if (point == null) { target = (target + 1) % route.Length; return; }
            Vector3 direction = point.position - transform.position; direction.y = 0;
            if (direction.sqrMagnitude < 36f)
            {
                target = (target + 1) % route.Length; pause = pauseAtPoint; SetRowing(false); FloatOnly(); return;
            }
            MoveAlongBow(direction, dt, cruiseSpeed);
        }

        void MoveAlongBow(Vector3 direction, float dt, float speed)
        {
            Quaternion facing = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0, -visualForwardYaw, 0);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * dt);
            Vector3 bow = transform.rotation * Quaternion.Euler(0, visualForwardYaw, 0) * Vector3.forward;
            Vector3 position = transform.position + bow * speed * dt;
            position.y = tide.transform.position.y + waterlineOffset + Mathf.Sin(Time.time * 1.45f + phase) * bobAmplitude;
            transform.position = position; SetRowing(true);
        }

        void FloatOnly()
        {
            Vector3 p = transform.position;
            p.y = tide.transform.position.y + waterlineOffset + Mathf.Sin(Time.time * 1.45f + phase) * bobAmplitude;
            transform.position = p;
        }

        void SetRowing(bool moving)
        {
            float speed = moving ? 1f : .35f;
            foreach (var animator in rowers) if (animator != null && animator.speed != speed) animator.speed = speed;
        }
    }
}
