using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Hand frames and grip morphs for the player's Meshy rig, which has no finger joints.</summary>
    public sealed class BowGripProfile : ScriptableObject
    {
        public Mesh source,posed;
        public Vector3 leftForward,leftAcross,leftCenter,rightForward,rightAcross,rightCenter,rightRodCenter;
        public static BowGripProfile Load()=>Resources.Load<BowGripProfile>("BachDang938/Player/BowGripProfile");
        public void Align(Transform upper,Transform lower,Transform hand,Vector3 contact,Vector3 hint,bool left,bool rod=false,Vector3 shaft=default)
        {
            var forward=left?leftForward:rightForward;var across=left?leftAcross:rightAcross;var center=left?leftCenter:rod?rightRodCenter:rightCenter;
            QuestRowboat.SolveArm(upper,lower,hand,contact,hint);
            for(int pass=0;pass<6;pass++)
            {
                var along=(hand.position-lower.position).normalized;
                var up=shaft.sqrMagnitude>.1f?shaft.normalized:Vector3.up;
                // The fist follows the forearm. Rotating the wrist towards the
                // shaft to improve contact causes the cuff to fold unnaturally.
                var frame=Quaternion.LookRotation(hand.TransformDirection(forward),hand.TransformDirection(across));
                var desired=Quaternion.LookRotation(along,up)*Quaternion.Inverse(frame)*hand.rotation;
                hand.rotation=desired;var offset=hand.TransformVector(center);
                QuestRowboat.SolveArm(upper,lower,hand,contact-offset,hint);hand.rotation=desired;
            }
        }
    }
}
