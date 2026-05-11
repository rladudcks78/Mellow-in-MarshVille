Shader "UI/IrisWipe"
{
    Properties
    {
        _Color ("Main Color", Color) = (0,0,0,1)
        _Radius ("Hole Radius", Range(0, 1.5)) = 1.0
        _CenterX ("Center X", Range(0, 1)) = 0.5
        _CenterY ("Center Y", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            fixed4 _Color;
            float _Radius;
            float _CenterX;
            float _CenterY;

            // 화면 비율 보정 (원 찌그러짐 방지)
            float _ScreenRatio; 

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 중심점 설정 (0.5, 0.5는 화면 정중앙)
                float2 center = float2(_CenterX, _CenterY);
                
                // 현재 픽셀과 중심점 사이의 거리 계산
                // 화면 비율(Aspect Ratio)을 고려하여 계산 (원이 타원이 되지 않게)
                float2 pos = i.uv;
                pos.x = (pos.x - 0.5) * (_ScreenParams.x / _ScreenParams.y) + 0.5;
                center.x = (center.x - 0.5) * (_ScreenParams.x / _ScreenParams.y) + 0.5;

                float dist = distance(pos, center);

                // 거리가 반지름보다 작으면 투명(구멍), 크면 검은색 출력
                if (dist < _Radius)
                {
                    return fixed4(0, 0, 0, 0); // 투명
                }
                else
                {
                    return _Color; // 검은색
                }
            }
            ENDCG
        }
    }
}