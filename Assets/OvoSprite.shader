Shader "Ovo/Sprite"
{
    // _Invert = 0 : normal unlit sprite (the level picture)
    // _Invert = 1 : "difference" look - whatever it covers turns into its opposite colour (ball, rings, exit)
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Invert ("Invert", Float) = 0
        [HideInInspector] _Src ("Src", Float) = 1
        [HideInInspector] _Dst ("Dst", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend [_Src] [_Dst]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
            float _Invert;
        CBUFFER_END
        struct A { float4 pos : POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
        struct V { float4 pos : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
        V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.col = i.col; o.uv = i.uv; return o; }
        half4 frag(V i) : SV_Target
        {
            half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.col;
            if (_Invert > 0.5) return half4(t.a, t.a, t.a, t.a);
            return half4(t.rgb * t.a, t.a);
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
