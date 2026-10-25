using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Five reachable pickups per journey. A saved journey retains its own layout.</summary>
    [DefaultExecutionOrder(-350)]
    public sealed class VillageWoodHunt : MonoBehaviour
    {
        public static VillageWoodHunt Instance {get;private set;}
        BeachBoatQuestSession session;WoodCollectible[] originals;
        readonly Collider[] overlaps=new Collider[96];readonly RaycastHit[] hits=new RaycastHit[96];
        public float TargetSeconds {get;private set;}=180;
        void Awake(){Instance=this;session=GetComponent<BeachBoatQuestSession>();originals=session.woodItems.ToArray();}
        void Start(){Scatter();}
        static bool Person(Collider c)=>c.GetComponentInParent<Animator>()!=null||c.GetComponentInParent<Character_Movement>()!=null;
        bool Ground(Vector3 p,out float ground)
        {
            foreach(var t in Terrain.activeTerrains)
            {
                var local=p-t.transform.position;var size=t.terrainData.size;
                if(local.x<0||local.z<0||local.x>size.x||local.z>size.z)continue;
                ground=t.SampleHeight(p)+t.transform.position.y;
                return ground>TideSystem.CurrentWaterHeight+3&&t.terrainData.GetInterpolatedNormal(local.x/size.x,local.z/size.z).y>.9f;
            }
            ground=0;return false;
        }
        bool Clear(Vector3 p,float radius)
        {
            int count=Physics.OverlapCapsuleNonAlloc(p+Vector3.up*(radius+.4f),p+Vector3.up*24,radius,overlaps,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return false;
            for(int i=0;i<count;i++)if(!(overlaps[i] is TerrainCollider)&&!Person(overlaps[i])&&overlaps[i].GetComponentInParent<WoodCollectible>()==null)return false;
            return true;
        }
        bool ApproachClear(Vector3 from,Vector3 to)
        {
            var delta=to-from;int count=Physics.SphereCastNonAlloc(from+Vector3.up*8,4,delta.normalized,hits,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(!(hits[i].collider is TerrainCollider)&&!Person(hits[i].collider)&&hits[i].collider.GetComponentInParent<WoodCollectible>()==null)return false;
            return true;
        }
        void Scatter()
        {
            if(originals.Length<5)return;
            Physics.SyncTransforms();var story=BachDangStoryManager.Instance;
            var anchors=new List<Vector3>(originals.Select(w=>w.transform.position));
            foreach(var s in FindObjectsByType<VillageWorkStall>())anchors.Add(s.InteractionPosition);
            foreach(var npc in FindObjectsByType<NPCWander>())
                if(QuestRowboat.FlatDistance(npc.transform.position,story.villageSpawnPoint.position)<1900)anchors.Add(npc.transform.position);
            // Shuffle seeds as well as offsets: picks spread across homes and market
            // approaches instead of merely moving five logs along the old straight row.
            for(int i=anchors.Count-1;i>0;i--){int j=UnityEngine.Random.Range(0,i+1);var p=anchors[i];anchors[i]=anchors[j];anchors[j]=p;}
            var chosen=new List<Vector3>();session.woodItems=originals.Take(5).ToArray();story.totalWoodRequired=5;
            foreach(var item in originals){item.gameObject.SetActive(false);var marker=item.GetComponent<StoryLandmarkMarker>();if(marker!=null)marker.enabled=false;foreach(var text in item.GetComponentsInChildren<TMPro.TMP_Text>(true))text.gameObject.SetActive(false);}
            foreach(var item in session.woodItems)
            {
                Vector3 position=item.transform.position;bool found=false;
                for(int attempt=0;attempt<anchors.Count*12;attempt++)
                {
                    var anchor=anchors[attempt%anchors.Count];var offset=UnityEngine.Random.insideUnitCircle*60;
                    var candidate=anchor+new Vector3(offset.x,0,offset.y);
                    if(!Ground(candidate,out float floor))continue;candidate.y=floor;
                    if(chosen.Any(p=>QuestRowboat.FlatDistance(p,candidate)<150)||!Clear(candidate,17)||!ApproachClear(anchor,candidate))continue;
                    position=candidate;found=true;break;
                }
                if(!found)
                    foreach(var anchor in originals.Select(w=>w.transform.position))
                        if(!chosen.Any(p=>QuestRowboat.FlatDistance(p,anchor)<60)&&Clear(anchor,8)){position=anchor;break;}
                item.transform.SetPositionAndRotation(position,Quaternion.Euler(0,UnityEngine.Random.Range(0,360f),0));
                item.interactDistance=10;item.RestoreCollection(false);chosen.Add(position);
            }
            Physics.SyncTransforms();TargetSeconds=CalculateTarget(story.villageSpawnPoint.position,chosen);
        }
        float CalculateTarget(Vector3 start,List<Vector3> positions)
        {
            // Exact shortest visit order for five pickups; the search allowance is
            // additional to conservative walking time around village buildings.
            float Route(Vector3 at,int mask)
            {
                if(mask==(1<<positions.Count)-1)return 0;float best=float.PositiveInfinity;
                for(int i=0;i<positions.Count;i++)if((mask&(1<<i))==0)best=Mathf.Min(best,QuestRowboat.FlatDistance(at,positions[i])+Route(positions[i],mask|(1<<i)));
                return best;
            }
            float speed=session.player.GetComponent<Character_Movement>().walkSpeed;
            return Mathf.Ceil(Mathf.Max(180,Route(start,0)/Mathf.Max(1,speed)*1.8f+100));
        }
        public void Restore(BachDangSaveData data)
        {
            if(data.woodIds!=null&&data.woodIds.Length==5)
                session.woodItems=data.woodIds.Select(id=>originals.FirstOrDefault(w=>w.name==id)).ToArray();
            else if(data.woods.Length>5)
                session.woodItems=originals.Select((w,i)=>new{w,i}).OrderByDescending(x=>x.i<data.woods.Length&&data.woods[x.i]).Take(5).Select(x=>x.w).ToArray();
            foreach(var w in originals)w.gameObject.SetActive(false);
            for(int i=0;i<session.woodItems.Length;i++)
            {
                var w=session.woodItems[i];if(w==null)continue;
                int source=data.woodIds==null&&data.woods.Length>5?Array.IndexOf(originals,w):i;
                if(data.woodPositions?.Length==5&&data.woodRotations?.Length==5)w.transform.SetPositionAndRotation(data.woodPositions[i],data.woodRotations[i]);
                w.RestoreCollection(source<data.woods.Length&&data.woods[source]);w.interactDistance=10;
            }
            Physics.SyncTransforms();
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
