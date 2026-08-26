// Procedural "dark glossy shell + glowing circuit lines" look (Echo Arena /
// sci-fi robot aesthetic), driven entirely by UV math — no textures needed,
// since this project has no working AI texture-generation account and hand-
// painting textures is out of scope. Two pattern modes share one shader so
// the ball, court panels, and robot trim can all reuse it:
//   0 = sphere rings/spokes (equator ring + N evenly-spaced radial spokes)
//   1 = panel border (glow near the UV edge of a flat panel)
// Verified against the actual installed URP/Core package sources
// (com.unity.render-pipelines.universal / .core) for the exact include
// paths and function signatures used below.
Shader "CourtClash/GlowPanel"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.02, 0.02, 0.035, 1)
        _Smoothness("Smoothness", Range(0,1)) = 0.75
        _GlowColor("Glow Color", Color) = (0.1, 0.6, 1.0, 1)
        _GlowIntensity("Glow Intensity", Float) = 4.0

        _PatternMode("Pattern Mode (0=sphere, 1=panel border)", Float) = 0
        _RingV("Ring V Position", Range(0,1)) = 0.5
        _RingWidth("Ring Width", Range(0,0.2)) = 0.025
        _SpokeCount("Spoke Count", Float) = 4
        _SpokeWidth("Spoke Width", Range(0,0.2)) = 0.03
        _BorderWidth("Border Width (panel mode)", Range(0,0.3)) = 0.06
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float2 uv          : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Smoothness;
                float4 _GlowColor;
                float _GlowIntensity;
                float _PatternMode;
                float _RingV;
                float _RingWidth;
                float _SpokeCount;
                float _SpokeWidth;
                float _BorderWidth;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            // Anti-aliased thin glowing band centered at `center` in a
            // wrapped 0-1 coordinate.
            float LineMask(float coord, float center, float width)
            {
                float d = abs(coord - center);
                d = min(d, 1.0 - d); // wrap around (for U-axis spokes)
                return 1.0 - smoothstep(width * 0.5, width * 0.5 + 0.015, d);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float glow = 0;

                if (_PatternMode < 0.5)
                {
                    // Sphere mode: one equator ring (V) + evenly-spaced radial spokes (U).
                    glow = max(glow, LineMask(i.uv.y, _RingV, _RingWidth));
                    float spokeU = frac(i.uv.x * _SpokeCount);
                    glow = max(glow, LineMask(spokeU, 0.0, _SpokeWidth));
                }
                else
                {
                    // Panel mode: glow near the border of the UV rect.
                    float edgeDist = min(min(i.uv.x, 1.0 - i.uv.x), min(i.uv.y, 1.0 - i.uv.y));
                    glow = 1.0 - smoothstep(_BorderWidth * 0.5, _BorderWidth, edgeDist);
                }

                Light mainLight = GetMainLight();
                float3 N = normalize(i.normalWS);
                float NdotL = saturate(dot(N, mainLight.direction));

                // Simple ambient fudge (no SH dependency) so unlit-facing
                // surfaces don't go fully black.
                float3 ambient = _BaseColor.rgb * 0.25;
                float3 diffuse = _BaseColor.rgb * NdotL * mainLight.color.rgb + ambient;

                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 halfVec = normalize(mainLight.direction + viewDir);
                float specPower = lerp(8.0, 200.0, _Smoothness);
                float spec = pow(saturate(dot(N, halfVec)), specPower);
                float3 specColor = mainLight.color.rgb * spec * _Smoothness;

                float3 emission = _GlowColor.rgb * _GlowIntensity * glow;
                float3 color = diffuse + specColor + emission;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
