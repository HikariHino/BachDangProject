using System.Collections.Generic;
using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>Scene-owned workers and anchored posts, independent of player minigame props.</summary>
    [DefaultExecutionOrder(850)]
    public sealed class RiverConstructionCamp : MonoBehaviour
    {
        sealed class Site {public int index;public float elapsed;public GameObject root;public StakeWorkCrew crew;}
        readonly List<Site> sites=new List<Site>();
        public int Workers {get{int count=0;foreach(var site in sites)count+=site.crew.Count;return count;}}
        public static RiverConstructionCamp Ensure(RiverStakeZone zone)
        {var camp=zone.GetComponent<RiverConstructionCamp>();return camp!=null?camp:zone.gameObject.AddComponent<RiverConstructionCamp>();}
        Site Add(BachDangSaveData.ConstructionSite data)
        {
            var zone=GetComponent<RiverStakeZone>();var root=new GameObject("Cong_binh_vi_tri_"+data.index);root.transform.SetParent(transform,true);root.transform.position=data.origin;
            var site=new Site{index=data.index,elapsed=data.elapsed,root=root};
            site.crew=new StakeWorkCrew(QuestRowboat.Instance,zone.fieldStakes[0],root.transform,zone.fieldStakes[0].GetComponentInChildren<Renderer>(true).sharedMaterial,data);
            sites.Add(site);return site;
        }
        public StakeWorkCrew BeginSite(int index)
        {
            foreach(var site in sites)if(site.index==index)return site.crew;
            return Add(new BachDangSaveData.ConstructionSite{index=index,origin=QuestRowboat.Instance.transform.position,rotation=Quaternion.identity}).crew;
        }
        void LateUpdate()
        {
            if(Time.timeScale<=0||BachDangFrontEnd.IsBlocking||OpeningEvent.IsOpeningActive||(StoryDialogueUI.Instance?.IsOpen??false)||(ScreenFader.Instance?.IsTransitioning??false))return;
            foreach(var site in sites){bool together=StakeConstruction.Active!=null&&GetComponent<RiverStakeZone>().WorkingIndex==site.index;if(together||site.elapsed<18)site.elapsed+=Time.deltaTime;site.crew.Step(site.elapsed,together);}
        }
        public BachDangSaveData.ConstructionSite[] Capture()
        {var result=new BachDangSaveData.ConstructionSite[sites.Count];for(int i=0;i<result.Length;i++)result[i]=sites[i].crew.Capture(sites[i].index,sites[i].elapsed);return result;}
        public void Restore(BachDangSaveData data)
        {
            foreach(var saved in data.constructionSites??System.Array.Empty<BachDangSaveData.ConstructionSite>())
                if(saved.index>=0&&saved.index<GetComponent<RiverStakeZone>().ClusterCount)Add(saved);
            // Existing partial saves predate persistent teams. Rebuild the finished
            // work crews once, without inventing extra completed player locations.
            if(data.constructionSites==null&&data.storyState==(int)BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver)
                for(int i=0;i<data.stakes.Length;i++)if(data.stakes[i])Add(new BachDangSaveData.ConstructionSite{index=i,origin=GetComponent<RiverStakeZone>().placements[i].approach.position,elapsed=18});
        }
        public void ParkAfterWork(){foreach(var site in sites)site.crew.ParkOutsideBattle();}
        public void ResetCamp(){foreach(var site in sites){site.root.SetActive(false);site.crew.Dispose();Destroy(site.root);}sites.Clear();}
        void OnDestroy(){ResetCamp();}
    }
}
