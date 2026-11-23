Shader "RE4/Village Window Glass"
{
    Properties
    {
        _MainTex ("原版玻璃污渍颜色", 2D) = "white" {}
        [NoScaleOffset] _NormalTex ("原版法线与粗糙度", 2D) = "gray" {}
        [NoScaleOffset] _OpacityTex ("原版污渍遮罩（红通道）", 2D) = "white" {}
        _ClearOpacity ("干净玻璃不透明度", Range(0, 1)) = 0.015
        _DirtOpacity ("污渍最浓处不透明度", Range(0, 1)) = 0.12
        _Reflectance ("正面反射率", Range(0, 0.08)) = 0.01
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("面剔除", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull [_Cull]
        ZWrite Off

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf StandardSpecular alpha:premul

        sampler2D _MainTex, _NormalTex, _OpacityTex;
        half _ClearOpacity, _DirtOpacity, _Reflectance;

        struct Input
        {
            float2 uv_MainTex;
            float facing : VFACE;
        };

        void surf(Input input, inout SurfaceOutputStandardSpecular surface)
        {
            float2 uv = input.uv_MainTex;
            half dirt = smoothstep(0.24h, 0.78h, tex2D(_OpacityTex, uv).r);
            half3 packedNormal = tex2D(_NormalTex, uv).rgb;
            half2 slope = (packedNormal.rg * 2.0h - 1.0h) * 0.32h;
            half3 normal = half3(slope, sqrt(saturate(1.0h - dot(slope, slope))));
            // 飞散碎片是单面网格，翻转后仍用朝向观察者的一面计算高光。
            if (input.facing < 0.0)
            {
                normal = -normal;
            }

            surface.Normal = normal;
            surface.Albedo = tex2D(_MainTex, uv).rgb;
            surface.Alpha = lerp(_ClearOpacity, _DirtOpacity, dirt);
            surface.Specular = _Reflectance;
            surface.Smoothness = lerp(0.90h, 0.64h, dirt * packedNormal.b);
            surface.Occlusion = 1.0h;
        }
        ENDCG
    }
    FallBack Off
}
