using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Apply once to authored warships before crew caches and hulls initialize.</summary>
    public static class RiverVesselScale
    {
        public const float WarshipFactor=1.2f;
        public static void ApplyScene()
        {
            foreach(var ship in Object.FindObjectsByType<EnemyShip>(FindObjectsInactive.Include))Enlarge(ship.transform);
            foreach(var ship in Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include))if(!ship.lightPatrol)Enlarge(ship.transform);
        }
        static void Enlarge(Transform ship)
        {
            if(ship.Find("Warship_Size_120")!=null)return;
            ship.localScale*=WarshipFactor;
            var marker=new GameObject("Warship_Size_120");marker.transform.SetParent(ship,false);
        }
    }
}
