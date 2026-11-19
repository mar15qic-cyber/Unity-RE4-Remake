Shader "RE4/Chapter One Village Surface"
{
    Properties
    {
        _MainTex ("主纹理", 2D) = "white" {}
        _NormalTex ("法线粗糙度", 2D) = "bump" {}
        _LayerTex ("第二层纹理", 2D) = "white" {}
        _LayerMask ("分层遮罩", 2D) = "black" {}
        _OpacityTex ("透明遮罩", 2D) = "white" {}
        _EmissionTex ("自发光", 2D) = "black" {}
        _Tint ("基色", Color) = (1,1,1,1)
        _LayerTint ("第二层基色", Color) = (1,1,1,1)
        _EmissionTint ("自发光颜色", Color) = (0,0,0,0)
        _MainTransform ("第一层平铺", Vector) = (1,1,0,0)
        _LayerTransform ("第二层平铺", Vector) = (1,1,0,0)
        _MaskChannel ("分层遮罩通道", Vector) = (1,0,0,0)
        _AlphaChannel ("透明遮罩通道", Vector) = (1,0,0,0)
        _LayerEnabled ("使用第二层", Float) = 0
        _LayerUV2 ("遮罩使用次级 UV", Float) = 0
        _MainUV2 ("第一层使用次级 UV", Float) = 0
        _ExtraUV2 ("第二层使用次级 UV", Float) = 0
        _NormalEnabled ("使用原版法线", Float) = 0
        [ToggleOff(RE4_NO_ALPHA_CLIP)] _AlphaEnabled ("使用裁剪", Float) = 0
        _AlphaSource ("透明遮罩来源", Float) = 0
        _Cutoff ("裁剪阈值", Range(0,1)) = .5
        _Smoothness ("光滑度", Range(0,1)) = .24
        _Cull ("剔除", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull [_Cull]
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #pragma shader_feature_local _ RE4_NO_ALPHA_CLIP
        sampler2D _MainTex, _NormalTex, _LayerTex, _LayerMask, _OpacityTex, _EmissionTex;
        half4 _Tint, _LayerTint, _EmissionTint;
        float4 _MainTransform, _LayerTransform, _MaskChannel, _AlphaChannel;
        half _LayerEnabled, _LayerUV2, _MainUV2, _ExtraUV2, _NormalEnabled, _AlphaEnabled, _AlphaSource, _Cutoff, _Smoothness;
        struct Input { float2 uv_MainTex; float2 uv2_LayerMask; };
        void surf(Input i, inout SurfaceOutputStandard o)
        {
            float2 uv = i.uv_MainTex;
            float2 u0 = lerp(uv, i.uv2_LayerMask, _MainUV2) * _MainTransform.xy + _MainTransform.zw;
            half4 b = tex2D(_MainTex, u0);
            #if !defined(RE4_NO_ALPHA_CLIP)
            // 只采样当前材质实际启用的纹理层。
            UNITY_BRANCH
            if (_AlphaEnabled != 0)
            {
                half opacity = b.a;
                UNITY_BRANCH
                if (_AlphaSource != 0)
                {
                    opacity = lerp(b.a, dot(tex2D(_OpacityTex, uv), _AlphaChannel), _AlphaSource);
                }
                clip(lerp(1, opacity - _Cutoff, _AlphaEnabled));
            }

            #endif
            o.Albedo = b.rgb * _Tint.rgb;
            UNITY_BRANCH
            if (_LayerEnabled != 0)
            {
                float2 u1 = lerp(uv, i.uv2_LayerMask, _ExtraUV2) * _LayerTransform.xy + _LayerTransform.zw;
                half layer = _LayerEnabled * dot(tex2D(_LayerMask, lerp(uv, i.uv2_LayerMask, _LayerUV2)), _MaskChannel);
                o.Albedo = lerp(o.Albedo, tex2D(_LayerTex, u1).rgb * _LayerTint.rgb, layer);
            }

            o.Normal = half3(0, 0, 1);
            o.Smoothness = _Smoothness;
            UNITY_BRANCH
            if (_NormalEnabled != 0)
            {
                half3 raw = tex2D(_NormalTex, u0).rgb;
                half2 n = (raw.rg * 2 - 1) * _NormalEnabled;
                o.Normal = half3(n, sqrt(saturate(1 - dot(n, n))));
                o.Smoothness = lerp(_Smoothness, saturate(1 - raw.b) * .75, _NormalEnabled);
            }

            o.Emission = 0;
            UNITY_BRANCH
            if (any(_EmissionTint.rgb != 0))
            {
                o.Emission = tex2D(_EmissionTex, uv).rgb * _EmissionTint.rgb;
            }
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
