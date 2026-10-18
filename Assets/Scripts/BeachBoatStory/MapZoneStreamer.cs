using System;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Cheap, allocation-free visibility control for distant decorative districts.</summary>
    [DisallowMultipleComponent]
    public sealed class MapZoneStreamer : MonoBehaviour
    {
        [Serializable]
        public sealed class Zone
        {
            public GameObject content;
            public Vector3 center;
            [Min(100)] public float visibleDistance = 1500;
            [NonSerialized] public bool visible = true;
        }

        public Zone[] zones = Array.Empty<Zone>();
        [Min(.2f)] public float checkInterval = .65f;
        [Range(1.02f, 1.5f)] public float hideHysteresis = 1.16f;
        float nextCheck;

        void OnEnable()
        {
            nextCheck = 0;
            foreach (var zone in zones)
                if (zone != null && zone.content != null) zone.visible = zone.content.activeSelf;
        }

        void LateUpdate()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + checkInterval;
            var camera = Camera.main;
            if (camera == null) return;
            Vector3 observer = camera.transform.position;
            observer.y = 0;
            foreach (var zone in zones)
            {
                if (zone == null || zone.content == null) continue;
                Vector3 center = zone.center; center.y = 0;
                float threshold = zone.visible ? zone.visibleDistance * hideHysteresis : zone.visibleDistance;
                bool shouldShow = (observer - center).sqrMagnitude <= threshold * threshold;
                if (shouldShow == zone.visible) continue;
                zone.visible = shouldShow;
                zone.content.SetActive(shouldShow);
            }
        }

        void OnDisable()
        {
            // Keep the hierarchy easy to inspect after leaving Play Mode.
            foreach (var zone in zones)
                if (zone != null && zone.content != null && !zone.content.activeSelf) zone.content.SetActive(true);
        }
    }
}
