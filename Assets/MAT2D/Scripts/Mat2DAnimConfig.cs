using UnityEngine;

namespace Mat2D
{
    [CreateAssetMenu(menuName = "MAT2D/Anim Config", fileName = "MAT2DAnimConfig")]
    public class Mat2DAnimConfig : ScriptableObject
    {
        public int clipCount = 5;
        public float sampleFPS = 30f;
        public int totalFrames = 0;
        public int[] clipStartFrame = new int[5];
        public int[] clipFrameCount = new int[5];

        public int GetStart(int animId)
        {
            if (clipStartFrame == null || clipStartFrame.Length == 0) return 0;
            int i = Mathf.Clamp(animId, 0, clipStartFrame.Length - 1);
            return clipStartFrame[i];
        }

        public int GetCount(int animId)
        {
            if (clipFrameCount == null || clipFrameCount.Length == 0) return 1;
            int i = Mathf.Clamp(animId, 0, clipFrameCount.Length - 1);
            return Mathf.Max(1, clipFrameCount[i]);
        }
    }
}
