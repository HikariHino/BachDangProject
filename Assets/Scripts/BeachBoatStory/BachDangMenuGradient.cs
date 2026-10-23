using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    // Custom Graphics need their renderer before they join Canvas rebuilds or pointer raycasts.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BachDangMenuGradient : MaskableGraphic
    {
        protected override void Awake(){base.Awake();raycastTarget=false;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float[] xs={0,.37f,.70f,1},alpha={.87f,.72f,.10f,0};
            for(int i=0;i<4;i++){var color=new Color(.005f,.02f,.019f,alpha[i]);vh.AddVert(new Vector3(r.xMin+r.width*xs[i],r.yMin),color,Vector2.zero);vh.AddVert(new Vector3(r.xMin+r.width*xs[i],r.yMax),color,Vector2.one);}
            for(int i=0;i<3;i++){int v=i*2;vh.AddTriangle(v,v+1,v+2);vh.AddTriangle(v+2,v+1,v+3);}
        }
    }
}
