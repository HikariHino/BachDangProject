using System;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Stable character scripts and cast, independent of scene ordering or a save slot.</summary>
    public static class NPCConversationLibrary
    {
        [Serializable]public sealed class Script
        {public string key,role,profile,voice,label;public int persona;public bool female;public string[] lines;}
        [Serializable]sealed class Catalog{public Script[] actors,residents,vendors;}
        static Catalog catalog;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){catalog=null;}
        static Catalog Data
        {get{if(catalog==null){var text=Resources.Load<TextAsset>("BachDang938/Voices/NPCConversations");catalog=text!=null?JsonUtility.FromJson<Catalog>(text.text):new Catalog();}return catalog;}}
        public static int StoryStage
        {
            get
            {
                var state=BachDangStoryManager.Instance?.currentState??BachDangStoryManager.StoryState.Act1_ExploreCamp;
                switch(state)
                {
                    case BachDangStoryManager.StoryState.Act3_Victory:case BachDangStoryManager.StoryState.Act3_SearchPrisoners:
                    case BachDangStoryManager.StoryState.Act3_CommanderDuel:case BachDangStoryManager.StoryState.Act3_FinalReport:case BachDangStoryManager.StoryState.Act3_Celebration:return 2;
                    case BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp:case BachDangStoryManager.StoryState.Act2_Completed:
                    case BachDangStoryManager.StoryState.Act3_PrepareForBattle:case BachDangStoryManager.StoryState.Act3_WaitForLowTide:case BachDangStoryManager.StoryState.Act3_BattleInProgress:return 1;
                    default:return 0;
                }
            }
        }
        static uint Hash(string s){uint h=2166136261;foreach(char c in s){h^=c;h*=16777619;}return h;}
        public static bool Resolve(Transform actor,string speaker,out string line,out string profile)
        {
            line=profile=null;if(actor==null||speaker=="Ngô Quyền")return false;
            foreach(var script in Data.actors??Array.Empty<Script>())if(script.key==actor.name)return Select(script,StoryStage,out line,out profile);
            var ambient=actor.GetComponentInParent<NPCAmbientDialogue>();if(ambient==null)return false;
            bool female=DialogueVoice.ActorIsFemale(speaker,actor);int persona=(int)(Hash(DialogueVoice.ActorIdentity("Resident",actor))%2);
            foreach(var script in Data.residents??Array.Empty<Script>())
                if(script.role==ambient.role.ToString()&&script.female==female&&script.persona==persona)return Select(script,StoryStage,out line,out profile);
            return false;
        }
        public static string SpeakerLabel(Transform actor,string fallback)
        {if(actor!=null)foreach(var script in Data.actors??Array.Empty<Script>())if(script.key==actor.name&&!string.IsNullOrEmpty(script.label))return script.label;return fallback;}
        public static Script Vendor(VillageWorkStall.WorkKind kind)
        {foreach(var script in Data.vendors??Array.Empty<Script>())if(script.key=="stall_"+kind)return script;return null;}
        static bool Select(Script script,int stage,out string line,out string profile)
        {profile=script.profile;line=script.lines!=null&&script.lines.Length>0?script.lines[Mathf.Clamp(stage,0,script.lines.Length-1)]:null;return !string.IsNullOrEmpty(line);}
    }
}
