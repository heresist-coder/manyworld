// 숲 현장의 풀 덤불. 뿌리는 어두운 초록, 끝은 볕 받은 연두(군데군데 마른 노랑), 바람에 끝이 흔들린다.
// 메시의 uv.y = 잎 높이 비율 (0 뿌리, 1 끝). GrassField가 GPU 인스턴싱으로 그린다.
Shader "Manyworld/Grass"
{
    Properties
    {
        _BaseColor ("Root Color", Color) = (0.17, 0.24, 0.09, 1)
        _TipColor ("Tip Color", Color) = (0.56, 0.64, 0.25, 1)
        _DryTip ("Dry Tip Color", Color) = (0.74, 0.66, 0.34, 1)
        _DryAmount ("Dry Amount", Range(0, 1)) = 0.55
        _PatchScale ("Patch Scale", Float) = 0.04
        _WindStrength ("Wind Strength", Float) = 0.14
        _WindSpeed ("Wind Speed", Float) = 1.4
        _Translucency ("Translucency", Range(0, 1)) = 0.4
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _TipColor;
            half4 _DryTip;
            float _DryAmount;
            float _PatchScale;
            float _WindStrength;
            float _WindSpeed;
            float _Translucency;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float Hash21(float2 p)
        {
            p = frac(p * float2(234.34, 435.345));
            p += dot(p, p + 34.23);
            return frac(p.x * p.y);
        }

        float Noise2(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), u.x), u.y);
        }

        // 끝으로 갈수록(높이²) 크게 흔들린다. 큰 돌풍 노이즈가 풀밭 위를 지나간다
        float3 Wind(float3 root, float h)
        {
            float t = _Time.y * _WindSpeed;
            float w = sin(t + root.x * 0.35 + root.z * 0.2) * 0.6 + sin(t * 1.7 + root.x * 0.9 - root.z * 0.6) * 0.25;
            float gust = Noise2(root.xz * 0.03 + float2(t * 0.15, t * 0.05));
            return float3(1.0, 0.0, 0.45) * w * (0.4 + gust) * _WindStrength * h * h;
        }

        float3 GrassWorldPos(Attributes v)
        {
            float3 ws = TransformObjectToWorld(v.positionOS.xyz);
            float3 root = TransformObjectToWorld(float3(0, 0, 0));
            return ws + Wind(root, v.uv.y);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 rootWS : TEXCOORD3;
                float fog : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 ws = GrassWorldPos(v);
                o.positionWS = ws;
                o.rootWS = TransformObjectToWorld(float3(0, 0, 0));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float h = saturate(i.uv.y);
                // 마른 풀 얼룩 (넓은 노이즈) + 포기마다 조금씩 다른 색
                float patch = smoothstep(0.4, 0.75, Noise2(i.rootWS.xz * _PatchScale)) * _DryAmount;
                float var = Hash21(floor(i.rootWS.xz * 3.0) + 0.5);
                half3 tip = lerp(_TipColor.rgb, _DryTip.rgb, saturate(patch + (var - 0.5) * 0.35));
                half3 albedo = lerp(_BaseColor.rgb, tip, smoothstep(0.0, 0.95, h)) * (0.86 + var * 0.28);

                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 n = normalize(i.normalWS);
                float wrap = saturate(dot(n, light.direction) * 0.5 + 0.5);
                float atten = light.shadowAttenuation * light.distanceAttenuation;
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float back = pow(saturate(dot(viewDir, -light.direction)), 4.0) * _Translucency * h; // 역광에 잎끝이 빛난다
                // 구운 간접광(APV)이 있으면 그 자리의 빛, 없으면 하늘 환경광. 뿌리 쪽은 어둡게
                #if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
                half3 ambient = SampleProbeVolumePixel(half3(0, 0, 0), i.positionWS, float3(0, 1, 0), viewDir, i.positionCS.xy);
                #else
                half3 ambient = SampleSH(float3(0, 1, 0));
                #endif
                ambient *= 0.5 + 0.5 * h;
                half3 col = albedo * (ambient + light.color * atten * (wrap + back));
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            float4 vertDepth(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformWorldToHClip(GrassWorldPos(v));
            }

            half fragDepth() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_instancing

            struct VaryingsDN
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            VaryingsDN vertDN(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                VaryingsDN o;
                o.positionCS = TransformWorldToHClip(GrassWorldPos(v));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 fragDN(VaryingsDN i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
