using UnityEditor;
using UnityEngine;
using BeachBoatStory;

namespace BeachBoatStoryEditor
{
    public static class BeachBoatMinigameSetup
    {
        [MenuItem("BeachBoat/Setup Minigames")]
        public static void Setup()
        {
            var scene = BeachBoatToolSafety.Begin("Minigames");
            if (GameObject.Find("BeachBoatMinigames") != null)
            {
                Debug.Log("BeachBoat minigames already exist; no duplicates created.");
                return;
            }
            var terrain = Terrain.activeTerrain;
            var commander = Object.FindAnyObjectByType<BeachBoatStory.NPCQuestGiver>();
            var villageGate = GameObject.Find("VillageToCamp");
            if (terrain == null || commander == null || villageGate == null)
                throw new System.InvalidOperationException("Terrain, commander or village gate is missing.");

            var wood = BeachBoatToolSafety.Material("MinigameWood", new Color(.28f, .12f, .045f));
            var skin = BeachBoatToolSafety.Material("DrumSkin", new Color(.56f, .42f, .25f));
            var signal = BeachBoatToolSafety.Material("MinigameSignal", new Color(1f, .55f, .08f), false, true);
            var root = new GameObject("BeachBoatMinigames");
            Undo.RegisterCreatedObjectUndo(root, "Add beachBoat minigames");

            // Keep the drum in the open training yard, away from the barracks row.
            var drumPosition = Ground(terrain, commander.transform.position + new Vector3(65, 0, 115), 8);
            var drum = Primitive("Minigame_TrongTran", PrimitiveType.Cylinder, root.transform, drumPosition,
                new Vector3(8, 6, 8), skin, true);
            drum.transform.rotation = Quaternion.Euler(0, 18, 90);
            var drumRenderer = drum.GetComponent<Renderer>();
            drum.transform.position += Vector3.up *
                (terrain.SampleHeight(drumPosition) + terrain.transform.position.y + .15f - drumRenderer.bounds.min.y);
            var rim = Primitive("WoodenRim", PrimitiveType.Cylinder, drum.transform, Vector3.zero,
                new Vector3(1.12f, .7f, 1.12f), wood, false, true);
            rim.transform.localPosition = Vector3.zero;
            var drumGame = Undo.AddComponent<BeachBoatMinigameStation>(drum);
            drumGame.kind = BeachBoatMinigameStation.ActivityKind.WarDrum;
            drumGame.displayName = "Minigame Trống trận (phím 1-2-3)";
            drumGame.interactDistance = 50;

            var supplyPosition = Ground(terrain, villageGate.transform.position + new Vector3(150, 0, 120), 0);
            var supplyRoot = new GameObject("Minigame_TiepTe");
            supplyRoot.transform.SetParent(root.transform); supplyRoot.transform.position = supplyPosition;
            var supplyGame = Undo.AddComponent<BeachBoatMinigameStation>(supplyRoot);
            supplyGame.kind = BeachBoatMinigameStation.ActivityKind.SupplyRelay;
            supplyGame.displayName = "Minigame Tiếp tế dân làng";
            supplyGame.interactDistance = 50;
            for (int i = 0; i < 3; i++)
                Primitive("SupplyCrate_" + (i + 1), PrimitiveType.Cube, supplyRoot.transform,
                    new Vector3((i - 1) * 14, 5, 0), new Vector3(12, 10, 12), wood, true, true);

            Vector3[] offsets =
            {
                new Vector3(95,0,30), new Vector3(145,0,-75),
                new Vector3(40,0,-145), new Vector3(-85,0,-55)
            };
            supplyGame.checkpoints = new Transform[offsets.Length];
            supplyGame.checkpointMarkers = new GameObject[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                var point = new GameObject("SupplyPoint_" + (i + 1));
                point.transform.SetParent(root.transform); point.transform.position = Ground(terrain, supplyPosition + offsets[i], 1.5f);
                var marker = Primitive("Signal", PrimitiveType.Cylinder, point.transform, Vector3.zero,
                    new Vector3(9, .5f, 9), signal, false, true);
                marker.transform.localPosition = Vector3.zero; marker.SetActive(false);
                supplyGame.checkpoints[i] = point.transform; supplyGame.checkpointMarkers[i] = marker;
            }
            EditorUtility.SetDirty(drumGame); EditorUtility.SetDirty(supplyGame);
            BeachBoatToolSafety.Save(scene);
            Selection.activeGameObject = root;
            Debug.Log("Added two lightweight repeatable minigames: war drum and village supply relay.");
        }

        static Vector3 Ground(Terrain terrain, Vector3 position, float lift)
        {
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + lift;
            return position;
        }

        static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position,
            Vector3 scale, Material material, bool collider, bool local = false)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            if (local) go.transform.localPosition = position; else go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }
}
