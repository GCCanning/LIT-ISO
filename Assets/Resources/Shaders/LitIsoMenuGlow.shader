// Additive UI glow with organic noise flicker + gentle heat-shimmer UV wobble.
// Used by the main-menu scene lighting (campfire / cabin windows / portal) and
// ambient particles. Built-in pipeline, uGUI-compatible (vertex color respected).
Shader "LitIso/UI/MenuGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlickerSpeed ("Flicker Speed", Float) = 1.6
        _FlickerAmount ("Flicker Amount", Range(0,1)) = 0.22
        _Distort ("Heat Distortion", Range(0,0.08)) = 0.018
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One   // additive: glows brighten the scene, never darken

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _FlickerSpeed;
            float _FlickerAmount;
            float _Distort;

            float hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // cheap value noise, 2 octaves
            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _FlickerSpeed;

                // heat shimmer: small noise-driven UV wobble, stronger mid-glow
                float2 wob = float2(
                    vnoise(i.texcoord * 5.0 + float2(t, 0.0)),
                    vnoise(i.texcoord * 5.0 + float2(0.0, t * 1.3))) - 0.5;
                float2 uv = i.texcoord + wob * _Distort;

                fixed4 tex = tex2D(_MainTex, uv);

                // organic brightness flicker (noise, not sine — fire isn't periodic)
                float flick = 1.0 - _FlickerAmount
                            + _FlickerAmount * vnoise(float2(t * 1.7, 7.31));

                fixed4 col = tex * i.color;
                col.rgb *= flick;
                col.a *= flick;
                return col;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
