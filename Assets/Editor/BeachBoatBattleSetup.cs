using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using System.Linq;
using System.Collections.Generic;
using System.IO;
namespace BeachBoatStoryEditor
{
    public static class BeachBoatBattleSetup
    {
        const string EnemyBoatAsset="Assets/junk/source/Sketchfab_2013_12_04_23_35_26.fbx";
        [MenuItem("BeachBoat/Setup Quest 3 Battle")]
        public static void SetupBattle()
        {
            var scene=BeachBoatToolSafety.Begin("BattleSetup");
            var zone=Object.FindAnyObjectByType<BeachBoatStory.RiverStakeZone>();var boat=Object.FindAnyObjectByType<QuestRowboat>();
            var warship=AssetDatabase.LoadAssetAtPath<GameObject>(EnemyBoatAsset);
            if(zone==null||boat==null||warship==null||zone.ClusterCount!=5)throw new System.InvalidOperationException("Missing five stake clusters, quest boat or red-sail enemy junk model.");
            var director=Object.FindAnyObjectByType<EnemyFleetDirector>();
            if(director!=null)Object.DestroyImmediate(director.gameObject);
            var terrain=boat.terrain;var tide=boat.tide;
            const int shipCount=10;
            if(terrain==null||tide==null)throw new System.InvalidOperationException("Battle terrain or tide missing.");
            var root=new GameObject("EnemyFleetDirector_RedSailJunk");director=root.AddComponent<EnemyFleetDirector>();
            var combat=root.AddComponent<RiverBattleChoreography>();
            combat.friendlyBoats=Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            combat.projectilePoolSize=32;combat.volleyInterval=.72f;
            director.enemyShips=new EnemyShip[shipCount];director.spawnPoints=new Transform[shipCount];
            director.shipsPerWave=2;director.waveDelay=8;
            for(int i=0;i<shipCount;i++)
            {
                var go=new GameObject("EnemyWarship_NamHan_"+(i+1));go.transform.SetParent(root.transform);
                go.transform.position=UpstreamSpawn(i,tide.lowTideY);
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(warship,go.transform);
                visual.name="RedSailJunkVisual";visual.transform.localPosition=Vector3.zero;
                // The imported junk is authored lengthwise on local X, while EnemyShip advances on local Z.
                // Turn only the shared visual so the bow follows the movement direction.
                visual.transform.localRotation=Quaternion.Euler(0,90,0);visual.transform.localScale=Vector3.one;
                foreach(var collider in visual.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
                foreach(var component in visual.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(component);
                // Normalize the AI model to a believable war boat while all ten instances keep
                // sharing the imported meshes/materials. This avoids ten duplicate mesh assets.
                var visualRenderers=visual.GetComponentsInChildren<Renderer>(true);
                if(visualRenderers.Length==0)throw new System.InvalidOperationException("ThuyenGo model has no renderer.");
                var boatBounds=visualRenderers[0].bounds;
                for(int r=1;r<visualRenderers.Length;r++)boatBounds.Encapsulate(visualRenderers[r].bounds);
                float horizontal=Mathf.Max(boatBounds.size.x,boatBounds.size.z);
                float modelScale=92f/Mathf.Max(.01f,horizontal);
                visual.transform.localScale=Vector3.one*modelScale;
                boatBounds=visualRenderers[0].bounds;
                for(int r=1;r<visualRenderers.Length;r++)boatBounds.Encapsulate(visualRenderers[r].bounds);
                // Sink the lower hull a little below the low-tide surface rather than floating above it.
                visual.transform.position+=Vector3.up*((tide.lowTideY-4f)-boatBounds.min.y);
                var ship=go.AddComponent<EnemyShip>();ship.spawnPoint=go.transform.position;ship.moveSpeed=26;ship.stakeHitDistance=28;
                director.enemyShips[i]=ship;go.SetActive(false);
                var spawn=new GameObject("Spawn_"+(i+1));spawn.transform.SetParent(root.transform);spawn.transform.position=go.transform.position;director.spawnPoints[i]=spawn.transform;
            }
            ConfigureHistoricalRoute(director,zone,terrain,tide);
            var trigger=new GameObject("BattleTrigger");trigger.transform.SetParent(root.transform);trigger.transform.position=boat.boardingPoint.position;
            trigger.AddComponent<BattleTrigger>().boat=boat;
            Undo.RegisterCreatedObjectUndo(root,"Set up quest battle");
            BeachBoatToolSafety.Save(scene);Selection.activeGameObject=root;
            Debug.Log("Quest 3: ten red-sail junks enter from the northern river branch in five player-signaled waves.",root);
        }

        static Vector3 UpstreamSpawn(int index,float water)
        {
            bool right=index%2==1;
            return new Vector3(right?-1355f:-1480f,water,right?4080f:3970f);
        }

        static Transform Point(Transform parent,string name,Vector3 position)
        {
            var existing=parent.Find(name);
            var point=existing!=null?existing:new GameObject(name).transform;
            point.SetParent(parent);point.position=position;
            return point;
        }

        static void CheckWetRoute(Terrain terrain,float lowWater,Vector3 from,Vector3 to,int ship)
        {
            int steps=Mathf.CeilToInt(Vector3.Distance(from,to)/35f);
            var size=terrain.terrainData.size;
            for(int step=0;step<=steps;step++)
            {
                var p=Vector3.Lerp(from,to,step/(float)Mathf.Max(1,steps));
                var local=p-terrain.transform.position;
                if(local.x<0||local.z<0||local.x>size.x||local.z>size.z||
                   terrain.SampleHeight(p)+terrain.transform.position.y+8f>lowWater)
                    throw new System.InvalidOperationException($"Warship {ship+1} route crosses land near {p}");
            }
        }

        /// <summary>North-to-south approach based on the user's Bạch Đằng battle diagram.</summary>
        public static void ConfigureHistoricalRoute(EnemyFleetDirector director,BeachBoatStory.RiverStakeZone zone,Terrain terrain,TideSystem tide)
        {
            if(director==null||zone==null||terrain==null||tide==null||zone.ClusterCount!=5||director.ShipCount!=10)
                throw new System.InvalidOperationException("Quest 3 fleet/river configuration is incomplete.");
            director.shipsPerWave=2;director.waveDelay=8;
            for(int i=0;i<10;i++)
            {
                var ship=director.enemyShips[i];
                int cluster=4-i/2; // northern cluster first, then downstream
                var indices=zone.placements[cluster].stakeIndices;
                int stakeIndex=indices[Mathf.Min((i%2)*Mathf.Max(1,indices.Length/2),indices.Length-1)];
                var target=zone.fieldStakes[stakeIndex];
                if(ship==null||target==null)throw new System.InvalidOperationException("Ship or target missing: "+i);
                var targetPos=target.GetComponentInChildren<Renderer>(true).bounds.center;
                bool right=i%2==1;
                Vector3 spawn=UpstreamSpawn(i,tide.lowTideY);
                Vector3 entry=new Vector3(right?-1460f:-1570f,tide.lowTideY,3510f);
                Vector3 turn=new Vector3(right?-1510f:-1620f,tide.lowTideY,targetPos.z+330f);
                Vector3 approach=new Vector3(targetPos.x+(right?50f:70f),tide.lowTideY,targetPos.z+150f);
                var route=new[]{spawn,entry,turn,approach,new Vector3(targetPos.x,tide.lowTideY,targetPos.z)};
                for(int j=1;j<route.Length;j++)CheckWetRoute(terrain,tide.lowTideY,route[j-1],route[j],i);
                if(director.spawnPoints==null||director.spawnPoints.Length!=10)
                    director.spawnPoints=new Transform[10];
                director.spawnPoints[i]=Point(director.transform,"Spawn_"+(i+1),spawn);
                ship.spawnPoint=spawn;ship.transform.position=spawn;ship.targetPoint=target;
                ship.routeWaypoints=new[]{
                    Point(director.transform,"Route_"+(i+1)+"_Entry",entry),
                    Point(director.transform,"Route_"+(i+1)+"_Channel",turn),
                    Point(director.transform,"Route_"+(i+1)+"_Approach",approach)
                };
                ship.moveSpeed=26f;ship.stakeHitDistance=28f;
            }
        }

        // Entry point used by the CLI after scripts finish compiling.
        public static void SetupBattleAndExit()
        {
            if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity",UnityEditor.SceneManagement.OpenSceneMode.Single);
            SetupBattle();
            EditorApplication.Exit(0);
        }

        public static void UpgradeExistingRouteAndExit()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity",UnityEditor.SceneManagement.OpenSceneMode.Single);
            var director=Object.FindAnyObjectByType<EnemyFleetDirector>();
            var zone=Object.FindAnyObjectByType<BeachBoatStory.RiverStakeZone>();
            var tide=Object.FindAnyObjectByType<TideSystem>();
            var terrain=Terrain.activeTerrain;
            if(director==null||zone==null||tide==null||terrain==null)
                throw new System.InvalidOperationException("Existing beachBoat quest 3 objects are missing.");
            Directory.CreateDirectory(".utmp/BeachBoatQuest3Route");
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,".utmp/BeachBoatQuest3Route/before.unity",true);
            ConfigureHistoricalRoute(director,zone,terrain,tide);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            EditorApplication.Exit(0);
        }

