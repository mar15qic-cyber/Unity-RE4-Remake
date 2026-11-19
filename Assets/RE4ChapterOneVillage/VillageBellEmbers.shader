Shader "RE4/Village Bell/Fire"
{
    Properties
    {
        _WaterDrop("水珠模式", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"
            float _WaterDrop;
            struct VertexInput { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Interpolator { float4 position : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            Interpolator Vertex(VertexInput v)
            {
                Interpolator o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            float4 Fragment(Interpolator i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float envelope = saturate(1 - abs(p.x + sin(p.y * 9 + _Time.y * 14) * 0.17) - abs(p.y) * 0.62);
                float tongues = 0.78 + sin(p.x * 17 + p.y * 23 - _Time.y * 20) * 0.22;
                float drop = 1 - smoothstep(0.04, 0.91, length(p * float2(1.13, 0.95)));
                float opacity = lerp(envelope * envelope * tongues, drop * drop * 0.42, _WaterDrop);
                return float4(i.color.rgb * lerp(1.75, 0.95, _WaterDrop), i.color.a * opacity);
            }
            ENDCG
        }
    }
}
