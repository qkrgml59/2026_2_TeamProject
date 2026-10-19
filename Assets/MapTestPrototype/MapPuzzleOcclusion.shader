Shader "Hwanjo/Map Puzzle Occlusion"
{
    Properties
    {
        _WallColor("Wall Color",Color)=(.8,.8,.8,1)
        _VisibleRect("Visible World Rect",Vector)=(94,10.5,110.5,27.75)
        _EntranceRect("Entrance World Rect",Vector)=(89,24,94,27.25)
        _ExteriorInset("Exterior Empty Space",Vector)=(0,0,0,0)
        _Feather("Entrance Feather",Float)=.5
        _EntranceTop("Top Entrance",Float)=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 world:TEXCOORD0;};
            CBUFFER_START(UnityPerMaterial)
                float4 _WallColor,_VisibleRect,_EntranceRect,_ExteriorInset;
                float _Feather,_EntranceTop;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;float3 world=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(world);output.world=world.xy;return output;
            }
            bool Inside(float2 samplePosition,float4 rect){return all(samplePosition>=rect.xy)&&all(samplePosition<=rect.zw);}
            half4 Frag(Varyings input):SV_Target
            {
                if(all(_ExteriorInset.zw>_ExteriorInset.xy)&&Inside(input.world,_ExteriorInset))return half4(_WallColor.rgb,1);
                if(Inside(input.world,_VisibleRect))discard;
                float alpha=1;
                if(Inside(input.world,_EntranceRect))
                    alpha=_EntranceTop>.5?saturate((input.world.y-(_EntranceRect.w-_Feather))/_Feather)
                        :1-saturate((input.world.x-_EntranceRect.x)/_Feather);
                clip(alpha-.001);
                return half4(_WallColor.rgb,alpha);
            }
            ENDHLSL
        }
    }
}
