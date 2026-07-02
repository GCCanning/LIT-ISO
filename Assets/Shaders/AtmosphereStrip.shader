Shader "LIT-ISO/AtmosphereStrip"
{
    Properties
    {
        [PerRendererData] _MainTex ("Strip Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FrameOffset ("Frame Offset", Float) = 0
        _Tiling ("Frame Tiling", Vector) = (1,1,0,0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _FrameOffset;
            float4 _Tiling;

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 sampleUv = input.uv + float2(_FrameOffset, 0);
                if (_Tiling.x > 1.001 || _Tiling.y > 1.001)
                {
                    float2 localUv = float2(input.uv.x * 48.0, input.uv.y);
                    localUv = frac(localUv * _Tiling.xy);
                    sampleUv = float2(localUv.x / 48.0 + _FrameOffset, localUv.y);
                }
                return tex2D(_MainTex, sampleUv) * input.color;
            }
            ENDCG
        }
    }
}
