// Expanding Tron ring for ball impacts (2026-08-27 design-refresh). Unlit
// transparent, no lighting or SRP dependencies, so it renders identically
// under URP on Quest. _Progress 0..1 drives the ring outward while it fades;
// the quad itself is scaled by the effect component.
Shader "CourtClash/ImpactRing"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 1.0, 1.0, 1)
        _Progress ("Progress", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One // additive-ish glow
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Progress;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float dist = length(i.uv - 0.5) * 2.0; // 0 center, 1 at quad edge
                float radius = _Progress;
                float ringWidth = lerp(0.25, 0.06, _Progress); // tightens as it expands
                float ring = 1.0 - saturate(abs(dist - radius) / ringWidth);
                ring *= ring; // sharpen
                float fade = 1.0 - _Progress;
                fixed4 col = _Color;
                col.a *= ring * fade;
                return col;
            }
            ENDCG
        }
    }
}
