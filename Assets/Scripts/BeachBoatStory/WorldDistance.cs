using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Display metres in the fitted character's scale; gameplay ranges remain world units.</summary>
    public static class WorldDistance
    {
        public static float ToMetres(float worldUnits)=>Mathf.Max(0,worldUnits)/Mathf.Max(.01f,BeachBoatQuestSession.Instance!=null?BeachBoatQuestSession.Instance.WorldUnitsPerMetre:1);
        public static string Format(float worldUnits)
        {
            float metres=ToMetres(worldUnits);
            return (metres<10?metres.ToString("0.0"):Mathf.RoundToInt(metres).ToString())+" m";
        }
    }
}