        public static void AuditBattleAndExit()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity",UnityEditor.SceneManagement.OpenSceneMode.Single);
            var director=Object.FindAnyObjectByType<EnemyFleetDirector>();
            var ships=director!=null?director.enemyShips.Where(x=>x!=null).Distinct().ToArray():new EnemyShip[0];
            var meshes=new HashSet<Mesh>();var materials=new HashSet<Material>();
            int renderers=0,totalVerticesPerShip=0,animators=0,colliders=0;
            Vector3 firstSize=Vector3.zero;
            for(int i=0;i<ships.Length;i++)
            {
                var filters=ships[i].GetComponentsInChildren<MeshFilter>(true);
                foreach(var f in filters)if(f.sharedMesh!=null)meshes.Add(f.sharedMesh);
                foreach(var r in ships[i].GetComponentsInChildren<Renderer>(true))
                {renderers++;foreach(var m in r.sharedMaterials)if(m!=null)materials.Add(m);}
                animators+=ships[i].GetComponentsInChildren<Animator>(true).Length;
                colliders+=ships[i].GetComponentsInChildren<Collider>(true).Length;
                if(i==0)
                {
                    totalVerticesPerShip=filters.Sum(f=>f.sharedMesh!=null?f.sharedMesh.vertexCount:0);
                    var visual=ships[i].transform.Find("RedSailJunkVisual");
                    if(visual!=null)
                    {
                        var localBounds=new Bounds(Vector3.zero,Vector3.zero);bool any=false;
                        foreach(var f in filters)if(f.sharedMesh!=null)
                        {
                            var b=f.sharedMesh.bounds;var corners=new[]{b.min,b.max,new Vector3(b.min.x,b.min.y,b.max.z),new Vector3(b.min.x,b.max.y,b.min.z),new Vector3(b.max.x,b.min.y,b.min.z),new Vector3(b.min.x,b.max.y,b.max.z),new Vector3(b.max.x,b.min.y,b.max.z),new Vector3(b.max.x,b.max.y,b.min.z)};
                            foreach(var c in corners){var p=ships[i].transform.InverseTransformPoint(f.transform.TransformPoint(c));if(!any){localBounds=new Bounds(p,Vector3.zero);any=true;}else localBounds.Encapsulate(p);}
                        }
                        firstSize=localBounds.size;
                    }
                }
            }
            bool correctBoat=ships.All(x=>x.transform.Find("RedSailJunkVisual")!=null&&x.GetComponentsInChildren<MeshFilter>(true).All(f=>AssetDatabase.GetAssetPath(f.sharedMesh)==EnemyBoatAsset));
            bool hullAlignedToTravel=firstSize.z>firstSize.x*2f;
            var report=new {success=director!=null&&ships.Length==10&&director.shipsPerWave==2&&animators==0&&colliders==0&&correctBoat&&hullAlignedToTravel,shipCount=ships.Length,waves=ships.Length/Mathf.Max(1,director!=null?director.shipsPerWave:1),correctBoat,hullAlignedToTravel,enemyBoatAsset=EnemyBoatAsset,renderers,uniqueMeshes=meshes.Count,uniqueMaterials=materials.Count,verticesPerShip=totalVerticesPerShip,firstShipSize=firstSize.ToString("F1"),animators,colliders,allInitiallyInactive=ships.All(x=>!x.gameObject.activeSelf)};
            System.IO.File.WriteAllText("Logs/FleetAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            Debug.Log("Fleet audit: "+Newtonsoft.Json.JsonConvert.SerializeObject(report));
            EditorApplication.Exit(report.success?0:1);
        }

    }
}

