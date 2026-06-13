Shader "Seeman/CRT"
{
    Properties
    {
        _Curvature("Curvature", Range(0, 0.6)) = 0.22
        _ScanlineIntensity("Scanline Intensity", Range(0, 1)) = 0.38
        _ScanlineCount("Scanline Count", Float) = 540
        _MaskIntensity("RGB Mask Intensity", Range(0, 1)) = 0.22
        _Aberration("Chromatic Aberration", Range(0, 0.01)) = 0.0023
        _NoiseAmount("Noise", Range(0, 1)) = 0.07
        _Static("Static", Range(0, 1)) = 0
        _RollSpeed("Roll Band Speed", Float) = 0.045
        _RollIntensity("Roll Band Intensity", Range(0, 0.3)) = 0.05
        _Vignette("Vignette", Range(0, 1)) = 0.5
        _PowerOn("Power On", Range(0, 1)) = 1
        _Brightness("Brightness", Float) = 1.18
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "SeemanCRT"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag

            float _Curvature;
            float _ScanlineIntensity;
            float _ScanlineCount;
            float _MaskIntensity;
            float _Aberration;
            float _NoiseAmount;
            float _Static;
            float _RollSpeed;
            float _RollIntensity;
            float _Vignette;
            float _PowerOn;
            float _Brightness;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float pw = saturate(_PowerOn);

                // power-on: image squeezed into a horizontal line (bounded math)
                float2 uv = input.texcoord;
                float squeeze = clamp(pw * pw, 0.06, 1.0);
                uv.y = clamp((uv.y - 0.5) / squeeze, -2.0, 2.0) + 0.5;

                // barrel distortion
                float2 cc = uv * 2.0 - 1.0;
                float r2 = min(dot(cc, cc), 16.0);
                cc *= 1.0 + _Curvature * r2 * 0.25;
                cc = clamp(cc, -8.0, 8.0);
                float2 cuv = cc * 0.5 + 0.5;

                // bezel: outside the curved screen is black
                if (cuv.x < 0.0 || cuv.x > 1.0 || cuv.y < 0.0 || cuv.y > 1.0)
                    return float4(0, 0, 0, 1);

                // soft rounded-corner mask
                float2 cornerDist = abs(cc) - float2(0.96, 0.96);
                float corner = length(max(cornerDist, 0.0));
                float cornerMask = 1.0 - smoothstep(0.0, 0.05, corner);

                // chromatic aberration (radial) — keep the max offset a few pixels,
                // otherwise UI text visibly doubles into RGB ghosts
                float2 ab = cc * _Aberration * 1.5;
                float3 col;
                col.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, cuv + ab).r;
                col.g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, cuv).g;
                col.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, cuv - ab).b;

                // static / white-noise mix (channel switching)
                float n = hash21(cuv * float2(640.0, 480.0) + floor(t * 60.0) * 7.31);
                col = lerp(col, float3(n, n, n), saturate(_Static));

                // film grain
                col += (n - 0.5) * _NoiseAmount;

                // scanlines
                float sl = sin(cuv.y * _ScanlineCount * 3.14159);
                col *= 1.0 - _ScanlineIntensity * sl * sl;

                // aperture grille (RGB triad mask)
                float px = cuv.x * _ScreenParams.x;
                float m = fmod(px, 3.0);
                float3 mask = 1.0 - _MaskIntensity.xxx;
                if (m < 1.0) mask.r = 1.0;
                else if (m < 2.0) mask.g = 1.0;
                else mask.b = 1.0;
                col *= mask;

                // slow rolling brightness band
                float band = frac(cuv.y * 0.6 + t * _RollSpeed);
                col += _RollIntensity * smoothstep(0.0, 0.18, band) * (1.0 - smoothstep(0.18, 0.42, band));

                // 60Hz-ish flicker
                col *= 1.0 + 0.014 * sin(t * 113.0);

                // vignette
                col *= 1.0 - _Vignette * r2 * 0.65;

                // power-on flash line
                float flashAmt = saturate(1.0 - pw);
                float dist = abs(input.texcoord.y - 0.5);
                float flash = exp(-dist * (30.0 + 300.0 * pw)) * flashAmt;
                col += flash * 2.0;

                col *= _Brightness * lerp(0.25, 1.0, pw) * cornerMask;
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
