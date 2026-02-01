using UnityEngine;
using UnityEngine.Rendering;

namespace Mat2D
{
    public class Mat2DInstancedRenderer : MonoBehaviour
    {
        [Header("Source")]
        public Mesh sourceMesh;
        public MeshFilter sourceMeshFilter;
        public Material material;

        [Header("Instances")]
        [Min(1)] public int instanceCount = 1000;
        [Min(1)] public int gridWidth = 40;
        public Vector2 spacing = new Vector2(2.2f, 2.2f);
        public Vector3 origin = Vector3.zero;
        public bool randomRotation = false;
        public Vector2 rotationRange = new Vector2(-10f, 10f);
        public bool randomScale = false;
        public Vector2 scaleRange = new Vector2(0.9f, 1.1f);
        public int randomSeed = 123;

        [Header("Render")]
        public ShadowCastingMode shadowCasting = ShadowCastingMode.Off;
        public bool receiveShadows = false;
        public bool rebuildEveryFrame = false;

        Matrix4x4[] _matrices = new Matrix4x4[0];
        int _cachedCount;
        int _cachedGridWidth;
        Vector2 _cachedSpacing;
        Vector3 _cachedOrigin;
        bool _cachedRandomRotation;
        bool _cachedRandomScale;
        int _cachedSeed;

        void OnEnable()
        {
            EnsureSources();
            RebuildMatrices();
        }

        void OnValidate()
        {
            EnsureSources();
            RebuildMatrices();
        }

        void EnsureSources()
        {
            if (sourceMesh == null && sourceMeshFilter != null)
            {
                sourceMesh = sourceMeshFilter.sharedMesh;
            }
            if (sourceMesh == null)
            {
                var mf = GetComponent<MeshFilter>();
                if (mf != null)
                {
                    sourceMesh = mf.sharedMesh;
                }
            }

            if (material == null)
            {
                var mr = GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    material = mr.sharedMaterial;
                }
            }
        }

        void Update()
        {
            if (rebuildEveryFrame || IsDirty())
            {
                RebuildMatrices();
            }

            if (sourceMesh == null || material == null || _matrices.Length == 0)
            {
                return;
            }

            int total = _matrices.Length;
            int offset = 0;
            const int batchSize = 1023;
            while (offset < total)
            {
                int count = Mathf.Min(batchSize, total - offset);
                Graphics.DrawMeshInstanced(sourceMesh, 0, material, _matrices, count, null,
                    shadowCasting, receiveShadows, 0, null, LightProbeUsage.Off, null);
                offset += count;
            }
        }

        bool IsDirty()
        {
            return _cachedCount != instanceCount ||
                   _cachedGridWidth != gridWidth ||
                   _cachedSpacing != spacing ||
                   _cachedOrigin != origin ||
                   _cachedRandomRotation != randomRotation ||
                   _cachedRandomScale != randomScale ||
                   _cachedSeed != randomSeed;
        }

        void RebuildMatrices()
        {
            if (instanceCount <= 0)
            {
                _matrices = new Matrix4x4[0];
                return;
            }

            int width = Mathf.Max(1, gridWidth);
            _matrices = new Matrix4x4[instanceCount];

            var rng = new System.Random(randomSeed);

            for (int i = 0; i < instanceCount; i++)
            {
                int x = i % width;
                int y = i / width;

                Vector3 pos = origin + new Vector3(x * spacing.x, -y * spacing.y, 0f);

                float rotZ = 0f;
                if (randomRotation)
                {
                    rotZ = Mathf.Lerp(rotationRange.x, rotationRange.y, (float)rng.NextDouble());
                }

                float scale = 1f;
                if (randomScale)
                {
                    scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)rng.NextDouble());
                }

                _matrices[i] = Matrix4x4.TRS(pos, Quaternion.Euler(0f, 0f, rotZ), new Vector3(scale, scale, 1f));
            }

            _cachedCount = instanceCount;
            _cachedGridWidth = width;
            _cachedSpacing = spacing;
            _cachedOrigin = origin;
            _cachedRandomRotation = randomRotation;
            _cachedRandomScale = randomScale;
            _cachedSeed = randomSeed;
        }
    }
}
