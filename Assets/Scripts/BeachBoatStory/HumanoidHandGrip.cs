using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>A hand frame measured on this avatar, independent of imported bone Euler axes.</summary>
    public sealed class HumanoidHandGrip
    {
        static readonly System.Collections.Generic.Dictionary<(Mesh,int),(Vector3 forward,Vector3 across,Vector3 palm,float length)> frames=new System.Collections.Generic.Dictionary<(Mesh,int),(Vector3,Vector3,Vector3,float)>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetFrames()=>frames.Clear();
        readonly Transform hand;
        readonly Vector3 forward,across,center,palm;
        readonly float length;
        internal Vector3 Forward=>forward;
        internal Vector3 Across=>across;
        internal Vector3 Palm=>palm;
        internal float Length=>length;
        public Vector3 Contact=>hand.TransformPoint(center);
        public Vector3 ThumbAxis=>hand.TransformDirection(across).normalized;
        public Vector3 FingerAxis=>hand.TransformDirection(forward).normalized;
        public Vector3 PalmAxis=>hand.TransformDirection(palm).normalized;
        internal static Transform FindEnd(Transform hand)
        {
            var named=hand.Find(hand.name+"_End");if(named!=null)return named;
            // The veteran uses Bone_029/Bone_0xx terminal joints, while the
            // player uses RightHand_End. Exclude attached weapon objects.
            foreach(Transform child in hand)if(child.localPosition.sqrMagnitude>1e-12f&&child.GetComponentInChildren<Renderer>(true)==null)return child;
            return null;
        }
        public HumanoidHandGrip(Animator rig,bool left)
        {
            hand=rig.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            var index=rig.GetBoneTransform(left?HumanBodyBones.LeftIndexProximal:HumanBodyBones.RightIndexProximal);
            var little=rig.GetBoneTransform(left?HumanBodyBones.LeftLittleProximal:HumanBodyBones.RightLittleProximal);
            if(index!=null&&little!=null)
            {
                var knuckles=(index.position+little.position)*.5f;
                forward=hand.InverseTransformDirection(knuckles-hand.position).normalized;
                across=hand.InverseTransformDirection(index.position-little.position).normalized;
                center=hand.InverseTransformPoint(Vector3.Lerp(hand.position,knuckles,.7f));
                length=center.magnitude/.7f;palm=Vector3.Cross(forward,across)*(left?-1:1);
                return;
            }
            var end=FindEnd(hand);
            forward=end!=null?end.localPosition.normalized:Vector3.up;
            length=end!=null?end.localPosition.magnitude:.001f;
            // Fingerless rigs still have a skin bind matrix. Model-forward in
            // that matrix gives the thumb side of the open bind-pose hand.
            across=Vector3.forward;
            foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                int bone=System.Array.IndexOf(skin.bones,hand);var mesh=skin.sharedMesh;
                var lod=skin.GetComponentInParent<CinematicCharacterLOD>();
                if(lod!=null&&lod.target==skin&&lod.closeMesh!=null)mesh=lod.closeMesh;
                mesh=CombatHandSources.Original(mesh);
                if(mesh==null||bone<0||bone>=mesh.bindposes.Length)continue;
                if(frames.TryGetValue((mesh,bone),out var fitted)){forward=fitted.forward;across=fitted.across;palm=fitted.palm;length=fitted.length;break;}
                if(mesh.isReadable)
                {
                    // Terminal joints can extend well past the fingertips. Fit
                    // the useful hand length to skin vertices in bind space.
                    var extents=new System.Collections.Generic.List<float>();var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes[bone];int tip=System.Array.IndexOf(skin.bones,end);
                    var mean=Vector3.zero;int count=0;
                    for(int i=0;i<vertices.Length;i++)if(Weight(weights[i],bone,tip)>.2f){mean+=bind.MultiplyPoint3x4(vertices[i]);count++;}
                    if(count>20&&mean.sqrMagnitude>1e-12f)
                    {
                        mean/=count;forward=mean.normalized;
                        // Snap near-cardinal bind axes; a thumb can skew a
                        // whole-palm PCA axis away from the four fingers.
                        int dominant=Mathf.Abs(forward.x)>Mathf.Abs(forward.y)?0:1;if(Mathf.Abs(forward.z)>Mathf.Abs(forward[dominant]))dominant=2;
                        if(Mathf.Abs(forward[dominant])>.85f){float sign=Mathf.Sign(forward[dominant]);forward=Vector3.zero;forward[dominant]=sign;}
                    }
                    for(int i=0;i<vertices.Length;i++)if(Weight(weights[i],bone,tip)>.2f)
                    {float s=Vector3.Dot(bind.MultiplyPoint3x4(vertices[i]),forward);if(s>0)extents.Add(s);}
                    if(extents.Count>20){extents.Sort();length=extents[Mathf.Min(extents.Count-1,Mathf.FloorToInt(extents.Count*.99f))];}
                }
                var axis=mesh.bindposes[bone].MultiplyVector(skin.transform.InverseTransformDirection(rig.transform.forward));
                axis=Vector3.ProjectOnPlane(axis,forward);
                if(axis.sqrMagnitude>.00001f)
                {
                    across=axis.normalized;
                    if(mesh.isReadable)
                    {
                        // Fit the broad plane of the actual palm. Model-forward
                        // can be its thin normal on a different avatar.
                        var normal=Vector3.Cross(forward,across).normalized;var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes[bone];
                        int tip=System.Array.IndexOf(skin.bones,end),n=0;float su=0,sn=0,suu=0,snn=0,sun=0;
                        for(int i=0;i<vertices.Length;i++)
                        {
                            var w=weights[i];float influence=Weight(w,bone,tip);if(influence<.2f)continue;
                            var p=bind.MultiplyPoint3x4(vertices[i])/length;float s=Vector3.Dot(p,forward);if(s<.4f||s>1.05f)continue;
                            float u=Vector3.Dot(p,across),v=Vector3.Dot(p,normal);su+=u;sn+=v;suu+=u*u;snn+=v*v;sun+=u*v;n++;
                        }
                        if(n>20)
                        {float angle=.5f*Mathf.Atan2(2*(sun-su*sn/n),(suu-su*su/n)-(snn-sn*sn/n));across=(across*Mathf.Cos(angle)+normal*Mathf.Sin(angle)).normalized;}
                        float min=0,max=0;
                        for(int i=0;i<vertices.Length;i++)
                        {if(Weight(weights[i],bone,tip)<.2f)continue;var p=bind.MultiplyPoint3x4(vertices[i])/length;float s=Vector3.Dot(p,forward);if(s<.3f||s>.72f)continue;float u=Vector3.Dot(p,across);min=Mathf.Min(min,u);max=Mathf.Max(max,u);}
                        if(max < -min)across=-across;
                    }
                    var down=mesh.bindposes[bone].MultiplyVector(skin.transform.InverseTransformDirection(-rig.transform.up));
                    var candidate=Vector3.Cross(forward,across).normalized;
                    palm=Vector3.Dot(candidate,down)>=0?candidate:-candidate;
                    frames[(mesh,bone)]=(forward,across,palm,length);
                    break;
                }
            }
            if(Vector3.Cross(forward,across).sqrMagnitude<.01f)across=Vector3.right;
            if(palm.sqrMagnitude<.1f)palm=Vector3.Cross(forward,across)*(left?-1:1);
            center=forward*(length*.64f)+palm*(length*.16f);
        }
        internal static float Weight(BoneWeight w,int hand,int end)=>(w.boneIndex0==hand||w.boneIndex0==end?w.weight0:0)+(w.boneIndex1==hand||w.boneIndex1==end?w.weight1:0)+(w.boneIndex2==hand||w.boneIndex2==end?w.weight2:0)+(w.boneIndex3==hand||w.boneIndex3==end?w.weight3:0);
        public void Align(Transform upper,Transform lower,Vector3 contact,Vector3 hint,Vector3 thumbUp)
        {
            var localFrame=Quaternion.LookRotation(forward,across);
            for(int pass=0;pass<3;pass++)
            {
                QuestRowboat.SolveArm(upper,lower,hand,contact-hand.TransformVector(center),hint);
                var along=(hand.position-lower.position).normalized;
                var up=Vector3.ProjectOnPlane(thumbUp,along);
                if(up.sqrMagnitude<.001f)up=Vector3.ProjectOnPlane(hint-upper.position,along);
                hand.rotation=Quaternion.LookRotation(along,up)*Quaternion.Inverse(localFrame);
            }
        }
    }
}
