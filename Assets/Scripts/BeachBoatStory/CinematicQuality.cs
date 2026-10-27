using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BeachBoatStory
{
    /// <summary>Scene quality choice. Hardware auto-detection keeps a lighter path available.</summary>
    public static class CinematicQuality
    {
        const string Key="BachDang938.Graphics";
        public static int Choice=>Mathf.Clamp(PlayerPrefs.GetInt(Key,0),0,2);
        public static bool High=>Choice==2||Choice==0&&SystemInfo.graphicsMemorySize>=6000&&SystemInfo.systemMemorySize>=16000;
        public static string Label=>Choice==0?"TỰ ĐỘNG • "+(High?"CAO":"CÂN BẰNG"):Choice==1?"CÂN BẰNG":"CAO";
        public static UniversalRenderPipelineAsset Select(UniversalRenderPipelineAsset fallback)
        {return High?Resources.Load<UniversalRenderPipelineAsset>("BachDang938/Visuals/CinematicRP")??fallback:fallback;}
        public static void Cycle()
        {
            PlayerPrefs.SetInt(Key,(Choice+1)%3);PlayerPrefs.Save();
            Object.FindAnyObjectByType<BachDangWorldScale>()?.ApplyRenderScale();
            Object.FindAnyObjectByType<LaptopLongViewProfile>()?.Apply();
        }
        public static void ConfigureCamera(Camera camera)
        {
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.dithering=true;
            data.antialiasing=High?AntialiasingMode.SubpixelMorphologicalAntiAliasing:AntialiasingMode.None;
            data.antialiasingQuality=AntialiasingQuality.High;
        }
    }
}
