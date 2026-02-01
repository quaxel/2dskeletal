using UnityEngine;

namespace Mat2D
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class Mat2DMatPackedDebugAnimator : MonoBehaviour
    {
        [Header("Material")]
        public Material material;

        [Header("Anim Config")]
        public Mat2DAnimConfig config;

        [Header("Clip Settings")]
        public int clipCount = 5;
        public int[] clipFrameCounts = new int[5] { 20, 20, 20, 20, 20 };
        public float sampleFPS = 30f;

        [Header("Motion (in pixels/degrees)")]
        public float pixelsPerUnit = 100f;
        public Vector2 baseTranslateAmplitudePixels = new Vector2(12f, 6f);
        public float baseRotateAmplitudeDegrees = 15f;
        public float baseScaleAmplitude = 0.05f;
        public float partPhaseStep = 0.35f;

        [Header("Playback")]
        public bool driveMaterialTime = false;
        public float animSpeed = 1f;
        public float timeOffset = 0f;
        public bool rebuildEveryFrame = false;

        Texture2D _mat0;
        Texture2D _mat1;
        int _cachedClipCount;
        float _cachedSampleFPS;
        float _cachedPpu;
        Vector2 _cachedBaseTranslate;
        float _cachedBaseRotate;
        float _cachedBaseScale;
        float _cachedPhaseStep;
        bool _cachedCountsValid;

        static readonly int Mat0ID = Shader.PropertyToID("_Mat0");
        static readonly int Mat1ID = Shader.PropertyToID("_Mat1");
        static readonly int MatTexSizeID = Shader.PropertyToID("_MatTexSize");
        static readonly int SampleFPSID = Shader.PropertyToID("_SampleFPS");
        static readonly int AnimTimeID = Shader.PropertyToID("_AnimTime");
        static readonly int AnimClipCountID = Shader.PropertyToID("_AnimClipCount");
        static readonly int AnimClipStartID = Shader.PropertyToID("_AnimClipStart");
        static readonly int AnimClipCountFramesID = Shader.PropertyToID("_AnimClipCountFrames");
        static readonly int AnimClipStart4ID = Shader.PropertyToID("_AnimClipStart4");
        static readonly int AnimClipCountFrames4ID = Shader.PropertyToID("_AnimClipCountFrames4");

        void OnEnable()
        {
            EnsureMaterial();
            RebuildTextures();
        }

        void OnValidate()
        {
            EnsureMaterial();
            RebuildTextures();
        }

        void OnDisable()
        {
            if (_mat0 != null)
            {
                if (Application.isPlaying) Destroy(_mat0);
                else DestroyImmediate(_mat0);
                _mat0 = null;
            }
            if (_mat1 != null)
            {
                if (Application.isPlaying) Destroy(_mat1);
                else DestroyImmediate(_mat1);
                _mat1 = null;
            }
        }

        void Update()
        {
            if (material == null)
            {
                return;
            }

            if (rebuildEveryFrame || IsDirty())
            {
                RebuildTextures();
            }

            if (driveMaterialTime)
            {
                float t = (Application.isPlaying ? Time.time : GetEditorTime()) * animSpeed + timeOffset;
                material.SetFloat(AnimTimeID, t);
            }

            material.SetFloat(SampleFPSID, sampleFPS);
            UpdateAnimConfig();
        }

        float GetEditorTime()
        {
#if UNITY_EDITOR
            return (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.time;
#endif
        }

        void EnsureMaterial()
        {
            if (material == null)
            {
                var mr = GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    material = mr.sharedMaterial;
                }
            }
        }

        bool CountsValid()
        {
            if (clipFrameCounts == null || clipFrameCounts.Length < clipCount) return false;
            for (int i = 0; i < clipCount; i++)
            {
                if (clipFrameCounts[i] <= 0) return false;
            }
            return true;
        }

        bool IsDirty()
        {
            bool countsValid = CountsValid();
            return _cachedClipCount != clipCount ||
                   _cachedSampleFPS != sampleFPS ||
                   _cachedPpu != pixelsPerUnit ||
                   _cachedBaseTranslate != baseTranslateAmplitudePixels ||
                   _cachedBaseRotate != baseRotateAmplitudeDegrees ||
                   _cachedBaseScale != baseScaleAmplitude ||
                   _cachedPhaseStep != partPhaseStep ||
                   _cachedCountsValid != countsValid;
        }

        void RebuildTextures()
        {
            if (material == null)
            {
                return;
            }

            int width = 6;
            int count = Mathf.Clamp(clipCount, 1, 5);
            if (clipFrameCounts == null || clipFrameCounts.Length < count)
            {
                clipFrameCounts = new int[count];
                for (int i = 0; i < count; i++) clipFrameCounts[i] = 20;
            }

            int totalFrames = 0;
            for (int i = 0; i < count; i++)
            {
                totalFrames += Mathf.Max(1, clipFrameCounts[i]);
            }

            if (_mat0 != null)
            {
                if (Application.isPlaying) Destroy(_mat0);
                else DestroyImmediate(_mat0);
            }
            if (_mat1 != null)
            {
                if (Application.isPlaying) Destroy(_mat1);
                else DestroyImmediate(_mat1);
            }

            _mat0 = new Texture2D(width, totalFrames, TextureFormat.RGBAHalf, false, true);
            _mat1 = new Texture2D(width, totalFrames, TextureFormat.RGBAHalf, false, true);

            _mat0.name = "MAT0_Debug_Packed";
            _mat1.name = "MAT1_Debug_Packed";
            _mat0.filterMode = FilterMode.Point;
            _mat1.filterMode = FilterMode.Point;
            _mat0.wrapMode = TextureWrapMode.Clamp;
            _mat1.wrapMode = TextureWrapMode.Clamp;
            _mat0.anisoLevel = 0;
            _mat1.anisoLevel = 0;

            var colors0 = new Color[width * totalFrames];
            var colors1 = new Color[width * totalFrames];

            float invPpu = pixelsPerUnit > 0f ? 1f / pixelsPerUnit : 0.01f;
            Vector2 baseAmpUnits = baseTranslateAmplitudePixels * invPpu;
            float baseRotRad = baseRotateAmplitudeDegrees * Mathf.Deg2Rad;

            int frameOffset = 0;
            for (int clip = 0; clip < count; clip++)
            {
                int frames = Mathf.Max(1, clipFrameCounts[clip]);
                float clipPhaseOffset = clip * 1.1f;

                for (int y = 0; y < frames; y++)
                {
                    float t = (float)y / frames * Mathf.PI * 2f + clipPhaseOffset;

                    for (int x = 0; x < width; x++)
                    {
                        int idx = (frameOffset + y) * width + x;
                        float partPhase = t + x * partPhaseStep;
                        float sin = Mathf.Sin(partPhase);

                        float ampScale = Mathf.Max(0.4f, 1f - x * 0.1f);
                        Vector2 amp = baseAmpUnits * ampScale;

                        float rotScale = 0.2f + x * 0.15f;
                        float angle = baseRotRad * rotScale * sin;
                        float s = Mathf.Sin(angle);
                        float c = Mathf.Cos(angle);

                        float scale = 1f + baseScaleAmplitude * (0.5f + x * 0.1f) * sin;

                        colors0[idx] = new Color(amp.x * sin, amp.y * Mathf.Sin(partPhase + 1.1f), s, c);
                        colors1[idx] = new Color(scale, scale, 0f, 0f);
                    }
                }

                frameOffset += frames;
            }

            _mat0.SetPixels(colors0);
            _mat1.SetPixels(colors1);
            _mat0.Apply(false, false);
            _mat1.Apply(false, false);

            material.SetTexture(Mat0ID, _mat0);
            material.SetTexture(Mat1ID, _mat1);
            material.SetVector(MatTexSizeID, new Vector4(width, totalFrames, 1f / width, 1f / totalFrames));
            material.SetFloat(SampleFPSID, sampleFPS);

            WriteConfigAsset(count, totalFrames);

            _cachedClipCount = clipCount;
            _cachedSampleFPS = sampleFPS;
            _cachedPpu = pixelsPerUnit;
            _cachedBaseTranslate = baseTranslateAmplitudePixels;
            _cachedBaseRotate = baseRotateAmplitudeDegrees;
            _cachedBaseScale = baseScaleAmplitude;
            _cachedPhaseStep = partPhaseStep;
            _cachedCountsValid = CountsValid();
        }

        void UpdateAnimConfig()
        {
            if (config == null || material == null) return;

            material.SetFloat(AnimClipCountID, config.clipCount);
            Vector4 start = Vector4.zero;
            Vector4 count = Vector4.one;
            Vector4 start1 = Vector4.zero;
            Vector4 count1 = Vector4.one;

            if (config.clipStartFrame != null && config.clipStartFrame.Length >= 4)
            {
                start = new Vector4(config.clipStartFrame[0], config.clipStartFrame[1], config.clipStartFrame[2], config.clipStartFrame[3]);
            }
            if (config.clipFrameCount != null && config.clipFrameCount.Length >= 4)
            {
                count = new Vector4(config.clipFrameCount[0], config.clipFrameCount[1], config.clipFrameCount[2], config.clipFrameCount[3]);
            }
            if (config.clipStartFrame != null && config.clipStartFrame.Length >= 5)
            {
                start1 = new Vector4(config.clipStartFrame[4], 0f, 0f, 0f);
            }
            if (config.clipFrameCount != null && config.clipFrameCount.Length >= 5)
            {
                count1 = new Vector4(config.clipFrameCount[4], 1f, 1f, 1f);
            }

            material.SetVector(AnimClipStartID, start);
            material.SetVector(AnimClipCountFramesID, count);
            material.SetVector(AnimClipStart4ID, start1);
            material.SetVector(AnimClipCountFrames4ID, count1);
        }

        void WriteConfigAsset(int count, int totalFrames)
        {
            if (config == null) return;

            config.clipCount = count;
            config.sampleFPS = sampleFPS;
            config.totalFrames = totalFrames;

            if (config.clipStartFrame == null || config.clipStartFrame.Length != count)
            {
                config.clipStartFrame = new int[count];
            }
            if (config.clipFrameCount == null || config.clipFrameCount.Length != count)
            {
                config.clipFrameCount = new int[count];
            }

            int offset = 0;
            for (int i = 0; i < count; i++)
            {
                int frames = Mathf.Max(1, clipFrameCounts[i]);
                config.clipStartFrame[i] = offset;
                config.clipFrameCount[i] = frames;
                offset += frames;
            }
        }
    }
}
