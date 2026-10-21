using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Small vector illustrations; no generated textures or per-frame meshes.</summary>
    public sealed class VillageWorkIcon : MaskableGraphic
    {
        public int DrawnVertices {get;private set;}
        public int kind;
        bool damaged;
        int ports;
        public void SetDamaged(bool value){if(value==damaged)return;damaged=value;SetVerticesDirty();}
        public void SetPorts(int value){if(value==ports)return;ports=value;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var rect=GetPixelAdjustedRect();
            void Poly(Color c,params Vector2[] points)
            {
                int first=vh.currentVertCount;
                foreach(var p in points)vh.AddVert(new Vector3(rect.xMin+p.x*rect.width,rect.yMin+p.y*rect.height),c*color,Vector2.zero);
                for(int n=1;n<points.Length-1;n++)vh.AddTriangle(first,first+n,first+n+1);
                DrawnVertices=vh.currentVertCount;
            }
            void Quad(float x,float y,float w,float h,Color c)=>Poly(c,new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h));
            var wood=new Color(.64f,.39f,.17f);var dark=new Color(.23f,.13f,.06f);var grain=new Color(.90f,.77f,.42f);
            if(kind==4)
            {
                Quad(.4f,.4f,.2f,.2f,grain);
                if((ports&1)!=0)Quad(.4f,.5f,.2f,.5f,grain);
                if((ports&2)!=0)Quad(.5f,.4f,.5f,.2f,grain);
                if((ports&4)!=0)Quad(.4f,0,.2f,.5f,grain);
                if((ports&8)!=0)Quad(0,.4f,.5f,.2f,grain);
                return;
            }
            if(kind==0)
            {
                Quad(.08f,.30f,.84f,.35f,wood);Quad(.12f,.35f,.70f,.025f,grain);Quad(.18f,.54f,.68f,.018f,dark);
                if(damaged){Poly(dark,new Vector2(.47f,.30f),new Vector2(.58f,.45f),new Vector2(.48f,.49f),new Vector2(.62f,.65f),new Vector2(.53f,.65f),new Vector2(.40f,.48f),new Vector2(.50f,.43f),new Vector2(.39f,.30f));}
                else {Quad(.10f,.30f,.025f,.35f,dark);Quad(.86f,.30f,.025f,.35f,dark);}return;
            }
            if(kind==1)
            {
                Poly(wood,new Vector2(.10f,.42f),new Vector2(.90f,.42f),new Vector2(.75f,.12f),new Vector2(.25f,.12f));
                for(int row=0;row<3;row++)for(int n=0;n<5-row;n++){float x=.18f+row*.09f+n*.13f,y=.45f+row*.09f;Poly(grain,new Vector2(x,y),new Vector2(x+.11f,y),new Vector2(x+.08f,y+.08f));}return;
            }
            if(kind==2)
            {
                Poly(new Color(.62f,.40f,.22f),new Vector2(.15f,.48f),new Vector2(.28f,.70f),new Vector2(.70f,.67f),new Vector2(.88f,.48f),new Vector2(.70f,.30f),new Vector2(.28f,.30f));
                Poly(wood,new Vector2(.22f,.48f),new Vector2(.02f,.70f),new Vector2(.02f,.25f));Quad(.72f,.51f,.035f,.035f,dark);return;
            }
            Poly(new Color(.89f,.88f,.79f),new Vector2(.08f,.20f),new Vector2(.50f,.78f),new Vector2(.92f,.20f));
            Poly(Color.white,new Vector2(.50f,.78f),new Vector2(.48f,.22f),new Vector2(.20f,.22f));
        }
    }
}
