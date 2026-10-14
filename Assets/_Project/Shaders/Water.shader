// 호수 물 (목표 샷 05). 맑은 물: 물 아래 장면(불투명 텍스처)을 굴절해 비추고 깊이만큼 푸르게 먹는다.
// 비스듬히 볼수록 하늘이 비친다(프레넬). 물결은 사인파 세 개의 기울기로 법선만 흔든다 (텍스처 없음).
Shader "Manyworld/Water"
{
    Properties
    {
        _ShallowColor ("얕은 물 색", Color) = (0.46, 0.66, 0.72, 1)
        _DeepColor ("깊은 물 색", Color) = (0.07, 0.25, 0.33, 1)
        _SkyColor ("비친 하늘 (위)", Color) = (0.45, 0.6, 0.82, 1)
        _HorizonColor ("비친 하늘 (지평)", Color) = (0.7, 0.76, 0.82, 1)
        _Clarity ("맑기 (m당 흡수)", Float) = 0.06
        _Reflect ("반사 세기", Range(0, 1)) = 0.85
        _WaveScale ("물결 크기 (m)", Float) = 9
        _WaveStrength ("물결 기울기", Float) = 0.022
        _Refraction ("굴절", Float) = 0.03
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _SkyColor, _HorizonColor;
                float _Clarity, _Reflect, _WaveScale, _WaveStrength, _Refraction;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // 사인파 세 개 (방향·파장·속도 다르게)의 높이 기울기
            float2 WaveSlope(float2 p, float t)
            {
                float2 g = 0;
                float2 d[3] = { float2(0.8, 0.6), float2(-0.45, 0.9), float2(0.2, -1.0) };
                float l[3] = { 1.0, 0.57, 0.31 };
                [unroll] for (int k = 0; k < 3; k++)
                {
                    float w = 6.2831 / (_WaveScale * l[k]);
                    float ph = dot(d[k], p) * w + t * (1.2 + k * 0.7);
                    g += d[k] * cos(ph) * l[k];
                }
                return g * _WaveStrength;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 slope = WaveSlope(i.positionWS.xz, _Time.y);
                float3 n = normalize(float3(-slope.x, 1, -slope.y));
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                float2 uv = i.screenPos.xy / i.screenPos.w;
                float waterEye = i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float thick = max(0, sceneEye - waterEye);
                // 굴절: 물보다 앞에 있는 물체를 끌어오지 않게 다시 확인
                float2 uv2 = uv + n.xz * _Refraction * saturate(thick * 0.3);
                float sceneEye2 = LinearEyeDepth(SampleSceneDepth(uv2), _ZBufferParams);
                if (sceneEye2 < waterEye) uv2 = uv; else thick = sceneEye2 - waterEye;
                half3 under = SampleSceneColor(uv2);

                // 물속: 깊을수록 얕은 물 색 → 깊은 물 색으로 먹힌다. 시선 방향 두께를 수직 깊이로 근사
                float depth = thick * saturate(v.y + 0.15);
                half absorb = exp(-depth * _Clarity);
                half3 body = lerp(_DeepColor.rgb, under * _ShallowColor.rgb * 1.3, absorb);

                // 반사 (프레넬): 반사 광선을 월드에서 걸어가며 화면에 투영해 장면 깊이보다 뒤로 들어가는 첫 점의 색
                // (화면 공간 반사. 물가 숲·먼 산·하늘이 비친다. 화면 밖으로 나가면 하늘색)
                // 반사 광선은 물결을 절반만 탄다 (다 타면 맞음·빗나감이 얼룩덜룩 섞였다)
                float3 r = reflect(-v, normalize(lerp(float3(0, 1, 0), n, 0.45)));
                r.y = abs(r.y);
                half3 sky = lerp(_HorizonColor.rgb, _SkyColor.rgb, saturate(r.y * 2.5));
                half3 refl = sky;
                float dist = 1.5;
                float2 lastUv = uv;
                bool hit = false;
                [loop] for (int k = 0; k < 40; k++)
                {
                    float3 p = i.positionWS + r * dist;
                    float4 cp = TransformWorldToHClip(p);
                    float4 sp = ComputeScreenPos(cp);
                    float2 puv = sp.xy / sp.w;
                    if (cp.w <= 0 || any(puv < 0) || any(puv > 1)) break;
                    lastUv = puv;
                    float sEye = LinearEyeDepth(SampleSceneDepth(puv), _ZBufferParams);
                    if (sEye < cp.w - 0.5 && sEye > waterEye) { hit = true; break; }
                    dist *= 1.2;
                }
                // 맞으면 그 색, 끝까지 안 맞으면 마지막 점(대개 하늘·먼 산)의 색. 화면 가장자리는 하늘로 녹인다
                half3 scene = SampleSceneColor(lastUv);
                float edge = saturate(min(min(lastUv.x, 1 - lastUv.x), min(lastUv.y, 1 - lastUv.y)) * 12);
                refl = lerp(sky, scene, hit ? edge : edge * 0.85);
                half fres = 0.02 + 0.98 * pow(1 - saturate(dot(n, v)), 5);
                half3 col = lerp(body, refl, saturate(fres * _Reflect + 0.06));

                // 물가 거품: 바닥·물체가 수면에 가까운 곳 (물가 띠와 뜬 바위 둘레 고리). 움직이는 얼룩으로 끊는다
                float foamBand = 1 - saturate(thick / 0.9);
                float2 fp = i.positionWS.xz * 0.9 + float2(_Time.y * 0.15, -_Time.y * 0.11);
                float blot = frac(sin(dot(floor(fp), float2(12.9898, 78.233))) * 43758.5453);
                float wave = sin(thick * 9 - _Time.y * 1.6) * 0.5 + 0.5; // 물가로 밀려오는 줄
                float foam = saturate(foamBand * foamBand * (0.55 + 0.45 * wave) * (0.6 + 0.4 * blot) * 1.6);
                col = lerp(col, half3(0.93, 0.95, 0.96), foam * 0.8);

                // 해 반짝임
                Light sun = GetMainLight();
                float3 hv = normalize(sun.direction + v);
                col += sun.color * pow(saturate(dot(n, hv)), 600) * 3;

                col = MixFog(col, i.fog);
                half alpha = saturate(thick * 2.5 + foam * 0.6); // 물가는 부드럽게 (거품은 남긴다)
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
