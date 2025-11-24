Shader "Unlit/ARScreen"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        ZTest Always
        Cull Off
        ZWrite Off
       
        Pass
        {
            Name "Unlit"
               
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
           
            #include "UnityCG.cginc"
 
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
 
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
           
            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            float4 _MainTex_ST;
            int _Orientation;
 
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
               
                // Flip X
                o.uv = float2(1.0 - v.uv.x, v.uv.y);
               
                if (_Orientation == 1) {
                    // Portrait
                    o.uv = float2(1.0 - o.uv.y, o.uv.x);
                }
                else if (_Orientation == 3) {
                    // Landscape left
                    o.uv = float2(1.0 - o.uv.x, 1.0 - o.uv.y);
                }
               
                o.uv = TRANSFORM_TEX(o.uv, _MainTex);
                return o;
            }
 
            fixed4 frag (v2f i) : SV_Target
            {
                return  _MainTex.Sample(sampler_MainTex, i.uv);
            }
            ENDHLSL
        }
    }
}
