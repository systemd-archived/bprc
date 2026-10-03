Shader "Hidden/BPRC/Aeronautics Blueprint/Part Mask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Part Texture", 2D) = "white" {}
        _Color ("Part Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Tint", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("External Alpha Enabled", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One Zero, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float4 _MainTex_ST;
            struct mask_v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };
            mask_v2f vert(appdata_t input)
            {
                v2f sprite = SpriteVert(input);
                mask_v2f output;
                output.vertex = sprite.vertex;
                output.color = sprite.color;
                output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
                return output;
            }
            float4 frag(mask_v2f input) : SV_Target
            {
                float alpha = SampleSpriteTexture(input.texcoord).a * input.color.a;
                clip(alpha - 0.003);
                float depth = input.vertex.z;
                #if defined(UNITY_REVERSED_Z)
                    depth = 1.0 - depth;
                #endif
                return float4(depth, 0, 0, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
