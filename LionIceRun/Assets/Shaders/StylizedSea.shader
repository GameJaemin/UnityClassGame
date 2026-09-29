// 폴가이즈풍 바다 (URP).
//  - 방향이 다른 사인파 3개로 수면이 오르내림 (정점 이동, 월드 좌표 기준이라 바다 크기를 바꿔도 파도 크기는 그대로)
//  - 두 방향으로 흘러가는 노이즈 잔물결로 법선을 흔듦
//  - 얼음판 아래(바다 중심)는 얕은 청록, 멀어질수록 깊은 남색 / 파도 마루는 밝게, 골은 어둡게
//  - 비스듬히 볼수록 하늘이 비치고(프레넬), 햇빛 하이라이트와 반짝이 알갱이, 파도 마루의 물거품
//  - 얼음판·캐릭터 그림자를 받음 / 멀어질수록 배경색으로 녹아듦
Shader "LionIceRun/StylizedSea"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor ("Shallow (center)", Color) = (0.30, 0.80, 0.95, 1)
        _DeepColor ("Deep (far)", Color) = (0.05, 0.30, 0.62, 1)
        _HorizonColor ("Horizon (match camera background)", Color) = (0.62, 0.85, 0.97, 1)
        _FoamColor ("Foam", Color) = (1, 1, 1, 1)
        _ShallowRadius ("Shallow radius", Float) = 14
        _DeepRadius ("Deep radius", Float) = 60
        _FadeStart ("Distance fade start", Float) = 55
        _FadeEnd ("Distance fade end", Float) = 150

        [Header(Waves)]
        _WaveHeight ("Wave height", Float) = 0.35
        _WaveLength ("Wave length", Float) = 9
        _WaveSpeed ("Wave speed", Float) = 1.2

        [Header(Surface)]
        _RippleScale ("Ripple scale", Float) = 0.35
        _RippleStrength ("Ripple strength", Range(0, 1)) = 0.4
        _SpecularStrength ("Sun highlight", Float) = 1.4
        _SunSharpness ("Sun highlight sharpness", Float) = 160
        _SparkleThreshold ("Sparkle threshold", Range(0, 1)) = 0.9
        _FoamThreshold ("Foam threshold", Range(0, 1)) = 0.74
        _ShadowStrength ("Shadow strength", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _HorizonColor;
                half4 _FoamColor;
                float _ShallowRadius;
                float _DeepRadius;
                float _FadeStart;
                float _FadeEnd;
                float _WaveHeight;
                float _WaveLength;
                float _WaveSpeed;
                float _RippleScale;
                float _RippleStrength;
                float _SpecularStrength;
                float _SunSharpness;
                float _SparkleThreshold;
                float _FoamThreshold;
                float _ShadowStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float crest : TEXCOORD2;   // 파도 높이 0(골) ~ 1(마루)
                float fogFactor : TEXCOORD3;
            };

            // 파도 5개: 길이 비율이 서로 딱 떨어지지 않게 골라 같은 모양이 금방 되풀이되지 않게 한다
            static const float2 WaveDirs[5] = { float2(1.0, 0.31), float2(-0.43, 1.0), float2(0.71, -0.83), float2(-0.97, -0.24), float2(0.19, 0.94) };
            static const float WaveLengths[5] = { 1.0, 0.618, 0.433, 0.291, 0.173 };
            static const float WaveAmps[5] = { 1.0, 0.56, 0.34, 0.18, 0.09 };
            static const float WaveSpeeds[5] = { 1.0, 1.27, 1.61, 1.93, 2.37 };
            static const float WaveSum = 2.17;  // WaveAmps 합 (마루 높이 정규화용)

            // 방향이 다른 사인파의 합. 높이와 기울기(법선 계산용)를 같이 돌려준다
            float Waves(float2 p, out float2 slope)
            {
                float t = _Time.y * _WaveSpeed;
                float h = 0;
                slope = 0;
                [unroll] for (int i = 0; i < 5; i++)
                {
                    float2 d = normalize(WaveDirs[i]);
                    float k = 6.2831853 / (_WaveLength * WaveLengths[i]);
                    float phase = dot(d, p) * k + t * WaveSpeeds[i] + i * 1.7;
                    float a = _WaveHeight * WaveAmps[i];
                    h += sin(phase) * a;
                    slope += d * (cos(phase) * a * k);
                }
                return h;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            // 크기가 다른 노이즈 4겹. 겹마다 좌표를 비틀어(회전) 격자 모양 결이 드러나지 않게 한다. 결과 약 0~1
            float Fbm(float2 p)
            {
                const float2x2 turn = float2x2(0.80, -0.60, 0.60, 0.80);
                float value = 0;
                float amplitude = 0.5;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    value += amplitude * ValueNoise(p);
                    p = mul(turn, p) * 2.03 + float2(17.1, 9.3);
                    amplitude *= 0.5;
                }
                return value / 0.9375;
            }

            // 노이즈로 노이즈 좌표를 휘게 만든다 (도메인 워핑): 반복되는 규칙성이 사라진다
            float WarpedFbm(float2 p, float t)
            {
                float2 warp = float2(Fbm(p + t * float2(0.03, 0.02)), Fbm(p + float2(5.2, 1.3) - t * float2(0.02, 0.03)));
                return Fbm(p + warp * 2.2);
            }

            // 노이즈의 기울기 (잔물결 법선용)
            float2 NoiseSlope(float2 p)
            {
                const float e = 0.05;
                float n = Fbm(p);
                return float2(Fbm(p + float2(e, 0)) - n, Fbm(p + float2(0, e)) - n) / e;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float2 slope;
                float h = Waves(positionWS.xz, slope);
                positionWS.y += h;

                output.positionWS = positionWS;
                output.normalWS = normalize(float3(-slope.x, 1.0, -slope.y));
                output.crest = saturate(h / max(_WaveHeight * WaveSum, 0.001) * 0.5 + 0.5);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float3 positionWS = input.positionWS;

                // 두 방향으로 흘러가는 잔물결
                float2 p = positionWS.xz * _RippleScale;
                float2 p1 = p + t * float2(0.12, 0.05);
                float2 p2 = p * 1.7 - t * float2(0.07, 0.11);
                float2 ripple = (NoiseSlope(p1) + NoiseSlope(p2) * 0.6) * 0.6;
                float3 normalWS = normalize(input.normalWS + float3(-ripple.x, 0, -ripple.y) * _RippleStrength * 0.3);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(positionWS);

                // 바다 중심(얼음판 아래)은 얕은 물, 멀어질수록 깊은 물
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float fromCenter = length(positionWS.xz - center.xz);
                half3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(_ShallowRadius, _DeepRadius, fromCenter));
                // 파도 마루는 밝게, 골은 어둡게
                water *= lerp(0.82, 1.12, input.crest);

                // 비스듬히 볼수록 하늘이 비침
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), 4.0);
                water = lerp(water, _HorizonColor.rgb, fresnel * 0.6);

                // 햇빛과 그림자
                Light light = GetMainLight(TransformWorldToShadowCoord(positionWS));
                float shadow = lerp(1.0, light.shadowAttenuation, _ShadowStrength);
                float diffuse = saturate(dot(normalWS, light.direction)) * 0.35 + 0.65;
                water *= diffuse * shadow;

                float fromCamera = length(positionWS - _WorldSpaceCameraPos);
                // 멀리 있는 작은 무늬는 뭉쳐서 규칙적으로 보이므로 거품·반짝이는 가까운 곳에만
                float nearFade = 1.0 - smoothstep(25.0, 70.0, fromCamera);

                float3 halfDir = normalize(light.direction + viewWS);
                float highlight = saturate(dot(normalWS, halfDir));
                float specular = pow(highlight, max(_SunSharpness, 8.0)) * _SpecularStrength;
                // 반짝이 알갱이: 두 겹 노이즈가 동시에 높은 곳만, 해가 비치는 쪽에서 (드문드문 불규칙하게)
                float glint = ValueNoise(positionWS.xz * 2.3 + t * float2(0.9, 0.3)) * ValueNoise(mul(float2x2(0.8, -0.6, 0.6, 0.8), positionWS.xz) * 3.1 - t * float2(0.4, 0.7));
                float sparkle = smoothstep(_SparkleThreshold * 0.85, 1.0, glint * 1.15) * smoothstep(0.55, 1.0, highlight) * nearFade;
                half3 color = water + (specular + sparkle * 1.2) * light.color * shadow;

                // 물거품: 아주 큰 노이즈로 "거품이 생기는 구역"을 정하고, 그 안의 파도 마루에만 휘어진 노이즈로 불규칙하게
                float2 foamUV = positionWS.xz * 0.35;
                float patch = smoothstep(0.45, 0.75, Fbm(positionWS.xz * 0.035 + t * float2(0.004, -0.003)));
                float foamNoise = WarpedFbm(foamUV, t);
                float foam = smoothstep(_FoamThreshold, _FoamThreshold + 0.12, input.crest * 0.55 + foamNoise * 0.6) * patch * nearFade;
                color = lerp(color, _FoamColor.rgb * (0.85 + 0.15 * shadow), foam * 0.75);

                // 멀어질수록 배경색으로 녹아들어 지평선이 하늘과 이어지게
                color = lerp(color, _HorizonColor.rgb, smoothstep(_FadeStart, _FadeEnd, fromCamera));

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
