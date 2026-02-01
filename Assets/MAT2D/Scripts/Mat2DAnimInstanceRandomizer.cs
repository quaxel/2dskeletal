using UnityEngine;

namespace Mat2D
{
    public class Mat2DAnimInstanceRandomizer : MonoBehaviour
    {
        [Header("Targets")]
        public Mat2DAnimInstance[] targets;
        public bool autoFindInChildren = true;

        [Header("Randomization")]
        public int clipCount = 5;
        public float speedMin = 0.8f;
        public float speedMax = 1.2f;
        public float timeOffsetMin = 0f;
        public float timeOffsetMax = 3f;
        public float flipChance = 0.5f;
        public int seed = 1234;
        public bool applyOnStart = true;

        void Start()
        {
            if (applyOnStart)
            {
                Apply();
            }
        }

        [ContextMenu("Apply")]
        public void Apply()
        {
            if (targets == null || targets.Length == 0)
            {
                if (autoFindInChildren)
                {
                    targets = GetComponentsInChildren<Mat2DAnimInstance>();
                }
            }

            if (targets == null || targets.Length == 0)
            {
                return;
            }

            var rng = new System.Random(seed);
            for (int i = 0; i < targets.Length; i++)
            {
                var t = targets[i];
                if (t == null) continue;

                t.animId = rng.Next(0, Mathf.Max(1, clipCount));
                t.animSpeed = Mathf.Lerp(speedMin, speedMax, (float)rng.NextDouble());
                t.timeOffset = Mathf.Lerp(timeOffsetMin, timeOffsetMax, (float)rng.NextDouble());
                t.flipX = rng.NextDouble() < flipChance;
            }
        }
    }
}
