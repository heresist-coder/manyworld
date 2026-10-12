// 숲 현장의 하늘 구름. 카메라를 감싸는 큰 구 안쪽에 그린다.
// 방향 벡터를 하늘 평면에 투영해 fbm 노이즈로 뭉게구름을 만들고, 해 쪽으로 한 번 더 샘플해 아래쪽 그늘을 넣는다.
Shader "Manyworld/CloudDome"
{
    Properties
    {
        _LitColor ("Lit Color", Color) = (1, 0.97, 0.92, 1)
        _ShadeColor ("Shade Color", Color) = (0.64, 0.7, 0.8, 1)
        _HorizonColor ("Horizon Color", Color) = (0.82, 0.85, 0.88, 1)
        _Cover ("Cover", Range(0, 1)) = 0.56
        _Softness ("Softness", Range(0.01, 0.5)) = 0.14
        _Scale ("Scale", Float) = 1.3
        _Speed ("Speed", Float) = 0.003
        _SunDir ("Sun Direction", Vector) = (0, 0.4, 1, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-400" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Clouds"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Front
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LitColor;
                half4 _ShadeColor;
                half4 _HorizonColor;
                float _Cover;
                float _Softness;
                float _Scale;
                float _Speed;
                float4 _SunDir;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dirWS : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.dirWS = ws - _WorldSpaceCameraPos;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 5; k++)
                {
                    v += a * Noise(p);
                    p = p * 2.03 + float2(17.1, 9.2);
                    a *= 0.5;
                }
                return v;
            }

            // 큰 덩어리(뭉게구름 무리) × 잔 주름
            float Density(float2 uv)
            {
                float big = Noise(uv * 0.3 + 4.0);
                return Fbm(uv) * (0.45 + 0.9 * big * big);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dirWS);
                if (d.y <= 0.0) return 0;
                float2 drift = float2(_Time.y * _Speed, _Time.y * _Speed * 0.4);
                float2 uv = d.xz / (d.y + 0.22) * _Scale + drift;
                float n = Density(uv);
                float dens = smoothstep(_Cover, _Cover + _Softness, n);
                // 해 쪽으로 조금 옮긴 곳이 더 짙으면 이 점은 그늘 (구름 아래쪽이 어둡다)
                float2 toSun = normalize(_SunDir.xz + 1e-4) * 0.09;
                float n2 = Density(uv + toSun);
                float lit = saturate(0.7 + (n - n2) * 7.0 - (n - _Cover) * 0.6); // 가장자리는 밝고 두꺼운 속은 살짝 그늘
                half3 col = lerp(_ShadeColor.rgb, _LitColor.rgb, lit);
                // 지평선 근처는 대기에 묻힌다
                float low = smoothstep(0.0, 0.16, d.y);
                col = lerp(_HorizonColor.rgb, col, 0.35 + 0.65 * low);
                float alpha = dens * smoothstep(0.0, 0.05, d.y) * 0.96;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
