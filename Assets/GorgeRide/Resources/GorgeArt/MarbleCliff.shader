Shader "TaiwanGo/MarbleCliff"
{
    Properties { _MainTex("Marble surface",2D)="white"{} _Color("Tint",Color)=(.93,.94,.93,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GorgeProfile.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            CBUFFER_END
            struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct Varyings{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float3 mapped:TEXCOORD2;float fog:TEXCOORD3;};
            Varyings Vert(Attributes v)
            {
                Varyings o;o.world=TransformObjectToWorld(v.positionOS.xyz);o.world.y+=GorgeRelativeHeight(o.world.z);o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normalOS);
                // Remove translation so the texture stays attached when canyon chunks recycle.
                o.mapped=mul((float3x3)unity_ObjectToWorld,v.positionOS.xyz)/9;
                o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 n=normalize(i.normal),weights=pow(abs(n),4);weights/=max(dot(weights,float3(1,1,1)),.001);
                half3 stone=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.mapped.zy).rgb*weights.x
                    +SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.mapped.xz).rgb*weights.y
                    +SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.mapped.xy).rgb*weights.z;
                float height=dot(stone,float3(.3,.59,.11));
                float3 dx=ddx(i.world),dy=ddy(i.world),r1=cross(dy,n),r2=cross(n,dx);
                float determinant=dot(dx,r1);
                float3 gradient=(r1*ddx(height)+r2*ddy(height))*sign(determinant)/max(abs(determinant),.00001);
                n=normalize(n-gradient*.24);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                float diffuse=saturate(dot(n,light.direction));
                float crevice=lerp(.67,1,smoothstep(.18,.78,height));
                half3 color=stone*_Color.rgb*(SampleSH(n)*.65+light.color*(.15+diffuse*.85)*light.shadowAttenuation)*crevice;
                // Dark, damp rock where the river meets the canyon walls.
                color*=lerp(.6,1,smoothstep(-.2,2.3,i.world.y-GorgeRelativeHeight(i.world.z)));
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "GorgeProfile.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct ShadowAttributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            float4 ShadowVert(ShadowAttributes v):SV_POSITION
            {
                float3 world=TransformObjectToWorld(v.positionOS.xyz);world.y+=GorgeRelativeHeight(world.z);
                float3 normal=TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirection=normalize(_LightPosition-world);
                #else
                float3 lightDirection=_LightDirection;
                #endif
                float4 clipPosition=TransformWorldToHClip(ApplyShadowBias(world,normal,lightDirection));
                #if UNITY_REVERSED_Z
                clipPosition.z=min(clipPosition.z,clipPosition.w*UNITY_NEAR_CLIP_VALUE);
                #else
                clipPosition.z=max(clipPosition.z,clipPosition.w*UNITY_NEAR_CLIP_VALUE);
                #endif
                return clipPosition;
            }
            half4 ShadowFrag():SV_Target{return 0;}
            ENDHLSL
        }
    }
}
