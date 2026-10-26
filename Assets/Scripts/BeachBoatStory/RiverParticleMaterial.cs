using UnityEngine;

namespace BeachBoatStory
{
    public static class RiverParticleMaterial
    {
        public static Material Create(Texture2D texture)
        {
            var template=Resources.Load<Material>("BachDang938/RuntimeParticle");
            var material=template!=null?new Material(template):new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);return material;
        }
    }
}
