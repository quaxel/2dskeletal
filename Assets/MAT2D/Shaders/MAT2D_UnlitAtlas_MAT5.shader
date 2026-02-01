Shader "MAT2D/UnlitAtlas_MAT5"
{
    Properties
    {
        _BaseMap("Atlas", 2D) = "white" {}
        _BaseColor("Color", Color) = (1,1,1,1)
        _Mat0("MAT0", 2D) = "white" {}
        _Mat1("MAT1", 2D) = "white" {}
        _MatTexSize("MAT Tex Size", Vector) = (6,64,0,0)
        _SampleFPS("Sample FPS", Float) = 30
        _AnimTime("Anim Time", Float) = 0
        _AnimSpeed("Anim Speed", Float) = 1
        _AnimId("Anim Id", Float) = 0
        _FlipX("FlipX", Float) = 0
        _AnimClipCount("Clip Count", Float) = 5
        _AnimClipStart("Clip Start", Vector) = (0,0,0,0)
        _AnimClipCountFrames("Clip Frames", Vector) = (1,1,1,1)
        _AnimClipStart4("Clip Start4", Vector) = (0,0,0,0)
        _AnimClipCountFrames4("Clip Frames4", Vector) = (1,1,1,1)
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
                float4 _AnimClipStart;
                float4 _AnimClipCountFrames;
                float4 _AnimClipStart4;
                float4 _AnimClipCountFrames4;
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

            float2 GetClipData(float animId, out float clipFrames)
            {
                // Supports up to 5 clips (limited by Vector4 storage)
                // For more clips, consider using a texture-based lookup or structured buffer
                float4 start0 = _AnimClipStart;
                float4 count0 = _AnimClipCountFrames;
                float4 start1 = _AnimClipStart4;
                float4 count1 = _AnimClipCountFrames4;

                if (animId < 0.5) { clipFrames = count0.x; return float2(start0.x, count0.x); }
                if (animId < 1.5) { clipFrames = count0.y; return float2(start0.y, count0.y); }
                if (animId < 2.5) { clipFrames = count0.z; return float2(start0.z, count0.z); }
                if (animId < 3.5) { clipFrames = count0.w; return float2(start0.w, count0.w); }
                if (animId < 4.5) { clipFrames = count1.x; return float2(start1.x, count1.x); }
                
                // Fallback for out-of-range animId (use first clip)
                clipFrames = count0.x;
                return float2(start0.x, count0.x);
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

                float clipFrames;
                float2 clipData = GetClipData(animId, clipFrames);
                float clipStart = clipData.x;
                clipFrames = max(1.0, clipFrames);

                float localTime = animTime * animSpeed;
                float frame = localTime * _SampleFPS;
                float localFrame0 = floor(frame);
                float frac = frame - localFrame0;

                // Wrap frame within clip range
                localFrame0 = localFrame0 - clipFrames * floor(localFrame0 / clipFrames);
                float localFrame1 = localFrame0 + 1.0;
                
                // Fixed: Clamp next frame to avoid wrapping to frame 0 at the end
                // This prevents interpolation between last and first frame
                if (localFrame1 >= clipFrames)
                {
                    localFrame1 = clipFrames - 1.0; // Use last frame instead of wrapping
                    frac = 1.0; // Full weight on last frame
                }

                float frame0 = clipStart + localFrame0;
                float frame1 = clipStart + localFrame1;

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
                float4 _AnimClipStart;
                float4 _AnimClipCountFrames;
                float4 _AnimClipStart4;
                float4 _AnimClipCountFrames4;
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

            float2 GetClipData(float animId, out float clipFrames)
            {
                // Supports up to 5 clips (limited by Vector4 storage)
                // For more clips, consider using a texture-based lookup or structured buffer
                float4 start0 = _AnimClipStart;
                float4 count0 = _AnimClipCountFrames;
                float4 start1 = _AnimClipStart4;
                float4 count1 = _AnimClipCountFrames4;

                if (animId < 0.5) { clipFrames = count0.x; return float2(start0.x, count0.x); }
                if (animId < 1.5) { clipFrames = count0.y; return float2(start0.y, count0.y); }
                if (animId < 2.5) { clipFrames = count0.z; return float2(start0.z, count0.z); }
                if (animId < 3.5) { clipFrames = count0.w; return float2(start0.w, count0.w); }
                if (animId < 4.5) { clipFrames = count1.x; return float2(start1.x, count1.x); }
                
                // Fallback for out-of-range animId (use first clip)
                clipFrames = count0.x;
                return float2(start0.x, count0.x);
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

                float clipFrames;
                float2 clipData = GetClipData(animId, clipFrames);
                float clipStart = clipData.x;
                clipFrames = max(1.0, clipFrames);

                float localTime = animTime * animSpeed;
                float frame = localTime * _SampleFPS;
                float localFrame0 = floor(frame);
                float frac = frame - localFrame0;

                // Wrap frame within clip range
                localFrame0 = localFrame0 - clipFrames * floor(localFrame0 / clipFrames);
                float localFrame1 = localFrame0 + 1.0;
                
                // Fixed: Clamp next frame to avoid wrapping to frame 0 at the end
                // This prevents interpolation between last and first frame
                if (localFrame1 >= clipFrames)
                {
                    localFrame1 = clipFrames - 1.0; // Use last frame instead of wrapping
                    frac = 1.0; // Full weight on last frame
                }

                float frame0 = clipStart + localFrame0;
                float frame1 = clipStart + localFrame1;

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
