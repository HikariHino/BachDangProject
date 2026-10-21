using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BeachBoatRiverCombatSetup
{
    static float DeckHeight(EnemyShip ship,Vector3 point)
    {
        float best=float.NegativeInfinity;
        foreach(var filter in ship.transform.Find("RedSailJunkVisual").GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh=filter.sharedMesh;if(mesh==null)continue;
            var matrix=ship.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            var vertices=mesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray();var indices=mesh.triangles;
            for(int n=0;n<indices.Length;n+=3)
            {
                var a=vertices[indices[n]];var b=vertices[indices[n+1]];var c=vertices[indices[n+2]];
                var normal=Vector3.Cross(b-a,c-a);if(normal.sqrMagnitude<.0001f||Mathf.Abs(normal.normalized.y)<.65f)continue;
                float den=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(den)<.0001f)continue;
                float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/den;
                float v=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/den;
                if(u<0||v<0||u+v>1)continue;
                float y=u*a.y+v*b.y+(1-u-v)*c.y;if(y>=0&&y<16)best=Mathf.Max(best,y);
            }
        }
        if(float.IsNegativeInfinity(best))throw new InvalidOperationException("No walkable deck under archer on "+ship.name);
        return best+.15f;
    }
    public static void FitDecksAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        foreach(var guard in UnityEngine.Object.FindObjectsByType<EnemyDeckGuard>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {var p=guard.transform.localPosition;p.y=DeckHeight(guard.ship,p);guard.transform.localPosition=p;}
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorApplication.Exit(0);
    }
    public static void ApplyAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        var session=UnityEngine.Object.FindFirstObjectByType<BeachBoatQuestSession>();
        if(session.GetComponent<RiverPlayerCombat>()!=null)throw new InvalidOperationException("River combat is already installed");
        Directory.CreateDirectory(".utmp/RiverCombat");EditorSceneManager.SaveScene(scene,".utmp/RiverCombat/before.unity",true);
        var combat=session.gameObject.AddComponent<RiverPlayerCombat>();
        combat.bowController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Main_Character/Animation/Crossbow_Animation/BowAnimation.controller");
        combat.bowMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/BeachBoatStory/Camp/Barracks_Dark_Beams.mat");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model_NPC/OldSoldier/Meshy_AI_Dũng_the_Veteran_biped/Meshy_AI_Dũng_the_Veteran_biped_Character_output.fbx");
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Model_NPC/OldSoldier/Meshy_AI_Dũng_the_Veteran_biped/Materials/Meshy_AI_Dũng_the_Veteran_biped_texture_0.mat");
        var idle=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/BachDangAtmosphere/BachDang_GuardIdle.controller");
        if(source==null||material==null||combat.bowController==null||combat.bowMaterial==null)throw new InvalidOperationException("Shared combat art is missing");
        var fleet=UnityEngine.Object.FindFirstObjectByType<EnemyFleetDirector>();var guards=new System.Collections.Generic.List<EnemyDeckGuard>();
        for(int index=0;index<fleet.enemyShips.Length;index++)
        for(int side=0;side<2;side++)
        {
            var ship=fleet.enemyShips[index];var root=new GameObject("Xa_Thu_Nam_Han_"+(side+1));root.transform.SetParent(ship.transform,false);
            root.transform.localPosition=new Vector3(side==0?-6:6,7.5f,side==0?25:-22);
            var deck=root.transform.localPosition;deck.y=DeckHeight(ship,deck);root.transform.localPosition=deck;
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source,root.transform);model.name="Linh_Nam_Han_Visual";model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one;
            foreach(var c in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
            foreach(var c in model.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(c);
            foreach(var c in model.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(c);
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();r.updateWhenOffscreen=false;}
            var bounds=BeachBoatWorldAudit.BoundsOf(model.transform);model.transform.localScale*=16/bounds.size.y;bounds=BeachBoatWorldAudit.BoundsOf(model.transform);
            model.transform.position+=Vector3.up*(root.transform.position.y-bounds.min.y);
            var animator=model.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=idle;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.CullCompletely;
            animator.gameObject.AddComponent<NPCUprightPosture>();
            var capsule=root.AddComponent<CapsuleCollider>();capsule.isTrigger=true;capsule.height=16;capsule.radius=3;capsule.center=Vector3.up*8;
            var guard=root.AddComponent<EnemyDeckGuard>();guard.ship=ship;guard.visual=model.transform;guard.waveIndex=index/Mathf.Max(1,fleet.shipsPerWave);guards.Add(guard);
            var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.012f,model.GetComponentsInChildren<Renderer>(true))});lod.RecalculateBounds();
        }
        combat.guards=guards.ToArray();EditorUtility.SetDirty(combat);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
}
