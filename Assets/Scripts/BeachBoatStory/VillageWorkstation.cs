using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>The same market furniture and supplies exist before, during and after a trade.</summary>
    public sealed class VillageWorkstation : MonoBehaviour
    {
        public float HeightScale {get;private set;}=1;
        public Material Wood {get;private set;}
        public readonly List<GameObject> BundleLogs=new List<GameObject>();
        public Transform Environment {get;private set;}
        readonly List<GameObject> hidden=new List<GameObject>();
        readonly List<Material> materials=new List<Material>();
        Transform awning;Vector3 awningScale;
        Material Make(string name,Color color)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};m.SetFloat("_Smoothness",.12f);materials.Add(m);return m;}
        GameObject Shape(string name,PrimitiveType type,Vector3 p,Vector3 size,Material material,bool solid=false)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(Environment,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid){var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);}return go;
        }
        Vector3 World(Vector3 p)=>Environment.TransformPoint(p);
        public void Build(VillageWorkStall owner)
        {
            if(Environment!=null)return;
            var player=BeachBoatQuestSession.Instance.player;var rig=player.GetComponentInChildren<Animator>();
            var chest=rig.GetBoneTransform(HumanBodyBones.Chest)??rig.GetBoneTransform(HumanBodyBones.Spine);
            HeightScale=Mathf.Clamp((chest.position.y-owner.transform.position.y+1)/13,1,2.5f);
            foreach(Transform child in owner.transform)
            {
                if(child.name=="Mai_Che_Mo_Mat_Truoc"){awning=child;awningScale=child.localScale;child.localScale=Vector3.Scale(awningScale,new Vector3(1.5f,HeightScale,1));continue;}
                if(child.gameObject.activeSelf&&child.name!="Nguoi_Ban_Hang"&&child.name!="Ten_Sap"&&child!=owner.interactionPoint)
                {hidden.Add(child.gameObject);child.gameObject.SetActive(false);}
            }
            Environment=new GameObject("Village_Workstation_Environment").transform;Environment.SetParent(owner.transform,false);Environment.localScale=new Vector3(1,HeightScale,1);
            Wood=Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m=>m.name.Contains("Courtyard_Weathered_Wood")&&m.mainTexture!=null)??Make("Trade wood",new Color(.32f,.20f,.1f));
            var fibre=Make("Hemp display",new Color(.57f,.40f,.21f));var rice=Make("Rice display",new Color(.91f,.84f,.61f));var fish=Make("Fish display",new Color(.39f,.23f,.1f));var salt=Make("Salt display",new Color(.88f,.9f,.85f));
            Shape("Solid_workbench",PrimitiveType.Cube,new Vector3(0,8,-3),new Vector3(32,2,13),Wood,true);
            foreach(int x in new[]{-13,13})foreach(int z in new[]{-7,1})Shape("Table_leg",PrimitiveType.Cube,new Vector3(x,3.5f,z),new Vector3(2,7,2),Wood,true);
            Shape("Delivery_crate",PrimitiveType.Cube,new Vector3(25,3,-14),new Vector3(10,6,12),Wood,true);
            Shape("Stock_crate",PrimitiveType.Cube,new Vector3(-25,3,-14),new Vector3(10,6,12),Wood,true);
            if(owner.kind==VillageWorkStall.WorkKind.Timber)
                for(int i=0;i<3;i++){var log=Shape("Uncut_stock",PrimitiveType.Cylinder,new Vector3(-25,7+i*1.8f,-14),new Vector3(2,7,2),Wood);log.transform.localRotation=Quaternion.Euler(90,0,0);}
            else if(owner.kind==VillageWorkStall.WorkKind.Rope)
            {
                for(int i=0;i<4;i++){var log=Shape("Bundle_to_tie",PrimitiveType.Cylinder,new Vector3(0,10+i/2*1.8f,-10.2f+(i%2)*1.8f-.9f),new Vector3(2,12,2),Wood);log.transform.localRotation=Quaternion.Euler(0,0,90);BundleLogs.Add(log);}
                var line=new GameObject("Stock_rope").AddComponent<LineRenderer>();line.transform.SetParent(Environment,false);line.sharedMaterial=fibre;line.widthMultiplier=.55f;line.positionCount=65;line.generateLightingData=true;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                for(int i=0;i<65;i++){float a=i*.32f;line.SetPosition(i,World(new Vector3(-25+Mathf.Cos(a)*(2+i*.025f),7+i*.018f,-14+Mathf.Sin(a)*(2+i*.025f))));}
            }
            else if(owner.kind==VillageWorkStall.WorkKind.Rice)
            {
                for(int i=0;i<3;i++)Shape("Folded_empty_sack",PrimitiveType.Cube,new Vector3(-25,6.4f+i*.55f,-14),new Vector3(6,.5f,7),fibre);
                Shape("Grain_basket",PrimitiveType.Cylinder,new Vector3(-5,10,-10.2f),new Vector3(9,2,9),fibre);
                for(int i=0;i<28;i++)Shape("Grain_in_basket",PrimitiveType.Sphere,new Vector3(-5+Mathf.Sin(i*2.4f)*3,12+(i%3)*.15f,-10.2f+Mathf.Cos(i*2.4f)*3),new Vector3(.7f,.35f,1),rice);
            }
            else
            {
                for(int i=0;i<3;i++)Shape("Ingredient_tray_"+i,PrimitiveType.Cylinder,new Vector3(-28+i*3,6.7f,-18),new Vector3(3,.5f,3.5f),fibre);
                Shape("Rice_portions",PrimitiveType.Sphere,new Vector3(-28,7.6f,-18),new Vector3(5,1.6f,3),rice);
                Shape("Dried_fish",PrimitiveType.Sphere,new Vector3(-25,7.5f,-18),new Vector3(6,.6f,2),fish);
                Shape("Salt_packet",PrimitiveType.Cube,new Vector3(-22,7.7f,-18),new Vector3(3,1.6f,3),salt);
            }
            Physics.SyncTransforms();
        }
        public void ResetDisplay(){foreach(var log in BundleLogs)if(log!=null)log.SetActive(true);}
        void OnDestroy()
        {
            if(Environment!=null)Destroy(Environment.gameObject);
            foreach(var go in hidden)if(go!=null)go.SetActive(true);
            if(awning!=null)awning.localScale=awningScale;
            foreach(var m in materials)if(m!=null)Destroy(m);
        }
    }
}
