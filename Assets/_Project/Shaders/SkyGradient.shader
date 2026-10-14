// 숲 현장 하늘 (스카이박스). 천정의 짙은 파랑 → 지평의 옅은 회청색, 해 둘레 빛무리.
// 유니티 기본 절차 하늘은 한낮이 희뿌옇고 지평이 노랗게 떠서 목표 샷 01·04의 맑은 파란 하늘이 안 나왔다.
Shader "Manyworld/SkyGradient"
{
    Properties
    {
        _ZenithColor ("천정", Color) = (0.3, 0.48, 0.78, 1)
        _MidColor ("중간 (고도 15°)", Color) = (0.52, 0.65, 0.84, 1) // (0.62,0.72,0.86은 목표보다 희었다: 04 하늘 파랑 155 vs 175, 03 하늘이 희뿌옇다)
        _HorizonColor ("지평", Color) = (0.72, 0.77, 0.84, 1)
        _GroundColor ("지평 아래", Color) = (0.45, 0.5, 0.55, 1)
        _SunGlow ("해 빛무리 색", Color) = (1, 0.88, 0.7, 1)
        _SunGlowSize ("빛무리 크기", Range(1, 64)) = 10
        _SunGlowStrength ("빛무리 세기", Range(0, 2)) = 0.45
        _Exposure ("밝기", Range(0.2, 3)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _MidColor, _HorizonColor, _GroundColor, _SunGlow;
                float _SunGlowSize, _SunGlowStrength, _Exposure;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dir = i.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                // 지평 → 고도 15° → 천정 (위로 갈수록 천천히 짙어진다)
                half3 col = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.26, y));
                col = lerp(col, _ZenithColor.rgb, smoothstep(0.2, 0.95, y));
                col = lerp(col, _GroundColor.rgb, smoothstep(0.0, -0.12, y));
                // 해 쪽 하늘이 조금 밝고 따뜻하다
                float3 sunDir = _MainLightPosition.xyz;
                float s = saturate(dot(d, sunDir));
                col += _SunGlow.rgb * (pow(s, _SunGlowSize) * _SunGlowStrength + pow(s, 3) * 0.08 * saturate(1 - y * 2));
                return half4(col * _Exposure, 1);
            }
            ENDHLSL
        }
    }
}
