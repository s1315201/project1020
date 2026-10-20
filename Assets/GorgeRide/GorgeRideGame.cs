using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TaiwanGo
{
    // Self-contained prototype: pressing Play in the default scene starts the game.
    public sealed class GorgeRideGame : MonoBehaviour
    {
        const float Duration = GorgeRunRules.Duration, HalfWidth = GorgeCourseLayout.RiverHalfWidth;
        const float RockfallWarningTime=2.5f, RockfallDropTime=1.5f;
        enum Phase { Ready, Riding, Paused, Failed, Won }
        Phase phase;
        Transform boat, riders, handlebar, water, backdrop;
        Camera rideCamera;
        Material marble, dark, red, yellow, mint, skin, green, white, waterMaterial;
        readonly List<Transform> sections = new List<Transform>();
        readonly List<Obstacle> obstacles = new List<Obstacle>();
        readonly List<Material> materials = new List<Material>();
        float elapsed, distance, lateral, velocity, wind;
        int view = 1;
        GorgeJetSkiModel model;
        System.Random courseRandom;
        float safeCorridor, currentSpeed=26, danger, impactFlash;
        AudioSource riverAudio;
        AudioClip riverClip;
        bool muted;
        Camera steeringCamera;
        Transform steeringAssembly;
        RenderTexture steeringRender;
        readonly List<Material> cliffPalette=new List<Material>();
        Font font;
        Texture2D marbleTexture;
        Texture2D cockpitTexture;
        Texture2D sprayTexture;
        readonly List<Mesh> generatedMeshes = new List<Mesh>();
        readonly List<ParticleSystem> waterEffects = new List<ParticleSystem>();
        Material sprayMaterial;
        float steeringVisual, riverTime;
        float surgeIntensity;
        float downhillPitch,dropIntensity;
        sealed class FallingRock
        {
            public Transform rock;public LineRenderer warning;public ParticleSystem splash;
            public bool active;public float age,z,targetX,startX,radius;
        }
        readonly List<FallingRock> rockfalls=new List<FallingRock>();
        float nextRockfall;
        float DisplaySpeed=>phase==Phase.Riding?currentSpeed*3.6f:0;
        struct Obstacle { public Transform transform; public float radius; public bool spawn,passed; }
        int evaded,nearMisses,landings;
        float peakSpeed,landingKick,feedbackCooldown,bestTime;
        bool wasDropping;
        string failureReason="";
        ParticleSystem landingSplash;
        AudioSource impactAudio;
        AudioClip impactClip;
        readonly List<Texture2D> clothTextures=new List<Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (!Object.FindAnyObjectByType<GorgeRideGame>())
                new GameObject("Taiwan Go - Gorge Ride").AddComponent<GorgeRideGame>();
        }

        void Awake()
        {
            foreach (Camera c in Object.FindObjectsByType<Camera>()) c.enabled = false;
            foreach (Light l in Object.FindObjectsByType<Light>()) l.enabled = false;
            Random.InitState(1020);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Noto Sans CJK TC", "Arial" }, 24);
            marble = Mat(new Color(.63f,.66f,.64f));
            marbleTexture = new Texture2D(256,256);
            for (int y=0;y<256;y++) for(int x=0;x<256;x++)
            {
                float n=Mathf.PerlinNoise(x*.035f,y*.035f);
                float vein=Mathf.Pow(Mathf.Abs(Mathf.Sin(y*.15f+n*13f)),18f);
                marbleTexture.SetPixel(x,y,Color.Lerp(new Color(.78f,.8f,.77f),new Color(.24f,.29f,.29f),vein*.68f+n*.2f));
            }
            marbleTexture.Apply(); marble.mainTexture=marbleTexture;
            var rockTexture=Resources.Load<Texture2D>("GorgeArt/Marble");
            var cliffShader=Resources.Load<Shader>("GorgeArt/MarbleCliff");if(cliffShader)marble.shader=cliffShader;
            if(rockTexture){marble.mainTexture=rockTexture; marble.color=new Color(.93f,.94f,.93f);}
            dark=Mat(new Color(.055f,.075f,.08f)); red=Mat(new Color(.8f,.09f,.055f));
            yellow=Mat(new Color(1f,.72f,.04f)); mint=Mat(new Color(.34f,.64f,.56f));
            skin=Mat(new Color(.72f,.49f,.34f)); green=Mat(new Color(.13f,.27f,.12f)); white=Mat(new Color(.87f,.88f,.83f));
            if(cliffShader){green.shader=cliffShader;green.mainTexture=rockTexture;green.color=new Color(.13f,.27f,.12f);}
            foreach(var tint in new[]{new Color(.93f,.94f,.93f),new Color(.72f,.8f,.82f),new Color(.88f,.79f,.66f),new Color(.64f,.7f,.6f)})
            {var variant=new Material(marble);variant.color=tint;materials.Add(variant);cliffPalette.Add(variant);}
            waterMaterial=Mat(new Color(.045f,.43f,.46f),.83f);
            var waterShader=Resources.Load<Shader>("GorgeArt/River");
            if(waterShader){Destroy(waterMaterial);materials.RemoveAt(materials.Count-1);waterMaterial=new Material(waterShader);materials.Add(waterMaterial);}
            var riverTexture=Resources.Load<Texture2D>("GorgeArt/Water");
            if(riverTexture)waterMaterial.mainTexture=riverTexture;
            cockpitTexture=Resources.Load<Texture2D>("GorgeArt/Cockpit");
            var fixedCockpit=Resources.Load<Texture2D>("GorgeArt/CockpitBase");
            if(fixedCockpit)cockpitTexture=fixedCockpit;
            RenderSettings.fog=true; RenderSettings.fogColor=new Color(.58f,.72f,.76f);
            RenderSettings.fogMode=FogMode.ExponentialSquared; RenderSettings.fogDensity=.0035f;
            RenderSettings.ambientLight=new Color(.58f,.65f,.68f);
            var sun=new GameObject("Afternoon sun").AddComponent<Light>(); sun.transform.SetParent(transform);
            sun.type=LightType.Directional; sun.intensity=1.6f; sun.transform.rotation=Quaternion.Euler(42,-25,0); sun.shadows=LightShadows.Soft;
            rideCamera=new GameObject("Ride camera").AddComponent<Camera>(); rideCamera.transform.SetParent(transform);
            rideCamera.fieldOfView=68; rideCamera.farClipPlane=420; rideCamera.backgroundColor=new Color(.53f,.72f,.82f);
            rideCamera.clearFlags=CameraClearFlags.Skybox;
            var skyShader=Shader.Find("Skybox/Procedural");
            if(skyShader){var sky=new Material(skyShader);materials.Add(sky);sky.SetFloat("_SunSize",.035f);sky.SetFloat("_AtmosphereThickness",.85f);RenderSettings.skybox=sky;RenderSettings.sun=sun;}
            water=CreateRiver();
            for(int i=0;i<10;i++) BuildSection(i*38f);
            BuildBackdrop(); BuildBoat(); BuildSteeringCamera(); BuildWake(); BuildRapids(); BuildRockfalls(); BuildRiverAudio(); BuildRunFeedback(); ResetRide();
        }

        Material Mat(Color color,float smooth=.15f)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(!shader) shader=Shader.Find("Standard");
            var m=new Material(shader); m.color=color;
            m.SetFloat("_Smoothness",smooth); materials.Add(m); return m;
        }
        Transform Shape(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material,Transform parent)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false);
            go.transform.localPosition=position; go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;
            var collider=go.GetComponent<Collider>(); if(collider) Destroy(collider);
            return go.transform;
        }
        void BuildSection(float z)
        {
            var root=new GameObject("Marble canyon segment").transform; root.SetParent(transform); root.position=new Vector3(0,0,z); sections.Add(root);
            BuildScenery(root,(int)z+1020);
            for(int j=0;j<6;j++)
            {
                float radius=2;
                var rock=Shape("Collision rock",PrimitiveType.Sphere,new Vector3(0,0,j*5),new Vector3(radius*2,5,radius*2),marble,root);
                FractureRock(rock,j+19);obstacles.Add(new Obstacle{transform=rock,radius=radius,spawn=true});
            }
        }
        void BuildScenery(Transform section,int seed)
        {
            var old=section.Find("Scenery");
            if(old)
            {
                foreach(var filter in old.GetComponentsInChildren<MeshFilter>())if(generatedMeshes.Remove(filter.sharedMesh))Destroy(filter.sharedMesh);
                Destroy(old.gameObject);
            }
            var savedRandom=Random.state;Random.InitState(seed);
            var root=new GameObject("Scenery").transform;root.SetParent(section,false);
            float z=seed*.37f;
            BuildCliff(root,-1,z); BuildCliff(root,1,z+127);
            // Actual projecting rock masses give close riverbanks parallax and silhouettes.
            for(int side=-1;side<=1;side+=2)for(int j=0;j<4;j++)
            {
                float height=Random.Range(4f,21f);
                var buttress=Shape("Projecting marble buttress",PrimitiveType.Sphere,new Vector3(side*Random.Range(15.5f,22f),height*.35f,j*9.5f+Random.Range(0,8)),new Vector3(Random.Range(5f,10f),height,Random.Range(5f,14f)),marble,root);
                buttress.localRotation=Quaternion.Euler(Random.Range(-15,15),Random.Range(-35,35),side*Random.Range(-15,18));
                FractureRock(buttress,j+side*7);
                var shelf=Shape("Overhanging marble ledge",PrimitiveType.Sphere,new Vector3(side*Random.Range(23f,27f),Random.Range(16f,27f),j*9.5f),new Vector3(11,Random.Range(4f,7f),12),marble,root);
                FractureRock(shelf,j+35);
            }
            for(int side=-1;side<=1;side+=2)
            {
                Material stone=cliffPalette[Random.Range(0,cliffPalette.Count)];
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())if(Mathf.Sign(renderer.bounds.center.x-section.position.x)==side)renderer.sharedMaterial=stone;
                int vegetation=Random.Range(4,11);
                for(int j=0;j<vegetation;j++)
                {
                    var shrub=Shape("Cliff vegetation",PrimitiveType.Sphere,new Vector3(side*Random.Range(26f,35f),Random.Range(23f,48f),Random.Range(0f,38f)),new Vector3(Random.Range(3f,8f),Random.Range(2f,5f),Random.Range(4f,9f)),green,root);
                    FractureRock(shrub,j+seed*.01f);
                }
                int debris=Random.Range(3,8);
                for(int j=0;j<debris;j++)
                {
                    var rock=Shape("Broken riverbank scree",PrimitiveType.Sphere,new Vector3(side*Random.Range(11.7f,15f),Random.Range(-.3f,1),Random.Range(0f,38f)),new Vector3(Random.Range(1f,3f),Random.Range(1f,3.4f),Random.Range(2f,5f)),stone,root);
                    FractureRock(rock,j+seed*.03f);
                }
            }
            Random.state=savedRandom;
        }
        void ArrangeSection(Transform section)
        {
            var local=new List<int>();for(int i=0;i<obstacles.Count;i++)if(obstacles[i].transform.parent==section)local.Add(i);
            float arrivalEstimate=Mathf.Max(elapsed,section.position.z/26f);
            var layout=GorgeCourseLayout.Scatter(courseRandom,safeCorridor,GorgeRunRules.Difficulty(arrivalEstimate));safeCorridor=layout.end;
            for(int i=0;i<local.Count;i++)
            {
                    int index=local[i];var rock=obstacles[index];rock.spawn=i<layout.rocks.Length;rock.passed=false;
                    if(arrivalEstimate<13&&i>=3)rock.spawn=false;
                    if(!rock.spawn){rock.transform.gameObject.SetActive(false);obstacles[index]=rock;continue;}
                    var position=layout.rocks[i];rock.radius=position.radius;
                    rock.transform.localPosition=new Vector3(position.x,.35f,position.z);
                    rock.transform.localScale=new Vector3(rock.radius*2,3.8f+(float)courseRandom.NextDouble()*3.7f,rock.radius*2);
                    rock.transform.localRotation=Quaternion.Euler(0,(float)courseRandom.NextDouble()*360,0);
                    rock.transform.gameObject.SetActive(true);obstacles[index]=rock;
                    if(waterEffects.Count>index+1)
                    {
                        var ps=waterEffects[index+1];ps.Clear();ps.transform.localPosition=rock.transform.localPosition+new Vector3(0,.8f,-rock.radius);
                        var shape=ps.shape;shape.scale=new Vector3(rock.radius*2.1f,.1f,.7f);
                    }
            }
        }
        void FractureRock(Transform rock,float seed)
        {
            var mesh=Instantiate(rock.GetComponent<MeshFilter>().sharedMesh);var vertices=mesh.vertices;
            for(int v=0;v<vertices.Length;v++)
            {
                Vector3 p=vertices[v];float n=Mathf.PerlinNoise(p.x*6+seed+21,p.z*6+p.y*4+37);
                p*=.82f+n*.48f;p.x=Mathf.Round(p.x*22)/22;p.y=Mathf.Round(p.y*18)/18;
                Vector2 radial=new Vector2(p.x,p.z);if(radial.magnitude>.5f){radial=radial.normalized*.5f;p.x=radial.x;p.z=radial.y;}vertices[v]=p;
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();rock.GetComponent<MeshFilter>().sharedMesh=mesh;generatedMeshes.Add(mesh);
        }
        Transform MeshObject(string name,Vector3[] vertices,int[] triangles,Vector2[] uv,Material material,Transform parent)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=triangles;mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();generatedMeshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
            return go.transform;
        }
        Transform CreateRiver()
        {
            const int cols=48,rows=360;
            var vertices=new Vector3[(cols+1)*(rows+1)];var uv=new Vector2[vertices.Length];var triangles=new int[cols*rows*6];int t=0;
            for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
            {
                int i=y*(cols+1)+x;vertices[i]=new Vector3(-14+x*28f/cols,-.2f,-35+y*1.25f);uv[i]=new Vector2(x*28f/cols/9,y*1.25f/9);
                if(x<cols&&y<rows){triangles[t++]=i;triangles[t++]=i+cols+1;triangles[t++]=i+1;triangles[t++]=i+1;triangles[t++]=i+cols+1;triangles[t++]=i+cols+2;}
            }
            return MeshObject("Flowing turquoise whitewater",vertices,triangles,uv,waterMaterial,transform);
        }
        void BuildCliff(Transform parent,int side,float worldZ)
        {
            const int cols=28,rows=22;var vertices=new Vector3[(cols+1)*(rows+1)];var uv=new Vector2[vertices.Length];var triangles=new int[cols*rows*6];int t=0;
            for(int y=0;y<=rows;y++)for(int z=0;z<=cols;z++)
            {
                float along=z*38f/cols;
                float height=2.1f+Mathf.PerlinNoise(worldZ*.13f+side*51,0)*1.5f;
                float vertical=y*height;
                float noise=Mathf.PerlinNoise((along+worldZ)*.075f+side*83,vertical*.085f+12);
                float detail=Mathf.PerlinNoise((along+worldZ)*.27f+side*19,vertical*.25f+71);
                float ledge=Mathf.Sin(vertical*.49f+noise*5)*2.4f;
                float fold=Mathf.Abs(Mathf.Sin((along+worldZ)*.11f+side*3+vertical*.09f))*3.5f;
                float profile=Mathf.PerlinNoise(worldZ*.007f+side*15,7);
                // V-shaped cross-section: narrow river floor, steep flared slopes above it.
                float x=side*Mathf.Max(11.5f,11.8f+vertical*Mathf.Lerp(.58f,.95f,profile)+noise*4+ledge+fold+(detail-.5f)*2.6f);
                int i=y*(cols+1)+z;vertices[i]=new Vector3(x,vertical-3,along);uv[i]=new Vector2((along+worldZ)/11,vertical/11);
                if(z<cols&&y<rows)
                {
                    if(side==1){triangles[t++]=i;triangles[t++]=i+1;triangles[t++]=i+cols+1;triangles[t++]=i+1;triangles[t++]=i+cols+2;triangles[t++]=i+cols+1;}
                    else{triangles[t++]=i;triangles[t++]=i+cols+1;triangles[t++]=i+1;triangles[t++]=i+1;triangles[t++]=i+cols+1;triangles[t++]=i+cols+2;}
                }
            }
            MeshObject("Fractured marble gorge wall",vertices,triangles,uv,marble,parent);
        }
        void BuildBackdrop()
        {
            var texture=Resources.Load<Texture2D>("GorgeArt/Backdrop");if(!texture)return;
            var shader=Shader.Find("Universal Render Pipeline/Unlit");if(!shader)return;
            var material=new Material(shader);material.mainTexture=texture;materials.Add(material);
            backdrop=Shape("Photographic distant gorge and forest",PrimitiveType.Quad,new Vector3(0,85,350),new Vector3(600,338,1),material,transform);
        }
        void BuildBoat()
        {
            var metal=Mat(new Color(.17f,.2f,.22f),.7f);metal.SetFloat("_Metallic",.55f);
            var denim=Cloth(new Color(.12f,.23f,.34f));
            var jacket=Cloth(new Color(.48f,.07f,.045f));var lifeJacket=Cloth(new Color(.045f,.065f,.06f));
            mint=Cloth(new Color(.29f,.53f,.46f));
            white.SetFloat("_Smoothness",.76f);red.SetFloat("_Smoothness",.5f);yellow.SetFloat("_Smoothness",.5f);
            model=new GorgeJetSkiModel(transform,generatedMeshes,white,red,dark,yellow,mint,skin,metal,denim,jacket,lifeJacket);
            boat=model.root;riders=model.riders;handlebar=model.handlebar;
        }
        Material Cloth(Color color)
        {
            var material=Mat(color,.12f);var texture=new Texture2D(64,64,TextureFormat.RGB24,true);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float weave=((x+y)%3==0?.84f:1f)*(.95f+Mathf.PerlinNoise(x*.19f,y*.19f)*.05f);
                texture.SetPixel(x,y,new Color(weave,weave,weave));
            }
            texture.wrapMode=TextureWrapMode.Repeat;texture.Apply();clothTextures.Add(texture);
            material.mainTexture=texture;material.mainTextureScale=new Vector2(5,5);return material;
        }
        void BuildSteeringCamera()
        {
            steeringAssembly=Instantiate(handlebar.gameObject,transform).transform;
            steeringAssembly.name="First person physical handlebar";steeringAssembly.position=new Vector3(0,-1000,0);
            foreach(var part in steeringAssembly.GetComponentsInChildren<Transform>())part.gameObject.layer=30;
            // Isolate the cockpit from all world cameras; animate the same steering axis as the vehicle.
            rideCamera.cullingMask&=~(1<<30);
            steeringRender=new RenderTexture(1024,384,24,RenderTextureFormat.ARGB32);steeringRender.Create();
            steeringCamera=new GameObject("Cockpit render camera").AddComponent<Camera>();steeringCamera.transform.SetParent(transform);
            steeringCamera.transform.position=new Vector3(0,-999.28f,-1.7f);steeringCamera.transform.LookAt(steeringAssembly.position);
            steeringCamera.clearFlags=CameraClearFlags.SolidColor;steeringCamera.backgroundColor=Color.clear;
            steeringCamera.cullingMask=1<<30;steeringCamera.targetTexture=steeringRender;steeringCamera.fieldOfView=25;
            steeringCamera.nearClipPlane=.05f;steeringCamera.farClipPlane=5;
        }
        void BuildWake()
        {
            var go=new GameObject("White water spray"); go.transform.SetParent(boat,false); go.transform.localPosition=new Vector3(0,0,-1.65f);
            go.transform.localRotation=Quaternion.Euler(12,180,0);
            var ps=go.AddComponent<ParticleSystem>(); var main=ps.main;
            main.startLifetime=.65f; main.startSpeed=25; main.startSize=new ParticleSystem.MinMaxCurve(.16f,.5f); main.startColor=new Color(.87f,.98f,1,.65f); main.maxParticles=650;
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission; emission.rateOverTime=330;
            var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Cone; shape.angle=18; shape.radius=.7f;
            var shader=Resources.Load<Shader>("GorgeArt/RiderCutout");
            if(shader)
            {
                sprayTexture=new Texture2D(32,32,TextureFormat.RGBA32,false);
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                {
                    float radius=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;
                    sprayTexture.SetPixel(x,y,new Color(.9f,.97f,1,Mathf.Pow(Mathf.Clamp01(1-radius),1.5f)));
                }
                sprayTexture.Apply();sprayMaterial=new Material(shader);sprayMaterial.mainTexture=sprayTexture;materials.Add(sprayMaterial);
                go.GetComponent<ParticleSystemRenderer>().sharedMaterial=sprayMaterial;
            }
            waterEffects.Add(ps);
        }
        void BuildRapids()
        {
            if(!sprayMaterial)return;
            foreach(var obstacle in obstacles)
            {
                var go=new GameObject("Breaking whitewater around boulder");go.transform.SetParent(obstacle.transform.parent,false);
                go.transform.localPosition=obstacle.transform.localPosition+new Vector3(0,.45f,-obstacle.radius);
                var ps=go.AddComponent<ParticleSystem>();var main=ps.main;
                main.startLifetime=new ParticleSystem.MinMaxCurve(.3f,.65f);main.startSpeed=new ParticleSystem.MinMaxCurve(3f,6.5f);
                main.startSize=new ParticleSystem.MinMaxCurve(.15f,.5f);main.startColor=new Color(.92f,.99f,1,.7f);main.maxParticles=100;
                main.simulationSpace=ParticleSystemSimulationSpace.Local;main.gravityModifier=.55f;
                var emission=ps.emission;emission.rateOverTime=70;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(obstacle.radius*2.2f,.1f,.35f);
                go.transform.localRotation=Quaternion.Euler(-65,180,0);
                var motion=ps.velocityOverLifetime;motion.enabled=true;motion.space=ParticleSystemSimulationSpace.World;motion.z=-9;
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=sprayMaterial;waterEffects.Add(ps);
            }
        }
        void ResetRide()
        {
            elapsed=distance=lateral=velocity=wind=steeringVisual=riverTime=0; phase=Phase.Ready;
            currentSpeed=0;danger=impactFlash=surgeIntensity=downhillPitch=dropIntensity=0;safeCorridor=0;courseRandom=new System.Random(System.Environment.TickCount);
            nextRockfall=0;evaded=nearMisses=landings=0;peakSpeed=landingKick=feedbackCooldown=0;wasDropping=false;failureReason="";
            bestTime=PlayerPrefs.GetFloat("GorgeRide.BestTime",0);
            landingSplash.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);impactAudio.Stop();
            water.position=Vector3.zero;if(backdrop)backdrop.position=new Vector3(0,85,350);
            foreach(var fall in rockfalls){fall.active=false;fall.rock.gameObject.SetActive(false);fall.warning.enabled=false;fall.splash.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
            UpdateTerrainProfile();
            for(int i=0;i<sections.Count;i++) sections[i].position=new Vector3(0,0,i*38-38);
            foreach(var section in sections)ArrangeSection(section);
            foreach(var rock in obstacles)rock.transform.gameObject.SetActive(rock.spawn&&rock.transform.position.z>=120);
            foreach(var ps in waterEffects){ps.Clear();ps.Pause();}
            waterMaterial.SetFloat("_RiverTime",riverTime);waterMaterial.SetFloat("_Surge",0);handlebar.localRotation=Quaternion.identity;
            boat.position=new Vector3(0,.3f,0); boat.rotation=Quaternion.identity; UpdateCamera(true);
        }
        void UpdateTerrainProfile()
        {
            // Geometry now uses absolute world Z; only vertical origin is rebased for precision.
            Shader.SetGlobalFloat("_GorgeDistance",0);
            Shader.SetGlobalFloat("_GorgeOriginHeight",GorgeCourseLayout.Elevation(distance));
        }
        float RelativeRiverHeight(float z){return GorgeCourseLayout.Elevation(distance+z)-GorgeCourseLayout.Elevation(distance);}
        void BuildRockfalls()
        {
            var shader=Shader.Find("Universal Render Pipeline/Unlit");if(!shader)shader=Shader.Find("Unlit/Color");
            var warningMaterial=new Material(shader);warningMaterial.color=new Color(1,.08f,.035f);materials.Add(warningMaterial);
            for(int i=0;i<6;i++)
            {
                var fall=new FallingRock();fall.rock=Shape("Falling canyon rock",PrimitiveType.Sphere,Vector3.zero,Vector3.one*2.4f,marble,transform);FractureRock(fall.rock,72+i);
                var ring=new GameObject("Rockfall landing warning");ring.transform.SetParent(transform);
                fall.warning=ring.AddComponent<LineRenderer>();fall.warning.sharedMaterial=warningMaterial;fall.warning.loop=true;fall.warning.useWorldSpace=true;fall.warning.positionCount=40;fall.warning.widthMultiplier=.1f;
                var splash=new GameObject("Rockfall impact splash");splash.transform.SetParent(transform);fall.splash=splash.AddComponent<ParticleSystem>();
                var main=fall.splash.main;main.playOnAwake=false;main.loop=false;main.duration=.35f;main.startLifetime=.7f;main.startSpeed=new ParticleSystem.MinMaxCurve(3,7);main.startSize=new ParticleSystem.MinMaxCurve(.15f,.5f);main.startColor=new Color(.88f,.98f,1,.8f);main.gravityModifier=.7f;main.simulationSpace=ParticleSystemSimulationSpace.Local;
                var emission=fall.splash.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,95)});
                var shape=fall.splash.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=55;shape.radius=.7f;
                splash.transform.rotation=Quaternion.Euler(-90,0,0);fall.splash.GetComponent<ParticleSystemRenderer>().sharedMaterial=sprayMaterial;
                fall.rock.gameObject.SetActive(false);fall.warning.enabled=false;rockfalls.Add(fall);
            }
        }
        void SpawnRockfall()
        {
            FallingRock fall=rockfalls.Find(f=>!f.active);if(fall==null)return;
            float radius=1+(float)courseRandom.NextDouble()*.55f;
            float targetX=Mathf.Clamp(lateral+(float)(courseRandom.NextDouble()*6-3),-6.2f,6.2f);
            float z=distance+currentSpeed*(RockfallWarningTime+RockfallDropTime+.4f)+8;bool clear=false;
            // Do not land a new hazard inside an existing boulder cluster.
            for(int attempt=0;attempt<9;attempt++)
            {
                clear=true;foreach(var obstacle in obstacles)
                {
                    if(!obstacle.transform.gameObject.activeSelf)continue;
                    Vector3 p=obstacle.transform.position;
                    if(new Vector2(p.x-targetX,p.z-z).magnitude<radius+obstacle.radius+2){clear=false;break;}
                }
                if(clear)break;z+=6;
            }
            if(!clear)return;
            fall.active=true;fall.age=0;fall.z=z;fall.targetX=targetX;fall.startX=courseRandom.Next(2)==0?-18:18;fall.radius=radius;
            fall.rock.localScale=new Vector3(radius*2,radius*2.3f,radius*2);fall.rock.gameObject.SetActive(true);fall.warning.enabled=true;
        }
        bool UpdateRockfalls(float dt,float previousX)
        {
            if(GorgeRunRules.CanSpawnRockfall(elapsed))
            {nextRockfall-=dt;if(nextRockfall<=0){SpawnRockfall();nextRockfall=3.8f+(float)courseRandom.NextDouble()*2.3f;}}
            bool hit=false;
            foreach(var fall in rockfalls)
            {
                if(!fall.active)continue;float previousAge=fall.age;fall.age+=dt;
                float t=Mathf.Clamp01((fall.age-RockfallWarningTime)/RockfallDropTime);
                float height=Mathf.Lerp(19,.35f,t*t);
                float x=Mathf.Lerp(fall.startX,fall.targetX,Mathf.SmoothStep(0,1,t));
                float previousT=Mathf.Clamp01((previousAge-RockfallWarningTime)/RockfallDropTime);
                float previousRockX=Mathf.Lerp(fall.startX,fall.targetX,Mathf.SmoothStep(0,1,previousT));
                fall.rock.position=new Vector3(x,height,fall.z);
                if(t<1)fall.rock.Rotate(new Vector3(45,31,22)*dt,Space.Self);
                float surface=RelativeRiverHeight(fall.z-distance);
                fall.splash.transform.position=new Vector3(fall.targetX,surface+.25f,fall.z);
                float landingTime=RockfallWarningTime+RockfallDropTime;
                if(previousAge<landingTime&&fall.age>=landingTime){fall.splash.Play();fall.warning.enabled=false;}
                if(fall.age<landingTime)
                {
                    float ringRadius=fall.radius+1+.2f*Mathf.Sin(fall.age*13);
                    for(int i=0;i<40;i++){float a=i*Mathf.PI*2/40;fall.warning.SetPosition(i,new Vector3(fall.targetX+Mathf.Cos(a)*ringRadius,surface+.7f,fall.z+Mathf.Sin(a)*ringRadius));}
                }
                if(height<2.6f&&GorgeCourseLayout.SweptBoatHit(previousX+x-previousRockX,lateral,x,fall.z-distance,currentSpeed*dt,fall.radius))hit=true;
                if(fall.z<distance-15){evaded++;fall.active=false;fall.rock.gameObject.SetActive(false);fall.warning.enabled=false;}
            }
            return hit;
        }
        void BuildRunFeedback()
        {
            var go=new GameObject("Landing spray burst");go.transform.SetParent(boat,false);go.transform.localPosition=new Vector3(0,-.15f,.65f);go.transform.localRotation=Quaternion.Euler(-90,0,0);
            landingSplash=go.AddComponent<ParticleSystem>();var main=landingSplash.main;main.playOnAwake=false;main.loop=false;main.duration=.3f;
            main.startLifetime=.5f;main.startSpeed=new ParticleSystem.MinMaxCurve(3,6);main.startSize=new ParticleSystem.MinMaxCurve(.12f,.4f);main.gravityModifier=.5f;main.startColor=new Color(.87f,.97f,1,.75f);
            var emission=landingSplash.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,85)});
            var shape=landingSplash.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=65;shape.radius=.8f;landingSplash.GetComponent<ParticleSystemRenderer>().sharedMaterial=sprayMaterial;
            const int samples=11025;var data=new float[samples];var rng=new System.Random(17);
            for(int i=0;i<samples;i++){float t=i/22050f;data[i]=((float)rng.NextDouble()*2-1)*Mathf.Exp(-t*9)*.5f+Mathf.Sin(t*370)*Mathf.Exp(-t*16)*.16f;}
            impactClip=AudioClip.Create("Landing water impact",samples,1,22050,false);impactClip.SetData(data,0);impactAudio=gameObject.AddComponent<AudioSource>();impactAudio.volume=.35f;impactAudio.playOnAwake=false;
        }
        float ReadSteering()
        {
            float input=0;var k=Keyboard.current;
            if(k!=null){if(k.aKey.isPressed||k.leftArrowKey.isPressed)input--;if(k.dKey.isPressed||k.rightArrowKey.isPressed)input++;}
            if(Gamepad.current!=null)input+=Gamepad.current.leftStick.ReadValue().x;
            var pointer=Pointer.current;
            if(pointer!=null&&pointer.press.isPressed)
            {
                float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f),w=Screen.width/scale,h=Screen.height/scale;
                Vector2 p=pointer.position.ReadValue();p=new Vector2(p.x/scale,(Screen.height-p.y)/scale);
                if(new Rect(25,h-140,92,70).Contains(p))input--;if(new Rect(w-117,h-140,92,70).Contains(p))input++;
            }
            return Mathf.Clamp(input,-1,1);
        }
        void FinishRun(bool success,string reason)
        {
            phase=success?Phase.Won:Phase.Failed;failureReason=reason;
            if(success)elapsed=Duration;else impactFlash=1;
            if(elapsed>bestTime){bestTime=elapsed;PlayerPrefs.SetFloat("GorgeRide.BestTime",bestTime);PlayerPrefs.Save();}
        }
        void BuildRiverAudio()
        {
            const int rate=22050,count=rate*3;var samples=new float[count];var random=new System.Random(63);float low=0;
            for(int i=0;i<count;i++)
            {
                float n=(float)random.NextDouble()*2-1;low=Mathf.Lerp(low,n,.14f);
                float pulse=1+Mathf.Sin(i/(float)rate*6)*.15f;
                float edge=Mathf.Min(1,Mathf.Min(i,count-1-i)/220f);
                samples[i]=(low*.8f+n*.12f+Mathf.Sin(i/(float)rate*340)*.025f)*pulse*edge;
            }
            riverClip=AudioClip.Create("Rapid river and engine",count,1,rate,false);riverClip.SetData(samples,0);
            riverAudio=gameObject.AddComponent<AudioSource>();riverAudio.clip=riverClip;riverAudio.loop=true;riverAudio.volume=.22f;
            if(!Object.FindAnyObjectByType<AudioListener>())rideCamera.gameObject.AddComponent<AudioListener>();
        }
        void Update()
        {
            var k=Keyboard.current;
            if(k!=null)
            {
                if(k.digit1Key.wasPressedThisFrame)view=0;
                if(k.digit2Key.wasPressedThisFrame)view=1;
                if(k.digit3Key.wasPressedThisFrame)view=2;
                if(k.cKey.wasPressedThisFrame)view=(view+1)%3;
                if(k.escapeKey.wasPressedThisFrame) TogglePause();
                if(k.rKey.wasPressedThisFrame)ResetRide();
                if(k.mKey.wasPressedThisFrame)muted=!muted;
                if(k.spaceKey.wasPressedThisFrame){ if(phase==Phase.Ready)phase=Phase.Riding; else if(phase==Phase.Failed||phase==Phase.Won){ResetRide();phase=Phase.Riding;} }
            }
            foreach(var fall in rockfalls)
            {
                if(phase!=Phase.Riding&&fall.splash.isPlaying)fall.splash.Pause();
                else if(phase==Phase.Riding&&fall.splash.isPaused)fall.splash.Play();
            }
            for(int i=0;i<waterEffects.Count;i++)
            {
                var ps=waterEffects[i];bool visible=i==0||obstacles[i-1].transform.gameObject.activeSelf;
                if(ps.gameObject.activeSelf!=visible)ps.gameObject.SetActive(visible);
                if(!visible)continue;
                if(phase==Phase.Riding){if(!ps.isPlaying)ps.Play();}else if(ps.isPlaying)ps.Pause();
            }
            if(riverAudio)
            {
                riverAudio.mute=muted;riverAudio.pitch=.85f+currentSpeed/70;
                riverAudio.volume=.22f+surgeIntensity*.1f+danger*.04f;
                if(phase==Phase.Riding){if(!riverAudio.isPlaying)riverAudio.Play();}else riverAudio.Pause();
            }
            impactAudio.mute=muted;
            if(phase!=Phase.Riding){if(landingSplash.isPlaying)landingSplash.Pause();if(impactAudio.isPlaying)impactAudio.Pause();}
            else {if(landingSplash.isPaused)landingSplash.Play();impactAudio.UnPause();}
            if(phase!=Phase.Riding)return;
            float dt=Mathf.Min(Mathf.Min(Time.deltaTime,.05f),Duration-elapsed), steer=ReadSteering();
            steeringVisual=Mathf.MoveTowards(steeringVisual,steer,dt*4.5f);
            float surgeTarget=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.48f,.76f,Mathf.PerlinNoise(elapsed*.32f+13,7)));
            surgeIntensity=Mathf.MoveTowards(surgeIntensity,surgeTarget*GorgeRunRules.Difficulty(elapsed),dt*1.8f);
            currentSpeed=Mathf.MoveTowards(currentSpeed,GorgeRunRules.TargetSpeed(elapsed)+surgeIntensity*2.5f,dt*18);
            peakSpeed=Mathf.Max(peakSpeed,currentSpeed*3.6f);
            waterMaterial.SetFloat("_Surge",surgeIntensity);
            riverTime+=dt;waterMaterial.SetFloat("_RiverTime",riverTime);
            wind=(Mathf.Sin(elapsed*.61f)*1.2f+Mathf.Sin(elapsed*.23f)*.7f)*Mathf.Lerp(.2f,1,GorgeRunRules.Difficulty(elapsed));
            velocity=Mathf.MoveTowards(velocity,steer*12f+wind,dt*32f);
            float previous=lateral; lateral+=velocity*dt; distance+=currentSpeed*dt; elapsed+=dt;
            if(elapsed<GorgeRunRules.PracticeTime){lateral=Mathf.Clamp(lateral,-HalfWidth+1,HalfWidth-1);if(Mathf.Abs(lateral)>HalfWidth-1.1f)velocity=0;}
            UpdateTerrainProfile();
            float slope=(GorgeCourseLayout.Elevation(distance+1.5f)-GorgeCourseLayout.Elevation(distance-1.5f))/3;
            downhillPitch=Mathf.Lerp(downhillPitch,Mathf.Atan(-slope)*Mathf.Rad2Deg,dt*7);
            dropIntensity=Mathf.Clamp01((-slope-.04f)*6);
            feedbackCooldown=Mathf.Max(0,feedbackCooldown-dt);
            if(wasDropping&&dropIntensity<.2f&&feedbackCooldown<=0)
            {
                landingSplash.Play();impactAudio.PlayOneShot(impactClip);landingKick=1;landings++;feedbackCooldown=1.5f;
            }
            if(dropIntensity>.45f)wasDropping=true;else if(dropIntensity<.2f)wasDropping=false;
            float suspension=Mathf.Min(.24f,RelativeRiverHeight(-1.7f));
            boat.position=new Vector3(lateral,.43f+suspension+Mathf.Sin(elapsed*9)*(.1f+surgeIntensity*.07f)+Mathf.Sin(elapsed*17)*.045f,distance);
            boat.rotation=Quaternion.Euler(downhillPitch+2+Mathf.Sin(elapsed*7)*(2.3f+surgeIntensity*1.5f),steeringVisual*10,-steeringVisual*10);
            model.Animate(steeringVisual,elapsed);
            foreach(var section in sections)
            {
                if(section.position.z < distance-55)
                {
                    section.position+=Vector3.forward*(sections.Count*38f);
                    ArrangeSection(section);
                    BuildScenery(section,courseRandom.Next(1,900000));
                }
            }
            water.position=new Vector3(0,0,distance);if(backdrop)backdrop.position=new Vector3(0,85,distance+350);
            for(int i=1;i<waterEffects.Count;i++)
            {
                var rock=obstacles[i-1];
                waterEffects[i].transform.localPosition=rock.transform.localPosition+new Vector3(0,.8f+RelativeRiverHeight(rock.transform.position.z-distance),-rock.radius);
            }
            bool hit=Mathf.Abs(lateral)>HalfWidth-.9f;
            string reason=hit?"擦撞河岸":"撞上岩石";
            danger=0;
            for(int rockIndex=0;rockIndex<obstacles.Count;rockIndex++)
            {
                var rock=obstacles[rockIndex];
                if(!rock.transform.gameObject.activeSelf)continue;
                Vector3 p=rock.transform.position;
                float relativeZ=p.z-distance;
                if(GorgeCourseLayout.SweptBoatHit(previous,lateral,p.x,relativeZ,currentSpeed*dt,rock.radius)){hit=true;break;}
                if(relativeZ>0&&relativeZ<32)danger=Mathf.Max(danger,Mathf.Clamp01(1-(Mathf.Abs(p.x-lateral)-rock.radius)/4f)*(1-relativeZ/38));
                if(!rock.passed&&relativeZ < -rock.radius-2.1f)
                {
                    rock.passed=true;obstacles[rockIndex]=rock;evaded++;
                    if(Mathf.Abs(p.x-lateral)-rock.radius<2)nearMisses++;
                }
            }
            if(UpdateRockfalls(dt,previous)){hit=true;reason="未避開落石";}
            if(elapsed<GorgeRunRules.PracticeTime)
            {
                hit=false;
            }
            if(hit)FinishRun(false,reason);
            else if(elapsed>=Duration)FinishRun(true,"");
        }
        void LateUpdate(){ if(boat){UpdateCamera(false);model.SetSpeed(DisplaySpeed);}impactFlash=Mathf.MoveTowards(impactFlash,0,Time.unscaledDeltaTime*2);if(phase==Phase.Riding)landingKick=Mathf.MoveTowards(landingKick,0,Time.deltaTime*4); }
        void UpdateCamera(bool instant)
        {
            riders.gameObject.SetActive(view!=0);
            steeringCamera.enabled=view==0;
            steeringAssembly.rotation=Quaternion.Euler(0,steeringVisual*32,0);
            // First person sees down the river; the small cockpit overlay cannot cover the center.
            Vector3 offset=view==0?new Vector3(0,2.3f,.4f):view==1?new Vector3(0,4.8f,-6.3f):new Vector3(6.7f,3.2f,-8.5f);
            Vector3 target=boat.position+offset;
            if(view==2)target.x=Mathf.Clamp(target.x,-10,10);
            if(phase==Phase.Riding)
            {
                float shake=.025f+surgeIntensity*.055f+danger*.025f+dropIntensity*.02f;
                target+=new Vector3((Mathf.PerlinNoise(elapsed*17,3)-.5f)*shake*2,Mathf.Sin(elapsed*29)*shake,0);
                target.y-=landingKick*.12f;
            }
            rideCamera.transform.position=instant?target:Vector3.Lerp(rideCamera.transform.position,target,1-Mathf.Exp(-Time.unscaledDeltaTime*8));
            Vector3 aim=boat.position+(view==0?new Vector3(steeringVisual*1.4f,-.1f,23):view==1?new Vector3(0,.4f,8):new Vector3(0,.9f,6));
            aim.y+=RelativeRiverHeight(view==0?23:view==1?8:6);
            rideCamera.transform.LookAt(aim);
            float baseFov=view==0?80:view==1?72:76;
            rideCamera.fieldOfView=Mathf.Lerp(rideCamera.fieldOfView,baseFov+surgeIntensity*4+danger*2+dropIntensity*2,instant?1:Time.unscaledDeltaTime*5);
            if(phase==Phase.Riding)rideCamera.transform.rotation*=Quaternion.Euler(Mathf.Sin(elapsed*13)*surgeIntensity*.35f+landingKick*1.4f,0,-steeringVisual*1.7f);
        }
        void TogglePause(){if(phase==Phase.Riding)phase=Phase.Paused;else if(phase==Phase.Paused)phase=Phase.Riding;}
        GUIStyle Style(int size,TextAnchor alignment=TextAnchor.MiddleCenter)
        { return new GUIStyle(GUI.skin.label){font=font,fontSize=size,alignment=alignment,normal={textColor=Color.white}}; }
        void DrawSpeedEffects(float w,float h)
        {
            Color saved=GUI.color;
            if(phase==Phase.Riding)
            {
                Vector2 center=new Vector2(w*.5f,h*.43f);
                for(int i=0;i<38;i++)
                {
                    float angle=i*2.39996f,progress=Mathf.Repeat(riverTime*(1.7f+currentSpeed*.035f)+i*.618f,1);
                    float radius=Mathf.Lerp(.38f,.88f,progress);
                    Vector2 p=center+new Vector2(Mathf.Cos(angle)*w,Mathf.Sin(angle)*h)*radius;
                    GUI.color=new Color(.79f,.94f,1,Mathf.Sin(progress*Mathf.PI)*(.14f+surgeIntensity*.12f));
                    Matrix4x4 matrix=GUI.matrix;
                    GUIUtility.RotateAroundPivot(Mathf.Atan2(p.y-center.y,p.x-center.x)*Mathf.Rad2Deg,p);
                    GUI.DrawTexture(new Rect(p.x,p.y,Mathf.Lerp(15,85+surgeIntensity*35,progress),1.3f),Texture2D.whiteTexture);GUI.matrix=matrix;
                }
                // Brief off-center spray wisps during rough water; the navigation center stays clear.
                for(int i=0;i<9&&sprayTexture;i++)
                {
                    float p=Mathf.Repeat(riverTime*(1.1f+i*.07f)+i*.713f,1);
                    float side=i%2==0?-1:1;
                    float x=w*.5f+side*w*(.28f+p*.27f),y=h*(.88f-p*.4f);
                    GUI.color=new Color(.8f,.94f,1,surgeIntensity*.2f*Mathf.Sin(p*Mathf.PI));
                    GUI.DrawTexture(new Rect(x,y,4+p*8,18+p*30),sprayTexture);
                }
                GUI.color=new Color(.015f,.035f,.045f,.14f+surgeIntensity*.14f);
                GUI.DrawTexture(new Rect(0,0,w,8),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0,h-8,w,8),Texture2D.whiteTexture);
                if(danger>.25f)
                {
                    GUI.color=new Color(1,.23f,.09f,danger*.25f*(.65f+.35f*Mathf.Sin(elapsed*17)));
                    GUI.DrawTexture(new Rect(0,0,7,h),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(w-7,0,7,h),Texture2D.whiteTexture);
                    GUI.color=new Color(1,.28f,.12f,danger*.1f);
                    GUI.DrawTexture(new Rect(7,0,22,h),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(w-29,0,22,h),Texture2D.whiteTexture);
                }
            }
            if(impactFlash>0){GUI.color=new Color(1,.16f,.06f,impactFlash*.28f);GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);}
            GUI.color=saved;
        }
        void OnGUI()
        {
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*scale);
            float w=Screen.width/scale,h=Screen.height/scale;
            if(view==0&&cockpitTexture)
            {
                // Dashboard stays in the bottom third; the moving grips remain visible below it.
                Rect cockpitRect=new Rect(w*.09f,h*.34f,w*.82f,h*.68f);
                GUI.DrawTexture(cockpitRect,cockpitTexture,ScaleMode.StretchToFill,true);
                Color saved=GUI.color;GUI.color=new Color(.015f,.025f,.035f);GUI.DrawTexture(new Rect(w*.443f,h*.775f,w*.114f,h*.072f),Texture2D.whiteTexture);GUI.color=saved;
                var digits=Style(27);digits.normal.textColor=new Color(.65f,1,1);
                GUI.Label(new Rect(w*.443f,h*.775f,w*.114f,h*.047f),DisplaySpeed.ToString("0"),digits);
                GUI.Label(new Rect(w*.443f,h*.819f,w*.114f,h*.02f),"km/h",Style(11));
                if(steeringRender)GUI.DrawTexture(new Rect(0,h*.70f,w,h*.34f),steeringRender,ScaleMode.StretchToFill,true);
            }
            GUI.Box(new Rect(20,20,270,66),GUIContent.none); GUI.Label(new Rect(30,24,250,32),"峽谷風阻穿梭",Style(25));
            GUI.Label(new Rect(30,55,250,22),"TAIWAN GO · 花蓮",Style(12));
            GUI.Box(new Rect(w/2-110,20,220,55),GUIContent.none);
            GUI.Label(new Rect(w/2-110,20,220,55),$"{(int)elapsed/60:00}:{(int)elapsed%60:00} / {(int)Duration/60:00}:{(int)Duration%60:00}",Style(27));
            if(phase==Phase.Riding)
            {
                string stage=elapsed<GorgeRunRules.PracticeTime?$"熟悉操作 · {Mathf.CeilToInt(GorgeRunRules.PracticeTime-elapsed)}":elapsed<20?"穿越急流":"最後衝刺";
                GUI.Label(new Rect(w/2-150,78,300,28),stage,Style(16));
                Color saved=GUI.color;GUI.color=new Color(.09f,.16f,.16f,.7f);GUI.DrawTexture(new Rect(w/2-150,110,300,3),Texture2D.whiteTexture);
                GUI.color=new Color(.42f,.8f,.71f);GUI.DrawTexture(new Rect(w/2-150,110,300*elapsed/Duration,3),Texture2D.whiteTexture);GUI.color=saved;
            }
            GUI.Label(new Rect(w-230,24,210,32),$"風力 {(wind>=0?"→":"←")} {Mathf.Abs(wind):0.0}",Style(20));
            DrawSpeedEffects(w,h);
            DrawRockfallWarnings(w,h,scale);
            GUI.Label(new Rect(20,143,160,30),$"{DisplaySpeed:0} km/h",Style(22));
            string[] names={"1 眼睛視角","2 立體追逐視角","3 側後方追拍"};
            for(int i=0;i<3;i++){GUI.backgroundColor=view==i?new Color(.2f,.75f,.65f):Color.white;if(GUI.Button(new Rect(w-212,78+i*43,192,36),names[i]))view=i;} GUI.backgroundColor=Color.white;
            if(GUI.Button(new Rect(20,100,90,36),phase==Phase.Paused?"繼續":"暫停"))TogglePause();
            GUI.Label(new Rect(w/2-300,h-48,600,28),"A / D 或 ← → 轉向　·　C 切換視角　·　Esc 暫停　·　R 重來",Style(16));
            if(phase==Phase.Riding)
            {
                GUI.Button(new Rect(25,h-140,92,70),"◀");
                GUI.Button(new Rect(w-117,h-140,92,70),"▶");
            }
            else if(phase==Phase.Failed||phase==Phase.Won)DrawResults(w,h);
            else
            {
                GUI.Box(new Rect(w/2-260,h/2-137,520,274),GUIContent.none);
                string title=phase==Phase.Ready?"峽谷風阻穿梭":phase==Phase.Failed?"挑戰失敗":phase==Phase.Won?"挑戰成功":"已暫停";
                GUI.Label(new Rect(w/2-240,h/2-112,480,60),title,Style(36));
                string message=phase==Phase.Ready?"前 5 秒練習 → 急流閃避 → 落石衝刺\n30 秒穿越花蓮峽谷":"休息一下，再出發";
                GUI.Label(new Rect(w/2-230,h/2-40,460,70),message,Style(20));
                if(GUI.Button(new Rect(w/2-100,h/2+65,200,48),phase==Phase.Paused?"繼續挑戰":"出發"))
                {if(phase==Phase.Paused)phase=Phase.Riding;else{ResetRide();phase=Phase.Riding;}}
            }
        }
        void DrawResults(float w,float h)
        {
            Rect panel=new Rect(w/2-290,h/2-205,580,410);Color saved=GUI.color;
            GUI.color=new Color(.025f,.065f,.065f,.95f);GUI.DrawTexture(panel,Texture2D.whiteTexture);GUI.color=saved;
            GUI.Label(new Rect(panel.x+20,panel.y+18,540,26),"TAIWAN GO · 花蓮站",Style(16));
            GUI.Label(new Rect(panel.x+20,panel.y+52,540,52),phase==Phase.Won?"挑戰成功":"航程中止",Style(34));
            GUI.Label(new Rect(panel.x+20,panel.y+106,540,30),phase==Phase.Won?"雙人共乘，完成峽谷穿梭":failureReason,Style(18));
            GUI.Label(new Rect(panel.x+20,panel.y+151,540,32),$"完成 {elapsed:0.0} / 30 秒　·　閃避 {evaded} 塊岩石",Style(23));
            GUI.Label(new Rect(panel.x+20,panel.y+191,540,28),$"最高 {peakSpeed:0} km/h　·　驚險閃避 {nearMisses} 次",Style(18));
            GUI.Label(new Rect(panel.x+20,panel.y+224,540,26),$"航行 {distance:0} 公尺　·　最佳紀錄 {bestTime:0.0} 秒",Style(16));
            GUI.Label(new Rect(panel.x+24,panel.y+270,532,42),"花蓮｜大理石峽谷與湍急溪流，交織山水景觀。",Style(16));
            if(GUI.Button(new Rect(w/2-215,panel.y+337,200,48),"再挑戰一次")){ResetRide();phase=Phase.Riding;}
            if(GUI.Button(new Rect(w/2+15,panel.y+337,200,48),"回到起點"))ResetRide();
        }
        void DrawRockfallWarnings(float w,float h,float scale)
        {
            if(phase!=Phase.Riding&&phase!=Phase.Paused)return;
            foreach(var fall in rockfalls)
            {
                if(!fall.active||fall.age>=RockfallWarningTime+RockfallDropTime)continue;
                Vector3 point=rideCamera.WorldToScreenPoint(new Vector3(fall.targetX,RelativeRiverHeight(fall.z-distance)+2.5f,fall.z));
                if(point.z<=0)continue;
                float x=Mathf.Clamp(point.x/scale,45,w-45),y=Mathf.Clamp((Screen.height-point.y)/scale,110,h-155);
                var shadow=Style(27);shadow.normal.textColor=new Color(.04f,.01f,.01f,.4f);
                GUI.Label(new Rect(x-18+1,y-23+1,36,42),"!",shadow);
                var redWarning=Style(27);redWarning.normal.textColor=new Color(.8f,.2f,.15f,.7f);
                GUI.Label(new Rect(x-18,y-23,36,42),"!",redWarning);
            }
        }
        void OnDestroy()
        {
            foreach(var m in materials)if(m)Destroy(m);
            if(marbleTexture)Destroy(marbleTexture);
            if(sprayTexture)Destroy(sprayTexture);
            if(font)Destroy(font);
            if(riverClip)Destroy(riverClip);
            if(impactClip)Destroy(impactClip);
            foreach(var texture in clothTextures)if(texture)Destroy(texture);
            if(steeringRender){steeringRender.Release();Destroy(steeringRender);}
            foreach(var mesh in generatedMeshes)if(mesh)Destroy(mesh);
        }
    }
}
