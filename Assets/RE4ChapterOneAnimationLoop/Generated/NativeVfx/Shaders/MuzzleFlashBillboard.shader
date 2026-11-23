Shader "RE4/Muzzle Flash Billboard"
{
    Properties
    {
        _MainTex ("火光图集", 2D) = "white" {}
        _Intensity ("火光强度", Range(0, 4)) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent+25" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Intensity;

            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            VertexOutput vert(appdata_base input)
            {
                VertexOutput output;
                float3 center = UnityObjectToViewPos(float3(0, 0, 0));
                float2 size = float2(length(unity_ObjectToWorld._m00_m10_m20),
                                     length(unity_ObjectToWorld._m01_m11_m21));
                output.position = mul(UNITY_MATRIX_P, float4(center.xy + input.vertex.xy * size, center.z, 1));
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                return output;
            }

            float4 frag(VertexOutput input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv);
                return float4(color.rgb * _Intensity, color.a);
            }
            ENDCG
        }
    }
}
