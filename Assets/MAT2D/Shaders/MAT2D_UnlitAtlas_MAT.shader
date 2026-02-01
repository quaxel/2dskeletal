Shader "MAT2D/UnlitAtlas_MAT"
{
    Properties
    {
        _BaseMap("Atlas", 2D) = "white" {}
        _BaseColor("Color", Color) = (1,1,1,1)
        _Mat0("MAT0", 2D) = "white" {}
        _Mat1("MAT1", 2D) = "white" {}
        _MatTexSize("MAT Tex Size", Vector) = (6,64,0,0)
        _SampleFPS("Sample FPS", Float) = 30
        _FrameCount("Frame Count", Float) = 64
        _AnimTime("Anim Time", Float) = 0
        _AnimSpeed("Anim Speed", Float) = 1
        _AnimId("Anim Id", Float) = 0
        _FlipX("FlipX", Float) = 0
        _DebugUV("Debug UV (0/1)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _MatTexSize;
                float _SampleFPS;
                float _FrameCount;
                float _DebugUV;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(AnimProps)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimTime)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimId)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlipX)
            UNITY_INSTANCING_BUFFER_END(AnimProps)

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Mat0);
            SAMPLER(sampler_Mat0);
            TEXTURE2D(_Mat1);
            SAMPLER(sampler_Mat1);

            float2 MatUV(float partIndex, float frameIndex)
            {
                return float2(partIndex + 0.5, frameIndex + 0.5) / _MatTexSize.xy;
            }

            float4 SampleMat0(float partIndex, float frameIndex)
            {
                return SAMPLE_TEXTURE2D_LOD(_Mat0, sampler_Mat0, MatUV(partIndex, frameIndex), 0);
            }

            float4 SampleMat1(float partIndex, float frameIndex)
            {
                return SAMPLE_TEXTURE2D_LOD(_Mat1, sampler_Mat1, MatUV(partIndex, frameIndex), 0);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float2 pos = input.positionOS.xy;
                float partIndex = floor(input.uv2.x + 0.5);

                float animTime = UNITY_ACCESS_INSTANCED_PROP(AnimProps, _AnimTime);
                float animSpeed = UNITY_ACCESS_INSTANCED_PROP(AnimProps, _AnimSpeed);
                float animId = UNITY_ACCESS_INSTANCED_PROP(AnimProps, _AnimId);
                float flipX = UNITY_ACCESS_INSTANCED_PROP(AnimProps, _FlipX);

                float localTime = animTime * animSpeed;
                float frame = localTime * _SampleFPS;
                float frame0 = floor(frame);
                float frac = frame - frame0;

                frame0 = frame0 - _FrameCount * floor(frame0 / _FrameCount);
                float frame1 = frame0 + 1.0;
                if (frame1 >= _FrameCount)
                {
                    frame1 = 0.0;
                }

                float4 mat0a = SampleMat0(partIndex, frame0);
                float4 mat0b = SampleMat0(partIndex, frame1);
                float4 mat1a = SampleMat1(partIndex, frame0);
                float4 mat1b = SampleMat1(partIndex, frame1);

                float4 mat0 = lerp(mat0a, mat0b, frac);
                float4 mat1 = lerp(mat1a, mat1b, frac);

                float2 t = mat0.xy;
                float s = mat0.z;
                float c = mat0.w;
                float2 sc = mat1.xy;

                pos *= sc;
                float2 r;
                r.x = pos.x * c - pos.y * s;
                r.y = pos.x * s + pos.y * c;
                pos = r + t;

                float flipSign = (flipX > 0.5) ? -1.0 : 1.0;
                pos.x *= flipSign;

                float3 positionWS = TransformObjectToWorld(float3(pos, input.positionOS.z));
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                if (_DebugUV > 0.5)
                {
                    return half4(input.uv.xy, 0.0h, 1.0h);
                }

                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                return col;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _MatTexSize;
                float _SampleFPS;
                float _FrameCount;
                float _DebugUV;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(AnimProps)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimTime)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimId)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlipX)
            UNITY_INSTANCING_BUFFER_END(AnimProps)

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Mat0);
            SAMPLER(sampler_Mat0);
            TEXTURE2D(_Mat1);
            SAMPLER(sampler_Mat1);

            float2 MatUV(float partIndex, float frameIndex)
            {
                return float2(partIndex + 0.5, frameIndex + 0.5) / _MatTexSize.xy;
            }

            float4 SampleMat0(float partIndex, float frameIndex)
            {
                return SAMPLE_TEXTURE2D_LOD(_Mat0, sampler_Mat0, MatUV(partIndex, frameIndex), 0);
            }

            float4 SampleMat1(float partIndex, float frameIndex)
            {
                return SAMPLE_TEXTURE2D_LOD(_Mat1, sampler_Mat1, MatUV(partIndex, frameIndex), 0);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float2 pos = input.positionOS.xy;
                float partIndex = floor(input.uv2.x + 0.5);

                float animTime = UNITY_ACCESS_INSTANCED_PROP(AnimProps, _AnimTime);
                float animSpeed = UNITY_ACCESS_INSTANCED_PROP(AnimProps, _AnimSpeed);
                float animId = UNITY_ACCESS_INSTANCED_PROP(AnimProps, _AnimId);
                float flipX = UNITY_ACCESS_INSTANCED_PROP(AnimProps, _FlipX);

                float localTime = animTime * animSpeed;
                float frame = localTime * _SampleFPS;
                float frame0 = floor(frame);
                float frac = frame - frame0;

                frame0 = frame0 - _FrameCount * floor(frame0 / _FrameCount);
                float frame1 = frame0 + 1.0;
                if (frame1 >= _FrameCount)
                {
                    frame1 = 0.0;
                }

                float4 mat0a = SampleMat0(partIndex, frame0);
                float4 mat0b = SampleMat0(partIndex, frame1);
                float4 mat1a = SampleMat1(partIndex, frame0);
                float4 mat1b = SampleMat1(partIndex, frame1);

                float4 mat0 = lerp(mat0a, mat0b, frac);
                float4 mat1 = lerp(mat1a, mat1b, frac);

                float2 t = mat0.xy;
                float s = mat0.z;
                float c = mat0.w;
                float2 sc = mat1.xy;

                pos *= sc;
                float2 r;
                r.x = pos.x * c - pos.y * s;
                r.y = pos.x * s + pos.y * c;
                pos = r + t;

                float flipSign = (flipX > 0.5) ? -1.0 : 1.0;
                pos.x *= flipSign;

                float3 positionWS = TransformObjectToWorld(float3(pos, input.positionOS.z));
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                if (_DebugUV > 0.5)
                {
                    return half4(input.uv.xy, 0.0h, 1.0h);
                }

                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                return col;
            }
            ENDHLSL
        }
    }
}
