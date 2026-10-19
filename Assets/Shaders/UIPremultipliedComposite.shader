// 이펙트 카메라가 찍은 렌더 텍스처를 UI 맨 위에 겹치는 셰이더 (UIEffectLayer가 쓴다).
//
// 투명(0,0,0,0)으로 지운 텍스처 위에 파티클을 그리면, 색은 이미 알파가 곱해진 상태
// (premultiplied)로 남는다. 보통 UI 셰이더(SrcAlpha, OneMinusSrcAlpha)로 그리면 알파가 한 번 더
// 곱해져 가산 파티클의 빛이 거의 사라진다. 그래서 One, OneMinusSrcAlpha로 섞는다.
Shader "RecycleLife/UI Premultiplied Composite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                // 이미 곱해진 색이라 틴트의 알파는 색과 알파에 똑같이 건다.
                return c * i.color.a * fixed4(i.color.rgb, 1);
            }
            ENDCG
        }
    }
}
