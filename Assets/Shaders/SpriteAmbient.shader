Shader "IsoCore/SpriteAmbient"
{
    // Like Sprites/Default, but multiplies the final colour by a GLOBAL "_AmbientColor"
    // so one Shader.SetGlobalColor call tints the whole world (ground, props, player) for
    // the day/night cycle — no per-renderer cost, batching preserved.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment SpriteFragAmbient
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 _AmbientColor; // global, set via Shader.SetGlobalColor
            float _NightGrade;    // global 0..1 (AmbientLightController): night colour grade
            float _WarmExempt;    // per-material: 1 on SpriteAmbient.WarmMaterial (light sources)

            fixed4 SpriteFragAmbient(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;

                // Night grade (playtest 2026-07-02 #8): the world desaturates and
                // blue-shifts as night deepens, EXCEPT warm light sources (campfire,
                // lantern flames on the Warm material), whose ambient is lifted toward
                // firelight instead — so they glow by contrast against the cool night.
                fixed3 amb = _AmbientColor.rgb;
                if (_WarmExempt > 0.5)
                    amb = lerp(amb, fixed3(1.0, 0.92, 0.78), _NightGrade * 0.85);
                c.rgb *= amb;

                float g = _NightGrade * (1.0 - saturate(_WarmExempt));
                if (g > 0.001)
                {
                    float lum = dot(c.rgb, fixed3(0.299, 0.587, 0.114));
                    c.rgb = lerp(c.rgb, lum * fixed3(0.80, 0.88, 1.16), 0.40 * g);
                }

                c.rgb *= c.a;               // premultiply (matches Sprites/Default blend)
                return c;
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
