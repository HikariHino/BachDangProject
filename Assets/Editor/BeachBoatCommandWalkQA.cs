using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>Drives the real character FixedUpdate and capsule up the relocated stage ramp.</summary>
public static class BeachBoatCommandWalkQA
{
    public static bool Check(BeachBoatQuestSession session)
    {
        var movement=session.player.GetComponent<BeachBoatStory.Character_Movement>();
        var body=session.player.GetComponent<Rigidbody>();
        var camp=GameObject.Find("TestQuest_Camp").transform;
        var stage=camp.Cast<Transform>().First(t=>t.name.Contains("Rustic_Wooden_Stage"));
        var start=camp.Find("CommandApproach").position;
        var mode=Physics.simulationMode;bool enabled=movement.enabled;
        var fixedUpdate=movement.GetType().GetMethod("FixedUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
        void Set(string name,object value)=>movement.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(movement,value);
        var samples=new System.Collections.Generic.List<object>();
        bool reached=false;
        try
        {
            BeachBoatStory.StoryDialogueUI.Instance?.Close();movement.enabled=false;
            body.isKinematic=false;body.useGravity=true;body.position=start;body.rotation=Quaternion.identity;
            session.player.transform.SetPositionAndRotation(start,Quaternion.identity);body.linearVelocity=Vector3.zero;
            Set("orbit",null);Set("view",null);Set("freeLook",true);Set("input",Vector2.up);
            Physics.SyncTransforms();Physics.simulationMode=SimulationMode.Script;
            for(int i=0;i<600;i++)
            {
                fixedUpdate.Invoke(movement,null);Physics.Simulate(Time.fixedDeltaTime);
                if(i%30==0)samples.Add(new{step=i,position=body.position.ToString("F2"),movement.IsGrounded});
                if(body.position.z>=stage.position.z-10){reached=body.position.y>Terrain.activeTerrain.SampleHeight(body.position)+Terrain.activeTerrain.transform.position.y+25;break;}
            }
            File.WriteAllText("Logs/CampCommandWalkQA.json",JsonConvert.SerializeObject(new{success=reached,start=start.ToString("F2"),end=body.position.ToString("F2"),samples},Formatting.Indented));
        }
        finally{Physics.simulationMode=mode;movement.enabled=enabled;}
        return reached;
    }
}
