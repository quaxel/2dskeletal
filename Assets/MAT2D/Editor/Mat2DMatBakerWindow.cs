using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mat2D
{
    public class Mat2DMatBakerWindow : EditorWindow
    {
        GameObject _rigPrefab;
        Mat2DAnimConfig _configAsset;
        Material _targetMaterial;
        bool _assignMaterial = true;

        float _sampleFps = 30f;
        string _outputFolder = "Assets/MAT2D/Baked";
        string _outputName = "MAT2D_Baked";

        readonly List<AnimationClip> _clips = new List<AnimationClip>();
        Vector2 _scroll;

        [MenuItem("MAT2D/MAT Baker")]
        public static void Open()
        {
            GetWindow<Mat2DMatBakerWindow>("MAT2D Baker");
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Rig", EditorStyles.boldLabel);
            _rigPrefab = (GameObject)EditorGUILayout.ObjectField("Rig Prefab", _rigPrefab, typeof(GameObject), false);
            _configAsset = (Mat2DAnimConfig)EditorGUILayout.ObjectField("Anim Config", _configAsset, typeof(Mat2DAnimConfig), false);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Clips", EditorStyles.boldLabel);
            DrawClipList();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Bake Settings", EditorStyles.boldLabel);
            _sampleFps = EditorGUILayout.FloatField("Sample FPS", _sampleFps);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            _outputName = EditorGUILayout.TextField("Output Name", _outputName);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Material (Optional)", EditorStyles.boldLabel);
            _assignMaterial = EditorGUILayout.Toggle("Assign To Material", _assignMaterial);
            using (new EditorGUI.DisabledScope(!_assignMaterial))
            {
                _targetMaterial = (Material)EditorGUILayout.ObjectField("Target Material", _targetMaterial, typeof(Material), false);
            }

            EditorGUILayout.Space(10);

            using (new EditorGUI.DisabledScope(!CanBake()))
            {
                if (GUILayout.Button("Bake MAT"))
                {
                    Bake();
                }
            }

            if (!CanBake())
            {
                EditorGUILayout.HelpBox("Assign Rig Prefab and at least one AnimationClip.", MessageType.Info);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Rig Prefab must include Mat2DRigDefinition with 6 parts assigned in the same order as the mesh partIndex.",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        void DrawClipList()
        {
            int removeIndex = -1;
            for (int i = 0; i < _clips.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _clips[i] = (AnimationClip)EditorGUILayout.ObjectField(_clips[i], typeof(AnimationClip), false);
                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    removeIndex = i;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
            {
                _clips.RemoveAt(removeIndex);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Clip"))
            {
                _clips.Add(null);
            }
            if (GUILayout.Button("Add Selected Clips"))
            {
                foreach (var obj in Selection.objects)
                {
                    if (obj is AnimationClip clip && !_clips.Contains(clip))
                    {
                        _clips.Add(clip);
                    }
                }
            }
            if (GUILayout.Button("Clear"))
            {
                _clips.Clear();
            }
            EditorGUILayout.EndHorizontal();
        }

        bool CanBake()
        {
            if (_rigPrefab == null) return false;
            if (_clips.Count == 0) return false;
            for (int i = 0; i < _clips.Count; i++)
            {
                if (_clips[i] == null) return false;
            }
            return true;
        }

        void Bake()
        {
            if (!CanBake()) return;

            EnsureOutputFolder();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(_rigPrefab);
            instance.hideFlags = HideFlags.HideAndDontSave;

            var rig = instance.GetComponentInChildren<Mat2DRigDefinition>();
            if (rig == null)
            {
                DestroyImmediate(instance);
                EditorUtility.DisplayDialog("MAT2D Baker", "Rig Prefab must contain Mat2DRigDefinition.", "OK");
                return;
            }

            if (rig.parts == null || rig.parts.Length != 6)
            {
                DestroyImmediate(instance);
                EditorUtility.DisplayDialog("MAT2D Baker", "Mat2DRigDefinition must have exactly 6 parts assigned.", "OK");
                return;
            }

            for (int i = 0; i < 6; i++)
            {
                if (rig.parts[i] == null)
                {
                    DestroyImmediate(instance);
                    EditorUtility.DisplayDialog("MAT2D Baker", "Mat2DRigDefinition has missing part references.", "OK");
                    return;
                }
            }

            Transform root = rig.root != null ? rig.root : instance.transform;
            
            // Log rig root scale for diagnostics
            Debug.Log($"MAT2D Baker: Rig root '{root.name}' scale: {root.localScale}, lossyScale: {root.lossyScale}");

            var clipStarts = new List<int>();
            var clipCounts = new List<int>();

            int totalFrames = 0;
            for (int i = 0; i < _clips.Count; i++)
            {
                // Calculate frames to include both start (t=0) and end (t=length)
                // For a 1-second clip at 30 FPS: we want frames at 0, 1/30, 2/30, ..., 30/30
                // That's 31 frames total (0 through 30 inclusive)
                // Formula: floor(length * fps) + 1
                int frames = Mathf.FloorToInt(_clips[i].length * _sampleFps) + 1;
                frames = Mathf.Max(1, frames); // At least 1 frame
                
                clipStarts.Add(totalFrames);
                clipCounts.Add(frames);
                totalFrames += frames;
            }
            
            // Validate texture size limits
            const int MAX_TEXTURE_SIZE = 2048;
            if (totalFrames > MAX_TEXTURE_SIZE)
            {
                DestroyImmediate(instance);
                EditorUtility.DisplayDialog("MAT2D Baker", 
                    $"Total frames ({totalFrames}) exceeds maximum texture size ({MAX_TEXTURE_SIZE}).\n" +
                    $"Reduce clip count, clip length, or sample FPS.\n" +
                    $"Current: {_clips.Count} clips × ~{totalFrames / _clips.Count} avg frames", 
                    "OK");
                return;
            }
            
            if (totalFrames == 0)
            {
                DestroyImmediate(instance);
                EditorUtility.DisplayDialog("MAT2D Baker", "No frames to bake. Check clip lengths and sample FPS.", "OK");
                return;
            }

            var mat0 = new Texture2D(6, totalFrames, TextureFormat.RGBAHalf, false, true);
            var mat1 = new Texture2D(6, totalFrames, TextureFormat.RGBAHalf, false, true);
            mat0.name = _outputName + "_MAT0";
            mat1.name = _outputName + "_MAT1";
            mat0.filterMode = FilterMode.Point;
            mat1.filterMode = FilterMode.Point;
            mat0.wrapMode = TextureWrapMode.Clamp;
            mat1.wrapMode = TextureWrapMode.Clamp;
            mat0.anisoLevel = 0;
            mat1.anisoLevel = 0;

            var colors0 = new Color[6 * totalFrames];
            var colors1 = new Color[6 * totalFrames];

            // Capture rest pose positions and scales (before any animation is applied)
            // CRITICAL: Use the same method as mesh builder to ensure consistency
            Vector3[] restPosePositions = new Vector3[6];
            Vector3[] restPoseScales = new Vector3[6];
            for (int part = 0; part < 6; part++)
            {
                var p = rig.parts[part];
                
                // Match mesh builder's position calculation method
                Vector3 localPos;
                if (p.parent == root)
                {
                    // Direct child - use localPosition (same as mesh builder)
                    localPos = p.localPosition;
                }
                else
                {
                    // Nested hierarchy - use InverseTransformPoint (same as mesh builder)
                    localPos = root.InverseTransformPoint(p.position);
                }
                
                restPosePositions[part] = localPos;
                
                // Capture rest pose scale
                // The mesh builder includes this scale in the mesh size (sizePixels)
                // So we need to track it to calculate scale deltas during animation
                restPoseScales[part] = p.localScale;
            }

            try
            {
                AnimationMode.StartAnimationMode();

                int globalFrame = 0;
                for (int clipIndex = 0; clipIndex < _clips.Count; clipIndex++)
                {
                    var clip = _clips[clipIndex];
                    int frames = clipCounts[clipIndex];

                    Debug.Log($"MAT2D Baker: Baking clip '{clip.name}' - Length: {clip.length:F3}s, Frames: {frames}, FPS: {_sampleFps}");

                    for (int f = 0; f < frames; f++)
                    {
                        // Improved frame sampling: distribute frames evenly across clip length
                        // This ensures the last frame samples exactly at clip.length
                        float t = frames > 1 ? (f / (float)(frames - 1)) * clip.length : 0f;
                        t = Mathf.Clamp(t, 0f, clip.length);

                        if (f == 0 || f == frames - 1)
                        {
                            Debug.Log($"  Frame {f}/{frames - 1}: t = {t:F4}s");
                        }

                        AnimationMode.SampleAnimationClip(instance, clip, t);

                        // CRITICAL: Force transform updates after sampling animation
                        // This ensures parent transforms are updated before we read child transforms
                        root.GetComponentsInChildren<Transform>();

                        for (int part = 0; part < 6; part++)
                        {
                            var p = rig.parts[part];
                            
                            // CRITICAL: Use the same position calculation method as mesh builder
                            // This ensures baked positions match exactly with mesh builder
                            Vector3 pos;
                            if (p.parent == root)
                            {
                                // Direct child - use localPosition (same as mesh builder)
                                pos = p.localPosition;
                            }
                            else
                            {
                                // Nested hierarchy - use InverseTransformPoint (same as mesh builder)
                                pos = root.InverseTransformPoint(p.position);
                            }
                            
                            // CRITICAL FIX: Subtract rest pose position to get relative offset
                            pos -= restPosePositions[part];
                            
                            // CRITICAL FIX for nested parts (LArm, RArm, etc.):
                            // Use LOCAL scale and rotation, NOT world scale/rotation
                            // This prevents parent scale from affecting child parts
                            // Each part in the mesh is rendered independently, so we need local transforms
                            
                            // CRITICAL FIX for rotation:
                            // - Direct children: Use rotation relative to rig root
                            // - Nested children: Use LOCAL rotation (relative to parent)
                            // This is because in the mesh, each part is independent.
                            // If we use world rotation for nested parts, parent rotation is applied twice!
                            
                            float angle;
                            if (p.parent == root)
                            {
                                // Direct child - use rotation relative to rig root
                                Quaternion worldRot = p.rotation;
                                Quaternion rootRot = root.rotation;
                                Quaternion localToRootRot = Quaternion.Inverse(rootRot) * worldRot;
                                angle = localToRootRot.eulerAngles.z * Mathf.Deg2Rad;
                            }
                            else
                            {
                                // Nested child - use LOCAL rotation (relative to parent)
                                // The mesh doesn't have parent-child relationships, so we can't
                                // apply parent rotation in the shader
                                angle = p.localEulerAngles.z * Mathf.Deg2Rad;
                            }
                            
                            // CRITICAL FIX: Scale delta calculation
                            // The mesh builder includes REST POSE scale in the mesh size (sizePixels).
                            // But if the animation CHANGES the scale, we need to bake that change as a delta.
                            // Formula: baked_scale = animated_scale / rest_pose_scale
                            // 
                            // Example:
                            //   Rest pose scale: (2.0, 2.0) → Included in mesh
                            //   Animated scale: (3.0, 3.0) → Animation changed it
                            //   Baked scale: 3.0 / 2.0 = 1.5 → Delta to apply in shader
                            //
                            // If animation doesn't change scale:
                            //   Animated scale: (2.0, 2.0) → Same as rest
                            //   Baked scale: 2.0 / 2.0 = 1.0 → No change
                            
                            Vector3 animatedScale = p.localScale;
                            Vector3 restScale = restPoseScales[part];
                            
                            float sx = Mathf.Abs(restScale.x) > 1e-6f ? Mathf.Abs(animatedScale.x) / Mathf.Abs(restScale.x) : 1.0f;
                            float sy = Mathf.Abs(restScale.y) > 1e-6f ? Mathf.Abs(animatedScale.y) / Mathf.Abs(restScale.y) : 1.0f;

                            float s = Mathf.Sin(angle);
                            float c = Mathf.Cos(angle);

                            int idx = (globalFrame + f) * 6 + part;
                            colors0[idx] = new Color(pos.x, pos.y, s, c);
                            colors1[idx] = new Color(sx, sy, 0f, 0f);
                            
                            // Debug log for first frame to verify scale/rotation/position
                            if (f == 0 && clipIndex == 0)
                            {
                                Vector3 transformScale = p.localScale;
                                Vector3 worldScale = p.lossyScale;
                                Vector3 animatedPos = pos + restPosePositions[part];
                                bool isDirect = p.parent == root;
                                float localRotZ = p.localEulerAngles.z;
                                float worldRotZ = p.eulerAngles.z;
                                Vector3 restScaleDebug = restPoseScales[part];
                                
                                Debug.Log($"  Part[{part}] '{p.name}' ({(isDirect ? "Direct" : "Nested")}):\n" +
                                    $"    Rest Pos: ({restPosePositions[part].x:F3}, {restPosePositions[part].y:F3})\n" +
                                    $"    Anim Pos: ({animatedPos.x:F3}, {animatedPos.y:F3})\n" +
                                    $"    Delta Pos: ({pos.x:F3}, {pos.y:F3})\n" +
                                    $"    Rest Scale: ({restScaleDebug.x:F3}, {restScaleDebug.y:F3}) [In mesh]\n" +
                                    $"    Anim Scale: ({transformScale.x:F3}, {transformScale.y:F3})\n" +
                                    $"    Baked Scale: ({sx:F3}, {sy:F3}) [Delta: anim/rest]\n" +
                                    $"    World Scale: ({worldScale.x:F3}, {worldScale.y:F3})\n" +
                                    $"    Local Rotation: {localRotZ:F1}° {(isDirect ? "[Not used]" : "[Used]")}\n" +
                                    $"    World Rotation: {worldRotZ:F1}° {(isDirect ? "[Used]" : "[Not used]")}\n" +
                                    $"    Baked Angle: {angle * Mathf.Rad2Deg:F1}°");
                            }
                        }
                    }

                    globalFrame += frames;
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }

            mat0.SetPixels(colors0);
            mat1.SetPixels(colors1);
            mat0.Apply(false, false);
            mat1.Apply(false, false);

            string mat0Path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(_outputFolder, _outputName + "_MAT0.asset"));
            string mat1Path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(_outputFolder, _outputName + "_MAT1.asset"));

            AssetDatabase.CreateAsset(mat0, mat0Path);
            AssetDatabase.CreateAsset(mat1, mat1Path);

            SetImporterSettings(mat0Path);
            SetImporterSettings(mat1Path);

            if (_configAsset == null)
            {
                string configPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(_outputFolder, _outputName + "_AnimConfig.asset"));
                _configAsset = CreateInstance<Mat2DAnimConfig>();
                AssetDatabase.CreateAsset(_configAsset, configPath);
            }

            _configAsset.clipCount = _clips.Count;
            _configAsset.sampleFPS = _sampleFps;
            _configAsset.totalFrames = totalFrames;
            _configAsset.clipStartFrame = clipStarts.ToArray();
            _configAsset.clipFrameCount = clipCounts.ToArray();

            EditorUtility.SetDirty(_configAsset);
            AssetDatabase.SaveAssets();

            if (_assignMaterial && _targetMaterial != null)
            {
                _targetMaterial.SetTexture("_Mat0", mat0);
                _targetMaterial.SetTexture("_Mat1", mat1);

                float w = 6f;
                float h = totalFrames;
                _targetMaterial.SetVector("_MatTexSize", new Vector4(w, h, 1f / w, 1f / h));
                _targetMaterial.SetFloat("_SampleFPS", _sampleFps);
            }

            DestroyImmediate(instance);

            EditorUtility.DisplayDialog("MAT2D Baker", "Bake complete. MAT textures and config saved.", "OK");
        }

        void EnsureOutputFolder()
        {
            if (AssetDatabase.IsValidFolder(_outputFolder)) return;

            string parent = "Assets";
            string[] parts = _outputFolder.Replace("Assets/", "").Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                string path = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(path))
                {
                    AssetDatabase.CreateFolder(parent, parts[i]);
                }
                parent = path;
            }
        }

        void SetImporterSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterPlatformSettings
            {
                name = "Default",
                overridden = true,
                format = TextureImporterFormat.RGBAHalf,
                maxTextureSize = 2048
            };
            importer.SetPlatformTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}
