using UnityEngine;
using UnityEditor;

namespace Mat2D
{
    public class Mat2DPositionDiagnostics : EditorWindow
    {
        GameObject _rigPrefab;
        Mat2DCharacterMeshBuilder _meshBuilder;
        float _pixelsPerUnit = 100f;

        [MenuItem("MAT2D/Position Diagnostics")]
        public static void Open()
        {
            GetWindow<Mat2DPositionDiagnostics>("Position Diagnostics");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Position Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Compare positions between Rig Prefab, Mesh Builder, and expected Baking values.",
                MessageType.Info);

            EditorGUILayout.Space();
            _rigPrefab = (GameObject)EditorGUILayout.ObjectField("Rig Prefab", _rigPrefab, typeof(GameObject), false);
            _meshBuilder = (Mat2DCharacterMeshBuilder)EditorGUILayout.ObjectField("Mesh Builder", _meshBuilder, typeof(Mat2DCharacterMeshBuilder), true);
            _pixelsPerUnit = EditorGUILayout.FloatField("Pixels Per Unit", _pixelsPerUnit);

            EditorGUILayout.Space();

            if (GUILayout.Button("Analyze Positions"))
            {
                AnalyzePositions();
            }
        }

        void AnalyzePositions()
        {
            if (_rigPrefab == null)
            {
                Debug.LogError("Please assign a Rig Prefab.");
                return;
            }

            var rigDef = _rigPrefab.GetComponent<Mat2DRigDefinition>();
            if (rigDef == null)
            {
                Debug.LogError("Rig Prefab must have Mat2DRigDefinition component.");
                return;
            }

            if (rigDef.parts == null || rigDef.parts.Length != 6)
            {
                Debug.LogError("Rig must have exactly 6 parts.");
                return;
            }

            Transform root = rigDef.root != null ? rigDef.root : rigDef.transform;
            float invPpu = 1f / _pixelsPerUnit;

            Debug.Log("=== MAT2D Position Diagnostics ===\n");

            for (int i = 0; i < 6; i++)
            {
                var part = rigDef.parts[i];
                if (part == null) continue;

                // Calculate position using both methods
                Vector3 localPosMethod1, localPosMethod2;

                // Method 1: localPosition / InverseTransformPoint (Mesh Builder method)
                if (part.parent == root)
                {
                    localPosMethod1 = part.localPosition;
                }
                else
                {
                    localPosMethod1 = root.InverseTransformPoint(part.position);
                }

                // Method 2: Matrix (Old baking method)
                Matrix4x4 m = root.worldToLocalMatrix * part.localToWorldMatrix;
                localPosMethod2 = m.GetColumn(3);

                // Get sprite data
                var sr = part.GetComponent<SpriteRenderer>();
                Vector2 pivotPixels = sr != null && sr.sprite != null ? sr.sprite.pivot : Vector2.zero;
                Vector2 sizePixels = sr != null && sr.sprite != null ? new Vector2(sr.sprite.textureRect.width, sr.sprite.textureRect.height) : Vector2.zero;

                // Mesh Builder calculations
                Vector2 offsetPixels = new Vector2(localPosMethod1.x * _pixelsPerUnit, localPosMethod1.y * _pixelsPerUnit);
                Vector2 offset = offsetPixels * invPpu;
                Vector2 pivot = pivotPixels * invPpu;
                Vector2 meshVertexBL = new Vector2(-pivot.x, -pivot.y) + offset;

                // Baking calculations (expected)
                Vector3 bakingPos = localPosMethod1;  // Unity units
                Vector3 bakingDelta = Vector3.zero;  // Delta from rest (0 for rest pose)

                // Shader final position (expected)
                Vector2 shaderPos = meshVertexBL + new Vector2(bakingDelta.x, bakingDelta.y);

                bool isDirect = part.parent == root;
                bool methodsMatch = Vector3.Distance(localPosMethod1, localPosMethod2) < 0.001f;

                Debug.Log($"Part[{i}] '{part.name}' ({(isDirect ? "Direct" : "Nested")}):\n" +
                    $"  Method 1 (Mesh Builder): ({localPosMethod1.x:F4}, {localPosMethod1.y:F4})\n" +
                    $"  Method 2 (Old Baking):   ({localPosMethod2.x:F4}, {localPosMethod2.y:F4})\n" +
                    $"  Methods Match: {(methodsMatch ? "YES ✓" : "NO ✗ PROBLEM!")}\n" +
                    $"  Pivot (pixels): ({pivotPixels.x:F1}, {pivotPixels.y:F1})\n" +
                    $"  Size (pixels): ({sizePixels.x:F1}, {sizePixels.y:F1})\n" +
                    $"  Offset (pixels): ({offsetPixels.x:F1}, {offsetPixels.y:F1})\n" +
                    $"  Offset (units): ({offset.x:F4}, {offset.y:F4})\n" +
                    $"  Pivot (units): ({pivot.x:F4}, {pivot.y:F4})\n" +
                    $"  Mesh Vertex BL: ({meshVertexBL.x:F4}, {meshVertexBL.y:F4})\n" +
                    $"  Baking Pos (units): ({bakingPos.x:F4}, {bakingPos.y:F4})\n" +
                    $"  Baking Delta: ({bakingDelta.x:F4}, {bakingDelta.y:F4})\n" +
                    $"  Shader Final Pos: ({shaderPos.x:F4}, {shaderPos.y:F4})\n");
            }

            // Compare with Mesh Builder if assigned
            if (_meshBuilder != null && _meshBuilder.parts != null && _meshBuilder.parts.Length == 6)
            {
                Debug.Log("\n=== Mesh Builder Comparison ===\n");
                for (int i = 0; i < 6; i++)
                {
                    var mbPart = _meshBuilder.parts[i];
                    var rigPart = rigDef.parts[i];
                    if (rigPart == null) continue;

                    Vector3 localPos;
                    if (rigPart.parent == root)
                    {
                        localPos = rigPart.localPosition;
                    }
                    else
                    {
                        localPos = root.InverseTransformPoint(rigPart.position);
                    }

                    Vector2 expectedOffsetPixels = new Vector2(localPos.x * _pixelsPerUnit, localPos.y * _pixelsPerUnit);
                    Vector2 actualOffsetPixels = mbPart.offsetPixels;
                    bool offsetMatches = Vector2.Distance(expectedOffsetPixels, actualOffsetPixels) < 1f;

                    Debug.Log($"Part[{i}] '{mbPart.name}':\n" +
                        $"  Expected Offset: ({expectedOffsetPixels.x:F1}, {expectedOffsetPixels.y:F1})\n" +
                        $"  Actual Offset:   ({actualOffsetPixels.x:F1}, {actualOffsetPixels.y:F1})\n" +
                        $"  Match: {(offsetMatches ? "YES ✓" : "NO ✗ PROBLEM!")}\n");
                }
            }

            Debug.Log("\n=== Diagnostics Complete ===");
        }
    }
}
