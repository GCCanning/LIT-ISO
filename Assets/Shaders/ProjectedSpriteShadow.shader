Shader "IsoCore/ProjectedSpriteShadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Shadow Color", Color) = (0.05,0.06,0.10,0.4)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment ShadowFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #include "UnitySprites.cginc"

            fixed4 ShadowFrag(v2f input) : SV_Target
            {
                fixed alpha = SampleSpriteTexture(input.texcoord).a * input.color.a;
                return fixed4(input.color.rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
