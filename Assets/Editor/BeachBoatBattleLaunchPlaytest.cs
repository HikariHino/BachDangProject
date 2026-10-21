using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatBattleLaunchPlaytest
{
    const string Flag="BattleLaunchRegression";
    static double started;
    static bool ran;
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||ran)return;
        if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<3)return;
        ran=true;string failure=null;var cases=new List<object>();float rowingTravel=0,recoveredTravel=0;
        try
        {
            foreach(var intro in UnityEngine.Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))intro.SkipOpening();
            BeachBoatStory.StoryDialogueUI.Instance?.Close();
            var boat=QuestRowboat.Instance;var session=BeachBoatQuestSession.Instance;var story=Story.Instance;var zone=BeachBoatStory.RiverStakeZone.Instance;
            foreach(var point in zone.placements)point.planted=true;
            typeof(BeachBoatStory.RiverStakeZone).GetField("<PlantedCount>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(zone,zone.RequiredCount);
            typeof(BeachBoatStory.RiverStakeZone).GetField("<CompletedClusterCount>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(zone,zone.ClusterCount);
            story.collectedWood=story.totalWoodRequired;
            var pier=GameObject.Find("Cau_Go_Ra_Ben").transform.position;
            for(int i=0;i<4;i++)
            {
                story.TransitionTo(Story.StoryState.Act2_Completed,true);
                var parked=i==3?pier+new Vector3(800,0,600):pier+new Vector3(i*30,0,0);parked.y=TideSystem.CurrentWaterHeight;
                boat.transform.SetPositionAndRotation(parked,Quaternion.Euler(0,i*65,0));Physics.SyncTransforms();
                story.OnTalkToNgoQuyenAct3();
                bool clear=boat.AtDock&&boat.CanOccupy(boat.transform.position,boat.transform.rotation);
                for(int level=0;level<2;level++)for(float distance=0;distance<=120;distance+=8)
                {var p=boat.transform.position+boat.transform.forward*distance;p.y=level==0?TideSystem.Instance.lowTideY:TideSystem.Instance.highTideY;clear&=boat.CanOccupy(p,boat.transform.rotation);}
                float moved=QuestRowboat.FlatDistance(parked,boat.transform.position);
                cases.Add(new{caseIndex=i,clear,moved,position=boat.transform.position.ToString("F1"),heading=boat.transform.eulerAngles.y});
                if(!clear||moved<25)throw new Exception("Act 3 did not replace unsafe Act 2 parking for case "+i);
            }
            session.player.transform.position=boat.boardingPoint.position;var playerBody=session.player.GetComponent<Rigidbody>();playerBody.position=boat.boardingPoint.position;playerBody.linearVelocity=Vector3.zero;Physics.SyncTransforms();
            if(!boat.TryBoard())throw new Exception("Could not board reset battle boat");
            var before=boat.transform.position;for(int i=0;i<60;i++)boat.Step(1,0,.05f);rowingTravel=QuestRowboat.FlatDistance(before,boat.transform.position);
            if(rowingTravel<90||!boat.IsAboard)throw new Exception("Boat cannot row out of the fresh launch pose");
            var wedged=pier;wedged.y=TideSystem.CurrentWaterHeight;boat.transform.SetPositionAndRotation(wedged,Quaternion.Euler(0,90,0));Physics.SyncTransforms();
            if(boat.CanOccupy(wedged,boat.transform.rotation))throw new Exception("Regression fixture is not actually trapped inside the pier");
            float health=session.player.GetComponent<PlayerVitals>().Health;
            for(int i=0;i<50;i++)boat.Step(1,0,.05f);
            recoveredTravel=QuestRowboat.FlatDistance(wedged,boat.transform.position);
            if(recoveredTravel<60||!boat.CanOccupy(boat.transform.position,boat.transform.rotation)||!boat.IsAboard)throw new Exception("Already-aboard dock recovery failed");
            if(session.player.GetComponent<PlayerVitals>().Health<health)throw new Exception("Existing dock overlap incorrectly damaged the player");
        }
        catch(Exception e){failure=e.ToString();}
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/BattleLaunchRegression.json",JsonConvert.SerializeObject(new{success=failure==null,failure,cases,rowingTravel,recoveredTravel},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
}
