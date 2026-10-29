using System.Linq;
using UnityEngine;
namespace BeachBoatStory
{
    public static class VillageStallLayout
    {
        public static void ClearLaundry(VillageWorkStall stall)
        {
            if(stall.kind!=VillageWorkStall.WorkKind.Rice)return;
            var laundry=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("San_Phoi_Vai_")).ToArray();
            bool NearLaundry(Vector3 p)
            {
                foreach(var t in laundry)
                {
                    var renderers=t.GetComponentsInChildren<Renderer>(true);var b=new Bounds(t.position,Vector3.zero);foreach(var r in renderers)b.Encapsulate(r.bounds);
                    if(QuestRowboat.FlatDistance(t.position,p)<85||p.x>b.min.x-50&&p.x<b.max.x+50&&p.z>b.min.z-45&&p.z<b.max.z+45)return true;
                }
                return false;
            }
            if(!NearLaundry(stall.transform.position))return;
            var old=stall.transform.position;var terrain=Terrain.activeTerrain;if(terrain==null)return;
            for(int ring=1;ring<=6;ring++)for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI/8;var p=old+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring*35;
                p.y=terrain.SampleHeight(p)+terrain.transform.position.y;
                if(p.y<TideSystem.CurrentWaterHeight+3||NearLaundry(p))continue;
                bool blocked=Physics.OverlapBox(p+Vector3.up*18,new Vector3(42,17,35),stall.transform.rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(c=>!(c is TerrainCollider)&&!c.transform.IsChildOf(stall.transform));
                if(blocked)continue;
                stall.transform.position=p;
                if(stall.interactionPoint!=null){var q=stall.interactionPoint.position;q.y=terrain.SampleHeight(q)+terrain.transform.position.y+3.2f;stall.interactionPoint.position=q;}
                Physics.SyncTransforms();return;
            }
            Debug.LogWarning("No clear rice stall site outside the laundry area");
        }
    }
}
