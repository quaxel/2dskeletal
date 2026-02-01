using UnityEngine;

namespace Mat2D
{
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
        
        [Header("Performance")]
        [Tooltip("Update every frame in Play mode. Disable for static animations.")]
        public bool updateInPlayMode = true;
        [Tooltip("Update in Edit mode. Useful for previewing animations in the editor.")]
        public bool updateInEditMode = false;

        static readonly int AnimTimeID = Shader.PropertyToID("_AnimTime");
        static readonly int AnimSpeedID = Shader.PropertyToID("_AnimSpeed");
        static readonly int AnimIdID = Shader.PropertyToID("_AnimId");
        static readonly int FlipXID = Shader.PropertyToID("_FlipX");

        MaterialPropertyBlock _mpb;
        
        // Cache to avoid unnecessary updates
        float _lastAnimTime;
        float _lastAnimSpeed;
        int _lastAnimId;
        bool _lastFlipX;

        void OnEnable()
        {
            EnsureTarget();
            Apply(true); // Force update on enable
        }

        void OnValidate()
        {
            EnsureTarget();
            Apply(true); // Force update on validate
        }

        void Update()
        {
            // Performance optimization: only update when needed
            if (Application.isPlaying)
            {
                if (!updateInPlayMode) return;
            }
            else
            {
                if (!updateInEditMode) return;
            }
            
            Apply(false);
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

        void Apply(bool forceUpdate)
        {
            if (targetRenderer == null)
            {
                return;
            }

            float t = useGlobalTime ? GetTime() + timeOffset : animTime;

            // Only update if values changed (performance optimization)
            bool hasChanged = forceUpdate ||
                              _lastAnimTime != t ||
                              _lastAnimSpeed != animSpeed ||
                              _lastAnimId != animId ||
                              _lastFlipX != flipX;
            
            if (!hasChanged) return;

            targetRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(AnimTimeID, t);
            _mpb.SetFloat(AnimSpeedID, animSpeed);
            _mpb.SetFloat(AnimIdID, animId);
            _mpb.SetFloat(FlipXID, flipX ? 1f : 0f);
            targetRenderer.SetPropertyBlock(_mpb);
            
            // Update cache
            _lastAnimTime = t;
            _lastAnimSpeed = animSpeed;
            _lastAnimId = animId;
            _lastFlipX = flipX;
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
