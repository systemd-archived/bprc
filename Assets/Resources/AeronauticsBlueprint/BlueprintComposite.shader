Shader "Hidden/BPRC/Aeronautics Blueprint/Composite"
{
    Properties
    {
        _MainTex ("Scene", 2D) = "white" {}
        _BlueprintMask ("Part Depth and Coverage", 2D) = "black" {}
        _Palette ("Diagram Palette", 2D) = "white" {}
        _Dither ("Ordered Dither", 2D) = "gray" {}
        _PaletteOffset ("Palette Row", Range(0,1)) = 0.25
        _LineColor ("Outline", Color) = (0.180392,0.188235,0.196078,1)
        _LineShadowColor ("Outline Shadow", Color) = (0.411765,0.411765,0.396078,1)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _BlueprintMask, _Palette, _Dither;
            float4 _MainTex_TexelSize, _BlueprintMask_TexelSize, _Palette_TexelSize, _Dither_TexelSize;
            float4 _LineColor, _LineShadowColor;
            float _PaletteOffset;

            float4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float2 maskUV = uv;
                #if UNITY_UV_STARTS_AT_TOP
                    if (_MainTex_TexelSize.y < 0) maskUV.y = 1 - maskUV.y;
                #endif
                float4 scene = tex2D(_MainTex, uv);
                float4 mask = tex2D(_BlueprintMask, maskUV);
                if (mask.a <= 0) return scene;
                float3 gammaColor = scene.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    gammaColor = LinearToGammaSpace(gammaColor);
                #endif
                float luminance = dot(gammaColor, float3(0.299, 0.587, 0.114)) * 1.7;
                luminance = saturate((luminance - 0.5 + 0.18) * 1.8 + 0.5);
                luminance = max(floor(luminance * 32.0) / 32.0 - 0.00001, 0);
                float columns = _Palette_TexelSize.z * 1.5 - 1;
                float level = luminance * columns;
                float2 pixel = maskUV * _BlueprintMask_TexelSize.zw;
                float threshold = tex2D(_Dither, pixel * _Dither_TexelSize.xy).r * 0.99 + 0.005;
                float ramp = (floor(level) + step(threshold, frac(level))) / columns;
                float3 diagram = tex2D(_Palette, float2(ramp, _PaletteOffset)).rgb;

                float2 dx = float2(_BlueprintMask_TexelSize.x, 0);
                float2 dy = float2(0, _BlueprintMask_TexelSize.y);
                float left = pow(abs(mask.r - tex2D(_BlueprintMask, maskUV - dx).r), 1.04);
                float right = pow(abs(mask.r - tex2D(_BlueprintMask, maskUV + dx).r), 1.04);
                float up = pow(abs(mask.r - tex2D(_BlueprintMask, maskUV - dy).r), 1.04);
                float down = pow(abs(mask.r - tex2D(_BlueprintMask, maskUV + dy).r), 1.04);
                float4 ink = _LineColor;
                if (left > right || up > down) ink = _LineShadowColor;
                float strength = min(saturate(left + right + up + down) * 20.0, 1.0) * ink.a;
                diagram = lerp(diagram, ink.rgb, strength);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    diagram = GammaToLinearSpace(diagram);
                #endif
                return float4(lerp(scene.rgb, diagram, saturate(mask.a)), scene.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
