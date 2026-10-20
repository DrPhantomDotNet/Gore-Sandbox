// Procedural gradient sky with sun/moon disc, glow and stars. Used as RenderSettings.skybox.
Shader "SolarEclipse/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.25,0.45,0.85,1)
        _Horizon ("Horizon", Color) = (0.75,0.82,0.9,1)
        _Bottom ("Bottom", Color) = (0.2,0.2,0.22,1)
        _SunDir ("Sun Direction", Vector) = (0.3,0.4,0.8,0)
        _SunColor ("Sun Color", Color) = (1,0.92,0.75,1)
        _SunSize ("Sun Size", Float) = 0.015
        _Stars ("Stars", Float) = 0
        _Eclipse ("Eclipse", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Top, _Horizon, _Bottom, _SunColor;
            float4 _SunDir;
            float _SunSize, _Stars, _Eclipse;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float3 col = h > 0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(h), 0.55)) : lerp(_Horizon.rgb, _Bottom.rgb, pow(saturate(-h * 3.0), 0.5));
                float3 sd = normalize(_SunDir.xyz);
                float s = dot(d, sd);
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.7, s);
                float glow = pow(saturate(s), 48.0) * 0.55 + pow(saturate(s), 6.0) * 0.12;
                // Eclipse: dark disc over the sun, bright corona ring around it.
                float corona = smoothstep(1.0 - _SunSize * 2.2, 1.0 - _SunSize, s) * (1.0 - disc);
                col += _SunColor.rgb * (disc * (1.0 - _Eclipse) * 2.0 + glow * (1.0 - _Eclipse * 0.6) + corona * _Eclipse * 2.5);
                col = lerp(col, float3(0.0, 0.0, 0.0), disc * _Eclipse);
                float3 p = floor(d * 380.0);
                float n = frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float tw = 0.6 + 0.4 * sin(_Time.y * 3.0 + n * 50.0);
                col += _Stars * step(0.9975, n) * saturate(h * 5.0) * tw;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
