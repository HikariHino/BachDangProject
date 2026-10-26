using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>A fresh battle opens on the approaching fleet, then returns control to the rower.</summary>
    [DefaultExecutionOrder(950)]
    public sealed class RiverInvasionIntro : MonoBehaviour
    {
        public static RiverInvasionIntro Instance {get;private set;}
        public static bool IsRunning=>Instance!=null&&Instance.IsActive;
        public bool IsActive {get;private set;}
        public bool IsPlaying {get;private set;}
        public const float Duration=6.5f;
        public float Elapsed {get;private set;}
        EnemyShip ship;Camera camera;Camera_Script orbit;
        Vector3 returnPosition;Quaternion returnRotation;float returnFov;
        bool orbitEnabled;CursorLockMode cursorLock;bool cursorVisible;
        void Awake(){Instance=this;}
        public void Queue(){Cancel();IsActive=true;Elapsed=0;}
        public void Begin(EnemyShip target)
        {
            var session=BeachBoatQuestSession.Instance;
            if(!IsActive||target==null||session==null||session.storyCamera==null){Cancel();return;}
            ship=target;camera=session.storyCamera;orbit=camera.GetComponent<Camera_Script>();
            returnPosition=camera.transform.position;returnRotation=camera.transform.rotation;returnFov=camera.fieldOfView;
            orbitEnabled=orbit!=null&&orbit.enabled;if(orbit!=null)orbit.enabled=false;
            cursorLock=Cursor.lockState;cursorVisible=Cursor.visible;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
            IsPlaying=true;Elapsed=0;GetComponent<RiverBattleVoices>()?.PlayInvasionIntroduction();Step(0);
        }
        void LateUpdate()
        {
            if(!IsActive)return;
            if(EnemyFleetDirector.Instance==null||!EnemyFleetDirector.Instance.BattleStarted){Cancel();return;}
            if(Time.timeScale<=0||BachDangFrontEnd.IsBlocking||(StoryDialogueUI.Instance?.IsOpen??false))return;
            if(IsPlaying)Step(Time.deltaTime);
        }
        public void Step(float dt)
        {
            if(!IsPlaying||camera==null||ship==null)return;Elapsed+=Mathf.Max(0,dt);
            var hull=BoatHullCollision.Get(ship.transform);float radius=Mathf.Max(100,hull.Radius);
            var focus=hull.SolidHull.bounds.center+Vector3.up*30;
            var side=(ship.transform.forward*.8f+ship.transform.right).normalized;
            float zoom=Mathf.SmoothStep(0,1,Mathf.Clamp01(Elapsed/3.7f));
            var position=focus+side*Mathf.Lerp(radius*2.8f,radius*1.65f,zoom)+Vector3.up*Mathf.Lerp(105,65,zoom);
            var rotation=Quaternion.LookRotation(focus-position);
            float back=Mathf.SmoothStep(0,1,Mathf.InverseLerp(5.4f,Duration,Elapsed));
            camera.transform.SetPositionAndRotation(Vector3.Lerp(position,returnPosition,back),Quaternion.Slerp(rotation,returnRotation,back));
            camera.fieldOfView=Mathf.Lerp(Mathf.Lerp(57,43,zoom),returnFov,back);
            if(Elapsed>=Duration)Cancel();
        }
        public void Cancel()
        {
            if(IsPlaying)
            {
                if(camera!=null){camera.fieldOfView=returnFov;camera.transform.SetPositionAndRotation(returnPosition,returnRotation);}
                if(orbit!=null){orbit.enabled=orbitEnabled;if(orbitEnabled)orbit.SnapToTarget();}
                Cursor.lockState=cursorLock;Cursor.visible=cursorVisible;
            }
            IsActive=IsPlaying=false;ship=null;
        }
        void OnDisable()=>Cancel();
        void OnDestroy(){Cancel();if(Instance==this)Instance=null;}
    }
}
