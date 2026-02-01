using UnityEngine;

namespace Mat2D
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class Mat2DAnimInstance : MonoBehaviour
    {
        [Header("Target")]
        public MeshRenderer targetRenderer;
        public bool autoEnableInstancing = true;

        [Header("Anim Params")]
        public bool useGlobalTime = true;
        public bool useUnscaledTime = false;
        public float animTime = 0f;
        public float animSpeed = 1f;
        public int animId = 0;
        public bool flipX = false;
        public float timeOffset = 0f;

        static readonly int AnimTimeID = Shader.PropertyToID("_AnimTime");
        static readonly int AnimSpeedID = Shader.PropertyToID("_AnimSpeed");
        static readonly int AnimIdID = Shader.PropertyToID("_AnimId");
        static readonly int FlipXID = Shader.PropertyToID("_FlipX");

        MaterialPropertyBlock _mpb;

        void OnEnable()
        {
            EnsureTarget();
            Apply();
        }

        void OnValidate()
        {
            EnsureTarget();
            Apply();
        }

        void Update()
        {
            Apply();
        }

        void EnsureTarget()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<MeshRenderer>();
            }

            if (autoEnableInstancing && targetRenderer != null && targetRenderer.sharedMaterial != null)
            {
                targetRenderer.sharedMaterial.enableInstancing = true;
            }

            if (_mpb == null)
            {
                _mpb = new MaterialPropertyBlock();
            }
        }

        void Apply()
        {
            if (targetRenderer == null)
            {
                return;
            }

            float t = useGlobalTime ? GetTime() + timeOffset : animTime;

            targetRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(AnimTimeID, t);
            _mpb.SetFloat(AnimSpeedID, animSpeed);
            _mpb.SetFloat(AnimIdID, animId);
            _mpb.SetFloat(FlipXID, flipX ? 1f : 0f);
            targetRenderer.SetPropertyBlock(_mpb);
        }

        float GetTime()
        {
            if (Application.isPlaying)
            {
                return useUnscaledTime ? Time.unscaledTime : Time.time;
            }

#if UNITY_EDITOR
            return (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.time;
#endif
        }
    }
}
