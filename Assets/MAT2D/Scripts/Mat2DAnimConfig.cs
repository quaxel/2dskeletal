using UnityEngine;

namespace Mat2D
{
    [CreateAssetMenu(menuName = "MAT2D/Anim Config", fileName = "MAT2DAnimConfig")]
    public class Mat2DAnimConfig : ScriptableObject
    {
        [Tooltip("Number of animation clips baked into the MAT textures.")]
        public int clipCount = 0;
        
        [Tooltip("Frames per second used during baking.")]
        public float sampleFPS = 30f;
        
        [Tooltip("Total number of frames across all clips.")]
        public int totalFrames = 0;
        
        [Tooltip("Starting frame index for each clip.")]
        public int[] clipStartFrame = new int[0];
        
        [Tooltip("Number of frames in each clip.")]
        public int[] clipFrameCount = new int[0];

        /// <summary>
        /// Get the starting frame index for a given animation ID.
        /// </summary>
        public int GetStart(int animId)
        {
            if (clipStartFrame == null || clipStartFrame.Length == 0)
            {
                Debug.LogWarning($"MAT2D: clipStartFrame is empty. Returning 0 for animId {animId}.", this);
                return 0;
            }
            
            if (animId < 0 || animId >= clipStartFrame.Length)
            {
                Debug.LogWarning($"MAT2D: animId {animId} out of range [0, {clipStartFrame.Length - 1}]. Clamping.", this);
                animId = Mathf.Clamp(animId, 0, clipStartFrame.Length - 1);
            }
            
            return clipStartFrame[animId];
        }

        /// <summary>
        /// Get the frame count for a given animation ID.
        /// </summary>
        public int GetCount(int animId)
        {
            if (clipFrameCount == null || clipFrameCount.Length == 0)
            {
                Debug.LogWarning($"MAT2D: clipFrameCount is empty. Returning 1 for animId {animId}.", this);
                return 1;
            }
            
            if (animId < 0 || animId >= clipFrameCount.Length)
            {
                Debug.LogWarning($"MAT2D: animId {animId} out of range [0, {clipFrameCount.Length - 1}]. Clamping.", this);
                animId = Mathf.Clamp(animId, 0, clipFrameCount.Length - 1);
            }
            
            return Mathf.Max(1, clipFrameCount[animId]);
        }
        
        /// <summary>
        /// Validate that arrays are properly sized.
        /// </summary>
        void OnValidate()
        {
            if (clipStartFrame == null) clipStartFrame = new int[0];
            if (clipFrameCount == null) clipFrameCount = new int[0];
            
            // Ensure arrays match clipCount
            if (clipCount > 0)
            {
                if (clipStartFrame.Length != clipCount)
                {
                    System.Array.Resize(ref clipStartFrame, clipCount);
                }
                if (clipFrameCount.Length != clipCount)
                {
                    System.Array.Resize(ref clipFrameCount, clipCount);
                }
            }
        }
    }
}
