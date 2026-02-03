using UnityEngine;

namespace Mat2D
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Mat2DCharacterMeshBuilder : MonoBehaviour
    {
        [System.Serializable]
        public struct Part
        {
            public string name;
            [Tooltip("Width/height in pixels within the atlas.")]
            public Vector2 sizePixels;
            [Tooltip("Pivot in pixels from the part's bottom-left corner.")]
            public Vector2 pivotPixels;
            [Tooltip("Pivot position in character space (pixels).")]
            public Vector2 offsetPixels;
            [Tooltip("Atlas UV rect (0-1).")]
            public Rect atlasRect;
            [Tooltip("SpriteRenderer sorting order for Z-depth calculation.")]
            public int sortingOrder;
        }

        [Header("Mesh")]
        public float pixelsPerUnit = 100f;
        public float zStep = 0.001f;
        public bool autoRebuild = true;

        [Header("Sharing")]
        [Tooltip("Use a single shared mesh for all instances to enable GPU instancing.")]
        public bool useStaticSharedMesh = false;
        public string sharedMeshName = "MAT2D_CharacterMesh_Shared";

        [Header("Parts (must be 6)")]
        public Part[] parts = new Part[6];

        [Header("Auto Fill (optional)")]
        [Tooltip("If assigned, parts will be built from the rig's transform hierarchy.")]
        public GameObject rigPrefab;
        public bool autoFillFromRig = false;
        
        [Tooltip("If assigned, size/pivot/atlasRect will be filled from these sprites.")]
        public Sprite[] partSprites = new Sprite[6];
        public bool autoFillFromSprites = true;
        [Tooltip("Keep offsetPixels as authored when auto-filling from sprites.")]
        public bool preserveOffsets = true;
        [Tooltip("Assign the material's _BaseMap from the first sprite's texture to avoid mismatches.")]
        public bool autoAssignBaseMap = true;
        public bool logWarnings = true;
        [Tooltip("If sprites/parts are missing, fill with a visible debug layout so the mesh still renders.")]
        public bool debugFillIfMissing = true;
        public Vector2 debugSizePixels = new Vector2(64, 64);
        public Vector2 debugSpacingPixels = new Vector2(10, 10);

        [Header("Rendering")]
        public Material material;

        Mesh _mesh;
        static Mesh s_sharedMesh;

        void OnEnable()
        {
            if (autoRebuild)
            {
                Rebuild();
            }
        }

        void OnValidate()
        {
            if (autoRebuild)
            {
                Rebuild();
            }
        }

        void OnDestroy()
        {
            if (_mesh != null)
            {
                if (useStaticSharedMesh && _mesh == s_sharedMesh)
                {
                    return;
                }
                if (Application.isPlaying)
                {
                    Destroy(_mesh);
                }
                else
                {
                    DestroyImmediate(_mesh);
                }
            }
        }

        [ContextMenu("Build From Rig Prefab")]
        public void BuildFromRigContextMenu()
        {
            if (rigPrefab == null)
            {
                Debug.LogError("MAT2D: Please assign a rigPrefab first.", this);
                return;
            }
            BuildFromRig();
            Rebuild();
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            if (parts == null || parts.Length == 0)
            {
                return;
            }

            if (autoFillFromRig && rigPrefab != null)
            {
                BuildFromRig();
            }
            else if (autoFillFromSprites)
            {
                ApplySpriteData();
            }
            else if (debugFillIfMissing)
            {
                ApplyDebugFallbackIfInvalid();
            }

            var meshFilter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();

            if (material != null)
            {
                meshRenderer.sharedMaterial = material;
            }

            if (_mesh == null)
            {
                if (useStaticSharedMesh)
                {
                    if (s_sharedMesh == null)
                    {
                        s_sharedMesh = new Mesh();
                        s_sharedMesh.name = sharedMeshName;
                        s_sharedMesh.hideFlags = HideFlags.DontSave;
                    }
                    _mesh = s_sharedMesh;
                }
                else
                {
                    _mesh = new Mesh();
                    _mesh.name = "MAT2D_CharacterMesh";
                    _mesh.hideFlags = HideFlags.DontSave;
                }
            }
            else
            {
                _mesh.Clear();
            }

            int partCount = parts.Length;
            int vertexCount = partCount * 4;
            int indexCount = partCount * 6;

            var vertices = new Vector3[vertexCount];
            var uv0 = new Vector2[vertexCount];
            var uv2 = new Vector2[vertexCount];
            var normals = new Vector3[vertexCount];
            var triangles = new int[indexCount];

            float invPpu = pixelsPerUnit > 0f ? 1f / pixelsPerUnit : 0.01f;

            // Create array of part indices sorted by sortingOrder (low to high)
            // Lower sortingOrder = rendered first (back), higher = rendered last (front)
            var sortedIndices = new int[partCount];
            for (int i = 0; i < partCount; i++)
            {
                sortedIndices[i] = i;
            }
            System.Array.Sort(sortedIndices, (a, b) => parts[a].sortingOrder.CompareTo(parts[b].sortingOrder));

            // Build mesh with parts in sorted order
            for (int sortedIdx = 0; sortedIdx < partCount; sortedIdx++)
            {
                int i = sortedIndices[sortedIdx];
                Part p = parts[i];

                Vector2 size = p.sizePixels * invPpu;
                Vector2 pivot = p.pivotPixels * invPpu;
                Vector2 offset = p.offsetPixels * invPpu;

                Vector2 bl = new Vector2(-pivot.x, -pivot.y) + offset;
                Vector2 br = new Vector2(size.x - pivot.x, -pivot.y) + offset;
                Vector2 tl = new Vector2(-pivot.x, size.y - pivot.y) + offset;
                Vector2 tr = new Vector2(size.x - pivot.x, size.y - pivot.y) + offset;

                // Z position is 0 for all parts - we rely on triangle order for sorting
                float z = 0f;
                int v = sortedIdx * 4;

                vertices[v + 0] = new Vector3(bl.x, bl.y, z);
                vertices[v + 1] = new Vector3(tl.x, tl.y, z);
                vertices[v + 2] = new Vector3(tr.x, tr.y, z);
                vertices[v + 3] = new Vector3(br.x, br.y, z);

                Rect r = p.atlasRect;
                uv0[v + 0] = new Vector2(r.xMin, r.yMin);
                uv0[v + 1] = new Vector2(r.xMin, r.yMax);
                uv0[v + 2] = new Vector2(r.xMax, r.yMax);
                uv0[v + 3] = new Vector2(r.xMax, r.yMin);

                // Use original part index for animation
                Vector2 partIndex = new Vector2(i, 0f);
                uv2[v + 0] = partIndex;
                uv2[v + 1] = partIndex;
                uv2[v + 2] = partIndex;
                uv2[v + 3] = partIndex;

                normals[v + 0] = Vector3.forward;
                normals[v + 1] = Vector3.forward;
                normals[v + 2] = Vector3.forward;
                normals[v + 3] = Vector3.forward;

                int t = sortedIdx * 6;
                triangles[t + 0] = v + 0;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 0;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }

            _mesh.vertices = vertices;
            _mesh.uv = uv0;
            _mesh.uv2 = uv2;
            _mesh.normals = normals;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();

            meshFilter.sharedMesh = _mesh;
        }

        void BuildFromRig()
        {
            if (rigPrefab == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("MAT2D: rigPrefab is null, cannot build from rig.", this);
                }
                return;
            }

            Mat2DRigDefinition rigDef = rigPrefab.GetComponent<Mat2DRigDefinition>();
            if (rigDef == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("MAT2D: rigPrefab does not have Mat2DRigDefinition component.", this);
                }
                return;
            }

            if (rigDef.parts == null || rigDef.parts.Length != 6)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("MAT2D: rigPrefab's Mat2DRigDefinition does not have exactly 6 parts.", this);
                }
                return;
            }

            Texture2D referenceTex = null;
            Transform rigRoot = rigDef.root != null ? rigDef.root : rigDef.transform;

            for (int i = 0; i < 6; i++)
            {
                Transform partTransform = rigDef.parts[i];
                if (partTransform == null)
                {
                    if (logWarnings)
                    {
                        Debug.LogWarning($"MAT2D: rigPrefab part[{i}] is null.", this);
                    }
                    continue;
                }

                Part p = parts[i];
                
                // Get sprite data from SpriteRenderer
                SpriteRenderer sr = partTransform.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    Sprite sprite = sr.sprite;
                    
                    // Use textureRect for packed sprites (handles SpriteAtlas correctly)
                    Rect rect = sprite.textureRect;
                    
                    // CRITICAL FIX: Account for transform scale!
                    // The sprite's texture size is in pixels, but if the transform is scaled,
                    // the actual rendered size is different
                    Vector3 localScale = partTransform.localScale;
                    p.sizePixels = new Vector2(
                        rect.width * Mathf.Abs(localScale.x), 
                        rect.height * Mathf.Abs(localScale.y)
                    );
                    
                    // sprite.pivot is in sprite's local space
                    // CRITICAL FIX: Scale pivot as well!
                    p.pivotPixels = new Vector2(
                        sprite.pivot.x * Mathf.Abs(localScale.x),
                        sprite.pivot.y * Mathf.Abs(localScale.y)
                    );

                    Texture2D tex = sprite.texture;
                    if (tex != null)
                    {
                        if (referenceTex == null)
                        {
                            referenceTex = tex;
                        }
                        else if (logWarnings && referenceTex != tex)
                        {
                            Debug.LogWarning($"MAT2D: rigPrefab part[{i}] uses different texture ({tex.name}) than first part ({referenceTex.name}). " +
                                "Ensure all parts are from the same atlas texture for correct UV mapping.", this);
                        }

                        float invW = 1f / tex.width;
                        float invH = 1f / tex.height;
                        
                        // textureRect already contains the correct pixel coordinates in the atlas
                        p.atlasRect = new Rect(rect.x * invW, rect.y * invH, rect.width * invW, rect.height * invH);
                    }
                    else if (logWarnings)
                    {
                        Debug.LogWarning($"MAT2D: Sprite texture is null for rigPrefab part[{i}]; cannot compute atlas rect.", this);
                    }
                }
                else if (logWarnings)
                {
                    Debug.LogWarning($"MAT2D: rigPrefab part[{i}] does not have a SpriteRenderer or sprite.", this);
                }

                // Get position from transform (relative to rig root)
                // Use localPosition if the part is a direct child of rig root, otherwise use InverseTransformPoint
                Vector3 localPos;
                if (partTransform.parent == rigRoot)
                {
                    // Direct child - use localPosition (more reliable)
                    localPos = partTransform.localPosition;
                }
                else
                {
                    // Nested hierarchy - convert world position to rig root space
                    localPos = rigRoot.InverseTransformPoint(partTransform.position);
                }
                
                // The transform position represents where the sprite's pivot point is located.
                // Our mesh builder uses offsetPixels as the position where the pivot should be.
                // Since the mesh vertices are calculated as: position = (-pivot + offset)
                // We need offsetPixels to represent where the pivot point should be in character space.
                
                // Account for rig scale (if rig is scaled, positions need to be adjusted)
                Vector3 rigScale = rigRoot.lossyScale;
                if (Mathf.Abs(rigScale.x - 1f) > 0.001f || Mathf.Abs(rigScale.y - 1f) > 0.001f)
                {
                    if (logWarnings)
                    {
                        Debug.LogWarning($"MAT2D: Rig root has non-uniform scale {rigScale}. This may cause positioning issues.", this);
                    }
                }
                
                p.offsetPixels = new Vector2(localPos.x * pixelsPerUnit, localPos.y * pixelsPerUnit);

                // Store the part name and sorting order for reference
                p.name = partTransform.name;
                p.sortingOrder = sr != null ? sr.sortingOrder : 0;

                // Debug logging
                if (logWarnings)
                {
                    var sr_debug = partTransform.GetComponent<SpriteRenderer>();
                    Vector3 partScale = partTransform.localScale;
                    Vector2 spriteSize = sr_debug != null && sr_debug.sprite != null ? 
                        new Vector2(sr_debug.sprite.textureRect.width, sr_debug.sprite.textureRect.height) : Vector2.zero;
                    float calculatedZ = p.sortingOrder * zStep;
                    
                    Debug.Log($"MAT2D Part[{i}] '{p.name}':\n" +
                        $"  Sorting Order: {p.sortingOrder} (Z: {calculatedZ:F4})\n" +
                        $"  World Pos: {partTransform.position}\n" +
                        $"  Local Pos: {localPos}\n" +
                        $"  Transform Scale: ({partScale.x:F3}, {partScale.y:F3})\n" +
                        $"  Sprite Size (texture): ({spriteSize.x:F1}, {spriteSize.y:F1})\n" +
                        $"  Rig Root: {rigRoot.name} at {rigRoot.position}\n" +
                        $"  Pivot (pixels): {p.pivotPixels}\n" +
                        $"  Size (pixels): {p.sizePixels}\n" +
                        $"  Offset (pixels): {p.offsetPixels}\n" +
                        $"  Expected Bottom-Left: ({p.offsetPixels.x - p.pivotPixels.x}, {p.offsetPixels.y - p.pivotPixels.y})", this);
                }

                parts[i] = p;
            }

            if (autoAssignBaseMap && referenceTex != null && material != null)
            {
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", referenceTex);
                }
            }

            if (debugFillIfMissing)
            {
                ApplyDebugFallbackIfInvalid();
            }
        }

        void ApplySpriteData()
        {
            int count = Mathf.Min(parts.Length, partSprites != null ? partSprites.Length : 0);
            Texture2D referenceTex = null;
            bool anySprite = false;
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = partSprites[i];
                if (sprite == null)
                {
                    continue;
                }

                anySprite = true;
                Part p = parts[i];

                // Use textureRect for packed sprites (handles SpriteAtlas correctly)
                Rect rect = sprite.textureRect;
                p.sizePixels = new Vector2(rect.width, rect.height);
                
                // Important: sprite.pivot is in sprite's local space, not texture space
                // For packed sprites, we need to use it directly as it's already correct
                p.pivotPixels = sprite.pivot;

                Texture2D tex = sprite.texture;
                if (tex != null)
                {
                    if (referenceTex == null)
                    {
                        referenceTex = tex;
                    }
                    else if (logWarnings && referenceTex != tex)
                    {
                        Debug.LogWarning($"MAT2D: partSprites[{i}] uses different texture ({tex.name}) than first sprite ({referenceTex.name}). " +
                            "Ensure all sprites are from the same atlas texture for correct UV mapping.", this);
                    }

                    float invW = 1f / tex.width;
                    float invH = 1f / tex.height;
                    
                    // textureRect already contains the correct pixel coordinates in the atlas
                    // whether the sprite is packed or not
                    p.atlasRect = new Rect(rect.x * invW, rect.y * invH, rect.width * invW, rect.height * invH);
                }
                else if (logWarnings)
                {
                    Debug.LogWarning($"MAT2D: Sprite texture is null for partSprites[{i}]; cannot compute atlas rect.", this);
                }

                if (!preserveOffsets)
                {
                    p.offsetPixels = Vector2.zero;
                }

                parts[i] = p;
            }

            if (autoAssignBaseMap && referenceTex != null && material != null)
            {
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", referenceTex);
                }
            }

            if (debugFillIfMissing)
            {
                if (!anySprite)
                {
                    if (logWarnings)
                    {
                        Debug.LogWarning("MAT2D: No sprites assigned. Using debug fallback layout.", this);
                    }
                    ApplyDebugFallbackIfInvalid();
                }
                else
                {
                    ApplyDebugFallbackIfInvalid();
                }
            }
        }

        void ApplyDebugFallbackIfInvalid()
        {
            for (int i = 0; i < parts.Length; i++)
            {
                Part p = parts[i];
                bool invalidSize = p.sizePixels.x <= 0.001f || p.sizePixels.y <= 0.001f;
                bool invalidUv = p.atlasRect.width <= 0f || p.atlasRect.height <= 0f;

                if (invalidSize)
                {
                    p.sizePixels = debugSizePixels;
                    p.pivotPixels = new Vector2(debugSizePixels.x * 0.5f, debugSizePixels.y * 0.5f);
                }

                if (invalidUv)
                {
                    p.atlasRect = new Rect(0f, 0f, 1f, 1f);
                }

                if (p.offsetPixels == Vector2.zero)
                {
                    int col = i % 3;
                    int row = i / 3;
                    p.offsetPixels = new Vector2(col * (debugSizePixels.x + debugSpacingPixels.x), -row * (debugSizePixels.y + debugSpacingPixels.y));
                }

                parts[i] = p;
            }
        }
    }
}
