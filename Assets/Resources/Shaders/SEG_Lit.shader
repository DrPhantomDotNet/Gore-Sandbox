// Lit opaque shader for all procedural meshes. Vertex color = albedo tint, vertex alpha = smoothness.
Shader "SolarEclipse/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness Scale", Range(0,1)) = 1.0
        _Metallic ("Metallic", Range(0,1)) = 0.0
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex;
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _EmissionColor;
        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb * IN.color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = saturate(_Glossiness * IN.color.a);
            o.Emission = _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
