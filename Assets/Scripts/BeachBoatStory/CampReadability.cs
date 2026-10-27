using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BeachBoatStory
{
    /// <summary>A small exposure/shadow lift limited to the camp, without adding more realtime lights.</summary>
    [DisallowMultipleComponent]
    public sealed class CampReadability : MonoBehaviour
    {
        GameObject region;VolumeProfile profile;
        public Volume CampVolume {get;private set;}
        public Bounds CampBounds {get;private set;}
        IEnumerator Start()
        {
            yield return null;
            var facilities=FindObjectsByType<CampFacility>(FindObjectsSortMode.None);if(facilities.Length==0)yield break;
            var bounds=new Bounds(facilities[0].transform.position,Vector3.zero);foreach(var f in facilities)bounds.Encapsulate(f.transform.position);
            bounds.Expand(new Vector3(150,160,150));CampBounds=bounds;
            region=new GameObject("Camp_Readability_Region");region.transform.SetParent(transform,false);region.transform.position=bounds.center;
            var box=region.AddComponent<BoxCollider>();box.isTrigger=true;box.size=bounds.size;
            CampVolume=region.AddComponent<Volume>();CampVolume.isGlobal=false;CampVolume.priority=25;CampVolume.blendDistance=55;CampVolume.weight=1;
            profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Camp readability (runtime)";CampVolume.sharedProfile=profile;
            var color=profile.Add<ColorAdjustments>();color.postExposure.Override(.7f);color.contrast.Override(4);
            // Open dark armour and faces without turning the night sky into daylight.
            var tone=profile.Add<LiftGammaGain>();tone.gamma.Override(new Vector4(1,1,1,.07f));
            foreach(var torch in FindObjectsByType<TorchFlame>(FindObjectsSortMode.None))
                if(bounds.Contains(torch.transform.position)&&torch.fireLight!=null){torch.baseIntensity*=1.2f;torch.fireLight.range*=1.15f;}
        }
        void OnDestroy(){if(region!=null)Destroy(region);if(profile!=null){foreach(var component in profile.components)Destroy(component);Destroy(profile);}}
    }
}
