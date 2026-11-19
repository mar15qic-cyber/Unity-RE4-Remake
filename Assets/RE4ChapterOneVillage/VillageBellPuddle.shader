Shader "RE4/Village Bell/Puddle"
{
    Properties
    {
        _ReflectionTex("局部反射", 2D) = "black" {}
        _ShotTime("镜头时间", Float) = 0
        _Tint("湿泥色", Color) = (0.16, 0.123, 0.085, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" }
        Cull Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"
            sampler2D _ReflectionTex;
            float _ShotTime;
            float4 _Tint;

            struct Interpolator
            {
                float4 position : SV_POSITION;
                float4 screen : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 world : TEXCOORD2;
            };
            Interpolator Vertex(appdata_base v)
            {
                Interpolator o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.screen = ComputeScreenPos(o.position);
                o.uv = v.texcoord;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            float Ripple(float2 uv, float2 origin, float elapsed)
            {
                float radius = distance(uv, origin);
                float ring = radius - elapsed * 0.43;
                return sin(ring * 85) * exp(-abs(ring) * 21) * exp(-max(elapsed, 0) * 2.3) * step(0, elapsed);
            }
            float4 Fragment(Interpolator i) : SV_Target
            {
                float2 d = (i.uv - 0.5) * 2;
                float edge = length(d) + sin(d.x * 21 + d.y * 7) * 0.055 + cos(d.y * 19) * 0.035;
                float opacity = (1 - smoothstep(0.57, 1.0, edge)) * 0.81;
                float ripple = Ripple(i.uv, float2(0.49, 0.54), _ShotTime - 10.51);
                ripple += Ripple(i.uv, float2(0.53, 0.47), _ShotTime - 17.11);
                float surface = sin(i.uv.x * 132 + i.uv.y * 91 + _ShotTime * 2) * 0.0015;
                float2 distort = float2(ripple, -ripple) * 0.018 + float2(surface, -surface);
                float2 uv = i.screen.xy / i.screen.w;
                float3 reflected = tex2D(_ReflectionTex, uv + distort).rgb;
                float3 view = normalize(_WorldSpaceCameraPos - i.world);
                float reflectivity = lerp(0.19, 0.72, pow(1 - abs(view.y), 2));
                return float4(lerp(_Tint.rgb, reflected * 0.86, reflectivity), opacity);
            }
            ENDCG
        }
    }
}
