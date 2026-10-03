Shader "BPRC/Online Lobby/Backdrop Blur"
{
	Properties
	{
		_MainTex ("Backdrop", 2D) = "black" {}
		_TintColor ("Tint", Color) = (0.82, 0.86, 0.90, 1)
		_BlurSize ("Blur Size", Range(0, 3)) = 2.5
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
		Blend SrcAlpha OneMinusSrcAlpha
		Cull Off
		Lighting Off
		ZWrite Off
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				fixed4 color : COLOR;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				fixed4 color : COLOR;
			};

			sampler2D _MainTex;
			float4 _MainTex_TexelSize;
			fixed4 _TintColor;
			float _BlurSize;

			v2f vert(appdata input)
			{
				v2f output;
				output.vertex = UnityObjectToClipPos(input.vertex);
				output.uv = input.uv;
				output.color = input.color;
				return output;
			}

			fixed4 frag(v2f input) : SV_Target
			{
				float2 step = _MainTex_TexelSize.xy * _BlurSize;
				fixed4 color = tex2D(_MainTex, input.uv) * 0.28;
				color += tex2D(_MainTex, input.uv + float2(step.x, 0)) * 0.12;
				color += tex2D(_MainTex, input.uv - float2(step.x, 0)) * 0.12;
				color += tex2D(_MainTex, input.uv + float2(0, step.y)) * 0.12;
				color += tex2D(_MainTex, input.uv - float2(0, step.y)) * 0.12;
				color += tex2D(_MainTex, input.uv + step) * 0.06;
				color += tex2D(_MainTex, input.uv - step) * 0.06;
				color.rgb = lerp(dot(color.rgb, float3(0.299, 0.587, 0.114)).xxx, color.rgb, 0.35);
                color.rgb *= _TintColor.rgb;
				color.a = _TintColor.a;
				return color * input.color;
			}
			ENDCG
		}
	}
}
