// Lit alpha-blended decal (blood, bullet holes, wounds). Drawn slightly toward the camera to avoid z-fighting.
Shader "SolarEclipse/Decal"
{
    Properties
    {
        _MainTex ("Decal", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.55
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest+50" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
        LOD 200
        ZWrite Off
        Offset -2, -2
        CGPROGRAM
        #pragma surface surf Standard decal:blend
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color * IN.color;
            o.Albedo = c.rgb;
            o.Smoothness = _Glossiness * c.a;
            o.Metallic = 0;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack Off
}
