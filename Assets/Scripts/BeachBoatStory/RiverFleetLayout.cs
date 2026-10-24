using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Distinct water lanes and grounded wreck sites inside the full stake field.</summary>
    public static class RiverFleetLayout
    {
        public static void Configure(EnemyFleetDirector fleet,RiverStakeZone authoredZone=null)
        {
            var zone=authoredZone!=null?authoredZone:RiverStakeZone.Instance;
            if(zone==null||zone.fieldStakes.Length==0||fleet.transform.Find("Battle_Lanes_938")!=null)return;
            var root=new GameObject("Battle_Lanes_938").transform;root.SetParent(fleet.transform,false);
            var points=new List<Vector3>();Bounds bounds=default;
            foreach(var stake in zone.fieldStakes)
            {
                if(stake==null)continue;
                var renderer=stake.GetComponentInChildren<Renderer>(true);var p=renderer!=null?renderer.bounds.center:stake.position;
                if(points.Count==0)bounds=new Bounds(p,Vector3.zero);else bounds.Encapsulate(p);points.Add(p);
            }
            var assigned=new List<Vector3>();int columns=4,rows=Mathf.CeilToInt(fleet.ShipCount/(float)columns);
            Transform Marker(string name,Vector3 p){var t=new GameObject(name).transform;t.SetParent(root,false);t.position=p;return t;}
            for(int i=0;i<fleet.enemyShips.Length;i++)
            {
                var ship=fleet.enemyShips[i];if(ship==null)continue;
                var hull=BoatHullCollision.Get(ship.transform);var size=Vector3.Scale(hull.SolidHull.size,hull.transform.lossyScale);
                ship.stakeHitDistance=Mathf.Max(ship.stakeHitDistance,Mathf.Abs(size.x)*.5f);ship.hullHalfLength=Mathf.Max(ship.hullHalfLength,Mathf.Abs(size.z)*.5f);
                float side=ship.stakeHitDistance+35,depth=ship.hullHalfLength+55;
                float x=Mathf.Lerp(bounds.min.x+side,bounds.max.x-side,(i%columns)/(float)(columns-1));
                float z=Mathf.Lerp(bounds.min.z+depth,bounds.max.z-depth,(i/columns)/(float)Mathf.Max(1,rows-1));
                var desired=new Vector3(x,TideSystem.CurrentWaterHeight,z);var goal=desired;float best=float.PositiveInfinity;
                foreach(var point in points)
                {
                    if(point.x<bounds.min.x+side||point.x>bounds.max.x-side||point.z<bounds.min.z+depth||point.z>bounds.max.z-depth)continue;
                    bool occupied=false;
                    foreach(var old in assigned)if(Mathf.Abs(old.x-point.x)<2*ship.stakeHitDistance+30&&Mathf.Abs(old.z-point.z)<2*ship.hullHalfLength+30){occupied=true;break;}
                    if(occupied)continue;float cost=(new Vector2(point.x-x,point.z-z)).sqrMagnitude;
                    if(cost>=best)continue;best=cost;goal=new Vector3(point.x,desired.y,point.z);
                }
                if(float.IsPositiveInfinity(best))throw new InvalidOperationException("Not enough separate stake-field berths for ship "+i);
                assigned.Add(goal);
                var entry=ship.routeWaypoints.Length>0&&ship.routeWaypoints[0]!=null?ship.routeWaypoints[0].position:ship.spawnPoint;
                ship.targetPoint=Marker("Wreck_Berth_"+i,goal);
                ship.routeWaypoints=new[]{Marker("Entry_"+i,entry),Marker("Lane_"+i,new Vector3(goal.x,goal.y,bounds.max.z+depth+50))};
                ship.moveSpeed=Mathf.Max(34,ship.moveSpeed);ship.ambushDelay=(i%3)*.6f;
            }
            fleet.waveDelay=Mathf.Max(10,fleet.waveDelay);
        }
    }
}
