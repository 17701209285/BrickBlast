Shader "BrickBlast/Particles/Rect Clip"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _UseClipRect ("Use Clip Rect", Float) = 0
        [HideInInspector] _ClipRectLocal ("Clip Rect Local", Vector) = (-99999,-99999,99999,99999)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _UseClipRect;
            float4 _ClipRectLocal;
            float4x4 _ClipWorldToLocal;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_UseClipRect > 0.5)
                {
                    float4 clipLocalPos = mul(_ClipWorldToLocal, float4(i.worldPos, 1.0));
                    if (clipLocalPos.x < _ClipRectLocal.x ||
                        clipLocalPos.y < _ClipRectLocal.y ||
                        clipLocalPos.x > _ClipRectLocal.z ||
                        clipLocalPos.y > _ClipRectLocal.w)
                    {
                        clip(-1);
                    }
                }

                fixed4 color = tex2D(_MainTex, i.uv) * i.color;
                clip(color.a - 0.001);
                return color;
            }
            ENDCG
        }
    }
}
