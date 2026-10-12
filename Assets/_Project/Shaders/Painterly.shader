// 회화풍 후처리 (D25): 일반화 쿠와하라 필터. 원 둘레를 8부채꼴로 나눠 가장 고른(분산이 작은) 부채꼴 색을 따른다.
// 경계는 살리고 면 안의 잔무늬는 붓질처럼 뭉갠다. 숲 현장에서만 켠다 (전역 _ManyworldPaint, F9로 끄고 켬).
Shader "Manyworld/Painterly"
{
    Properties
    {
        _Radius ("Radius (px)", Range(1, 8)) = 3
        _Amount ("Amount", Range(0, 1)) = 1
        _Sharpness ("Sharpness", Range(1, 16)) = 4
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

            float _Radius;
            float _Amount;
            float _Sharpness;
            float _ManyworldPaint;

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
                return half4(lerp(src.rgb, paint, amount), src.a);
            }
            ENDHLSL
        }
    }
}
