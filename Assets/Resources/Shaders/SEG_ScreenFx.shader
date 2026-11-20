// Full-screen fun filters (Gore Sandbox 2, Player/ScreenFx.cs): 1 = noir (black and white, blood stays red), 2 = retro (low-res, few colours, scanlines).
Shader "SolarEclipse/ScreenFx"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "white" {}
        _Mode ("Mode", Float) = 1
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
            float _Mode;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
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
