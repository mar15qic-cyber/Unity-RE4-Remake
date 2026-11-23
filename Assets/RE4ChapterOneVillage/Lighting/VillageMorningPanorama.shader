Shader "RE4/Village Morning Panorama"
{
    Properties
    {
        _Tint ("天空色调", Color) = (0.5, 0.5, 0.5, 0.5)
        _Exposure ("线性曝光", Range(0, 16)) = 1
        _CloudContrast ("云层明暗层次", Range(0, 1)) = 0
        _Rotation ("朝向", Range(0, 360)) = 0
        [NoScaleOffset] _MainTex ("全天球 HDR 天空", 2D) = "grey" {}
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _MainTex_HDR;
            half4 _Tint;
            half _Exposure;
            half _CloudContrast;
            float _Rotation;

            struct Interpolator
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Interpolator Vertex(appdata_base input)
            {
                float sine, cosine;
                sincos(radians(_Rotation), sine, cosine);
                float2 rotated = mul(float2x2(cosine, -sine, sine, cosine), input.vertex.xz);
                float3 position = float3(rotated.x, input.vertex.y, rotated.y);
                Interpolator output;
                output.position = UnityObjectToClipPos(position);
                output.direction = input.vertex.xyz;
                return output;
            }

            half4 Fragment(Interpolator input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float longitude = atan2(direction.z, direction.x);
                float latitude = acos(clamp(direction.y, -1.0, 1.0));
                float2 uv = float2(0.5 - longitude / (2.0 * UNITY_PI), 1.0 - latitude / UNITY_PI);

                // 横向跨越全景接缝时，按连续的经度变化选择纹理层级。
                float2 horizontal = ddx(uv);
                float2 vertical = ddy(uv);
                horizontal.x -= floor(horizontal.x + 0.5);
                vertical.x -= floor(vertical.x + 0.5);
                half4 encodedSky = tex2Dgrad(_MainTex, uv, horizontal, vertical);
                half3 sky = DecodeHDR(encodedSky, _MainTex_HDR);
                half3 radiance = sky * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
                // 按线性亮度压低云底，保留透光处的晨光和原全景纹理。
                half luminance = dot(radiance, half3(0.2126h, 0.7152h, 0.0722h));
                half cloudLight = smoothstep(0.055h, 0.48h, luminance);
                half gain = 1.0h + _CloudContrast * lerp(-0.54h, 0.12h, cloudLight);
                return half4(radiance * gain, 1.0h);
            }
            ENDCG
        }
    }
}
