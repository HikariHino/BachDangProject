using System.IO;
using UnityEditor;
using UnityEngine;

public static class BeachBoatNPCConversationSetup
{
    public static void Apply()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var file in Directory.GetFiles("Assets/Resources/BachDang938/Voices/NPCast","*.mp3"))
        {
            var importer=(AudioImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));importer.forceToMono=true;importer.loadInBackground=false;
            var settings=importer.defaultSampleSettings;settings.preloadAudioData=true;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.8f;settings.sampleRateSetting=AudioSampleRateSetting.OverrideSampleRate;settings.sampleRateOverride=24000;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        }
        Debug.Log("NPC speech imported as offline mono 24 kHz clips");
    }
}
