using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Validate the full player capsule and a walking exit at every arrival.</summary>
    public static class PlayerArrival
    {
        static void Shape(GameObject player, Vector3 position, Quaternion rotation, out Vector3 a, out Vector3 b, out float radius)
        {
            var capsule=player.GetComponent<CapsuleCollider>();
            var scale=player.transform.lossyScale;
            radius=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
            float half=Mathf.Max(0,capsule.height*Mathf.Abs(scale.y)*.5f-radius);
            var center=position+rotation*Vector3.Scale(capsule.center,scale);
            a=center+Vector3.up*half;b=center-Vector3.up*half;
        }
        public static bool IsClear(GameObject player,Vector3 position,Quaternion rotation)
        {
            Shape(player,position,rotation,out var a,out var b,out float radius);
            foreach(var c in Physics.OverlapCapsule(a,b,radius+.35f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                if(c is TerrainCollider||c.transform.IsChildOf(player.transform))continue;
                if(Physics.ComputePenetration(player.GetComponent<CapsuleCollider>(),position,rotation,c,c.transform.position,c.transform.rotation,out _,out float depth)&&depth>.12f)return false;
            }
            return true;
        }
        public static bool HasExit(GameObject player,Vector3 position,Quaternion rotation)
        {
            Shape(player,position,rotation,out var a,out var b,out float radius);
            for(int i=0;i<8;i++)
            {
                var direction=Quaternion.Euler(0,i*45,0)*Vector3.forward;
                bool blocked=false;
                foreach(var hit in Physics.CapsuleCastAll(a,b,radius*.95f,direction,radius*4,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    if(!(hit.collider is TerrainCollider)&&!hit.transform.IsChildOf(player.transform)){blocked=true;break;}
                if(!blocked&&IsClear(player,position+direction*radius*4,rotation))return true;
            }
            return false;
        }
        public static bool TryResolve(GameObject player,Vector3 requested,Quaternion rotation,out Vector3 resolved)
        {
            Physics.SyncTransforms();resolved=requested;
            if(IsClear(player,requested,rotation)&&HasExit(player,requested,rotation))return true;
            var terrain=Terrain.activeTerrain;if(terrain==null)return false;
            Shape(player,requested,rotation,out _,out _,out float radius);
            var capsule=player.GetComponent<CapsuleCollider>();
            float bottom=Vector3.Scale(capsule.center,player.transform.lossyScale).y-capsule.height*Mathf.Abs(player.transform.lossyScale.y)*.5f;
            for(int ring=1;ring<=12;ring++)for(int i=0;i<16;i++)
            {
                var p=requested+Quaternion.Euler(0,i*22.5f,0)*Vector3.forward*(ring*radius*1.4f);
                float ground=terrain.SampleHeight(p)+terrain.transform.position.y;
                if(TideSystem.Instance!=null&&ground<TideSystem.Instance.highTideY+1)continue;
                if(Mathf.Abs(ground-requested.y)>radius*3)continue;
                p.y=ground-bottom+.4f;
                if(!IsClear(player,p,rotation)||!HasExit(player,p,rotation))continue;
                resolved=p;return true;
            }
            return false;
        }
        public static bool Teleport(GameObject player,Vector3 requested,Quaternion rotation)
        {
            if(!TryResolve(player,requested,rotation,out var position))
            {Debug.LogError("No clear player arrival near "+requested,player);return false;}
            var controller=player.GetComponent<CharacterController>();bool wasEnabled=controller!=null&&controller.enabled;
            if(wasEnabled)controller.enabled=false;
            player.transform.SetPositionAndRotation(position,rotation);
            var body=player.GetComponent<Rigidbody>();
            if(body!=null){body.position=position;body.rotation=rotation;if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}}
            if(wasEnabled)controller.enabled=true;Physics.SyncTransforms();return true;
        }
    }
}
