using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Keeps the 6 km battlefield visible while limiting costly near-detail on 16 GB laptops.</summary>
    [DisallowMultipleComponent]
    public sealed class LaptopLongViewProfile : MonoBehaviour
    {
        [Min(1000)] public float cameraDistance = 9500;
        [Min(100)] public float treeDistance = 1400;
        [Min(50)] public float treeBillboardDistance = 280;
        [Min(1)] public int maximumFullLodTrees = 500;
        [Min(64)] public float textureStreamingBudgetMB = 384;

        void Awake() { Apply(); }
        void OnEnable() { if (Application.isPlaying) Apply(); }

        public void Apply()
        {
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) continue;
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, cameraDistance);
                CinematicQuality.ConfigureCamera(camera);
            }
            foreach (var terrain in FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if(CinematicQuality.High)
                {
                    terrain.treeDistance=2800;terrain.treeBillboardDistance=650;terrain.treeCrossFadeLength=90;
                    terrain.treeMaximumFullLODCount=1400;terrain.detailObjectDistance=100;terrain.detailObjectDensity=.6f;
                    terrain.heightmapPixelError=4;terrain.basemapDistance=1800;continue;
                }
                terrain.treeDistance = Mathf.Min(terrain.treeDistance, treeDistance);
                terrain.treeBillboardDistance = Mathf.Min(terrain.treeBillboardDistance, treeBillboardDistance);
                terrain.treeCrossFadeLength = Mathf.Min(terrain.treeCrossFadeLength, 60);
                terrain.treeMaximumFullLODCount = Mathf.Min(terrain.treeMaximumFullLODCount, maximumFullLodTrees);
                terrain.detailObjectDistance = 0;
                terrain.heightmapPixelError = Mathf.Max(terrain.heightmapPixelError, 8);
                terrain.basemapDistance = Mathf.Min(terrain.basemapDistance, 1200);
            }
            QualitySettings.streamingMipmapsActive = true;
            QualitySettings.globalTextureMipmapLimit=CinematicQuality.High?0:1;
            QualitySettings.streamingMipmapsMemoryBudget = CinematicQuality.High?2048:textureStreamingBudgetMB;
            QualitySettings.streamingMipmapsMaxLevelReduction = CinematicQuality.High?1:2;
            QualitySettings.lodBias = CinematicQuality.High?1.4f:.9f;
            QualitySettings.anisotropicFiltering=CinematicQuality.High?AnisotropicFiltering.ForceEnable:AnisotropicFiltering.Enable;
        }
    }
}
