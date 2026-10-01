Shader "BachDang/Dynamic Sky"
{
    Properties
    {
        [NoScaleOffset] _DayTex("Sunless day panorama", 2D) = "white" {}
        [NoScaleOffset] _NightTex("Moonless night panorama", 2D) = "black" {}
        _DayBlend("Daylight", Range(0, 1)) = 1
        _TwilightWeight("Twilight", Range(0, 1)) = 0
        _DayExposure("Day exposure", Range(0, 3)) = 1.05
        _NightExposure("Night exposure", Range(0, 3)) = 0.55
        _NightTint("Night tint", Color) = (0.48, 0.60, 0.85, 1)
        _TwilightTint("Twilight tint", Color) = (1, 0.43, 0.20, 1)
        _HorizonColor("Horizon haze", Color) = (0.64, 0.72, 0.73, 1)
        _Rotation("Cloud rotation", Range(0, 360)) = 0
        _SunDirection("Sun direction in world space", Vector) = (-0.67, 0.63, 0.39, 0)
        _MoonDirection("Moon direction in world space", Vector) = (0.67, -0.63, -0.39, 0)
        [HDR] _SunColor("Sun color", Color) = (1, 0.93, 0.82, 1)
        [HDR] _MoonColor("Moon color", Color) = (0.58, 0.72, 1, 1)
        _SunRadius("Sun angular radius (degrees)", Range(0.1, 2)) = 0.45
        _MoonRadius("Moon angular radius (degrees)", Range(0.1, 2)) = 0.55
        _SunDiscIntensity("Sun disc brightness", Range(0, 20)) = 8
        _MoonDiscIntensity("Moon disc brightness", Range(0, 5)) = 1.8
        _SunVisibility("Sun visibility", Range(0, 1)) = 1
        _MoonVisibility("Moon visibility", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SkyVertex
            #pragma fragment SkyFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_DayTex); SAMPLER(sampler_DayTex);
            TEXTURE2D(_NightTex); SAMPLER(sampler_NightTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _NightTint, _TwilightTint, _HorizonColor, _SunColor, _MoonColor;
                float4 _SunDirection, _MoonDirection;
                float _DayBlend, _TwilightWeight, _DayExposure, _NightExposure, _Rotation;
                float _SunRadius, _MoonRadius, _SunDiscIntensity, _MoonDiscIntensity;
                float _SunVisibility, _MoonVisibility;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings SkyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.directionWS = input.positionOS;
                return output;
            }

            float DiscMask(float3 direction, float3 bodyDirection, float radiusDegrees)
            {
                float cosine = dot(direction, bodyDirection);
                float edge = cos(radians(radiusDegrees));
                float softness = max(fwidth(cosine), 0.000003);
                return smoothstep(edge - softness, edge + softness, cosine);
            }

            half4 SkyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 direction = normalize(input.directionWS);
                float2 uv = float2(0.5 - atan2(direction.z, direction.x) / TWO_PI,
                                   1.0 - acos(clamp(direction.y, -1.0, 1.0)) / PI);
                uv.x = frac(uv.x + _Rotation / 360.0);
                float3 day = SAMPLE_TEXTURE2D(_DayTex, sampler_DayTex, uv).rgb * _DayExposure;
                float3 night = SAMPLE_TEXTURE2D(_NightTex, sampler_NightTex, uv).rgb * _NightTint.rgb * _NightExposure;
                float horizon = exp(-abs(direction.y) * 5.0);
                day *= lerp(float3(1, 1, 1), _TwilightTint.rgb, _TwilightWeight * (0.25 + 0.50 * horizon));
                float3 sky = lerp(night, day, _DayBlend);
                sky += _TwilightTint.rgb * _TwilightWeight * horizon * 0.10;
                sky = lerp(sky, _HorizonColor.rgb, horizon * 0.22);
                sky = lerp(_HorizonColor.rgb * 0.65, sky, smoothstep(-0.20, 0.01, direction.y));

                float3 sunDirection = normalize(_SunDirection.xyz);
                float3 moonDirection = normalize(_MoonDirection.xyz);
                float aboveHorizon = smoothstep(-0.02, 0.015, direction.y);
                float sunDisc = DiscMask(direction, sunDirection, _SunRadius);
                float sunHalo = pow(saturate(dot(direction, sunDirection)), 160.0) * 0.18;
                sky += _SunColor.rgb * (sunDisc * _SunDiscIntensity + sunHalo) * _SunVisibility * aboveHorizon;

                float moonDisc = DiscMask(direction, moonDirection, _MoonRadius);
                float3 moonRight = normalize(cross(float3(0, 1, 0.001), moonDirection));
                float3 moonUp = cross(moonDirection, moonRight);
                float2 moonUV = float2(dot(direction, moonRight), dot(direction, moonUp)) / max(sin(radians(_MoonRadius)), 0.001);
                float moonDetail = 0.82 + 0.10 * sin(moonUV.x * 13.0 + moonUV.y * 8.0) * sin(moonUV.y * 17.0 - moonUV.x * 5.0);
                float moonHalo = pow(saturate(dot(direction, moonDirection)), 320.0) * 0.035;
                sky += _MoonColor.rgb * (moonDisc * moonDetail * _MoonDiscIntensity + moonHalo) * _MoonVisibility * aboveHorizon;
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
