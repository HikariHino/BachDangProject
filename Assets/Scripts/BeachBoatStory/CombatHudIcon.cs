using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    public sealed class CombatHudIcon : MaskableGraphic
    {
        public enum Symbol { Sword, Bow, Swim, Climb, Reload, Enemy, Allies }
        public Symbol symbol;
        public int DrawnVertices {get;private set;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();
            void Poly(params Vector2[] p){int first=vh.currentVertCount;foreach(var v in p)vh.AddVert(new Vector3(r.xMin+v.x*r.width,r.yMin+v.y*r.height),color,Vector2.zero);for(int i=1;i<p.Length-1;i++)vh.AddTriangle(first,first+i,first+i+1);}
            void Line(Vector2 a,Vector2 b,float width=.055f){var n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;Poly(a+n,b+n,b-n,a-n);}
            void Circle(Vector2 center,float radius){for(int i=0;i<16;i++){float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;Poly(center,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius);}}
            if(symbol==Symbol.Sword){Poly(new Vector2(.33f,.27f),new Vector2(.75f,.85f),new Vector2(.87f,.94f),new Vector2(.84f,.75f),new Vector2(.43f,.22f));Line(new Vector2(.20f,.37f),new Vector2(.53f,.12f),.08f);Line(new Vector2(.35f,.24f),new Vector2(.19f,.05f),.10f);}
            else if(symbol==Symbol.Bow){for(int i=0;i<12;i++){float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;Line(new Vector2(.28f+Mathf.Sin(a)*.35f,.12f+i*.76f/12),new Vector2(.28f+Mathf.Sin(b)*.35f,.12f+(i+1)*.76f/12));}Line(new Vector2(.28f,.12f),new Vector2(.28f,.88f),.025f);Line(new Vector2(.12f,.50f),new Vector2(.90f,.50f),.04f);Poly(new Vector2(.79f,.61f),new Vector2(.94f,.5f),new Vector2(.79f,.39f));}
            else if(symbol==Symbol.Swim){Circle(new Vector2(.65f,.74f),.11f);Line(new Vector2(.25f,.56f),new Vector2(.52f,.65f),.08f);Line(new Vector2(.52f,.65f),new Vector2(.82f,.48f),.08f);for(int row=0;row<2;row++)for(int i=0;i<5;i++)Line(new Vector2(.08f+i*.17f,.18f+row*.17f+(i%2)*.07f),new Vector2(.08f+(i+1)*.17f,.18f+row*.17f+((i+1)%2)*.07f),.04f);}
            else if(symbol==Symbol.Climb){Line(new Vector2(.27f,.10f),new Vector2(.27f,.92f));Line(new Vector2(.72f,.10f),new Vector2(.72f,.92f));for(int i=0;i<4;i++)Line(new Vector2(.27f,.22f+i*.18f),new Vector2(.72f,.22f+i*.18f));}
            else if(symbol==Symbol.Reload){for(int i=0;i<14;i++){float a=(40+i*20)*Mathf.Deg2Rad,b=(60+i*20)*Mathf.Deg2Rad;Line(new Vector2(.5f+Mathf.Cos(a)*.32f,.5f+Mathf.Sin(a)*.32f),new Vector2(.5f+Mathf.Cos(b)*.32f,.5f+Mathf.Sin(b)*.32f),.065f);}Poly(new Vector2(.83f,.37f),new Vector2(.92f,.66f),new Vector2(.64f,.55f));}
            else{Circle(new Vector2(.5f,.70f),.17f);Poly(new Vector2(.17f,.18f),new Vector2(.24f,.42f),new Vector2(.5f,.5f),new Vector2(.76f,.42f),new Vector2(.83f,.18f));if(symbol==Symbol.Enemy)Line(new Vector2(.32f,.88f),new Vector2(.68f,.88f),.08f);else{Line(new Vector2(.85f,.53f),new Vector2(.85f,.85f),.055f);Line(new Vector2(.69f,.69f),new Vector2(.99f,.69f),.055f);}}
            DrawnVertices=vh.currentVertCount;
        }
    }
}
