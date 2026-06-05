Shader "Sprites/DiffuseBumped"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0
        [Header(Wind)]
        _WindStrength ("Wind Strength", Float) = 0.1
        _WindSpeed ("Wind Speed", Float) = 1.0
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
        Lighting On
        ZWrite Off
        Blend One OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert nofog nolightmap nodynlightmap keepalpha noinstancing
        #pragma multi_compile_local _ PIXELSNAP_ON
        #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _RendererColor;
        float2 _Flip;
        sampler2D _BumpMap;
        float _BumpScale;
        float _WindStrength;
        float _WindSpeed;
        sampler2D _AlphaTex;
        float _EnableExternalAlpha;

        struct Input
        {
            float2 uv_MainTex;
            fixed4 color;
        };

        void vert (inout appdata_full v, out Input o)
        {
            v.vertex.xy *= _Flip.xy;

            // Simple wind sway: displacement increases with vertex Y (top of sprite sways more)
            // Using world position X to offset the sine wave so not all sprites sway in perfect sync
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            float windOffset = sin(_Time.y * _WindSpeed + worldPos.x * 2.0) * _WindStrength;
            
            // Only apply sway to vertices that are "high up" (v.texcoord.y > 0.5)
            // For sprites, texcoord.y is usually 0 at bottom and 1 at top.
            v.vertex.x += windOffset * v.texcoord.y;

            #if defined(PIXELSNAP_ON)
            v.vertex = UnityPixelSnap (v.vertex);
            #endif

            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.color = v.color * _Color * _RendererColor;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * IN.color;
            o.Albedo = c.rgb * c.a;
            o.Alpha = c.a;
            
            fixed3 n = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            n.xy *= _BumpScale;
            o.Normal = normalize(n);
        }
        ENDCG
    }

    Fallback "Sprites/Default"
}
