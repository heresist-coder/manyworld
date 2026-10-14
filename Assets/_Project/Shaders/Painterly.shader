// 회화풍 후처리 (D25): 일반화 쿠와하라 필터. 원 둘레를 8부채꼴로 나눠 가장 고른(분산이 작은) 부채꼴 색을 따른다.
// 경계는 살리고 면 안의 잔무늬는 붓질처럼 뭉갠다. 숲 현장에서만 켠다 (전역 _ManyworldPaint, F9로 끄고 켬).
Shader "Manyworld/Painterly"
{
    Properties
    {
        _Radius ("Radius (px)", Range(1, 8)) = 3
        _Amount ("Amount", Range(0, 1)) = 1
        _Sharpness ("Sharpness", Range(1, 16)) = 4
        _Grain ("붓결 세기", Range(0, 0.4)) = 0.13
        _StrokeLength ("붓결 길이 (px)", Range(2, 40)) = 18
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off
        Pass
        {
            Name "Painterly"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _Radius;
            float _Amount;
            float _Sharpness;
            float _ManyworldPaint;
            float _Grain;
            float _StrokeLength;

            float Hash21(float2 p)
            {
                p = frac(p * float2(233.34, 851.73));
                p += dot(p, p + 23.45);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }

            #define R 4

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 src = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
                float amount = _Amount * _ManyworldPaint;
                if (amount <= 0.001) return src;

                float2 step = _BlitTexture_TexelSize.xy * (_Radius / R);
                float3 m[8];
                float3 s[8];
                float w[8];
                [unroll] for (int k = 0; k < 8; k++) { m[k] = 0; s[k] = 0; w[k] = 0; }

                [unroll] for (int y = -R; y <= R; y++)
                [unroll] for (int x = -R; x <= R; x++)
                {
                    if (x * x + y * y > R * R) continue;
                    float3 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2(x, y) * step, 0).rgb;
                    float g = exp(-(x * x + y * y) / (float)(R * R));
                    if (x == 0 && y == 0)
                    {
                        // 가운데 점은 모든 부채꼴에 들어간다
                        [unroll] for (int a = 0; a < 8; a++) { m[a] += c * g; s[a] += c * c * g; w[a] += g; }
                    }
                    else
                    {
                        int sector = (int)floor((atan2((float)y, (float)x) + PI) / (PI / 4.0)) % 8;
                        m[sector] += c * g; s[sector] += c * c * g; w[sector] += g;
                    }
                }

                // 가장 고른 부채꼴을 기준으로 가중치를 매긴다 (모든 부채꼴이 거친 경계에서 0으로 수렴해 검게 뜨지 않게)
                float sig[8];
                float3 mean[8];
                float minSig = 10.0;
                [unroll] for (int i = 0; i < 8; i++)
                {
                    mean[i] = m[i] / max(w[i], 1e-4);
                    float3 var = abs(s[i] / max(w[i], 1e-4) - mean[i] * mean[i]);
                    sig[i] = sqrt(var.r + var.g + var.b);
                    minSig = min(minSig, sig[i]);
                }
                float3 acc = 0;
                float total = 0;
                [unroll] for (int j = 0; j < 8; j++)
                {
                    float wt = 1.0 / (1.0 + pow(sig[j] / (minSig + 0.004), _Sharpness));
                    acc += mean[j] * wt;
                    total += wt;
                }
                float3 paint = acc / max(total, 1e-6);

                // 붓결: 화면 결(밝기 기울기에 수직)을 따라 길쭉한 무늬로 밝기를 흔든다 (목표 샷들의 칠한 면 질감.
                // 쿠와하라만으로는 면이 고르게 뭉개져 단색 판처럼 보였다). 어두운 곳·하늘은 약하게
                float2 px = _BlitTexture_TexelSize.xy;
                float lx = dot(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2(px.x * 2, 0), 0).rgb - SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv - float2(px.x * 2, 0), 0).rgb, 0.333);
                float ly = dot(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2(0, px.y * 2), 0).rgb - SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv - float2(0, px.y * 2), 0).rgb, 0.333);
                float2 grad = float2(lx, ly);
                float2 along = dot(grad, grad) > 1e-6 ? normalize(float2(-grad.y, grad.x)) : float2(0.70, 0.71); // 결이 없으면 비스듬히
                float2 pix = uv / px;
                float2 q = float2(dot(pix, along), dot(pix, float2(-along.y, along.x)));
                float n = ValueNoise(float2(q.x / _StrokeLength, q.y / 3.6)) * 0.7 + ValueNoise(float2(q.x / (_StrokeLength * 0.5), q.y / 2.0) + 17.0) * 0.3;
                float lum = dot(paint, float3(0.3, 0.59, 0.11));
                // 하늘(깊이 없음)과 아주 먼 산에는 붓결을 넣지 않는다 (고른 하늘에서 필름 잡티처럼 보였다)
                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool sky = rawDepth <= 1e-5;
                #else
                    bool sky = rawDepth >= 1 - 1e-5;
                #endif
                float eye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float grainAmt = sky ? 0 : _Grain * smoothstep(0.02, 0.15, lum) * (1 - smoothstep(600, 1500, eye));
                n = smoothstep(0.25, 0.75, n); // 붓자국 경계가 또렷하게
                paint *= 1 + (n - 0.5) * 2 * grainAmt;
                return half4(lerp(src.rgb, paint, amount), src.a);
            }
            ENDHLSL
        }
    }
}
