Shader "Biformis/Boss Blade"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BladeTint ("Blade Colour", Color) = (1,1,1,1)
        _BladeEndpoints ("Blade Segment", Vector) = (0,0,0,0)
        _BladeAtlasScale ("Blade Atlas Scale", Vector) = (1,1,0,0)
        _BladeWidth ("Blade Half Width", Float) = 0.11
        _BladeOn ("Colour Blade", Float) = 0
        _HitEffectAmount ("Hit Flash", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _BladeTint;
                float4 _BladeEndpoints;
                float4 _BladeAtlasScale;
                float _BladeWidth, _BladeOn, _HitEffectAmount;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 colour = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                float2 segment = _BladeEndpoints.zw - _BladeEndpoints.xy;
                float2 bladeCoordinate = input.uv * _BladeAtlasScale.xy;
                float t = saturate(dot(bladeCoordinate - _BladeEndpoints.xy, segment) / max(dot(segment, segment), 0.0001));
                float distance = length(bladeCoordinate - (_BladeEndpoints.xy + segment * t));
                float mask = (1 - smoothstep(_BladeWidth, _BladeWidth + 0.015, distance)) * _BladeOn;
                // Restrict colour to the pale metal inside the annotated blade, leaving its black outline intact.
                float lightness = max(colour.r, max(colour.g, colour.b));
                mask *= smoothstep(0.42, 0.72, lightness);
                colour.rgb = lerp(colour.rgb, _BladeTint.rgb * lightness, mask);
                colour.rgb = lerp(colour.rgb, half3(1,1,1), saturate(_HitEffectAmount));
                return colour;
            }
            ENDHLSL
        }
    }
}
