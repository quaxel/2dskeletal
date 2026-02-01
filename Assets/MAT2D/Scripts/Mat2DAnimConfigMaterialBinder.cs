using UnityEngine;

namespace Mat2D
{
    public class Mat2DAnimConfigMaterialBinder : MonoBehaviour
    {
        public Mat2DAnimConfig config;
        public Material material;
        [Min(1)] public int partCount = 6;

        static readonly int SampleFPSID = Shader.PropertyToID("_SampleFPS");
        static readonly int AnimClipCountID = Shader.PropertyToID("_AnimClipCount");
        static readonly int AnimClipStartID = Shader.PropertyToID("_AnimClipStart");
        static readonly int AnimClipCountFramesID = Shader.PropertyToID("_AnimClipCountFrames");
        static readonly int AnimClipStart4ID = Shader.PropertyToID("_AnimClipStart4");
        static readonly int AnimClipCountFrames4ID = Shader.PropertyToID("_AnimClipCountFrames4");
        static readonly int MatTexSizeID = Shader.PropertyToID("_MatTexSize");

        void OnEnable()
        {
            Apply();
        }

        void OnValidate()
        {
            Apply();
        }

        [ContextMenu("Apply")]
        public void Apply()
        {
            if (config == null || material == null) return;

            material.SetFloat(SampleFPSID, config.sampleFPS);
            material.SetFloat(AnimClipCountID, config.clipCount);

            Vector4 start0 = Vector4.zero;
            Vector4 count0 = Vector4.one;
            Vector4 start1 = Vector4.zero;
            Vector4 count1 = Vector4.one;

            for (int i = 0; i < config.clipCount; i++)
            {
                // Bounds check to prevent IndexOutOfRangeException
                if (i >= config.clipStartFrame.Length || i >= config.clipFrameCount.Length)
                {
                    Debug.LogWarning($"MAT2D: Clip {i} is out of bounds. clipStartFrame.Length={config.clipStartFrame.Length}, clipFrameCount.Length={config.clipFrameCount.Length}", this);
                    break;
                }
                
                int start = config.clipStartFrame[i];
                int count = Mathf.Max(1, config.clipFrameCount[i]);

                if (i < 4)
                {
                    if (i == 0) { start0.x = start; count0.x = count; }
                    if (i == 1) { start0.y = start; count0.y = count; }
                    if (i == 2) { start0.z = start; count0.z = count; }
                    if (i == 3) { start0.w = start; count0.w = count; }
                }
                else if (i == 4)
                {
                    start1.x = start;
                    count1.x = count;
                }
            }

            material.SetVector(AnimClipStartID, start0);
            material.SetVector(AnimClipCountFramesID, count0);
            material.SetVector(AnimClipStart4ID, start1);
            material.SetVector(AnimClipCountFrames4ID, count1);

            if (config.totalFrames > 0)
            {
                float w = Mathf.Max(1, partCount);
                float h = Mathf.Max(1, config.totalFrames);
                material.SetVector(MatTexSizeID, new Vector4(w, h, 1f / w, 1f / h));
            }
        }
    }
}
