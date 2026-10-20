using System.Collections.Generic;
using UnityEngine;

namespace TaiwanGo
{
    // Real volume and independent moving parts, rather than a camera-facing character card.
    public sealed class GorgeJetSkiModel
    {
        public Transform root, riders, handlebar;
        readonly List<Mesh> meshes;
        readonly Material hull, red, black, yellow, mint, skin, trim, denim, jacket, lifeJacket;
        readonly List<Transform> wrists=new List<Transform>();
        readonly List<Transform> forearms=new List<Transform>();
        readonly List<Vector3> elbows=new List<Vector3>();
        TextMesh speedDisplay;
        public GorgeJetSkiModel(Transform parent,List<Mesh> ownedMeshes,Material white,Material red,Material black,Material yellow,Material mint,Material skin,Material trim,Material denim,Material jacket,Material lifeJacket)
        {
            meshes=ownedMeshes;hull=white;this.red=red;this.black=black;this.yellow=yellow;this.mint=mint;this.skin=skin;this.trim=trim;this.denim=denim;this.jacket=jacket;this.lifeJacket=lifeJacket;
            root=new GameObject("Volumetric touring jet ski").transform;root.SetParent(parent,false);
            BuildHull();
            riders=new GameObject("Animated 3D riders").transform;riders.SetParent(root,false);
            Rider("Chen",.02f,red,jacket,false);Rider("Yu",-.98f,yellow,mint,true);
        }
        void BuildHull()
        {
            // Cross sections form a pointed bow, chine, flared gunwale and stepped stern.
            float[] z={-1.95f,-1.75f,-1.1f,0,.85f,1.55f,2.05f};
            float[] width={.57f,.83f,.91f,.87f,.75f,.43f,.025f};
            var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
            for(int i=0;i<z.Length;i++)
            {
                float w=width[i];float rise=i>3?(i-3)*.055f:0;
                Vector3[] ring={new Vector3(-w,.25f+rise,z[i]),new Vector3(-w*.83f,-.04f+rise,z[i]),new Vector3(-w*.4f,-.26f+rise,z[i]),new Vector3(w*.4f,-.26f+rise,z[i]),new Vector3(w*.83f,-.04f+rise,z[i]),new Vector3(w,.25f+rise,z[i]),new Vector3(w*.7f,.4f+rise,z[i]),new Vector3(-w*.7f,.4f+rise,z[i])};
                for(int j=0;j<8;j++){v.Add(ring[j]);uv.Add(new Vector2(j/7f,i/6f));}
                if(i==0)continue;
                for(int j=0;j<8;j++){int a=(i-1)*8+j,b=(i-1)*8+(j+1)%8,c=i*8+j,d=i*8+(j+1)%8;tri.Add(a);tri.Add(b);tri.Add(c);tri.Add(b);tri.Add(d);tri.Add(c);}
            }
            // Close both ends.
            for(int j=1;j<7;j++){tri.Add(0);tri.Add(j+1);tri.Add(j);int e=(z.Length-1)*8;tri.Add(e);tri.Add(e+j);tri.Add(e+j+1);}
            MeshObject("Sculpted fiberglass hull",v.ToArray(),tri.ToArray(),uv.ToArray(),hull,root);
            Ellipsoid("Red upper deck",new Vector3(0,.37f,.12f),new Vector3(1.57f,.34f,3.55f),red,root);
            Ellipsoid("Black padded saddle",new Vector3(0,.63f,-.63f),new Vector3(.66f,.24f,2.0f),black,root);
            for(int s=-1;s<=1;s+=2)
            {
                Tube("Hull rub rail",new Vector3(s*.82f,.23f,-1.65f),new Vector3(s*.68f,.36f,1.1f),.045f,black,root);
                Ellipsoid("Footwell",new Vector3(s*.62f,.46f,-.65f),new Vector3(.3f,.1f,1.9f),black,root);
                for(int j=0;j<6;j++)Ellipsoid("Footwell grip",new Vector3(s*.62f,.515f,-1.4f+j*.27f),new Vector3(.26f,.014f,.045f),trim,root);
            }
            Ellipsoid("Cockpit fairing",new Vector3(0,.67f,.85f),new Vector3(.91f,.63f,.94f),black,root);
            Ellipsoid("Digital instrument bezel",new Vector3(0,.98f,.76f),new Vector3(.48f,.08f,.3f),trim,root);
            Ellipsoid("Dashboard glass",new Vector3(0,1.025f,.73f),new Vector3(.37f,.025f,.22f),mint,root);
            var readout=new GameObject("Live speedometer km per hour");readout.transform.SetParent(root,false);
            readout.transform.localPosition=new Vector3(0,1.052f,.73f);readout.transform.localRotation=Quaternion.Euler(90,0,0);
            speedDisplay=readout.AddComponent<TextMesh>();speedDisplay.anchor=TextAnchor.MiddleCenter;speedDisplay.alignment=TextAlignment.Center;
            speedDisplay.fontSize=64;speedDisplay.characterSize=.055f;speedDisplay.color=new Color(.65f,1,1);SetSpeed(0);
            handlebar=new GameObject("Independent steering assembly").transform;handlebar.SetParent(root,false);handlebar.localPosition=new Vector3(0,1.02f,.44f);
            Tube("Steering stem",new Vector3(0,-.25f,.12f),Vector3.zero,.065f,black,handlebar);
            for(int s=-1;s<=1;s+=2)
            {
                Tube("Handlebar",Vector3.zero,new Vector3(s*.55f,0,-.12f),.04f,trim,handlebar);
                Tube("Rubber grip",new Vector3(s*.37f,0,-.08f),new Vector3(s*.67f,0,-.14f),.065f,black,handlebar);
                Ellipsoid("Red switch housing",new Vector3(s*.37f,0,-.08f),new Vector3(.14f,.17f,.18f),red,handlebar);
            }
        }
        void Rider(string name,float z,Material helmet,Material shirt,bool female)
        {
            var body=new GameObject(name+" articulated rider").transform;body.SetParent(riders,false);body.localPosition=new Vector3(0,.73f,z);
            Ellipsoid("Clothed hips",new Vector3(0,.02f,-.03f),new Vector3(.55f,.37f,.4f),female?denim:black,body);
            var torso=Ellipsoid("Fitted fabric torso",new Vector3(0,.43f,.1f),new Vector3(.59f,.76f,.36f),shirt,body);torso.localRotation=Quaternion.Euler(12,0,0);
            Ellipsoid("Life jacket back",new Vector3(0,.43f,-.09f),new Vector3(.62f,.65f,.2f),lifeJacket,body);
            for(int s=-1;s<=1;s+=2)
            {
                Ellipsoid("Buoyancy panel",new Vector3(s*.165f,.45f,.27f),new Vector3(.28f,.6f,.16f),lifeJacket,body);
                Tube("Shoulder strap",new Vector3(s*.19f,.77f,-.06f),new Vector3(s*.19f,.67f,.29f),.048f,black,body);
                Vector3 knee=new Vector3(s*.43f,-.26f,.36f),ankle=new Vector3(s*.59f,-.51f,.02f);
                Tube("Trouser thigh",new Vector3(s*.16f,.03f,0),knee,.16f,female?denim:black,body);
                Tube("Trouser shin",knee,ankle,.115f,female?denim:black,body);
                Ellipsoid("Shoe",ankle+new Vector3(0,-.05f,.13f),new Vector3(.23f,.19f,.43f),hull,body);
                Vector3 shoulder=new Vector3(s*.29f,.65f,.13f),elbow=new Vector3(s*.41f,.38f,.4f),hand=new Vector3(s*.53f,.29f,.48f);
                Tube("Jacket sleeve",shoulder,elbow,.115f,shirt,body);
                var forearm=Tube("Forearm",elbow,hand,.078f,female?skin:shirt,body);
                var wrist=Ellipsoid("Hand",hand,new Vector3(.14f,.12f,.18f),skin,body);
                if(!female){wrists.Add(wrist);forearms.Add(forearm);elbows.Add(elbow);}
            }
            for(int j=0;j<2;j++)
            {
                Ellipsoid("Life jacket waist webbing",new Vector3(0,.22f+j*.22f,-.2f),new Vector3(.6f,.055f,.028f),trim,body);
                Ellipsoid("Buckle",new Vector3(.13f,.22f+j*.22f,-.225f),new Vector3(.1f,.08f,.035f),black,body);
            }
            Ellipsoid("Neck",new Vector3(0,.83f,.18f),new Vector3(.2f,.24f,.2f),skin,body);
            Ellipsoid("Face",new Vector3(0,1.03f,.23f),new Vector3(.34f,.4f,.35f),skin,body);
            Ellipsoid("Rounded protective helmet",new Vector3(0,1.16f,.16f),new Vector3(.47f,.43f,.49f),helmet,body);
            for(int j=-1;j<=1;j++)Ellipsoid("Helmet ventilation slot",new Vector3(j*.11f,1.345f,.12f),new Vector3(.045f,.025f,.18f),black,body);
            Tube("Helmet chin strap",new Vector3(-.18f,1.05f,.14f),new Vector3(0,.89f,.28f),.023f,black,body);
            Tube("Helmet chin strap",new Vector3(.18f,1.05f,.14f),new Vector3(0,.89f,.28f),.023f,black,body);
            if(female)for(int j=0;j<7;j++)
            {
                var hair=Tube("Ponytail lock",new Vector3(j*.02f-.06f,1.1f,-.04f),new Vector3(.12f+j*.025f,.7f-j*.025f,-.48f-j*.02f),.035f,black,body);
                hair.localRotation*=Quaternion.Euler(0,j*2,0);
            }
        }
        public void Animate(float steer,float time)
        {
            handlebar.localRotation=Quaternion.Euler(0,steer*32,0);
            riders.localRotation=Quaternion.Euler(Mathf.Sin(time*8)*1.5f,steer*2,-steer*7);
            for(int i=0;i<wrists.Count;i++)
            {
                int s=i==0?-1:1;Transform body=wrists[i].parent;
                Vector3 hand=body.InverseTransformPoint(handlebar.TransformPoint(new Vector3(s*.53f,0,-.12f)));
                wrists[i].localPosition=hand;FitTube(forearms[i],elbows[i],hand,.078f);
            }
        }
        public void SetSpeed(float kilometersPerHour){speedDisplay.text=kilometersPerHour.ToString("0")+"\nkm/h";}
        Transform Ellipsoid(string name,Vector3 p,Vector3 scale,Material material,Transform parent)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;
            Object.Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
        }
        Transform Tube(string name,Vector3 a,Vector3 b,float radius,Material material,Transform parent)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);
            Object.Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;FitTube(go.transform,a,b,radius);return go.transform;
        }
        static void FitTube(Transform t,Vector3 a,Vector3 b,float radius)
        {t.localPosition=(a+b)*.5f;t.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);t.localScale=new Vector3(radius*2,(b-a).magnitude*.5f,radius*2);}
        void MeshObject(string name,Vector3[] v,int[] tri,Vector2[] uv,Material material,Transform parent)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var mesh=new Mesh{name=name};mesh.vertices=v;mesh.triangles=tri;mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
    }
}
