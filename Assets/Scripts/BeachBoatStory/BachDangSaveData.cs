using System;
using UnityEngine;

namespace BeachBoatStory
{
    [Serializable] public sealed class BachDangSaveData
    {
        public int version=1;
        public string scene="beachBoat",savedAt,location;
        public long savedUtcTicks;
        public int rowboatDurabilityVersion;
        public float boatDurability;
        public int storyState,woodCount,destroyedCount,armor,trainingLevels;
        public int archeryBestScore;public bool archeryTrainingCompleted;
        public int healingItemsVersion,healingItems;
        public bool lureBriefed;
        public float health,stamina,maxHealth,maxStamina;
        public Vector3 playerPosition,boatPosition;
        public Quaternion playerRotation=Quaternion.identity,boatRotation=Quaternion.identity;
        public bool aboard;
        public bool[] woods,stakes;
        public string[] woodIds;public Vector3[] woodPositions;public Quaternion[] woodRotations;
        public int[] stallLevels;
        public Activity[] activities;
        public Tide tide;
        public Fleet fleet;
        public Ship[] ships;
        public Guard[] guards;
        public Boarding boarding;
        public Patrol[] patrols;
        public Dialogue dialogue;
        public Campaign campaign;
        [Serializable] public sealed class MiniRecord {public string id;public float seconds,limit;}
        [Serializable] public sealed class Campaign
        {
            public float seconds,stakeSeconds,stakeTargetSeconds;public int completedMask,visitedMask,defeats,casualties,friendlyBoardings,enemyCount=64,finalScore,rankRulesVersion;
            public bool stakeStarted,stakeFinished,finished,historyIncomplete;public string finalRank;
            public bool woodStarted,woodFinished;public float woodSeconds,woodTargetSeconds;
            public MiniRecord[] minigames=Array.Empty<MiniRecord>();public string[] kills=Array.Empty<string>();
        }
        [Serializable] public sealed class Dialogue {public bool active,femaleVoice;public string speaker,line,action,voiceIdentity,voiceProfile;}
        [Serializable] public sealed class Activity {public string name;public bool completed;public float bestTime;public int bestMistakes;}
        [Serializable] public sealed class Tide {public float height,target,start,elapsed,duration,hour;public bool rising,controlled,locked,high;public int moment;}
        [Serializable] public sealed class Fleet {public bool active,ebb,invasionVoiced,ambushVoiced,counterattackVoiced,pursuitVoiced;public float counterattackDelay;public int commands,kills,alliedKills,arrows,shots,hits,voiceAdvanceMask,voicePanicMask,lureVersion,lureStage,voiceWreckCalls;public Vector3 lureAnchor;public int[] scored,suppressed;
            public int baitVersion,baitVolleys,baitResolved,baitHits;public bool baitStarted,baitPending;public float baitStartZ,baitElapsed,baitDelay,baitTime;public Vector3 baitTarget,baitFrom;}
        [Serializable] public sealed class Ship
        {
            public string name;public bool active,started,destroyed,settled,released,signal;
            public int route,routeCount;public float destroyedTime,sinkDepth,speed;public Vector3 position,sinkStart;public Quaternion rotation,sinkRotation;
        }
        [Serializable] public sealed class Guard
        {public string id,ship;public bool active,independent,floatingCorpse,anchored;public int health;public Vector3 position,localPosition;public Quaternion rotation,localRotation;}
        [Serializable] public sealed class Fighter
        {
            public string id,source,boat,target;public bool active,fromWreck,targetPlayer;public int health,state,arrows;
            public float time;public Vector3 position,start,local;public Quaternion rotation;
        }
        [Serializable] public sealed class Boarding
        {
            public int state;public bool sword;public string deckShip;public float climbTime;
            public Vector3 deckLocal,climbLocalStart;
            public Fighter[] allies,hostiles,defenders;
        }
        [Serializable] public sealed class Patrol {public string name;public Vector3 position;public Quaternion rotation;public int layoutVersion;}
    }
}
