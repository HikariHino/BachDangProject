using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Full surface detail for nearby faces, lighter skinned meshes for the village crowds.</summary>
    [DisallowMultipleComponent]
    public sealed class CinematicCharacterLOD : MonoBehaviour
    {
        public SkinnedMeshRenderer target;
        public Mesh closeMesh,middleMesh,farMesh;
        public bool important;
        static readonly List<CinematicCharacterLOD> All=new List<CinematicCharacterLOD>();
        static float nextUpdate;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){All.Clear();nextUpdate=0;}
        void OnEnable(){if(!All.Contains(this))All.Add(this);nextUpdate=0;}
        void OnDisable(){All.Remove(this);if(target!=null){if(target.sharedMesh!=closeMesh){target.sharedMesh=null;target.sharedMesh=closeMesh;}target.forceRenderingOff=false;}}
        void Update()
        {
            if(Time.unscaledTime<nextUpdate)return;nextUpdate=Time.unscaledTime+.2f;
            RefreshForCamera(Camera.main);
        }
        public static void RefreshForCamera(Camera camera)
        {
            if(camera==null)return;
            foreach(var actor in All)
            {
                if(actor==null||actor.target==null||actor.closeMesh==null)continue;
                float d=(actor.transform.position-camera.transform.position).sqrMagnitude;
                actor.target.forceRenderingOff=!actor.important&&d>=700*700;
                var mesh=actor.important||d<100*100?actor.closeMesh:d<260*260?actor.middleMesh:actor.farMesh;
                if(mesh!=null&&actor.target.sharedMesh!=mesh){actor.target.sharedMesh=null;actor.target.sharedMesh=mesh;}
                actor.target.shadowCastingMode=d<300*300?UnityEngine.Rendering.ShadowCastingMode.On:UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
