using TMPro;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Names the physical drop-off so a colored marker is never the only instruction.</summary>
    [DisallowMultipleComponent]
    public sealed class SupplyDestinationSign : MonoBehaviour
    {
        public TextMeshPro label;
        public Transform board;

        public void SetCargo(string cargo, int trip)
        {
            if (label != null) label.text = $"ĐIỂM GIAO {trip}\n{cargo}";
        }

        void LateUpdate()
        {
            var camera = Camera.main;
            if (label != null && camera != null)
            {
                if (board != null)
                {
                    var towardCamera = camera.transform.position - board.position;
                    towardCamera.y = 0;
                    if (towardCamera.sqrMagnitude > .01f)
                        label.transform.position = board.position + towardCamera.normalized * 1.7f;
                }
                var direction = label.transform.position - camera.transform.position;
                direction.y = 0;
                if (direction.sqrMagnitude > .01f) label.transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }
}
