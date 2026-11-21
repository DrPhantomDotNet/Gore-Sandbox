// Full-screen filters (Gore Sandbox 2, Player/ScreenFx.cs): 1 = noir (black and white, blood stays red), 2 = retro (low-res, few colours, scanlines), 3 = colour-blind correction (_CB: 1 protan, 2 deutan, 3 tritan).
Shader "SolarEclipse/ScreenFx"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "white" {}
        _Mode ("Mode", Float) = 1
        _CB ("Colour blind type", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Mode, _CB;
            // Daltonize: simulate the colour-blind view (LMS space), then push the lost difference into channels that remain visible.
            float3 Daltonize(float3 c)
            {
                float3 lms = float3(dot(c, float3(17.8824, 43.5161, 4.11935)), dot(c, float3(3.45565, 27.1554, 3.86714)), dot(c, float3(0.0299566, 0.184309, 1.46709)));
                float3 sim = lms;
                if (_CB < 1.5) sim = float3(2.02344 * lms.y - 2.52581 * lms.z, lms.y, lms.z);
                else if (_CB < 2.5) sim = float3(lms.x, 0.494207 * lms.x + 1.24827 * lms.z, lms.z);
                else sim = float3(lms.x, lms.y, -0.395913 * lms.x + 0.801109 * lms.y);
                float3 rgb = float3(dot(sim, float3(0.0809444479, -0.130504409, 0.116721066)), dot(sim, float3(-0.0102485335, 0.0540193266, -0.113614708)), dot(sim, float3(-0.000365296938, -0.00412161469, 0.693511405)));
                float3 err = c - rgb;
                return saturate(c + float3(0.0, err.r * 0.7 + err.g, err.r * 0.7 + err.b));
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                if (_Mode > 2.5) { fixed4 cb = tex2D(_MainTex, uv); return fixed4(Daltonize(cb.rgb), 1); }
                if (_Mode > 1.5)
                {
                    float2 px = float2(400.0, 225.0);
                    uv = (floor(uv * px) + 0.5) / px;
                    fixed4 r = tex2D(_MainTex, uv);
                    r.rgb = floor(r.rgb * 6.0 + 0.5) / 6.0;
                    r.rgb *= 0.82 + 0.18 * step(0.5, frac(i.uv.y * 225.0));
                    return fixed4(r.rgb, 1);
                }
                fixed4 c = tex2D(_MainTex, uv);
                float l = dot(c.rgb, float3(0.299, 0.587, 0.114));
                l = saturate((l - 0.5) * 1.5 + 0.47);
                float2 d = i.uv - 0.5;
                l *= 1.0 - dot(d, d) * 1.3;
                float red = saturate((c.r - max(c.g, c.b)) * 3.0);
                fixed3 o = lerp(fixed3(l, l, l), fixed3(saturate(c.r * 1.25), 0.03, 0.03), red * 0.9);
                return fixed4(o, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
