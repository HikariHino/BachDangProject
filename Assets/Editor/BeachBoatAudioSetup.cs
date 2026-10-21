using System;
using System.IO;
using BeachBoatStory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Original small procedural ambience/foley WAVs; shared with the existing imported action clips.</summary>
public static class BeachBoatAudioSetup
{
    const string Folder="Assets/BeachBoatStory/Audio";
    const int Rate=22050;
    static AudioClip Make(string name,float duration,int kind)
    {
        string path=Folder+"/"+name+".wav";
        if(!File.Exists(path))
        {
            int count=(int)(duration*Rate);var random=new System.Random(938+kind);float low=0,phase=0;
            using(var stream=new BinaryWriter(File.Create(path)))
            {
                stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));stream.Write(36+count*2);stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));stream.Write(16);stream.Write((short)1);stream.Write((short)1);stream.Write(Rate);stream.Write(Rate*2);stream.Write((short)2);stream.Write((short)16);stream.Write(System.Text.Encoding.ASCII.GetBytes("data"));stream.Write(count*2);
                for(int i=0;i<count;i++)
                {
                    float t=i/(float)Rate,n=(float)random.NextDouble()*2-1;low=Mathf.Lerp(low,n,kind==1?.014f:.09f);float value=0;
                    if(kind==0)value=(low*1.7f+n*.12f)*(.54f+.22f*Mathf.Sin(t*Mathf.PI*.5f)+.10f*Mathf.Sin(t*Mathf.PI*1.5f));
                    if(kind==1)value=low*2.7f+n*.025f*(.7f+.2f*Mathf.Sin(t*2));
                    if(kind==2)
                    {float local=t% .48f;float env=local<.21f?Mathf.Sin(local/.21f*Mathf.PI):0;phase+=(2600+1200*Mathf.Sin(local*14))*Mathf.PI*2/Rate;value=Mathf.Sin(phase)*env*.35f;}
                    if(kind==3)value=(low*2+n*.25f)*Mathf.Sin(Mathf.Min(1,t/.1f)*Mathf.PI*.5f)*Mathf.Exp(-t*8)+Mathf.Sin(t*(240-t*180)*Mathf.PI*2)*Mathf.Exp(-t*12)*.12f;
                    if(kind==4)value=Mathf.Sin(t*(230-t*500)*Mathf.PI*2)*Mathf.Exp(-t*28)*.65f+n*Mathf.Exp(-t*65)*.25f;
                    if(kind==5)value=(Mathf.Sin(t*84*Mathf.PI*2)+.35f*Mathf.Sin(t*139*Mathf.PI*2))*Mathf.Exp(-t*8)*.55f+n*Mathf.Exp(-t*35)*.15f;
                    if(kind==6)value=(n*.7f+low)*Mathf.Exp(-t*5)+n*.3f*Mathf.Exp(-Mathf.Abs(t-.11f)*65)+Mathf.Sin(t*65*Mathf.PI*2)*Mathf.Exp(-t*8)*.25f;
                    if(kind==7)value=(Mathf.Sin(t*640*Mathf.PI*2)+Mathf.Sin(t*850*Mathf.PI*2))*.22f*Mathf.Exp(-t*20);
                    if(kind==8)value=Mathf.Sin(t*150*Mathf.PI*2)*Mathf.Exp(-t*18)*.5f+n*.12f*Mathf.Exp(-t*30);
                    float edge=Mathf.Min(1,Mathf.Min(t/.008f,(duration-t)/.025f));stream.Write((short)(Mathf.Clamp(value*edge,-.9f,.9f)*32767));
                }
            }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    public static void ApplyAndExit()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var session=UnityEngine.Object.FindFirstObjectByType<BeachBoatQuestSession>();var audio=session.GetComponent<BeachBoatSoundscape>();if(audio==null)audio=session.gameObject.AddComponent<BeachBoatSoundscape>();
        audio.river=Make("River_Water_Loop",12,0);audio.wind=Make("Wind_Leaves_Loop",12,1);audio.bird=Make("Bird_Chirp",.95f,2);audio.oar=Make("Oar_Splash",.55f,3);audio.bow=Make("Bow_Twang",.18f,4);audio.drum=Make("War_Drum",.65f,5);audio.crash=Make("Wood_Crack_Splash",.9f,6);audio.good=Make("Work_Success",.18f,7);audio.bad=Make("Work_Mistake",.22f,8);
        audio.hit=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/chem trung.mp3");
        audio.footsteps=new[]{AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Action/Walk1.mp3"),AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Action/Walk2.mp3")};
        var listener=session.storyCamera.GetComponent<AudioListener>();if(listener==null)listener=session.storyCamera.gameObject.AddComponent<AudioListener>();listener.enabled=true;
        EditorUtility.audioMasterMute=false;EditorUtility.SetDirty(audio);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
}
