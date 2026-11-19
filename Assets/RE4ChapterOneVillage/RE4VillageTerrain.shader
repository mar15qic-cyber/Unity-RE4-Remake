Shader "RE4/Chapter One Village Terrain"
{
    Properties
    {
        _TerrainAlbedo ("原版地表基色数组", 2DArray) = "" {}
        _TerrainNormal ("原版地表法线数组", 2DArray) = "" {}
        _Splat ("原版地表索引图", 2D) = "black" {}
        _Tile ("平铺", Float) = 5
        _Wetness ("潮湿程度", Range(0,1)) = .15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.5
        #include "UnityCG.cginc"
        UNITY_DECLARE_TEX2DARRAY(_TerrainAlbedo);
        UNITY_DECLARE_TEX2DARRAY(_TerrainNormal);
        sampler2D _Splat;
        float4 _Splat_TexelSize;
        float _Tile, _Wetness;
        struct Input { float2 uv_Splat; float3 worldPos; };
        float LayerAt(float2 pixel)
        {
            float val = tex2Dlod(_Splat, float4((pixel + .5) * _Splat_TexelSize.xy, 0, 0)).r;
            return floor((val * 255.0 + .5) / 16.0);
        }
        void surf(Input i, inout SurfaceOutputStandard o)
        {
            float2 p = i.uv_Splat * _Splat_TexelSize.zw - .5;
            float2 grid = floor(p);
            float2 weight = frac(p);
            float4 layer = float4(LayerAt(grid), LayerAt(grid + float2(1,0)), LayerAt(grid + float2(0,1)), LayerAt(grid + 1));
            float2 uv = float2(-i.worldPos.x, -i.worldPos.z) * (_Tile * .2);
            half3 albedo = UNITY_SAMPLE_TEX2DARRAY(_TerrainAlbedo, float3(uv,layer.x)).rgb;
            half3 nrm = UNITY_SAMPLE_TEX2DARRAY(_TerrainNormal, float3(uv,layer.x)).rgb;
            if (any(layer != layer.x))
            {
                half4 weights = half4((1-weight.x)*(1-weight.y), weight.x*(1-weight.y), (1-weight.x)*weight.y, weight.x*weight.y);
                albedo = albedo * weights.x
                    + UNITY_SAMPLE_TEX2DARRAY(_TerrainAlbedo,float3(uv,layer.y)).rgb * weights.y
                    + UNITY_SAMPLE_TEX2DARRAY(_TerrainAlbedo,float3(uv,layer.z)).rgb * weights.z
                    + UNITY_SAMPLE_TEX2DARRAY(_TerrainAlbedo,float3(uv,layer.w)).rgb * weights.w;
                nrm = nrm * weights.x
                    + UNITY_SAMPLE_TEX2DARRAY(_TerrainNormal,float3(uv,layer.y)).rgb * weights.y
                    + UNITY_SAMPLE_TEX2DARRAY(_TerrainNormal,float3(uv,layer.z)).rgb * weights.z
                    + UNITY_SAMPLE_TEX2DARRAY(_TerrainNormal,float3(uv,layer.w)).rgb * weights.w;
            }
            o.Albedo = albedo * lerp(1, .75, _Wetness);
            half2 tangentNormal = (nrm.rg * 2 - 1) * .55;
            o.Normal = half3(tangentNormal, sqrt(saturate(1 - dot(tangentNormal, tangentNormal))));
            o.Smoothness = lerp((1 - nrm.b) * .55, .5, _Wetness);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
