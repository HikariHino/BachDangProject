using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>First-person forearms made from the player's mesh, with an isolated display rig.</summary>
    public sealed class WorkbenchHands
    {
        public enum GripPose { Support, Rod, Pinch }
        readonly Dictionary<Renderer,bool> originalVisibility=new Dictionary<Renderer,bool>();
        readonly List<GameObject> objects=new List<GameObject>();
        readonly List<Mesh> meshes=new List<Mesh>();
        sealed class DisplayRig
        {
            public Dictionary<Transform,Transform> bones;
            public Transform leftUpper,leftLower,leftHand,rightUpper,rightLower,rightHand;
            public Quaternion leftLowerFrame,rightLowerFrame,leftUpperFrame,rightUpperFrame;
            public Vector3 leftLowerAlong,rightLowerAlong;
        }
        readonly List<DisplayRig> rigs=new List<DisplayRig>();
        readonly BowGripProfile profile;
        GripPose leftGrip,rightGrip;bool usesWorkMesh;
        public bool Visible {get;private set;}
        public int TriangleCount {get;private set;}
        public float MaxSegmentError {get;private set;}
        public float MaxWristAngle {get;private set;}
        public float MaxContactError {get;private set;}
        public float ActiveGripWeight {get;private set;}
        public WorkbenchHands(Transform player,Animator rig)
        {
            profile=BowGripProfile.Load();
            var armBones=new HashSet<Transform>(new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightHand}.Select(rig.GetBoneTransform));
            foreach(var hand in new[]{rig.GetBoneTransform(HumanBodyBones.LeftHand),rig.GetBoneTransform(HumanBodyBones.RightHand)})foreach(Transform child in hand)if(child.name==hand.name+"_End")armBones.Add(child);
            foreach(var source in player.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(source.sharedMesh==null||!source.sharedMesh.isReadable||!source.bones.Any(armBones.Contains))continue;
                var workMesh=Resources.Load<Mesh>("BachDang938/Player/WorkbenchHandGrips");
                usesWorkMesh|=workMesh!=null;
                var input=profile!=null&&(source.sharedMesh==profile.source||source.sharedMesh==profile.posed)?(workMesh!=null?workMesh:profile.posed):source.sharedMesh;var weights=input.boneWeights;var bones=source.bones;
                bool Arm(int index)=>index>=0&&index<bones.Length&&armBones.Contains(bones[index]);
                bool Keep(int vertex)
                {
                    var w=weights[vertex];float sum=(Arm(w.boneIndex0)?w.weight0:0)+(Arm(w.boneIndex1)?w.weight1:0)+(Arm(w.boneIndex2)?w.weight2:0)+(Arm(w.boneIndex3)?w.weight3:0);
                    return sum>=.65f;
                }
                var mesh=Object.Instantiate(input);mesh.name="Workbench forearms only";
                var displayWeights=(BoneWeight[])weights.Clone();
                for(int v=0;v<displayWeights.Length;v++)
                {
                    var w=displayWeights[v];if(!Arm(w.boneIndex0))w.weight0=0;if(!Arm(w.boneIndex1))w.weight1=0;if(!Arm(w.boneIndex2))w.weight2=0;if(!Arm(w.boneIndex3))w.weight3=0;
                    float sum=w.weight0+w.weight1+w.weight2+w.weight3;
                    if(sum>0){w.weight0/=sum;w.weight1/=sum;w.weight2/=sum;w.weight3/=sum;}displayWeights[v]=w;
                }
                mesh.boneWeights=displayWeights;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var triangles=input.GetTriangles(sub);var kept=new List<int>();
                    for(int i=0;i<triangles.Length;i+=3)if(Keep(triangles[i])&&Keep(triangles[i+1])&&Keep(triangles[i+2]))kept.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});
                    mesh.SetTriangles(kept,sub);TriangleCount+=kept.Count/3;
                }
                var go=new GameObject("Workbench_Hands_Only");go.transform.SetParent(source.transform,false);
                var map=new Dictionary<Transform,Transform>();
                foreach(var bone in source.bones)if(bone!=null&&!map.ContainsKey(bone))map[bone]=new GameObject(bone.name+"_Workbench").transform;
                foreach(var item in map)item.Value.SetParent(item.Key.parent!=null&&map.TryGetValue(item.Key.parent,out var parent)?parent:go.transform,false);
                var display=new DisplayRig{bones=map,leftUpper=rig.GetBoneTransform(HumanBodyBones.LeftUpperArm),rightUpper=rig.GetBoneTransform(HumanBodyBones.RightUpperArm),leftLower=rig.GetBoneTransform(HumanBodyBones.LeftLowerArm),rightLower=rig.GetBoneTransform(HumanBodyBones.RightLowerArm),leftHand=rig.GetBoneTransform(HumanBodyBones.LeftHand),rightHand=rig.GetBoneTransform(HumanBodyBones.RightHand)};
                void RestFrames(bool left,out Quaternion upperFrame,out Quaternion lowerFrame,out Vector3 lowerAlong)
                {
                    Matrix4x4 Bind(Transform bone)=>input.bindposes[System.Array.IndexOf(bones,bone)].inverse;
                    var u=Bind(left?display.leftUpper:display.rightUpper);var l=Bind(left?display.leftLower:display.rightLower);var h=Bind(left?display.leftHand:display.rightHand);
                    var across=h.MultiplyVector(profile!=null?(left?profile.leftAcross:profile.rightAcross):Vector3.forward);
                    lowerAlong=l.inverse.MultiplyVector((Vector3)h.GetColumn(3)-(Vector3)l.GetColumn(3)).normalized;
                    lowerFrame=Quaternion.LookRotation(lowerAlong,l.inverse.MultiplyVector(across));
                    upperFrame=Quaternion.LookRotation(u.inverse.MultiplyVector((Vector3)l.GetColumn(3)-(Vector3)u.GetColumn(3)),u.inverse.MultiplyVector(across));
                }
                RestFrames(true,out display.leftUpperFrame,out display.leftLowerFrame,out display.leftLowerAlong);RestFrames(false,out display.rightUpperFrame,out display.rightLowerFrame,out display.rightLowerAlong);
                rigs.Add(display);CopyPose(display);
                var view=go.AddComponent<SkinnedMeshRenderer>();view.sharedMesh=mesh;view.bones=source.bones.Select(b=>b!=null?map[b]:null).ToArray();view.rootBone=source.rootBone!=null&&map.TryGetValue(source.rootBone,out var root)?root:null;view.sharedMaterials=source.sharedMaterials;
                view.localBounds=source.localBounds;view.updateWhenOffscreen=true;view.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;view.enabled=true;
                go.SetActive(false);objects.Add(go);meshes.Add(mesh);
            }
            foreach(var renderer in player.GetComponentsInChildren<Renderer>(true))if(!objects.Contains(renderer.gameObject))originalVisibility[renderer]=renderer.forceRenderingOff;
        }
        public void Show(bool value)
        {
            Visible=value;
            foreach(var go in objects)if(go!=null)go.SetActive(value);
            // Reapply after distance LOD and weapon updates, which also touch renderer visibility.
            foreach(var item in originalVisibility)if(item.Key!=null)item.Key.forceRenderingOff=value||item.Value;
        }
        public bool BodyHidden=>Visible&&originalVisibility.All(p=>p.Key==null||p.Key.forceRenderingOff);
        static Vector3 Divide(Vector3 a,Vector3 b)=>new Vector3(a.x/b.x,a.y/b.y,a.z/b.z);
        static void CopyPose(DisplayRig rig)
        {
            foreach(var item in rig.bones)
            {
                var src=item.Key;var dst=item.Value;
                if(src.parent!=null&&rig.bones.ContainsKey(src.parent)){dst.localPosition=src.localPosition;dst.localRotation=src.localRotation;dst.localScale=src.localScale;}
                else {dst.SetPositionAndRotation(src.position,src.rotation);dst.localScale=Divide(src.lossyScale,dst.parent.lossyScale);}
            }
        }
        public void Pose(Transform camera,Vector3 rightContact,Vector3 leftContact,bool flatLeft,bool flatRight,Vector3 rightShaft=default)
        {
            if(!Visible||camera==null)return;
            foreach(var rig in rigs)
            {
                CopyPose(rig);
                PoseArm(rig,rig.rightUpper,rig.rightLower,rig.rightHand,rightContact,camera,false,flatRight,rightShaft);
                PoseArm(rig,rig.leftUpper,rig.leftLower,rig.leftHand,leftContact,camera,true,flatLeft,Vector3.zero);
            }
        }
        void PoseArm(DisplayRig rig,Transform originalUpper,Transform originalLower,Transform originalHand,Vector3 contact,Transform camera,bool left,bool flat,Vector3 shaft)
        {
            if(!rig.bones.TryGetValue(originalUpper,out var upper)||!rig.bones.TryGetValue(originalLower,out var lower)||!rig.bones.TryGetValue(originalHand,out var hand))return;
            // Keep a natural bent arm below each contact, with the shoulder cap at the lower edge.
            const float displayScale=1.65f;
            var along=(camera.up*.82f+camera.forward*.57f+camera.right*(left?.05f:-.05f)).normalized;
            var forward=profile!=null?(left?profile.leftForward:profile.rightForward):Vector3.up;
            var across=profile!=null?(left?profile.leftAcross:profile.rightAcross):Vector3.forward;
            var up=shaft.sqrMagnitude>.1f?shaft.normalized:flat?(left?Vector3.Cross(along,Vector3.down):Vector3.Cross(Vector3.down,along)):camera.up;
            var handRotation=Quaternion.LookRotation(along,up)*Quaternion.Inverse(Quaternion.LookRotation(forward,across));
            var center=profile!=null?(left?profile.leftCenter:profile.rightRodCenter):Vector3.zero;
            if(profile!=null&&usesWorkMesh)
            {
                var pose=left?leftGrip:rightGrip;float length=left?.00195941f:.00209216f;
                var palm=Vector3.Cross(forward,across)*(left?-1:1);
                center=(forward*(pose==GripPose.Pinch?.64f:pose==GripPose.Rod?.59f:.63f)+palm*(pose==GripPose.Pinch?.26f:pose==GripPose.Rod?.2f:.07f)+across*(pose==GripPose.Pinch?.26f:.1f))*length;
            }
            var wrist=contact-handRotation*Vector3.Scale(center,originalHand.lossyScale*displayScale);
            float lowerLength=Vector3.Distance(originalLower.position,originalHand.position)*displayScale;
            float upperLength=Vector3.Distance(originalUpper.position,originalLower.position)*displayScale;
            var elbow=wrist-along*lowerLength;
            var upperDirection=(camera.up*.9f+camera.forward*.42f+camera.right*(left?.1f:-.1f)).normalized;
            var shoulder=elbow-upperDirection*upperLength;
            // Translate/rotate a fixed-length chain backwards from the contact.
            // Never scale an individual axis to make a distant table reachable.
            upper.localScale=originalUpper.localScale*displayScale;
            upper.SetPositionAndRotation(shoulder,Quaternion.LookRotation(upperDirection,up)*Quaternion.Inverse(left?rig.leftUpperFrame:rig.rightUpperFrame));
            lower.localScale=originalLower.localScale;lower.position=elbow;
            // Use the mesh bind frame, not a bent wrist inherited from the third-person animation.
            lower.rotation=Quaternion.LookRotation(along,up)*Quaternion.Inverse(left?rig.leftLowerFrame:rig.rightLowerFrame);
            hand.localScale=originalHand.localScale;hand.SetPositionAndRotation(wrist,handRotation);
            MaxContactError=Mathf.Max(MaxContactError,Vector3.Distance(hand.TransformPoint(center),contact));
            MaxSegmentError=Mathf.Max(MaxSegmentError,Mathf.Abs(Vector3.Distance(lower.position,hand.position)/lowerLength-1),Mathf.Abs(Vector3.Distance(upper.position,lower.position)/upperLength-1));
            MaxWristAngle=Mathf.Max(MaxWristAngle,Vector3.Angle(hand.TransformDirection(forward),lower.TransformDirection(left?rig.leftLowerAlong:rig.rightLowerAlong)));
        }
        public void Grip(GripPose left,GripPose right)
        {
            leftGrip=left;rightGrip=right;
            ActiveGripWeight=0;
            foreach(var go in objects)if(go!=null)
            {
                var r=go.GetComponent<SkinnedMeshRenderer>();
                foreach(bool side in new[]{true,false})foreach(GripPose pose in System.Enum.GetValues(typeof(GripPose)))
                {
                    int index=r.sharedMesh.GetBlendShapeIndex((side?"Left":"Right")+pose);if(index<0)continue;
                    float goal=(side?left:right)==pose?100:0;
                    float current=Mathf.MoveTowards(r.GetBlendShapeWeight(index),goal,Time.unscaledDeltaTime*700);r.SetBlendShapeWeight(index,current);ActiveGripWeight=Mathf.Max(ActiveGripWeight,current);
                }
            }
        }
        public void Dispose()
        {
            Show(false);foreach(var go in objects)if(go!=null){go.SetActive(false);Object.Destroy(go);}objects.Clear();
            foreach(var mesh in meshes)if(mesh!=null)Object.Destroy(mesh);meshes.Clear();rigs.Clear();originalVisibility.Clear();
        }
    }
}
