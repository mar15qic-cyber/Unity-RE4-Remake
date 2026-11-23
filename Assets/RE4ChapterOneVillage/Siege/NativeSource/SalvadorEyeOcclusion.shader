Shader "RE4/Salvador Eye Occlusion"
{
    Properties
    {
        _MainTex ("原版眼部遮蔽", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent-1" "RenderType"="Transparent" }
        Blend DstColor Zero
        ZWrite Off
        ColorMask RGB
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            struct VertexInput { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct VertexOutput { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            VertexOutput vert(VertexInput input)
            {
                VertexOutput output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
            fixed4 frag(VertexOutput input) : SV_Target
            {
                fixed4 mask = tex2D(_MainTex, input.uv);
                // 遮蔽层只压暗已有表面，不能画出白色眼罩。
                // ALBA 的 RGB 为白色，实际遮蔽权重在透明度通道中。
                fixed shade = lerp(1.0, 0.12, mask.a);
                return fixed4(shade, shade, shade, 1.0);
            }
            ENDCG
        }
    }
}
