Shader "Custom/FluidShader"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _VelocityTex ("Velocity Texture",2D) = ""
    }

    CGINCLUDE
        
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;

        sampler2D _VelocityTex;

        float2 _ForceOrigin;
        float _ForceExponent;

        half4 frag_advect(v2f_img i) : SV_TARGET
        {
            // Time parameters
            float time = _Time.y;
            float deltaTime = unity_DeltaTime.x;

            // Aspect ratio coefficients
            float2 aspect = float2(_MainTex_TexelSize.y * _MainTex_TexelSize.z, 1);
            float2 aspect_uv = float2(_MainTex_TexelSize.x * _MainTex_TexelSize.w, 1);

            // Color advection with velocity field
            float2 delta = tex2D(_VelocityTex, i.uv).xy * aspect_uv * deltaTime;
            float3 color = tex2D(_MainTex, i.uv - delta).xyz;

            // Dye---injectoin Color
            float3 dye = saturate(sin(time * float3(2.72, 5.12, 4.98)) + 0.5);

            // Blend dye with the color from the buffer 
            float2 pos = (i.uv - 0.5) * aspect;
            float amp = exp(-_ForceExponent * distance(_ForceOrigin, pos));
            color = lerp(color, dye, saturate(amp * 100));

            return half4(color,1);
        }

        half4 frag_render(v2f_img i) : SV_TARGET
        {
            half3 color = tex2D(_MainTex, i.uv).rgb;

            return half4(GammaToLinearSpace(color), 1);
        }

    ENDCG

    SubShader
    {
        ZTest Always Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM

            #pragma vertex vert_img
            #pragma fragment frag_advect

            ENDCG
        }

        Pass
        {
            CGPROGRAM

            #pragma vertex vert_img
            #pragma fragment frag_render

            ENDCG
        }
    }
}
