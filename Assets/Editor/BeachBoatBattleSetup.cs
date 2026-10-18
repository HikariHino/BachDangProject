using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using System.Linq;
using System.Collections.Generic;
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
            var spawns=new Vector3[shipCount];var targets=new Transform[shipCount];
            // Check every route before touching the scene. All points must remain wet at low tide.
            for(int i=0;i<shipCount;i++)
            {
                int cluster=i/2;
                var indices=zone.placements[cluster].stakeIndices;
                int stakeIndex=indices[Mathf.Min((i%2)*Mathf.Max(1,indices.Length/2),indices.Length-1)];
                var stake=zone.fieldStakes[stakeIndex];targets[i]=stake;
                var target=stake.GetComponentInChildren<Renderer>(true).bounds.center;target.y=tide.lowTideY;
                bool found=false;
                for(int a=0;a<24&&!found;a++)
                {
                    var dir=Quaternion.Euler(0,a*15+(i%2==0?-6:6),0)*Vector3.right;bool wet=true;
                    for(int j=1;j<=16;j++)
                    {
                        var p=target+dir*j*10;var local=p-terrain.transform.position;var size=terrain.terrainData.size;
                        if(local.x<0||local.z<0||local.x>size.x||local.z>size.z||terrain.SampleHeight(p)+terrain.transform.position.y+8>tide.lowTideY){wet=false;break;}
                    }
                    if(wet){spawns[i]=target+dir*160;found=true;}
                }
                if(!found)throw new System.InvalidOperationException("No water route for enemy ship "+i);
            }
            var root=new GameObject("EnemyFleetDirector_RedSailJunk");director=root.AddComponent<EnemyFleetDirector>();
            var combat=root.AddComponent<RiverBattleChoreography>();
            combat.friendlyBoats=Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            combat.projectilePoolSize=32;combat.volleyInterval=.72f;
            director.enemyShips=new EnemyShip[shipCount];director.spawnPoints=new Transform[shipCount];
            director.shipsPerWave=2;director.waveDelay=2;
            for(int i=0;i<shipCount;i++)
            {
                var go=new GameObject("EnemyWarship_NamHan_"+(i+1));go.transform.SetParent(root.transform);
                go.transform.position=spawns[i];
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
                var ship=go.AddComponent<EnemyShip>();ship.spawnPoint=spawns[i];ship.targetPoint=targets[i];ship.moveSpeed=30;ship.stakeHitDistance=28;
                director.enemyShips[i]=ship;go.SetActive(false);
                var spawn=new GameObject("Spawn_"+(i+1));spawn.transform.SetParent(root.transform);spawn.transform.position=spawns[i];director.spawnPoints[i]=spawn.transform;
            }
            var trigger=new GameObject("BattleTrigger");trigger.transform.SetParent(root.transform);trigger.transform.position=boat.boardingPoint.position;
            trigger.AddComponent<BattleTrigger>().boat=boat;
            Undo.RegisterCreatedObjectUndo(root,"Set up quest battle");
            BeachBoatToolSafety.Save(scene);Selection.activeGameObject=root;
            Debug.Log("Quest 3 upgraded: 10 shared-mesh red-sail war junks in five two-ship waves.",root);
        }

        // Entry point used by the CLI after scripts finish compiling.
        public static void SetupBattleAndExit()
        {
            if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity",UnityEditor.SceneManagement.OpenSceneMode.Single);
            SetupBattle();
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

