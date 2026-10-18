using System.Linq;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;

namespace BeachBoatStoryEditor
{
    public static class BeachBoatGateFix
    {
        [MenuItem("BeachBoat/Fix Gates - Sửa tất cả cổng")]
        [MenuItem("BeachBoat/Fix Gates/Fix All Gates")]
        public static void FixAllGates()=>Fix(null,true,true);
        [MenuItem("BeachBoat/Fix Gates/Fix Camp Gate")]
        public static void FixCampGate()=>Fix(new[]{"CampToVillage","CampToRiver"},true,true);
        [MenuItem("BeachBoat/Fix Gates/Fix Village Gate")]
        public static void FixVillageGate()=>Fix(new[]{"VillageToCamp"},true,true);
        [MenuItem("BeachBoat/Fix Gates/Fix River Gate")]
        public static void FixRiverGate()=>Fix(new[]{"RiverToCamp"},true,true);
        [MenuItem("BeachBoat/Fix Gates/Clean Gate Area")]
        public static void CleanGateArea()=>Fix(null,false,true);
        [MenuItem("BeachBoat/Fix Gates/Add Gate Collider")]
        public static void AddGateCollider()=>Fix(null,true,false);

        static void Fix(string[] names,bool collider,bool clean)
        {
            var scene=BeachBoatToolSafety.Begin("GateFix");int removed=0;
            var gates=Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsSortMode.None)
                .Where(g=>g.gameObject.scene==scene&&(names==null||names.Contains(g.name))).ToArray();
            foreach(var gate in gates)
            {
                // Preserve the authored portal, signs and arrival points.
                if(gate.destinationTarget==null){Debug.LogWarning("Missing arrival point: "+gate.name,gate);continue;}
                if(collider && gate.GetComponent<Collider>()==null)
                {
                    var box=Undo.AddComponent<BoxCollider>(gate.gameObject);
                    box.size=new Vector3(15,20,5);box.center=Vector3.up*10;box.isTrigger=true;
                }
                if(!clean)continue;
                foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if(t==null||t.gameObject.scene!=scene||!t.gameObject.activeInHierarchy)continue;
                    var root=PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
                    if(root!=t.gameObject)continue;
                    string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                    string name=System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    // Exact asset families, never short substrings such as "tu" or "ke".
                    if(!name.StartsWith("pf_props_stall")&&!name.StartsWith("pf_props_table")&&!name.StartsWith("pf_props_counter"))continue;
                    if(root.GetComponentsInChildren<MonoBehaviour>(true).Length>0||root.GetComponentInChildren<Animator>(true)!=null)continue;
                    var local=gate.transform.InverseTransformPoint(root.transform.position);
                    if(Mathf.Abs(local.x)>12||Mathf.Abs(local.z)>18||Mathf.Abs(local.y)>20)continue;
                    Undo.RecordObject(root,"Clear gate passage");root.SetActive(false);removed++;
                }
            }
            BeachBoatToolSafety.Save(scene);
            Debug.Log($"Checked {gates.Length} gates; cleared {removed} blocking props. Portal positions preserved.");
        }
    }
}

