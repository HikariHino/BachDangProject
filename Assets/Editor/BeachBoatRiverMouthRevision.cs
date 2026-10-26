using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using BeachBoatStoryEditor;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BeachBoatRiverMouthRevision
{
    public static readonly float[] Belts={1460,1780,2100,2420,2780};
    static Bounds BoundsOf(Transform t)=>BeachBoatWorldAudit.BoundsOf(t);
    public static void Apply()
    {
        var scene=BeachBoatToolSafety.Begin("RiverMouthAndLightPatrols");
        var zone=Object.FindAnyObjectByType<BeachBoatStory.RiverStakeZone>();var terrain=Terrain.activeTerrain;var tide=Object.FindAnyObjectByType<TideSystem>();
        var boats=Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include).OrderBy(b=>b.name,StringComparer.Ordinal).ToArray();
        var row=Object.FindAnyObjectByType<QuestRowboat>();
        if(zone==null||zone.ClusterCount!=5||boats.Length!=10||row==null)throw new Exception("Expected five work clusters and ten allied vessels.");
        int added=ExtendStakes(zone,terrain,tide);
        int small=0,large=0;
        foreach(var boat in boats)
        {
            bool keepLarge=boat.name=="Thuyen_Nhu_Giac_1_TienPhong"||boat.name=="Thuyen_Nhu_Giac_2_HuuDuc";
            if(!keepLarge)MakeLightPatrol(boat,row,tide,boats.First(b=>b.name=="Thuyen_Nhu_Giac_1_TienPhong").rowers[0]);
            else boat.lightPatrol=false;
            boat.trafficPadding=90;boat.pauseAtPoint=0;boat.bobAmplitude=.06f;
            int index=keepLarge?large++:small++;
            bool north=!keepLarge&&index>=4;
            var center=north?new Vector3(-3400,tide.transform.position.y,3700):new Vector3(-1950,tide.transform.position.y,650);
            float rx=north?620:keepLarge?900:680,rz=north?400:keepLarge?440:240;
            float phase=keepLarge?45+index*180:(index%4)*90;
            boat.cruiseSpeed=keepLarge?32:38;boat.turnSpeed=keepLarge?36:60;
            AuthorRoute(boat,center,rx,rz,phase,tide,terrain);
            EditorUtility.SetDirty(boat);
        }
        RiverWaterNavigation.Reset();Physics.SyncTransforms();
        var combat=Object.FindAnyObjectByType<RiverBoardingCombat>();
        // Preserve registry order and vessel names: old saves use both as identities.
        if(combat.friendlyBoats.Length!=10)throw new Exception("Allied combat roster must remain intact.");
        BeachBoatToolSafety.Save(scene);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/RiverMouthRevision.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{added,total=zone.RequiredCount,small,large,boats=boats.Select(b=>new{b.name,b.lightPatrol,b.cruiseSpeed,points=b.route.Length,perimeter=Enumerable.Range(0,b.route.Length).Sum(i=>QuestRowboat.FlatDistance(b.route[i].position,b.route[(i+1)%b.route.Length].position))})},Newtonsoft.Json.Formatting.Indented));
    }
    static int ExtendStakes(BeachBoatStory.RiverStakeZone zone,Terrain terrain,TideSystem tide)
    {
        var stakes=zone.fieldStakes.ToList();var centers=stakes.Select(s=>BoundsOf(s).center).ToList();int added=0;
        var groups=zone.placements.Select(p=>p.stakeIndices.ToList()).ToArray();
        for(int belt=0;belt<Belts.Length;belt++)for(int row=-1;row<=1;row++)
        {
            float z=Belts[belt]+row*32;float left=-2400,right=-2400;
            float Ground(float x)=>terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y;
            while(left>-6000&&Ground(left-12)<tide.lowTideY)left-=12;
            while(right<0&&Ground(right+12)<tide.lowTideY)right+=12;
            for(float x=left+10+(row==0?22:0);x<=right-6;x+=44)
            {
                var p=new Vector3(x,0,z);
                if(zone.placements.Any(a=>QuestRowboat.FlatDistance(a.approach.position,p)<65)||centers.Any(c=>QuestRowboat.FlatDistance(c,p)<19))continue;
                float ground=Ground(x),bottom=ground-8,top=Mathf.Lerp(tide.lowTideY,tide.highTideY,.56f+(added%5)*.015f);
                var stake=Object.Instantiate(stakes[0].gameObject,stakes[0].parent).transform;
                stake.name="CocGo_CuaSong_"+belt+"_"+(++added).ToString("0000");stake.gameObject.SetActive(true);
                var b=BoundsOf(stake);p.y=b.center.y;stake.position+=p-b.center;
                b=BoundsOf(stake);stake.localScale*=(top-bottom)/b.size.y;b=BoundsOf(stake);stake.position+=Vector3.up*(bottom-b.min.y);
                foreach(var child in stake.GetComponentsInChildren<Transform>(true))GameObjectUtility.SetStaticEditorFlags(child.gameObject,0);
                stake.gameObject.SetActive(false);groups[belt].Add(stakes.Count);stakes.Add(stake);centers.Add(BoundsOf(stake).center);
            }
        }
        zone.fieldStakes=stakes.ToArray();
        for(int i=0;i<groups.Length;i++)
        {
            zone.placements[i].stakeIndices=groups[i].ToArray();
            var text=zone.placements[i].marker.GetComponentInChildren<TMPro.TMP_Text>(true);text.text=$"CỤM {i+1} • {groups[i].Count} CỌC";EditorUtility.SetDirty(text);
        }
        EditorUtility.SetDirty(zone);return added;
    }
    static void MakeLightPatrol(AmbientRiverBoat boat,QuestRowboat row,TideSystem tide,Animator rowerTemplate)
    {
        var oldRower=rowerTemplate;
        var person=Object.Instantiate(oldRower.gameObject);person.transform.SetParent(null,true);
        var personScale=oldRower.transform.lossyScale;person.name="ThuyBinh_Cheo_Thuyen";
        var root=boat.transform;while(root.childCount>0)Object.DestroyImmediate(root.GetChild(0).gameObject);
        var parentScale=root.parent!=null?root.parent.lossyScale:Vector3.one;
        root.localScale=new Vector3(1/parentScale.x,1/parentScale.y,1/parentScale.z);root.rotation=Quaternion.identity;var at=root.position;at.y=tide.transform.position.y;root.position=at;
        var visual=Object.Instantiate(row.transform.Find("Thuyen_Go_Song").gameObject,root,false).transform;visual.name="DaiVietPatrolVisual_Rebuilt";
        visual.localScale=Vector3.Scale(visual.localScale,new Vector3(.65f,1,.65f));
        var b=BoundsOf(visual);if(b.size.x>b.size.z)visual.localRotation=Quaternion.Euler(0,90,0)*visual.localRotation;
        b=BoundsOf(visual);visual.position+=new Vector3(root.position.x-b.center.x,root.position.y-3.2f-b.min.y,root.position.z-b.center.z);
        person.transform.SetParent(root,false);person.transform.localScale=personScale;person.transform.localRotation=Quaternion.identity;person.transform.localPosition=Vector3.zero;
        // Retain the compatible seated controller already used by this avatar.
        var animator=person.GetComponent<Animator>();animator.applyRootMotion=false;animator.enabled=true;animator.Update(0);
        float deck=BeachBoatFleetAndStallUpgrade.Deck(visual,root,Vector3.zero,-20);
        ActorSolidBody.WalkingDimensions(animator,out _,out float sole,out _);person.transform.localPosition=Vector3.up*(deck+.15f-sole);
        var crew=new GameObject("Quan_Si_Tren_Boong").AddComponent<FleetDeckCrew>();crew.transform.SetParent(root,false);crew.soldiers=new[]{person.transform};
        var landing=new GameObject("Boarding_Port").transform;landing.SetParent(root,false);landing.localPosition=Vector3.up*deck;
        boat.rowers=new[]{animator};boat.visualForwardYaw=0;boat.lightPatrol=true;
        BoatHullCollision.Get(root).Initialize();
    }
    static void AuthorRoute(AmbientRiverBoat boat,Vector3 center,float rx,float rz,float phase,TideSystem tide,Terrain terrain)
    {
        var root=GameObject.Find("Tuyen_Tuan_Tra_Cua_Song_938")?.transform;
        if(root==null)root=new GameObject("Tuyen_Tuan_Tra_Cua_Song_938").transform;
        var old=root.Find(boat.name);if(old!=null)Object.DestroyImmediate(old.gameObject);
        var group=new GameObject(boat.name).transform;group.SetParent(root,false);
        var points=new List<Transform>();float radius=BoatHullCollision.Get(boat.transform).Radius;
        for(int i=0;i<16;i++)
        {
            float angle=(phase+i*22.5f)*Mathf.Deg2Rad;var p=center+new Vector3(Mathf.Cos(angle)*rx,0,Mathf.Sin(angle)*rz);
            // Author the entire clearance disc in deep water; navigation may add detours later.
            for(int n=0;n<9;n++)
            {
                var sample=p+(n==0?Vector3.zero:Quaternion.Euler(0,(n-1)*45,0)*Vector3.forward*(radius+42));
                if(terrain.SampleHeight(sample)+terrain.transform.position.y>tide.lowTideY-14)throw new Exception("Patrol route too shallow for "+boat.name+" at "+sample);
            }
            var point=new GameObject("Point_"+i.ToString("00")).transform;point.SetParent(group,false);point.position=p;points.Add(point);
        }
        boat.route=points.ToArray();boat.transform.position=points[0].position;boat.transform.rotation=Quaternion.LookRotation(points[1].position-points[0].position);
    }
}
