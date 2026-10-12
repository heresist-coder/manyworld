// 먼 산 능선 띠. 정점색 그대로(빛·안개 없음): 공기 원근의 겹겹 색을 직접 칠한다.
Shader "Manyworld/Backdrop"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+50" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        Pass
        {
            Name "Backdrop"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return half4(i.color.rgb, 1); }
            ENDHLSL
        }
    }
}
