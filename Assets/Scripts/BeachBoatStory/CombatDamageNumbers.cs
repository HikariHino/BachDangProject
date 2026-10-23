using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>One bounded pool, shared by arrows and swords, reporting actual health lost.</summary>
    public sealed class CombatDamageNumbers : MonoBehaviour
    {
        sealed class Number { public TMP_Text label; public Vector3 point; public float age; }
        static CombatDamageNumbers instance;
        readonly Number[] pool=new Number[24];
        int next;
        public int DisplayedCount {get;private set;}
        public int LastDamage {get;private set;}
        public static CombatDamageNumbers Instance=>instance;
        public static void Show(Vector3 point,int damage)
        {
            if(damage<=0)return;
            if(instance==null)new GameObject("CombatDamageNumbers").AddComponent<CombatDamageNumbers>();
            var item=instance.pool[instance.next++%instance.pool.Length];
            item.point=point;item.age=0;item.label.text="−"+damage;item.label.gameObject.SetActive(true);
            instance.LastDamage=damage;instance.DisplayedCount++;
        }
        void Awake()
        {
            instance=this;
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=125;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
            for(int i=0;i<pool.Length;i++)
            {
                var go=new GameObject("Damage"+i,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(transform,false);
                var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(110,50);
                var text=go.GetComponent<TMP_Text>();StoryFont.Apply(text);text.fontSize=30;text.fontStyle=FontStyles.Bold;text.alignment=TextAlignmentOptions.Center;
                text.color=new Color(1,.83f,.32f);text.outlineWidth=.18f;text.outlineColor=Color.black;text.raycastTarget=false;
                pool[i]=new Number{label=text,age=2};go.SetActive(false);
            }
        }
        void LateUpdate()
        {
            var camera=BeachBoatQuestSession.Instance?.storyCamera;if(camera==null)return;
            foreach(var item in pool)
            {
                if(!item.label.gameObject.activeSelf)continue;
                item.age+=Time.deltaTime;
                var screen=camera.WorldToScreenPoint(item.point+Vector3.up*(2+item.age*9));
                if(item.age>1.1f||screen.z<=0){item.label.gameObject.SetActive(false);continue;}
                item.label.transform.position=new Vector3(screen.x,screen.y,0);
                item.label.alpha=1-Mathf.InverseLerp(.55f,1.1f,item.age);
            }
        }
        void OnDestroy(){if(instance==this)instance=null;}
    }
}
