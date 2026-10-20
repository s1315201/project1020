Shader "TaiwanGo/River"
{
    Properties { _MainTex("River surface",2D)="white"{} _RiverTime("Simulation time",Float)=0 _Surge("Rapid surge",Float)=0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GorgeProfile.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float _RiverTime;
            float _Surge;
            CBUFFER_END
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            float Turbulence(float2 p){return Noise(p)*.57+Noise(p*2.13+7.4)*.28+Noise(p*4.37-3.2)*.15;}
            float Height(float2 p,float t)
            {
                float2 current=p+float2(.28*t,25*t);
                float2 warp=float2(Turbulence(current*.11),Turbulence(current*.13+37))*5;
                float surge=Turbulence((current+warp)*.24);
                float crossWave=sin(p.x*1.16+p.y*.83+t*(17+surge*5)+warp.x)*.11;
                float breaker=pow(saturate(surge),3)*1.1;
                return ((surge-.5)*.52+crossWave+breaker)*(1+_Surge*.38);
            }
            struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float fog:TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;float3 p=TransformObjectToWorld(v.positionOS.xyz);
                float t=_RiverTime;
                p.y+=GorgeRelativeHeight(p.z)+Height(p.xz,t);
                o.world=p;o.positionCS=TransformWorldToHClip(o.world);o.uv=v.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float t=_RiverTime;
                // Warped local currents move at different speeds and shear across the river.
                float2 current=i.world.xz+float2(.28*t,25*t);
                float turbulence=Turbulence(current*.13);
                float2 distortion=float2(Turbulence(current*.19+13),Turbulence(current*.21-21))-.5;
                float2 flow=float2(sin(i.world.z*.12+t)*.11,3.9)*t;
                half3 a=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+flow+distortion*.48).rgb;
                half3 b=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv*float2(2.1,.74)+flow*1.37-distortion*.7).rgb;
                half3 baseColor=lerp(a,b,.34);
                float3 n=normalize(cross(ddy(i.world),ddx(i.world)));if(n.y<0)n=-n;
                n=normalize(n+float3(distortion.x*.45,0,distortion.y*.35));
                float3 v=GetWorldSpaceNormalizeViewDir(i.world);
                Light sun=GetMainLight();float fresnel=pow(1-saturate(dot(n,v)),4);
                float spec=pow(saturate(dot(n,normalize(sun.direction+v))),100);
                baseColor=lerp(baseColor,float3(.57,.75,.82),fresnel*.48)+spec*sun.color*.65;
                float crest=Turbulence(current*.3+distortion*2);
                float textureFoam=saturate((dot(b,float3(.3,.59,.11))-.52)*4);
                float whitewater=smoothstep(.48,.78,crest)*(.4+textureFoam*.6);
                float downhill=saturate((GorgeElevation(_GorgeDistance+i.world.z-1)-GorgeElevation(_GorgeDistance+i.world.z+1))*.5*7);
                whitewater=saturate(whitewater+downhill*textureFoam*.55);
                float streaks=pow(saturate(Noise(float2(current.x*.8+distortion.x,current.y*.09))),6);
                whitewater=saturate(whitewater+streaks*(.65+_Surge*.25)+turbulence*textureFoam*(.24+_Surge*.2));
                float bankFoam=smoothstep(8.5,13.5,abs(i.world.x))*(.3+textureFoam*.7);
                baseColor=lerp(baseColor*float3(.82,.95,.97),float3(.87,.98,.98),saturate(whitewater*.86+bankFoam*.9));
                return half4(MixFog(baseColor,i.fog),1);
            }
            ENDHLSL
        }
    }
}
