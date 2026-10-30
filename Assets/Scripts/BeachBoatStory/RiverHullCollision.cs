using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Scripted swimmers sweep and slide against real hulls as well as upright ships.</summary>
    public static class RiverHullCollision
    {
        static readonly RaycastHit[] hits=new RaycastHit[64];
        static readonly Collider[] overlaps=new Collider[96];
        static SphereCollider probe;
        static bool Ignore(Collider c,Transform owner,BoatHullCollision deckCarrier)=>c==null||c.GetComponent<TideSystem>()!=null||
            (owner!=null&&c.transform.IsChildOf(owner))||(deckCarrier!=null&&c==deckCarrier.SolidHull);
        public static bool ClearAt(Vector3 point,float radius,Transform owner=null,BoatHullCollision deckCarrier=null,bool ignoreActors=false)
        {
            int count=Physics.OverlapSphereNonAlloc(point,radius,overlaps,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(!(overlaps[i] is TerrainCollider)&&!(ignoreActors&&overlaps[i].GetComponentInParent<ActorSolidBody>()!=null)&&!Ignore(overlaps[i],owner,deckCarrier))return false;
            return count<overlaps.Length;
        }
        public static bool ClearSegment(Vector3 from,Vector3 to,float radius,Transform owner=null,BoatHullCollision carrier=null,bool ignoreActors=false)
        {
            if(!ClearAt(to,radius,owner,carrier,ignoreActors))return false;
            var d=to-from;if(d.sqrMagnitude<.00001f)return true;
            int count=Physics.SphereCastNonAlloc(from,radius,d.normalized,hits,d.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(!(ignoreActors&&hits[i].collider.GetComponentInParent<ActorSolidBody>()!=null)&&!Ignore(hits[i].collider,owner,carrier)&&!IsWalkableBank(hits[i]))return false;
            return true;
        }
        static Vector3 Separate(Vector3 point,float radius,Transform owner,BoatHullCollision carrier)
        {
            if(probe==null){var go=new GameObject("River_Separation_Probe"){hideFlags=HideFlags.HideAndDontSave,layer=2};probe=go.AddComponent<SphereCollider>();probe.isTrigger=true;}
            probe.radius=radius;
            // Old saves and wreck releases can begin inside a stake. Resolve only
            // existing overlap, then sweep every subsequent move normally.
            for(int pass=0;pass<6;pass++)
            {
                bool moved=false;int count=Physics.OverlapSphereNonAlloc(point,radius,overlaps,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)
                {
                    var c=overlaps[i];if(c is TerrainCollider||Ignore(c,owner,carrier))continue;
                    bool penetrating=Physics.ComputePenetration(probe,point,Quaternion.identity,c,c.transform.position,c.transform.rotation,out var normal,out float depth);
                    // Non-convex stake meshes can return a vertical/zero normal
                    // when an old saved swimmer starts fully inside their solid core.
                    if((!penetrating||new Vector2(normal.x,normal.z).sqrMagnitude<.001f)&&c is MeshCollider mesh&&!mesh.convex&&c.bounds.Contains(point))
                    {
                        var bounds=c.bounds;float best=float.PositiveInfinity;var push=Vector3.zero;
                        foreach(var axis in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
                        {
                            var outside=point+axis*(bounds.size.magnitude+radius);
                            if(!c.Raycast(new Ray(outside,-axis),out var contact,bounds.size.magnitude+radius))continue;
                            float amount=Vector3.Dot(contact.point-point,axis)+radius+.12f;
                            if(amount>0&&amount<best){best=amount;push=axis;}
                        }
                        if(push.sqrMagnitude>.1f){normal=push;depth=best;penetrating=true;}
                    }
                    if(!penetrating)continue;
                    normal.y=0;if(normal.sqrMagnitude<.001f)continue;
                    point+=normal.normalized*(depth+.12f);moved=true;
                }
                if(!moved)break;
            }
            return point;
        }
        static bool IsWalkableBank(RaycastHit hit)
        {
            if(!(hit.collider is TerrainCollider))return false;
            var terrain=hit.collider.GetComponent<Terrain>();if(terrain==null)return hit.normal.y>=.65f;
            var p=hit.point-terrain.transform.position;var size=terrain.terrainData.size;
            // A sphere touching a bank from below can report a horizontal contact
            // normal. Use the terrain's real slope so swimmers can reach dry sand.
            return terrain.terrainData.GetInterpolatedNormal(Mathf.Clamp01(p.x/size.x),Mathf.Clamp01(p.z/size.z)).y>=.65f;
        }
        public static Vector3 Move(Vector3 from,Vector3 destination,float radius,bool navigateAround=false,Transform owner=null,BoatHullCollision deckCarrier=null)
        {
            Physics.SyncTransforms();
            var position=Separate(from,radius,owner,deckCarrier);var remaining=destination-from;
            for(int pass=0;pass<3&&remaining.sqrMagnitude>.00001f;pass++)
            {
                float length=remaining.magnitude;var direction=remaining/length;
                int count=Physics.SphereCastNonAlloc(position,radius,direction,hits,length+.15f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                if(count==hits.Length)return position;
                float nearest=float.PositiveInfinity;Vector3 normal=Vector3.zero;
                for(int i=0;i<count;i++)
                {
                    var collider=hits[i].collider;
                    if(Ignore(collider,owner,deckCarrier)||IsWalkableBank(hits[i])||hits[i].distance>=nearest)continue;
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
