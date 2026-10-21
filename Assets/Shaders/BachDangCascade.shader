Shader "BachDang/Cascade"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; float4 color:COLOR; };
            V Vert(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.fog=ComputeFogFactor(o.p.z);o.color=i.color;return o; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p) { float2 k=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(k),Hash(k+float2(1,0)),f.x),lerp(Hash(k+float2(0,1)),Hash(k+1),f.x),f.y); }
            half4 Frag(V i):SV_Target
            {
                float t=i.uv.y-_Time.y*1.7;
                half foam=pow(Noise(float2(i.uv.x*6,t*4)),2.4);
                half streak=Noise(float2(i.uv.x*22,t*1.5));
                half3 c=lerp(half3(.12,.29,.27),half3(.76,.88,.83),foam*.85+streak*.15);
                half edge=smoothstep(0,.09,i.uv.x)*smoothstep(0,.09,1-i.uv.x);
                return half4(MixFog(c,i.fog),edge*.91);
            }
            ENDHLSL
        }
    }
}
