using UnityEditor;
using UnityEngine;
using BeachBoatStory;

namespace BeachBoatStoryEditor
{
    public static class BeachBoatSceneryPolish
    {
        [MenuItem("BeachBoat/Polish Scenery - Tối ưu Phong cảnh")]
        public static void PolishScenery()
        {
            var scene=BeachBoatToolSafety.Begin("Scenery");
            // Skybox, fog and sunlight are already driven by DayNightCycle.
            // Do not layer opaque primitive clouds over that authored sky.
            var fireMat=BeachBoatToolSafety.Material("CampFlame",new Color(1,.4f,.05f),false,true);
            var woodMat=BeachBoatToolSafety.Material("RiverWood",new Color(.3f,.18f,.09f));
            var birdMat=BeachBoatToolSafety.Material("Bird",new Color(.15f,.16f,.12f));
            var terrain=Terrain.activeTerrain;
            var camp=Object.FindAnyObjectByType<BeachBoatStory.NPCQuestGiver>();
            var boat=Object.FindAnyObjectByType<QuestRowboat>();
            if(camp==null||boat==null||terrain==null)throw new System.InvalidOperationException("Missing camp, boat or terrain.");
            var fires=Root("CampFires");
            for(int i=0;i<4;i++)
            {
                var p=camp.transform.position+new Vector3(100+(i%2)*60,0,-100-(i/2)*60);
                p.y=terrain.SampleHeight(p)+terrain.transform.position.y+3;
                if(p.y<=TideSystem.CurrentWaterHeight+3)continue;
                var fire=Primitive(fires,"CampFire_"+(i+1),PrimitiveType.Sphere,p,new Vector3(4,6,4),fireMat);
                var light=fire.GetComponentInChildren<Light>();
                if(light==null){var go=new GameObject("FireLight");go.transform.SetParent(fire.transform,false);light=go.AddComponent<Light>();}
                light.type=LightType.Point;light.color=new Color(1,.6f,.2f);light.intensity=1.5f;light.range=30;light.shadows=LightShadows.None;
                if(light.GetComponent<FireFlicker>()==null)Undo.AddComponent<FireFlicker>(light.gameObject);
            }
            for(int i=0;i<3;i++)
            {
                var flock=Root("BirdFlock_"+(i+1));
                for(int b=0;b<5;b++)
                {
                    var p=camp.transform.position+new Vector3(300+i*150+b*8,160+i*30+b*2,150);
                    var bird=Primitive(flock,"Bird_"+b,PrimitiveType.Cube,p,new Vector3(2,.3f,3),birdMat);
                    if(bird.GetComponent<BirdFlight>()==null)Undo.AddComponent<BirdFlight>(bird);
                }
            }
            // Repair old tool-created clouds if present; the sky itself needs no extra cloud meshes.
            var cloudRoot=GameObject.Find("CloudLayer");
            if(cloudRoot!=null){Undo.RecordObject(cloudRoot,"Remove duplicate sky layer");cloudRoot.SetActive(false);}
            var debris=Root("RiverDebris");
            for(int i=0;i<12;i++)
            {
                var p=boat.transform.position+new Vector3(80+(i%4)*30,0,(i/4)*60-60);
                if(terrain.SampleHeight(p)+terrain.transform.position.y+12>=boat.tide.lowTideY)continue;
                p.y=TideSystem.CurrentWaterHeight+.3f;
                var piece=Primitive(debris,"Debris_"+i,PrimitiveType.Cube,p,new Vector3(2,.4f,4),woodMat);
                if(piece.GetComponent<FloatDebris>()==null)Undo.AddComponent<FloatDebris>(piece);
            }
            BeachBoatToolSafety.Save(scene);
        }
        static GameObject Root(string name)
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach(var go in scene.GetRootGameObjects())if(go.name==name){Undo.RegisterFullObjectHierarchyUndo(go,"Repair scenery");return go;}
            var root=new GameObject(name);Undo.RegisterCreatedObjectUndo(root,"Add scenery");return root;
        }
        static GameObject Primitive(GameObject parent,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material material)
        {
            var child=parent.transform.Find(name);
            var go=child!=null?child.gameObject:GameObject.CreatePrimitive(type);
            if(child==null){go.name=name;go.transform.SetParent(parent.transform);Undo.RegisterCreatedObjectUndo(go,"Add scenery");}
            go.transform.position=pos;go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();if(collider!=null)Undo.DestroyObjectImmediate(collider);
            var r=go.GetComponent<Renderer>();if(r!=null){r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
            return go;
        }
    }
}

