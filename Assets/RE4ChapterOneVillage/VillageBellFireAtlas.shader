Shader "RE4/Village Bell/Fire Atlas"
{
    Properties
    {
        _MainTex ("原版粒子图集", 2D) = "white" {}
        _Flame ("火焰辉光强度", Range(0, 3)) = 0
        _FadeDistance ("软交界距离", Range(0.01, 1)) = 0.18
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_particles
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half _Flame;
            float _FadeDistance;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct VertexInput
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Interpolator
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 projected : TEXCOORD1;
                float depth : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            Interpolator Vertex(VertexInput input)
            {
                Interpolator output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                output.projected = ComputeScreenPos(output.position);
                output.depth = -UnityObjectToViewPos(input.vertex).z;
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }

            half4 Fragment(Interpolator input) : SV_Target
            {
                half4 atlas = tex2D(_MainTex, input.uv);
                half alpha = saturate(atlas.a * input.color.a);
#ifdef SOFTPARTICLES_ON
                float sceneryDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(input.projected)));
                alpha *= saturate((sceneryDepth - input.depth) / _FadeDistance);
#endif
                half heat = smoothstep(0.31, 0.94, atlas.a);
                half3 color = atlas.rgb * input.color.rgb;
                color += _Flame * heat * half3(1.0, 0.46, 0.12);
                half4 result = half4(color * alpha, alpha);
                UNITY_APPLY_FOG_COLOR(input.fogCoord, result, fixed4(0, 0, 0, 0));
                return result;
            }
            ENDCG
        }
    }
}
