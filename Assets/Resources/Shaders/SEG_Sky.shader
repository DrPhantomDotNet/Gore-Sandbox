// Procedural gradient sky with sun/moon disc, glow, stars and (GS2) drifting clouds. Used as RenderSettings.skybox.
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
        _CloudCover ("Cloud Cover", Float) = 0.35
        _CloudColor ("Cloud Color", Color) = (1,1,1,1)
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
            float _SunSize, _Stars, _Eclipse, _CloudCover;
            fixed4 _CloudColor;
            float hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash2(i), hash2(i + float2(1, 0)), f.x), lerp(hash2(i + float2(0, 1)), hash2(i + float2(1, 1)), f.x), f.y);
            }
            float fbm(float2 p) { float v = 0.0, a = 0.5; for (int k = 0; k < 4; k++) { v += a * vnoise(p); p *= 2.03; a *= 0.5; } return v; }
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
                // Clouds: a flat layer projected onto the sky dome, drifting slowly; fades towards the horizon.
                if (h > 0.0 && _CloudCover > 0.001)
                {
                    float2 uv = d.xz / (h + 0.12) * 0.9 + _Time.y * float2(0.012, 0.004);
                    float c = fbm(uv);
                    float cov = smoothstep(1.0 - _CloudCover, 1.0 - _CloudCover + 0.25, c) * saturate(h * 6.0);
                    float3 cc = _CloudColor.rgb * (0.75 + 0.35 * fbm(uv * 2.0 + 3.1)) + _SunColor.rgb * glow * 0.6;
                    col = lerp(col, cc, cov * 0.85);
                }
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
