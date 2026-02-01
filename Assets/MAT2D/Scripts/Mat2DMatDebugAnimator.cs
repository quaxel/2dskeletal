using UnityEngine;

namespace Mat2D
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class Mat2DMatDebugAnimator : MonoBehaviour
    {
        [Header("Material")]
        public Material material;

        [Header("MAT Texture")]
        [Min(2)] public int frameCount = 64;
        public float sampleFPS = 30f;

        [Header("Part 0 Motion (in pixels)")]
        public float pixelsPerUnit = 100f;
        public float translateAmplitudePixels = 20f;
        public float rotateAmplitudeDegrees = 0f;
        public float scaleAmplitude = 0f;

        [Header("Playback")]
        public float animSpeed = 1f;
        public float timeOffset = 0f;
        public bool rebuildEveryFrame = false;

        Texture2D _mat0;
        Texture2D _mat1;
        int _cachedFrameCount;
        float _cachedSampleFPS;
        float _cachedTranslateAmp;
        float _cachedRotateAmp;
        float _cachedScaleAmp;
        float _cachedPpu;

        static readonly int Mat0ID = Shader.PropertyToID("_Mat0");
        static readonly int Mat1ID = Shader.PropertyToID("_Mat1");
        static readonly int MatTexSizeID = Shader.PropertyToID("_MatTexSize");
        static readonly int SampleFPSID = Shader.PropertyToID("_SampleFPS");
        static readonly int FrameCountID = Shader.PropertyToID("_FrameCount");
        static readonly int AnimTimeID = Shader.PropertyToID("_AnimTime");

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

            float t = (Application.isPlaying ? Time.time : GetEditorTime()) * animSpeed + timeOffset;
            material.SetFloat(AnimTimeID, t);
            material.SetFloat(SampleFPSID, sampleFPS);
            material.SetFloat(FrameCountID, frameCount);
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

        bool IsDirty()
        {
            return _cachedFrameCount != frameCount ||
                   _cachedSampleFPS != sampleFPS ||
                   _cachedTranslateAmp != translateAmplitudePixels ||
                   _cachedRotateAmp != rotateAmplitudeDegrees ||
                   _cachedScaleAmp != scaleAmplitude ||
                   _cachedPpu != pixelsPerUnit;
        }

        void RebuildTextures()
        {
            if (material == null)
            {
                return;
            }

            int width = 6;
            int height = Mathf.Max(2, frameCount);

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

            _mat0 = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);
            _mat1 = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);

            _mat0.name = "MAT0_Debug";
            _mat1.name = "MAT1_Debug";
            _mat0.filterMode = FilterMode.Point;
            _mat1.filterMode = FilterMode.Point;
            _mat0.wrapMode = TextureWrapMode.Clamp;
            _mat1.wrapMode = TextureWrapMode.Clamp;
            _mat0.anisoLevel = 0;
            _mat1.anisoLevel = 0;

            var colors0 = new Color[width * height];
            var colors1 = new Color[width * height];

            float invPpu = pixelsPerUnit > 0f ? 1f / pixelsPerUnit : 0.01f;
            float ampUnits = translateAmplitudePixels * invPpu;
            float rotRadAmp = rotateAmplitudeDegrees * Mathf.Deg2Rad;

            for (int y = 0; y < height; y++)
            {
                float phase = (float)y / height * Mathf.PI * 2f;
                float sin = Mathf.Sin(phase);
                float cos = Mathf.Cos(phase);

                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;

                    // Default identity for all parts.
                    colors0[idx] = new Color(0f, 0f, 0f, 1f);
                    colors1[idx] = new Color(1f, 1f, 0f, 0f);

                    // Only part 0 has motion.
                    if (x == 0)
                    {
                        float tx = ampUnits * sin;
                        float ty = 0f;

                        float angle = rotRadAmp * sin;
                        float s = Mathf.Sin(angle);
                        float c = Mathf.Cos(angle);

                        float scale = 1f + scaleAmplitude * sin;

                        colors0[idx] = new Color(tx, ty, s, c);
                        colors1[idx] = new Color(scale, scale, 0f, 0f);
                    }
                }
            }

            _mat0.SetPixels(colors0);
            _mat1.SetPixels(colors1);
            _mat0.Apply(false, false);
            _mat1.Apply(false, false);

            material.SetTexture(Mat0ID, _mat0);
            material.SetTexture(Mat1ID, _mat1);
            material.SetVector(MatTexSizeID, new Vector4(width, height, 1f / width, 1f / height));
            material.SetFloat(SampleFPSID, sampleFPS);
            material.SetFloat(FrameCountID, height);

            _cachedFrameCount = frameCount;
            _cachedSampleFPS = sampleFPS;
            _cachedTranslateAmp = translateAmplitudePixels;
            _cachedRotateAmp = rotateAmplitudeDegrees;
            _cachedScaleAmp = scaleAmplitude;
            _cachedPpu = pixelsPerUnit;
        }
    }
}
