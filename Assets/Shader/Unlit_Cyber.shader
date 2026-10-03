// Unused shader
Shader "Unlit/Cyber" {
	Properties {
		_MainTex ("Texture", 2D) = "white" {}
		_Red ("洋红-红色-橙色", Range(-1, 0.5)) = 0
		_Orange ("红色-橙色-黄色", Range(-0.5, 0.5)) = 0
		_Yellow ("橙色-黄色-绿色", Range(-0.5, 1)) = 0
		_Green ("黄色-绿色-靛色", Range(-1, 1)) = 0
		_Cyan ("绿色-靛色-蓝色", Range(-1, 1)) = 0
		_Blue ("靛色-蓝色-紫色", Range(-1, 0.5)) = 0
		_Purple ("蓝色-紫色-洋红", Range(-0.5, 0.5)) = 0
		_Magenta ("紫色-洋红-红色", Range(-0.5, 1)) = 0
		_Hue ("Hue", Float) = 0
		_Saturation ("Saturation", Float) = 1
		_Value ("Value", Float) = 1
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _MainTex_ST;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy);
			}

			ENDHLSL
		}
	}
}