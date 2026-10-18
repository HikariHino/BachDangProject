using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Small natural head/body shifts for a crew member posted on a boat.</summary>
    [DisallowMultipleComponent]
    public sealed class BoatCrewLookout : MonoBehaviour
    {
        [Range(5,55)] public float sweepAngle = 28;
        [Range(2,20)] public float turnSpeed = 8;
        Quaternion rest;
        float targetYaw, nextChange;
        void Start() { rest = transform.localRotation; Pick(); }
        void Pick() { targetYaw = Random.Range(-sweepAngle, sweepAngle); nextChange = Time.time + Random.Range(2.5f, 6f); }
        void Update()
        {
            if (Time.time >= nextChange) Pick();
            var target = rest * Quaternion.Euler(0, targetYaw, 0);
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, target, turnSpeed * Time.deltaTime);
        }
    }
}
