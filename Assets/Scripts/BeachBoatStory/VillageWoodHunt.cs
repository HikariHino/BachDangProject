using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Five pickups at the original village positions; collection time still counts toward rank.</summary>
    [DefaultExecutionOrder(-350)]
    public sealed class VillageWoodHunt : MonoBehaviour
    {
        public static VillageWoodHunt Instance {get;private set;}
        BeachBoatQuestSession session;WoodCollectible[] originals;
        Vector3[] fixedPositions;Quaternion[] fixedRotations;
        public float TargetSeconds {get;private set;}=180;
        void Awake()
        {
            Instance=this;session=GetComponent<BeachBoatQuestSession>();originals=session.woodItems.ToArray();
            fixedPositions=originals.Select(w=>w.transform.position).ToArray();fixedRotations=originals.Select(w=>w.transform.rotation).ToArray();
        }
        void Start()
        {
            session.woodItems=originals.Take(5).ToArray();BachDangStoryManager.Instance.totalWoodRequired=5;
            foreach(var item in originals)
            {
                item.gameObject.SetActive(false);var marker=item.GetComponent<StoryLandmarkMarker>();if(marker!=null)marker.enabled=false;
                foreach(var text in item.GetComponentsInChildren<TMPro.TMP_Text>(true))text.gameObject.SetActive(false);
            }
            foreach(var item in session.woodItems){item.interactDistance=10;item.RestoreCollection(false);}
            TargetSeconds=CalculateTarget(BachDangStoryManager.Instance.villageSpawnPoint.position,session.woodItems.Select(w=>w.transform.position).ToList());
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
                int original=Array.IndexOf(originals,w);w.transform.SetPositionAndRotation(fixedPositions[original],fixedRotations[original]);
                w.RestoreCollection(source<data.woods.Length&&data.woods[source]);w.interactDistance=10;
            }
            Physics.SyncTransforms();
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
