using UnityEngine;
using UnityEditor;

namespace Mat2D
{
    /// <summary>
    /// Validation utility to check MAT2D system health after improvements.
    /// Use: Window > MAT2D > System Validator
    /// </summary>
    public class Mat2DSystemValidator : EditorWindow
    {
        Vector2 _scroll;
        
        [MenuItem("MAT2D/System Validator")]
        public static void ShowWindow()
        {
            GetWindow<Mat2DSystemValidator>("MAT2D Validator");
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            
            EditorGUILayout.HelpBox("MAT2D System Validator - Checks for common issues after improvements", MessageType.Info);
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Run Full Validation", GUILayout.Height(40)))
            {
                RunValidation();
            }
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quick Checks:", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Check All Mat2DAnimConfig Assets"))
            {
                CheckAnimConfigs();
            }
            
            if (GUILayout.Button("Check All Materials with MAT Textures"))
            {
                CheckMaterials();
            }
            
            if (GUILayout.Button("Check All Mat2DAnimInstance Components"))
            {
                CheckAnimInstances();
            }
            
            EditorGUILayout.EndScrollView();
        }

        void RunValidation()
        {
            Debug.Log("=== MAT2D System Validation Started ===");
            CheckAnimConfigs();
            CheckMaterials();
            CheckAnimInstances();
            Debug.Log("=== MAT2D System Validation Complete ===");
        }

        void CheckAnimConfigs()
        {
            var configs = AssetDatabase.FindAssets("t:Mat2DAnimConfig");
            Debug.Log($"Found {configs.Length} Mat2DAnimConfig assets");
            
            int issues = 0;
            foreach (var guid in configs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<Mat2DAnimConfig>(path);
                
                if (config == null) continue;
                
                // Check array sizes
                if (config.clipStartFrame == null || config.clipFrameCount == null)
                {
                    Debug.LogWarning($"Config '{config.name}' has null arrays!", config);
                    issues++;
                    continue;
                }
                
                if (config.clipCount != config.clipStartFrame.Length)
                {
                    Debug.LogWarning($"Config '{config.name}': clipCount ({config.clipCount}) != clipStartFrame.Length ({config.clipStartFrame.Length})", config);
                    issues++;
                }
                
                if (config.clipCount != config.clipFrameCount.Length)
                {
                    Debug.LogWarning($"Config '{config.name}': clipCount ({config.clipCount}) != clipFrameCount.Length ({config.clipFrameCount.Length})", config);
                    issues++;
                }
                
                // Check total frames
                if (config.totalFrames > 2048)
                {
                    Debug.LogWarning($"Config '{config.name}': totalFrames ({config.totalFrames}) exceeds recommended max (2048)", config);
                    issues++;
                }
                
                // Check clip count
                if (config.clipCount > 5)
                {
                    Debug.LogWarning($"Config '{config.name}': clipCount ({config.clipCount}) > 5. Shader only supports 5 clips (will fallback to clip 0)", config);
                }
                
                Debug.Log($"✓ Config '{config.name}': {config.clipCount} clips, {config.totalFrames} total frames, {config.sampleFPS} FPS");
            }
            
            if (issues == 0)
            {
                Debug.Log($"<color=green>✓ All {configs.Length} Mat2DAnimConfig assets are valid!</color>");
            }
            else
            {
                Debug.LogWarning($"Found {issues} issues in Mat2DAnimConfig assets");
            }
        }

        void CheckMaterials()
        {
            var materials = AssetDatabase.FindAssets("t:Material");
            int matCount = 0;
            int issues = 0;
            
            foreach (var guid in materials)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                
                if (mat == null) continue;
                if (!mat.HasProperty("_Mat0") || !mat.HasProperty("_Mat1")) continue;
                
                matCount++;
                
                var mat0 = mat.GetTexture("_Mat0") as Texture2D;
                var mat1 = mat.GetTexture("_Mat1") as Texture2D;
                
                if (mat0 == null || mat1 == null)
                {
                    Debug.LogWarning($"Material '{mat.name}' has MAT properties but missing textures", mat);
                    issues++;
                    continue;
                }
                
                if (mat0.width != 6)
                {
                    Debug.LogWarning($"Material '{mat.name}': MAT0 width is {mat0.width}, expected 6", mat);
                    issues++;
                }
                
                if (mat0.height != mat1.height)
                {
                    Debug.LogWarning($"Material '{mat.name}': MAT0 height ({mat0.height}) != MAT1 height ({mat1.height})", mat);
                    issues++;
                }
                
                if (mat0.height > 2048)
                {
                    Debug.LogWarning($"Material '{mat.name}': MAT texture height ({mat0.height}) exceeds 2048", mat);
                    issues++;
                }
                
                Debug.Log($"✓ Material '{mat.name}': MAT textures {mat0.width}x{mat0.height}");
            }
            
            if (matCount == 0)
            {
                Debug.Log("No materials with MAT textures found");
            }
            else if (issues == 0)
            {
                Debug.Log($"<color=green>✓ All {matCount} MAT materials are valid!</color>");
            }
            else
            {
                Debug.LogWarning($"Found {issues} issues in {matCount} MAT materials");
            }
        }

        void CheckAnimInstances()
        {
            var instances = FindObjectsByType<Mat2DAnimInstance>(FindObjectsSortMode.None);
            Debug.Log($"Found {instances.Length} Mat2DAnimInstance components in scene");
            
            int issues = 0;
            foreach (var instance in instances)
            {
                if (instance.targetRenderer == null)
                {
                    Debug.LogWarning($"Mat2DAnimInstance on '{instance.gameObject.name}' has no targetRenderer", instance);
                    issues++;
                    continue;
                }
                
                var mat = instance.targetRenderer.sharedMaterial;
                if (mat == null)
                {
                    Debug.LogWarning($"Mat2DAnimInstance on '{instance.gameObject.name}' has no material", instance);
                    issues++;
                    continue;
                }
                
                if (!mat.HasProperty("_AnimTime"))
                {
                    Debug.LogWarning($"Mat2DAnimInstance on '{instance.gameObject.name}' uses material '{mat.name}' which doesn't have _AnimTime property", instance);
                    issues++;
                }
                
                if (!mat.enableInstancing)
                {
                    Debug.LogWarning($"Mat2DAnimInstance on '{instance.gameObject.name}': Material '{mat.name}' doesn't have GPU instancing enabled", instance);
                }
                
                Debug.Log($"✓ Instance '{instance.gameObject.name}': animId={instance.animId}, speed={instance.animSpeed}");
            }
            
            if (instances.Length == 0)
            {
                Debug.Log("No Mat2DAnimInstance components found in scene");
            }
            else if (issues == 0)
            {
                Debug.Log($"<color=green>✓ All {instances.Length} Mat2DAnimInstance components are valid!</color>");
            }
            else
            {
                Debug.LogWarning($"Found {issues} issues in {instances.Length} Mat2DAnimInstance components");
            }
        }
    }
}
