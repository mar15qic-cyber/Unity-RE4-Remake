Shader "RE4/Native Particle Alpha"
{
    Properties
    {
        _MainTex ("粒子图集", 2D) = "white" {}
        _Opacity ("透明度倍率", Range(0, 4)) = 1
        _TextureColorWeight ("图集色彩权重", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Opacity;
            float _TextureColorWeight;

            struct ParticleInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct ParticleOutput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            ParticleOutput vert(ParticleInput input)
            {
                ParticleOutput output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            fixed4 frag(ParticleOutput input) : SV_Target
            {
                fixed4 textureColor = tex2D(_MainTex, input.uv);
                fixed3 tint = input.color.rgb * lerp(fixed3(1, 1, 1), textureColor.rgb, _TextureColorWeight);
                return fixed4(tint, saturate(textureColor.a * input.color.a * _Opacity));
            }
            ENDCG
        }
    }
}
