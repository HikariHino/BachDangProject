using UnityEngine;
using TMPro;

namespace BeachBoatStory
{
    /// <summary>Scene-owned drill station, beside the camp's central path rather than inside a barracks.</summary>
    public sealed class DrumTrainingGround : MonoBehaviour
    {
        Material timber,skin,iron,cloth;
        public Vector3 Approach=>transform.TransformPoint(new Vector3(0,0,-24));
        public static void Ensure(BeachBoatMinigameStation station)
        {
            if(station.GetComponentInParent<DrumTrainingGround>()!=null)return;
            var camp=GameObject.Find("TestQuest_Camp");var origin=camp!=null?camp.transform.Find("CampLayoutOrigin"):null;
            if(origin==null||Terrain.activeTerrain==null)return;
            var root=new GameObject("San_Luyen_Hieu_Lenh_Trong_Tran");root.transform.SetParent(station.transform.parent,true);
            var scale=root.transform.parent.lossyScale;root.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
            var p=origin.position+new Vector3(-48,0,-120);p.y=Terrain.activeTerrain.SampleHeight(p)+Terrain.activeTerrain.transform.position.y;root.transform.position=p;
            root.AddComponent<DrumTrainingGround>().Build(station);
        }
        Material Make(Material source,string label,Color color)
        {var material=new Material(source){name=label,color=color};return material;}
        GameObject Shape(string label,PrimitiveType shape,Vector3 p,Vector3 size,Material material,Quaternion rotation=default)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=label;go.transform.SetParent(transform,false);go.transform.localPosition=p;go.transform.localScale=size;go.transform.localRotation=rotation==default?Quaternion.identity:rotation;
            go.GetComponent<Renderer>().sharedMaterial=material;go.isStatic=true;return go;
        }
        void Build(BeachBoatMinigameStation station)
        {
            var original=station.GetComponent<Renderer>().sharedMaterial;var oldRim=station.transform.Find("WoodenRim");
            var sourceWood=oldRim!=null?oldRim.GetComponent<Renderer>().sharedMaterial:original;
            timber=Make(sourceWood,"Training_Dark_Timber",new Color(.22f,.13f,.065f));skin=Make(original,"Training_Drum_Hide",new Color(.73f,.57f,.34f));iron=Make(original,"Training_Bronze",new Color(.37f,.26f,.10f));cloth=Make(original,"Training_Red_Banner",new Color(.40f,.065f,.035f));
            if(oldRim!=null)oldRim.gameObject.SetActive(false);
            station.transform.SetParent(transform,true);station.transform.SetPositionAndRotation(transform.position+Vector3.up*15,Quaternion.Euler(90,0,0));station.transform.localScale=new Vector3(16,9,16);
            station.GetComponent<Renderer>().sharedMaterial=cloth;foreach(var c in station.GetComponents<Collider>())c.isTrigger=false;
            station.displayName="Sân luyện hiệu lệnh trống trận";station.interactDistance=26;
            Shape("Woven_Drill_Mat",PrimitiveType.Cube,new Vector3(0,.08f,0),new Vector3(40,.16f,50),timber);
            for(int side=-1;side<=1;side+=2)
            {
                for(int z=-1;z<=1;z+=2)Shape("Solid_Drum_Rack_Leg",PrimitiveType.Cube,new Vector3(side*11,9,z*7),new Vector3(2,18,2),timber,Quaternion.Euler(0,0,-side*10));
                Shape("Solid_Drum_Rack_Beam",PrimitiveType.Cube,new Vector3(0,8,side*7),new Vector3(27,2,2),timber);
                Shape("Bronze_Drum_Rim",PrimitiveType.Cylinder,new Vector3(0,15,side*8.7f),new Vector3(17,.6f,17),iron,Quaternion.Euler(90,0,0));
                Shape("Hide_Drum_Head",PrimitiveType.Cylinder,new Vector3(0,15,side*9.4f),new Vector3(15.5f,.12f,15.5f),skin,Quaternion.Euler(90,0,0));
                for(int i=0;i<12;i++)
                {float angle=i*Mathf.PI/6;Shape("Bronze_Head_Stud",PrimitiveType.Sphere,new Vector3(Mathf.Cos(angle)*7.15f,15+Mathf.Sin(angle)*7.15f,side*9.65f),Vector3.one*.7f,iron);}
                Shape("Solid_Training_Banner_Pole",PrimitiveType.Cylinder,new Vector3(side*18,15,17),new Vector3(1,15,1),timber);
                Shape("Camp_Command_Banner",PrimitiveType.Cube,new Vector3(side*15,23,17),new Vector3(5,12,.25f),cloth);
            }
            Shape("Training_Sign_Board",PrimitiveType.Cube,new Vector3(0,29,17),new Vector3(35,6,1),timber);
            var sign=new GameObject("Training_Sign");sign.transform.SetParent(transform,false);sign.transform.localPosition=new Vector3(0,29,16.4f);
            var text=sign.AddComponent<TextMeshPro>();StoryFont.Apply(text);text.text="THAO TRƯỜNG\nHIỆU LỆNH TRỐNG TRẬN";text.fontSize=16;text.alignment=TextAlignmentOptions.Center;text.color=new Color(.96f,.78f,.43f);text.rectTransform.sizeDelta=new Vector2(34,5.5f);text.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            Physics.SyncTransforms();
        }
        void OnDestroy(){if(timber!=null)Destroy(timber);if(skin!=null)Destroy(skin);if(iron!=null)Destroy(iron);if(cloth!=null)Destroy(cloth);}
    }
}
