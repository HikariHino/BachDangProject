using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Scripted swimmers sweep and slide against real hulls as well as upright ships.</summary>
    public static class RiverHullCollision
    {
        static readonly RaycastHit[] hits=new RaycastHit[64];
        public static Vector3 Move(Vector3 from,Vector3 destination,float radius,bool navigateAround=false,Transform owner=null)
        {
            Physics.SyncTransforms();
            var position=from;var remaining=destination-from;
            for(int pass=0;pass<3&&remaining.sqrMagnitude>.00001f;pass++)
            {
                float length=remaining.magnitude;var direction=remaining/length;
                int count=Physics.SphereCastNonAlloc(position,radius,direction,hits,length+.15f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                if(count==hits.Length)return position;
                float nearest=float.PositiveInfinity;Vector3 normal=Vector3.zero;
                for(int i=0;i<count;i++)
                {
                    var collider=hits[i].collider;
                    if(collider==null||(collider is TerrainCollider&&hits[i].normal.y>.45f)||collider.GetComponent<TideSystem>()!=null||
                        (owner!=null&&collider.transform.IsChildOf(owner))||hits[i].distance>=nearest)continue;
                    nearest=hits[i].distance;normal=hits[i].normal;
                }
                if(float.IsPositiveInfinity(nearest)){position+=remaining;break;}
                float travel=Mathf.Clamp(nearest-.2f,0,length);position+=direction*travel;
                // Swimmers stay at the water surface instead of sliding down through a tilted hull.
                var horizontal=normal;horizontal.y=0;if(horizontal.sqrMagnitude>.001f)normal=horizontal.normalized;
                remaining=Vector3.ProjectOnPlane(direction*(length-travel),normal);
                if(navigateAround&&remaining.magnitude<(length-travel)*.5f)
                {
                    var tangent=Vector3.Cross(Vector3.up,normal).normalized;
                    if(Vector3.Dot(tangent,destination-position)<0)tangent=-tangent;
                    remaining=tangent*(length-travel);
                }
            }
            return position;
        }
    }
}
