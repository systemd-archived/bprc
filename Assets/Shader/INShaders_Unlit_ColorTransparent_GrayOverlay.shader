Shader "INShaders/Unlit_ColorTransparent_GrayOverlay"
{
    Properties
    {
        _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        _Blend ("Blend", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Cull Off

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4x4 unity_ObjectToWorld;
            float4x4 unity_MatrixVP;

            float4 _MainTex_ST;

            float4 _Color;
            float  _Blend;

            Texture2D _MainTex;
            SamplerState sampler_MainTex;

            struct appdata
            {
                float3 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv  : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float4 worldPos =
                    mul(unity_ObjectToWorld,
                        float4(v.vertex, 1.0));

                o.pos =
                    mul(unity_MatrixVP, worldPos);

                o.uv =
                    v.uv * _MainTex_ST.xy +
                    _MainTex_ST.zw;

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 tex =
                    _MainTex.Sample(
                        sampler_MainTex,
                        i.uv);

                float gray =
                    dot(tex.rgb,
                        float3(
                            0.2989,
                            0.5870,
                            0.1140));

                float darkness =
                    1.0 - gray;

                float grayFactor =
                    gray * gray * 2.0;

                float3 invColor =
                    1.0 - _Color.rgb;

                float3 overlay =
                    darkness *
                    (1.0 - (darkness * 2.0) * invColor);

                overlay +=
                    grayFactor * _Color.rgb;

                float3 finalRgb =
                    lerp(
                        tex.rgb,
                        overlay,
                        _Blend);

                float finalAlpha =
                    tex.a * _Color.a;

                return float4(
                    finalRgb,
                    finalAlpha);
            }

            ENDHLSL
        }
    }
}