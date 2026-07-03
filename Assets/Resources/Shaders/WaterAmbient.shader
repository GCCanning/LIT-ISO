Shader "IsoCore/WaterAmbient"
{
    // Water tiles only (playtest 2026-07-02 #8): SpriteAmbient's day/night tint +
    //   - a subtle scrolling 2-tone shimmer (quantized in space AND time so it stays
    //     pixel-crisp at native scale), and
    //   - an animated shoreline foam edge on the diamond sides that touch land
    //     (per-renderer flags via MaterialPropertyBlock — no material instantiation).
    // Built-in pipeline. Geometry assumptions match IsoWorldRenderer.FlatTile:
    // 32x32 tile, top-face diamond centre (15.5, 16), half-extents (16, 11).
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _FoamNE ("Foam NE", Float) = 0
        [PerRendererData] _FoamNW ("Foam NW", Float) = 0
        [PerRendererData] _FoamSW ("Foam SW", Float) = 0
        [PerRendererData] _FoamSE ("Foam SE", Float) = 0
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
            #pragma fragment WaterFragAmbient
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 _AmbientColor; // global (SpriteAmbient)
            float _NightGrade;    // global (SpriteAmbient)
            float _FoamNE, _FoamNW, _FoamSW, _FoamSE; // per-renderer land-adjacency flags

            fixed4 WaterFragAmbient(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;

                // --- pixel-space coords on the 32x32 tile ---
                float2 px = floor(IN.texcoord * 32.0);
                float nx = (px.x - 15.5) / 16.0;   // -1..1 across the diamond
                float ny = (px.y - 16.0) / 11.0;
                float rim = abs(nx) + abs(ny);      // 1.0 at the diamond edge

                // --- 2-tone shimmer: diagonal bands crawling across the surface,
                //     quantized time (6 steps/s) + pixel-quantized space = crisp ---
                float t = floor(_Time.y * 6.0) / 6.0;
                float band = step(0.5, frac((px.x + px.y * 2.0) / 12.0 + t * 0.5));
                c.rgb *= lerp(0.96, 1.05, band);

                // --- shoreline foam: a light band hugging diamond edges that touch
                //     land, breathing slowly so the waterline reads alive ---
                float foamFlag =
                    (nx >= 0 && ny >= 0) ? _FoamNE :
                    (nx <  0 && ny >= 0) ? _FoamNW :
                    (nx <  0 && ny <  0) ? _FoamSW : _FoamSE;
                float breathe = 0.04 * sin(floor(_Time.y * 4.0) / 4.0 * 3.14159);
                float edge = smoothstep(0.78 + breathe, 0.98 + breathe, rim);
                float foam = foamFlag * edge * c.a;
                c.rgb = lerp(c.rgb, fixed3(0.86, 0.93, 0.96), foam * 0.8);

                // --- shared ambient + night grade (water is never a warm source) ---
                c.rgb *= _AmbientColor.rgb;
                if (_NightGrade > 0.001)
                {
                    float lum = dot(c.rgb, fixed3(0.299, 0.587, 0.114));
                    c.rgb = lerp(c.rgb, lum * fixed3(0.80, 0.88, 1.16), 0.40 * _NightGrade);
                }

                c.rgb *= c.a; // premultiply (matches Sprites/Default blend)
                return c;
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
