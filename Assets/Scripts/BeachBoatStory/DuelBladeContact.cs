using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Tight posed body volumes and the visible blade's swept surface. No root-distance damage.</summary>
    public sealed class DuelBladeContact
    {
        struct Part {public Transform a,b;public float radius;public Vector3 endOffset;}
        readonly List<Part> parts=new List<Part>(20);
        public DuelBladeContact(Animator rig)
        {
            float h=EnemyDeckGuard.RigHeight(rig);
            void Add(HumanBodyBones a,HumanBodyBones b,float radius,Vector3 offset=default)
            {var from=rig.GetBoneTransform(a);var to=rig.GetBoneTransform(b);if(from!=null&&to!=null)parts.Add(new Part{a=from,b=to,radius=h*radius,endOffset=offset});}
            Add(HumanBodyBones.Hips,HumanBodyBones.Chest,.09f);Add(HumanBodyBones.Chest,HumanBodyBones.Neck,.085f);
            Add(HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,.048f);
            Add(HumanBodyBones.Neck,HumanBodyBones.Head,.055f,Vector3.up*h*.035f);
            Add(HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg,.065f);
            foreach(bool left in new[]{true,false})
            {
                Add(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm,left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm,.045f);
                Add(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm,left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand,.033f);
                Add(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand,left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand,.027f);
                Add(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg,left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg,.055f);
                Add(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg,left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot,.042f);
                Add(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot,left?HumanBodyBones.LeftToes:HumanBodyBones.RightToes,.035f);
            }
        }
        public bool Touches(Vector3 previousRoot,Vector3 previousTip,Vector3 root,Vector3 tip,float bladeRadius,out Vector3 contact)
        {
            contact=default;var sweep=new Bounds(root,Vector3.zero);sweep.Encapsulate(tip);sweep.Encapsulate(previousRoot);sweep.Encapsulate(previousTip);sweep.Expand(bladeRadius*2);
            foreach(var part in parts)
            {
                var a=part.a.position;var b=part.b.position+part.endOffset;var area=new Bounds(a,Vector3.zero);area.Encapsulate(b);area.Expand(part.radius*2);if(!sweep.Intersects(area))continue;
                float radius=part.radius+bladeRadius;
                if(SegmentDistance(a,b,root,tip,out contact)<=radius*radius||SegmentDistance(a,b,previousRoot,previousTip,out contact)<=radius*radius||
                    SegmentTriangleDistance(a,b,previousRoot,previousTip,tip,out contact)<=radius*radius||SegmentTriangleDistance(a,b,previousRoot,tip,root,out contact)<=radius*radius)return true;
            }
            return false;
        }
        static float SegmentDistance(Vector3 p,Vector3 q,Vector3 a,Vector3 b,out Vector3 point)
        {
            var d=q-p;var e=b-a;var r=p-a;float dd=Vector3.Dot(d,d),ee=Vector3.Dot(e,e),de=Vector3.Dot(d,e),dr=Vector3.Dot(d,r),er=Vector3.Dot(e,r),s=0,t=0;
            if(dd<.000001f){t=ee>.000001f?Mathf.Clamp01(er/ee):0;}
            else if(ee<.000001f)s=Mathf.Clamp01(-dr/dd);
            else{float den=dd*ee-de*de;s=den>.000001f?Mathf.Clamp01((de*er-dr*ee)/den):0;t=(de*s+er)/ee;if(t<0){t=0;s=Mathf.Clamp01(-dr/dd);}else if(t>1){t=1;s=Mathf.Clamp01((de-dr)/dd);}}
            var x=p+d*s;point=a+e*t;return(x-point).sqrMagnitude;
        }
        static Vector3 ClosestTriangle(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float total=va+vb+vc;if(Mathf.Abs(total)<.000001f)return a;return a+ab*(vb/total)+ac*(vc/total);
        }
        static float SegmentTriangleDistance(Vector3 p,Vector3 q,Vector3 a,Vector3 b,Vector3 c,out Vector3 point)
        {
            var ab=b-a;var ac=c-a;var d=q-p;var normal=Vector3.Cross(ab,ac);float den=Vector3.Dot(normal,d);
            if(Mathf.Abs(den)>.000001f)
            {
                float t=Vector3.Dot(normal,a-p)/den;if(t>=0&&t<=1){var hit=p+d*t;var v=hit-a;float aa=Vector3.Dot(ab,ab),bb=Vector3.Dot(ac,ac),cc=Vector3.Dot(ab,ac),det=aa*bb-cc*cc;
                    if(Mathf.Abs(det)>.000001f){float u=(Vector3.Dot(v,ab)*bb-Vector3.Dot(v,ac)*cc)/det,w=(Vector3.Dot(v,ac)*aa-Vector3.Dot(v,ab)*cc)/det;if(u>=0&&w>=0&&u+w<=1){point=hit;return 0;}}}
            }
            point=ClosestTriangle(p,a,b,c);float best=(p-point).sqrMagnitude;var other=ClosestTriangle(q,a,b,c);float value=(q-other).sqrMagnitude;if(value<best){best=value;point=other;}
            value=SegmentDistance(p,q,a,b,out other);if(value<best){best=value;point=other;}value=SegmentDistance(p,q,b,c,out other);if(value<best){best=value;point=other;}value=SegmentDistance(p,q,c,a,out other);if(value<best){best=value;point=other;}return best;
        }
    }
}
