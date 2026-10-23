using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BeachBoatDialogueVoiceSetup
{
    public static void ApplyAndExit()
    {
        const string folder="Assets/Resources/BachDang938/Voices";
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var files=Directory.GetFiles(folder,"*.mp3",SearchOption.AllDirectories);
        if(files.Length<76)throw new Exception("Vietnamese dialogue recordings are incomplete");
        foreach(var file in files)
        {
            var path=file.Replace('\\','/');var importer=(AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono=true;importer.loadInBackground=false;
            var settings=importer.defaultSampleSettings;settings.preloadAudioData=true;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.8f;importer.defaultSampleSettings=settings;
            importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if(clip==null||clip.length<.5f)throw new Exception("Invalid dialogue clip: "+path);
        }
        Debug.Log("Imported "+files.Length+" offline Vietnamese dialogue clips.");AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
}
