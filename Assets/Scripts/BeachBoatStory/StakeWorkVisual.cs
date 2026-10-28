using UnityEngine;

namespace BeachBoatStory
{
    public static class StakeWorkVisual
    {
        // Use the same textured pointed timber mesh as the completed river field.
        // Canonical work coordinates are centred, 24 units long, with the tip up.
        public static Transform Create(string name,Transform parent,Transform template)
        {
            var source=template.GetComponentInChildren<MeshFilter>(true);var renderer=source.GetComponent<Renderer>();
            var root=new GameObject(name).transform;root.SetParent(parent,false);
            var visual=new GameObject("Field_stake_mesh",typeof(MeshFilter),typeof(MeshRenderer)).transform;visual.SetParent(root,false);
            visual.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;visual.GetComponent<Renderer>().sharedMaterials=renderer.sharedMaterials;
            var bounds=source.sharedMesh.bounds;int axis=bounds.size.z>=bounds.size.y&&bounds.size.z>=bounds.size.x?2:bounds.size.y>=bounds.size.x?1:0;
            var along=axis==2?Vector3.forward:axis==1?Vector3.up:Vector3.right;visual.localRotation=Quaternion.FromToRotation(along,Vector3.up);
            float length=axis==2?bounds.size.z:axis==1?bounds.size.y:bounds.size.x;visual.localScale=Vector3.one*(24/Mathf.Max(.001f,length));visual.localPosition=-(visual.localRotation*(bounds.center*visual.localScale.x));
            return root;
        }
    }
}
