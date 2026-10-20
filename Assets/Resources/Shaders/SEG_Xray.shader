// See-through overlay used by the X-ray cheat to show organs and bones inside bodies.
Shader "SolarEclipse/Xray"
{
    Properties { _Color ("Color", Color) = (1,0.3,0.3,0.6) }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        ZTest Always
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 viewN = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                float rim = 1.0 - abs(viewN.z);
                o.color = fixed4(v.color.rgb * _Color.rgb, _Color.a * (0.25 + rim));
                return o;
            }
            fixed4 frag (v2f i) : SV_Target { return i.color; }
            ENDCG
        }
    }
}
