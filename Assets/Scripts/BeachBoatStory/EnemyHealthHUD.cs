using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>One pooled screen overlay for deck soldiers and boarding survivors.</summary>
    [DefaultExecutionOrder(850)]
    public sealed class EnemyHealthHUD : MonoBehaviour
    {
        sealed class Bar
        {
            public EnemyDeckGuard guard;
            public RectTransform root;
            public Image fill;
            public Transform head;
        }
        Bar[] bars;
        RectTransform canvas;
        RiverPlayerCombat combat;
        readonly RaycastHit[] hits=new RaycastHit[16];
        public int VisibleCount {get;private set;}
        void Start()
        {
            combat=GetComponent<RiverPlayerCombat>();
            var go=new GameObject("Enemy_Health_Bars",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            go.transform.SetParent(transform,false);canvas=(RectTransform)go.transform;
            var overlay=go.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.sortingOrder=12;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        }
        void BuildBars()
        {
            if(bars!=null)foreach(var bar in bars)Destroy(bar.root.gameObject);
            bars=new Bar[combat.guards.Length];
            for(int i=0;i<bars.Length;i++)
            {
                var guard=combat.guards[i];
                var background=Graphic("Enemy_HP_"+i,canvas,new Color(.055f,.025f,.025f,.92f));
                background.rectTransform.sizeDelta=new Vector2(66,9);
                var fill=Graphic("Health",background.transform,new Color(.87f,.16f,.12f));
                fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;
                fill.rectTransform.offsetMin=new Vector2(1.5f,1.5f);fill.rectTransform.offsetMax=new Vector2(-1.5f,-1.5f);
                fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;
                bars[i]=new Bar{guard=guard,root=background.rectTransform,fill=fill,head=guard.visual.GetComponentInChildren<Animator>(true).GetBoneTransform(HumanBodyBones.Head)};
                background.gameObject.SetActive(false);
            }
        }
        static Image Graphic(string name,Transform parent,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        public float DisplayedHealth(EnemyDeckGuard guard)
        {if(bars!=null)foreach(var bar in bars)if(bar.guard==guard)return bar.fill.fillAmount;return -1;}
        void LateUpdate()
        {
            if(canvas==null)return;
            if(bars==null||bars.Length!=combat.guards.Length)BuildBars();
            var camera=BeachBoatQuestSession.Instance.storyCamera;VisibleCount=0;
            bool battle=combat.BattleActive&&Time.timeScale>0&&!RiverInvasionIntro.IsRunning;
            foreach(var bar in bars)
            {
                var guard=bar.guard;bar.fill.fillAmount=guard.HealthFraction;
                bar.fill.rectTransform.anchorMax=new Vector2(guard.HealthFraction,1);
                bool show=battle&&guard.IsAlive;
                var point=bar.head.position+Vector3.up*(guard.BodyHeight*.15f);
                var screen=camera.WorldToScreenPoint(point);
                show&=screen.z>camera.nearClipPlane&&screen.z<650&&screen.x>0&&screen.x<Screen.width&&screen.y>0&&screen.y<Screen.height;
                if(show)
                {
                    var ray=point-camera.transform.position;
                    int count=Physics.RaycastNonAlloc(camera.transform.position,ray.normalized,hits,ray.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                    for(int i=0;i<count;i++)
                    {
                        var hit=hits[i];
                        if(hit.collider==null||hit.transform.IsChildOf(guard.transform)||hit.collider.GetComponentInParent<EnemyDeckGuard>()!=null||hit.collider.GetComponentInParent<Character_Movement>()!=null)continue;
                        show=false;break;
                    }
                }
                bar.root.gameObject.SetActive(show);
                if(!show)continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas,screen,null,out var local);
                bar.root.anchoredPosition=local;VisibleCount++;
            }
        }
    }
}
