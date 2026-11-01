using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Short, non-blocking vendor replies on one shared speech channel.</summary>
    public sealed class VillageVendorSpeech:MonoBehaviour
    {
        public static VillageVendorSpeech Instance {get;private set;}
        DialogueVoice voice;float remaining;string caption;GUIStyle captionStyle;
        public DialogueVoice Voice=>voice;
        public string Caption=>remaining>0?caption:"";
        public static void Say(VillageWorkStall.WorkKind kind,int stage)
        {
            if(StoryDialogueUI.Instance?.IsOpen==true)return;
            if(Instance==null){var go=new GameObject("Village_vendor_speech");go.transform.SetParent(BeachBoatQuestSession.Instance.transform,false);go.AddComponent<VillageVendorSpeech>();}
            Instance.Play(kind,stage);
        }
        void Awake(){Instance=this;voice=gameObject.AddComponent<DialogueVoice>();}
        void Play(VillageWorkStall.WorkKind kind,int stage)
        {
            var script=NPCConversationLibrary.Vendor(kind);if(script==null)return;
            string speaker=kind==VillageWorkStall.WorkKind.Rice?"Người đong gạo":kind==VillageWorkStall.WorkKind.Timber?"Thợ cưa":kind==VillageWorkStall.WorkKind.Rope?"Người bện dây":"Người gói lương";
            string line=script.lines[Mathf.Clamp(stage,0,script.lines.Length-1)];voice.Play(speaker,line,script.key,script.female,script.profile);caption=speaker+": "+line;remaining=Mathf.Max(2,voice.Duration);
        }
        public void Stop(){remaining=0;voice?.Stop();}
        void Update(){if(StoryDialogueUI.Instance?.IsOpen==true){Stop();return;}if(!BachDangFrontEnd.IsBlocking&&remaining>0)remaining=Mathf.Max(0,remaining-Time.unscaledDeltaTime);}
        void OnGUI()
        {
            if(remaining<=0||BachDangFrontEnd.IsBlocking||OpeningEvent.IsOpeningActive)return;
            if(captionStyle==null)captionStyle=new GUIStyle(GUI.skin.box){alignment=TextAnchor.MiddleLeft,wordWrap=true};captionStyle.fontSize=Mathf.Clamp(Screen.height/45,14,21);
            GUI.Box(new Rect(Screen.width*.025f,Screen.height*.285f,Screen.width*.30f,Screen.height*.12f),caption,captionStyle);
        }
        void OnDisable()=>Stop();
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
