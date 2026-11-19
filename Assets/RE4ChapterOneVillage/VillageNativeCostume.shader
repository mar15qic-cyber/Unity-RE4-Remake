Shader "RE4/Village Native Costume"
{
    Properties
    {
        _Color ("原版材质颜色", Color) = (1, 1, 1, 1)
        _MainTex ("底色", 2D) = "white" {}
        [NoScaleOffset] _NativeNormal ("原版法线与粗糙度", 2D) = "gray" {}
        [NoScaleOffset] _PackedMap ("原版透明度与遮蔽", 2D) = "white" {}
        [NoScaleOffset] _PatternMap ("服饰花纹", 2D) = "gray" {}
        _PatternColor ("花纹颜色", Color) = (0.5, 0.5, 0.5, 1)
        _PatternWeight ("花纹强度", Range(0, 1)) = 0
        _NormalWeight ("法线强度", Range(0, 1)) = 0
        _Glossiness ("光滑度", Range(0, 1)) = 0.25
        _Cutoff ("裁切阈值", Range(0, 1)) = 0.28
        [HideInInspector] _Cull ("剔除模式", Float) = 2
        [Toggle(_VILLAGE_COSTUME_CUTOUT)] _AlphaClip ("毛发透明度裁切", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 250
        Cull [_Cull]

        CGPROGRAM
        #pragma surface Surface Standard fullforwardshadows addshadow
        #pragma target 3.0
        #pragma shader_feature_local _VILLAGE_COSTUME_CUTOUT

        sampler2D _MainTex;
        sampler2D _NativeNormal;
        sampler2D _PackedMap;
        sampler2D _PatternMap;
        fixed4 _Color;
        fixed4 _PatternColor;
        half _PatternWeight;
        half _NormalWeight;
        half _Glossiness;
        half _Cutoff;

        struct Input { float2 uv_MainTex; };

        void Surface(Input input, inout SurfaceOutputStandard output)
        {
            float2 uv = input.uv_MainTex;
            half4 packed = tex2D(_PackedMap, uv);
#ifdef _VILLAGE_COSTUME_CUTOUT
            clip(packed.r - _Cutoff);
#endif
            half3 baseColor = tex2D(_MainTex, uv).rgb * _Color.rgb;
            half3 pattern = tex2D(_PatternMap, uv).rgb * _PatternColor.rgb;
            output.Albedo = baseColor * lerp(1.0, saturate(pattern * 3.0 + 0.25), _PatternWeight);

            half4 nativeNormal = tex2D(_NativeNormal, uv);
            half2 tangent = (nativeNormal.rg * 2.0 - 1.0) * _NormalWeight;
            output.Normal = half3(tangent, sqrt(saturate(1.0 - dot(tangent, tangent))));
            output.Smoothness = lerp(_Glossiness, saturate(1.0 - nativeNormal.b) * 0.52, _NormalWeight);
            output.Occlusion = lerp(0.64, 1.0, packed.b);
            output.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
