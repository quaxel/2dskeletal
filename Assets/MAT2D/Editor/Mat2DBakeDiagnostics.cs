using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace Mat2D
{
    /// <summary>
    /// Diagnostic tool to analyze bake issues and compare animation clip with baked result.
    /// Use: Window > MAT2D > Bake Diagnostics
    /// </summary>
    public class Mat2DBakeDiagnostics : EditorWindow
    {
        GameObject _rigPrefab;
        AnimationClip _clip;
        Texture2D _mat0;
        Texture2D _mat1;
        Mat2DAnimConfig _config;
        
        Vector2 _scroll;
        string _diagnosticReport = "";

        [MenuItem("MAT2D/Bake Diagnostics")]
        public static void ShowWindow()
        {
            GetWindow<Mat2DBakeDiagnostics>("Bake Diagnostics");
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            
            EditorGUILayout.HelpBox("Analyze bake issues - compare animation clip with baked MAT textures", MessageType.Info);
            EditorGUILayout.Space(10);
            
            _rigPrefab = (GameObject)EditorGUILayout.ObjectField("Rig Prefab", _rigPrefab, typeof(GameObject), false);
            _clip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", _clip, typeof(AnimationClip), false);
            _mat0 = (Texture2D)EditorGUILayout.ObjectField("MAT0 Texture", _mat0, typeof(Texture2D), false);
            _mat1 = (Texture2D)EditorGUILayout.ObjectField("MAT1 Texture", _mat1, typeof(Texture2D), false);
            _config = (Mat2DAnimConfig)EditorGUILayout.ObjectField("Anim Config", _config, typeof(Mat2DAnimConfig), false);
            
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Run Diagnostics", GUILayout.Height(40)))
            {
                RunDiagnostics();
            }
            
            if (!string.IsNullOrEmpty(_diagnosticReport))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Diagnostic Report:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_diagnosticReport, GUILayout.ExpandHeight(true));
            }
            
            EditorGUILayout.EndScrollView();
        }

        void RunDiagnostics()
        {
            _diagnosticReport = "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            
            sb.AppendLine("=== MAT2D BAKE DIAGNOSTICS ===");
            sb.AppendLine($"Time: {System.DateTime.Now}");
            sb.AppendLine();
            
            // Check rig
            if (_rigPrefab == null)
            {
                sb.AppendLine("❌ ERROR: No rig prefab assigned!");
                _diagnosticReport = sb.ToString();
                return;
            }
            
            var rig = _rigPrefab.GetComponentInChildren<Mat2DRigDefinition>();
            if (rig == null)
            {
                sb.AppendLine("❌ ERROR: Rig prefab has no Mat2DRigDefinition component!");
                _diagnosticReport = sb.ToString();
                return;
            }
            
            sb.AppendLine("✓ RIG ANALYSIS:");
            sb.AppendLine($"  Prefab: {_rigPrefab.name}");
            sb.AppendLine($"  Root: {(rig.root != null ? rig.root.name : "NULL")}");
            sb.AppendLine($"  Parts: {(rig.parts != null ? rig.parts.Length : 0)}");
            
            if (rig.parts != null && rig.parts.Length == 6)
            {
                for (int i = 0; i < 6; i++)
                {
                    if (rig.parts[i] != null)
                    {
                        sb.AppendLine($"    Part[{i}]: {rig.parts[i].name}");
                    }
                    else
                    {
                        sb.AppendLine($"    Part[{i}]: ❌ NULL");
                    }
                }
            }
            else
            {
                sb.AppendLine($"  ❌ ERROR: Expected 6 parts, found {(rig.parts != null ? rig.parts.Length : 0)}");
            }
            sb.AppendLine();
            
            // Check animation clip
            if (_clip == null)
            {
                sb.AppendLine("⚠️ WARNING: No animation clip assigned for comparison");
            }
            else
            {
                sb.AppendLine("✓ ANIMATION CLIP ANALYSIS:");
                sb.AppendLine($"  Name: {_clip.name}");
                sb.AppendLine($"  Length: {_clip.length:F3}s");
                sb.AppendLine($"  Frame Rate: {_clip.frameRate} FPS");
                sb.AppendLine($"  Sample Rate: {GetClipSampleRate(_clip)} FPS");
                sb.AppendLine($"  Loop: {_clip.isLooping}");
                sb.AppendLine($"  Legacy: {_clip.legacy}");
                
                var bindings = AnimationUtility.GetCurveBindings(_clip);
                sb.AppendLine($"  Curve Bindings: {bindings.Length}");
                
                // Group by path
                var pathGroups = new System.Collections.Generic.Dictionary<string, int>();
                foreach (var binding in bindings)
                {
                    if (!pathGroups.ContainsKey(binding.path))
                        pathGroups[binding.path] = 0;
                    pathGroups[binding.path]++;
                }
                
                sb.AppendLine($"  Animated Paths: {pathGroups.Count}");
                foreach (var kvp in pathGroups)
                {
                    sb.AppendLine($"    '{kvp.Key}': {kvp.Value} properties");
                }
                sb.AppendLine();
                
                // Check if paths match rig parts
                sb.AppendLine("✓ PATH MATCHING:");
                bool allMatch = true;
                if (rig.parts != null)
                {
                    for (int i = 0; i < rig.parts.Length; i++)
                    {
                        if (rig.parts[i] == null) continue;
                        
                        string partPath = GetRelativePath(rig.root, rig.parts[i]);
                        bool found = false;
                        foreach (var path in pathGroups.Keys)
                        {
                            if (path == partPath || path.EndsWith("/" + rig.parts[i].name))
                            {
                                found = true;
                                break;
                            }
                        }
                        
                        if (found)
                        {
                            sb.AppendLine($"  ✓ Part[{i}] '{rig.parts[i].name}' → Found in animation");
                        }
                        else
                        {
                            sb.AppendLine($"  ❌ Part[{i}] '{rig.parts[i].name}' → NOT FOUND in animation!");
                            sb.AppendLine($"     Expected path: '{partPath}'");
                            allMatch = false;
                        }
                    }
                }
                
                if (!allMatch)
                {
                    sb.AppendLine();
                    sb.AppendLine("⚠️ CRITICAL: Some rig parts are not animated!");
                    sb.AppendLine("   This will cause incorrect baking.");
                }
                sb.AppendLine();
            }
            
            // Check MAT textures
            if (_mat0 != null && _mat1 != null)
            {
                sb.AppendLine("✓ MAT TEXTURE ANALYSIS:");
                sb.AppendLine($"  MAT0: {_mat0.name}");
                sb.AppendLine($"    Size: {_mat0.width}×{_mat0.height}");
                sb.AppendLine($"    Format: {_mat0.format}");
                sb.AppendLine($"    Filter: {_mat0.filterMode}");
                sb.AppendLine($"  MAT1: {_mat1.name}");
                sb.AppendLine($"    Size: {_mat1.width}×{_mat1.height}");
                sb.AppendLine($"    Format: {_mat1.format}");
                
                if (_mat0.width != 6)
                {
                    sb.AppendLine($"  ❌ ERROR: MAT0 width should be 6, found {_mat0.width}");
                }
                
                if (_mat0.width != _mat1.width || _mat0.height != _mat1.height)
                {
                    sb.AppendLine($"  ❌ ERROR: MAT0 and MAT1 sizes don't match!");
                }
                
                if (_clip != null && _config != null)
                {
                    float expectedFrames = _clip.length * _config.sampleFPS;
                    if (Mathf.Abs(_mat0.height - expectedFrames) > 2)
                    {
                        sb.AppendLine($"  ⚠️ WARNING: Expected ~{expectedFrames:F0} frames, texture has {_mat0.height}");
                        sb.AppendLine($"     Clip length: {_clip.length}s × {_config.sampleFPS} FPS = {expectedFrames:F1} frames");
                    }
                }
                sb.AppendLine();
            }
            
            // Check config
            if (_config != null)
            {
                sb.AppendLine("✓ ANIM CONFIG ANALYSIS:");
                sb.AppendLine($"  Clip Count: {_config.clipCount}");
                sb.AppendLine($"  Sample FPS: {_config.sampleFPS}");
                sb.AppendLine($"  Total Frames: {_config.totalFrames}");
                
                if (_clip != null)
                {
                    float clipSampleRate = GetClipSampleRate(_clip);
                    if (Mathf.Abs(clipSampleRate - _config.sampleFPS) > 0.1f)
                    {
                        sb.AppendLine($"  ⚠️ WARNING: Clip sample rate ({clipSampleRate}) != Config sample FPS ({_config.sampleFPS})");
                        sb.AppendLine($"     Animation will play at wrong speed!");
                    }
                }
                
                if (_config.clipStartFrame != null && _config.clipFrameCount != null)
                {
                    for (int i = 0; i < _config.clipCount; i++)
                    {
                        if (i < _config.clipStartFrame.Length && i < _config.clipFrameCount.Length)
                        {
                            sb.AppendLine($"  Clip[{i}]: Start={_config.clipStartFrame[i]}, Count={_config.clipFrameCount[i]}");
                        }
                    }
                }
                sb.AppendLine();
            }
            
            // Final recommendations
            sb.AppendLine("=== RECOMMENDATIONS ===");
            
            if (_clip != null && _config != null)
            {
                float clipSampleRate = GetClipSampleRate(_clip);
                if (Mathf.Abs(clipSampleRate - _config.sampleFPS) > 0.1f)
                {
                    sb.AppendLine($"1. Re-bake with Sample FPS = {clipSampleRate} (match clip sample rate)");
                }
            }
            
            if (_mat0 != null && _mat0.filterMode != FilterMode.Point)
            {
                sb.AppendLine("2. Set MAT texture filter mode to 'Point' (no filtering)");
            }
            
            if (_mat0 != null && _mat0.width != 6)
            {
                sb.AppendLine("3. MAT texture width must be exactly 6 (for 6 parts)");
            }
            
            _diagnosticReport = sb.ToString();
            Debug.Log(_diagnosticReport);
        }

        float GetClipSampleRate(AnimationClip clip)
        {
            // AnimationClip.frameRate is the sample rate
            return clip.frameRate;
        }

        string GetRelativePath(Transform root, Transform target)
        {
            if (root == null || target == null) return "";
            if (root == target) return "";
            
            string path = target.name;
            Transform current = target.parent;
            
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            
            return path;
        }
    }
}
