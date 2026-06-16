Shader "Custom/XorFluidRaymarching"
{
    Properties
    {
        [Header(Fluid Settings)]
        _StepCount ("Raymarch Steps", Range(10, 100)) = 30
        _Speed ("Animation Speed", Float) = 1.0
        _Brightness ("Brightness", Float) = 0.02

        [Header(Color)]
        [HDR] _Color1 ("Color 1 (Inner)", Color) = (0.2, 0.5, 1.0, 1.0)
        [HDR] _Color2 ("Color 2 (Mid)", Color) = (1.0, 0.3, 0.6, 1.0)
        [HDR] _Color3 ("Color 3 (Outer)", Color) = (0.1, 1.0, 0.8, 1.0)
        _ColorShift ("Color Shift Speed", Float) = 0.7

        [Header(Interactive Ripple)]
        _RippleCenterUV ("Ripple Center UV", Vector) = (0.5, 0.5, 0.0, 0.0)
        // X: Time/Radius, Y: Strength, Z: Frequency, W: Ring Width
        _RippleParams ("Ripple Params", Vector) = (0.0, 0.0, 30.0, 0.05) 
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Cull Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _StepCount;
                float _Speed;
                float _Brightness;
                float4 _Color1;
                float4 _Color2;
                float4 _Color3;
                float _ColorShift;
                float4 _RippleCenterUV;
                float4 _RippleParams;
            CBUFFER_END

            // 計算漣漪：返回一個向外擴散的環形扭曲值
            // dist: 該點到漣漪中心的距離
            float rippleAtDistance(float dist)
            {
                float rippleTime = _RippleParams.x;     // 當前擴散半徑
                float rippleStrength = _RippleParams.y;  // 強度
                float rippleFreq = _RippleParams.z;      // 波紋密度
                float rippleWidth = _RippleParams.w;     // 環的寬度

                // ★ 核心：環只在 dist ≈ rippleTime 的位置活躍
                // smoothstep 讓邊緣更柔和，視覺更自然
                float ringDist = abs(dist - rippleTime);
                float ringMask = smoothstep(rippleWidth, 0.0, ringDist);
                
                // 只在環已經擴散到的區域有效（dist < rippleTime 的區域不受影響）
                // 這讓漣漪看起來是從中心向外擴散，已經通過的區域恢復平靜
                float frontMask = smoothstep(0.0, rippleWidth * 0.5, rippleTime - dist + rippleWidth);
                
                return sin(dist * rippleFreq) * rippleStrength * ringMask * frontMask;
            }

            // 三色漸變調色板
            float3 palette(float t)
            {
                float3 c;
                if (t < 0.5)
                    c = lerp(_Color1.rgb, _Color2.rgb, t * 2.0);
                else
                    c = lerp(_Color2.rgb, _Color3.rgb, (t - 0.5) * 2.0);
                return c;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                
                // --- 表面漣漪 UV 扭曲（向外擴散的環） ---
                float2 dir = uv - _RippleCenterUV.xy;
                float dist = length(dir);
                float surfaceWave = rippleAtDistance(dist);
                if (dist > 0.001)
                {
                    uv += (dir / dist) * surfaceWave;
                }
                // ------------------------------------------

                // 映射到 [-1, 1] 坐標系供 Raymarching 使用
                uv = uv * 2.0 - 1.0;
                float t = _Time.y * _Speed;
                float3 rayDir = normalize(float3(uv, 1.0)); 
                
                float3 color = float3(0.0, 0.0, 0.0);
                float z = 0.5;
                float d = 0.5;

                // 預計算漣漪中心在 [-1,1] 空間的位置
                float2 rippleCenterNorm = _RippleCenterUV.xy * 2.0 - 1.0;

                int steps = (int)_StepCount;
                for (int iter = 1; iter <= steps; iter++)
                {
                    float fi = (float)iter;
                    
                    // 用三色調色板
                    float paletteT = frac(fi * _ColorShift * 0.1 + t * 0.05);
                    float3 col = palette(paletteT) / (d * fi + 1.0);
                    color += col;

                    // 光線步進位置
                    float3 p = z * rayDir;
                    
                    // 極坐標變換
                    float angle = atan2(p.y, p.x) * 2.0;
                    float radius = length(p.xy) - 6.0;
                    p = float3(angle, p.z / 3.0, radius);

                    // ★ 漣漪注入到 Raymarching 空間 — 基於每步的位置計算
                    // 用當前步的 xy 投影位置算出到漣漪中心的距離
                    float3 pWorld = z * rayDir;
                    float stepDist = length(pWorld.xy - rippleCenterNorm);
                    // 用同樣的環形擴散邏輯，讓體積也是從中心向外擴散
                    float volRipple = rippleAtDistance(stepDist * 0.5);
                    p += volRipple * float3(1.0, 1.0, 0.5) * 5.0;

                    // 多八度 sin 湍流 — XorDev 核心流體扭曲
                    float fd = 1.0;
                    for (int j = 1; j < 9; j++)
                    {
                        p += sin(p.yzx * fd - t + 0.2 * fi) / fd;
                        fd += 1.0;
                    }

                    // 距離場估算
                    float3 cosP = 0.1 * cos(p * 3.0) - 0.1;
                    d = 0.2 * length(float4(cosP, p.z));
                    d = max(d, 0.01);
                    z += d;
                }

                // 色調映射
                color = color * color * _Brightness;
                color = tanh(color);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}