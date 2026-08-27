// Flat-color overlay for the game menu: draws over everything (ZTest Always,
// overlay-range queue set per material — dim dome 3900, panel backing 4000)
// so no scene geometry or text can bleed into the menu. Cull Off because the
// dim dome is viewed from inside. Plain unlit, so it works under URP, with
// the stereo macros single-pass instanced rendering needs on Quest.
Shader "CourtClash/MenuOverlay"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 0.75)
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Lighting Off
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
