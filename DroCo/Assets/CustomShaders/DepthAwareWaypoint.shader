Shader "Custom/DepthAwareWaypoint"
{
    Properties
    {
        _Color ("Color", Color) = (1,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 worldPos : TEXCOORD0;
                float4 projPos : TEXCOORD1;
            };

            float4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.projPos = ComputeScreenPos(o.pos);
                return o;
            }

            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            half4 frag (v2f i) : SV_Target
            {
                float2 uv = i.projPos.xy / i.projPos.w;
                float sceneDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
                float myDepth = i.projPos.z / i.projPos.w;

                // fallback: pokud není validní depth (např. = 1), ignoruj test
                if (sceneDepth < 0.99)
                {
                    if (myDepth > sceneDepth + 0.001)
                        discard;
                }

                return _Color;
            }

            ENDHLSL
        }
    }
}
