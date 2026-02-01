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

            var clipStarts = new List<int>();
            var clipCounts = new List<int>();

            int totalFrames = 0;
            for (int i = 0; i < _clips.Count; i++)
            {
                // Calculate frames more accurately to avoid sampling beyond clip length
                int frames = Mathf.Max(1, Mathf.CeilToInt(_clips[i].length * _sampleFps));
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

            try
            {
                AnimationMode.StartAnimationMode();

                int globalFrame = 0;
                for (int clipIndex = 0; clipIndex < _clips.Count; clipIndex++)
                {
                    var clip = _clips[clipIndex];
                    int frames = clipCounts[clipIndex];

                    for (int f = 0; f < frames; f++)
                    {
                        // Improved frame sampling: distribute frames evenly across clip length
                        // This ensures the last frame samples exactly at clip.length
                        float t = frames > 1 ? (f / (float)(frames - 1)) * clip.length : 0f;
                        t = Mathf.Clamp(t, 0f, clip.length);

                        AnimationMode.SampleAnimationClip(instance, clip, t);

                        for (int part = 0; part < 6; part++)
                        {
                            var p = rig.parts[part];
                            Matrix4x4 m = root.worldToLocalMatrix * p.localToWorldMatrix;

                            Vector3 pos = m.GetColumn(3);
                            Vector2 axisX = new Vector2(m.m00, m.m01);
                            Vector2 axisY = new Vector2(m.m10, m.m11);

                            float sx = axisX.magnitude;
                            float sy = axisY.magnitude;
                            if (sx < 1e-6f) sx = 1e-6f;
                            if (sy < 1e-6f) sy = 1e-6f;

                            float angle = Mathf.Atan2(axisX.y, axisX.x);
                            float s = Mathf.Sin(angle);
                            float c = Mathf.Cos(angle);

                            int idx = (globalFrame + f) * 6 + part;
                            colors0[idx] = new Color(pos.x, pos.y, s, c);
                            colors1[idx] = new Color(sx, sy, 0f, 0f);
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
