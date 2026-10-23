using UnityEngine;

namespace BeachBoatStory
{
    public static class BachDangPreferences
    {
        public static float Volume=>Mathf.Clamp01(PlayerPrefs.GetFloat("BachDang938.Volume",1));
        public static float Sensitivity=>Mathf.Clamp(PlayerPrefs.GetFloat("BachDang938.Mouse",1),.25f,2.5f);
        public static void SetVolume(float value)
        {PlayerPrefs.SetFloat("BachDang938.Volume",Mathf.Clamp01(value));PlayerPrefs.Save();Apply();}
        public static void SetSensitivity(float value)
        {PlayerPrefs.SetFloat("BachDang938.Mouse",Mathf.Clamp(value,.25f,2.5f));PlayerPrefs.Save();Apply();}
        public static void Apply()
        {
            AudioListener.volume=BeachBoatSoundscape.Instance!=null&&BeachBoatSoundscape.Instance.Muted?0:Volume;
            var camera=BeachBoatQuestSession.Instance?.storyCamera?.GetComponent<Camera_Script>();
            if(camera!=null){camera.mouseXSpeed=2*Sensitivity;camera.mouseYSpeed=1.5f*Sensitivity;}
        }
    }
}
