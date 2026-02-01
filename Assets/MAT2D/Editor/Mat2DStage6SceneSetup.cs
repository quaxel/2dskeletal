using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mat2D
{
    public static class Mat2DStage6SceneSetup
    {
        const string RootName = "MAT2D_Stage6";
        const string MaterialFolder = "Assets/MAT2D/Materials";
        const string DefaultAtlasPath = "Assets/MAT2D/Baked/MAT2D_DefaultAtlas.asset";

        [MenuItem("MAT2D/Setup/Stage6 Scene")]
        public static void SetupStage6Scene()
        {
            EnsureFolder("Assets/MAT2D");
            EnsureFolder(MaterialFolder);

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("MAT2D: No valid scene open.");
                return;
            }

            var root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Create MAT2D Stage6 Root");
            }

            var character = FindOrCreateChild(root.transform, "Character");
            var meshFilter = character.GetComponent<MeshFilter>() ?? Undo.AddComponent<MeshFilter>(character);
            if (meshFilter == null) meshFilter = character.AddComponent<MeshFilter>();

            var meshRenderer = character.GetComponent<MeshRenderer>() ?? Undo.AddComponent<MeshRenderer>(character);
            if (meshRenderer == null) meshRenderer = character.AddComponent<MeshRenderer>();

            var meshBuilder = character.GetComponent<Mat2DCharacterMeshBuilder>() ?? Undo.AddComponent<Mat2DCharacterMeshBuilder>(character);
            if (meshBuilder == null) meshBuilder = character.AddComponent<Mat2DCharacterMeshBuilder>();

            meshBuilder.useStaticSharedMesh = true;
            meshBuilder.autoFillFromSprites = true;
            meshBuilder.debugFillIfMissing = true;

            var material = FindOrCreateMaterial();
            if (material == null)
            {
                Debug.LogError("MAT2D: Material creation failed. Aborting setup.");
                return;
            }
            meshRenderer.sharedMaterial = material;

            AssignAtlasAndSprites(meshBuilder, material);

            var animInstance = character.GetComponent<Mat2DAnimInstance>() ?? Undo.AddComponent<Mat2DAnimInstance>(character);
            animInstance.useGlobalTime = true;
            animInstance.animId = 0;
            animInstance.animSpeed = 1f;
            animInstance.flipX = false;

            var controller = FindOrCreateChild(root.transform, "AnimConfigBinder");
            var binder = controller.GetComponent<Mat2DAnimConfigMaterialBinder>() ?? Undo.AddComponent<Mat2DAnimConfigMaterialBinder>(controller);
            binder.material = material;

            var config = FindFirstAsset<Mat2DAnimConfig>();
            if (config != null)
            {
                binder.config = config;
                binder.Apply();
            }
            else
            {
                Debug.LogWarning("MAT2D: No Mat2DAnimConfig asset found. Bake a clip to create one.");
            }

            // If MAT0 exists but config missing, still set MatTexSize.
            var mat0 = FindTextureBySuffix("_MAT0");
            if (mat0 != null)
            {
                float w = 6f;
                float h = mat0.height;
                material.SetVector("_MatTexSize", new Vector4(w, h, 1f / w, 1f / h));
            }

            EnsureCamera();

            meshBuilder.Rebuild();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("MAT2D: Stage 6 scene setup complete.");
        }

        static Material FindOrCreateMaterial()
        {
            var shader = Shader.Find("MAT2D/UnlitAtlas_MAT5");
            if (shader == null)
            {
                Debug.LogError("MAT2D: Shader MAT2D/UnlitAtlas_MAT5 not found.");
                return null;
            }

            var mat = FindFirstAsset<Material>(m => m.shader == shader);
            if (mat != null) return mat;

            mat = new Material(shader);
            string path = AssetDatabase.GenerateUniqueAssetPath(MaterialFolder + "/MAT2D_MAT5_Mat.mat");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void AssignAtlasAndSprites(Mat2DCharacterMeshBuilder builder, Material material)
        {
            // Try to find a sprite atlas source: pick first sprite and gather 6 with same texture.
            var spriteGuids = AssetDatabase.FindAssets("t:Sprite");
            Sprite[] sprites = spriteGuids
                .Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                .ToArray();

            Texture2D atlas = null;
            if (sprites.Length > 0)
            {
                var first = sprites[0];
                atlas = first.texture;
                var sameAtlas = sprites.Where(s => s.texture == atlas).Take(6).ToArray();
                if (sameAtlas.Length == 6)
                {
                    builder.partSprites = sameAtlas;
                }
                else
                {
                    builder.partSprites = new Sprite[6];
                }
            }

            if (atlas == null)
            {
                atlas = LoadOrCreateDefaultAtlas();
            }

            if (material != null && atlas != null)
            {
                material.SetTexture("_BaseMap", atlas);
            }

            // Try to assign MAT textures if baked ones exist.
            var mat0 = FindTextureBySuffix("_MAT0");
            var mat1 = FindTextureBySuffix("_MAT1");
            if (material != null)
            {
                if (mat0 != null) material.SetTexture("_Mat0", mat0);
                if (mat1 != null) material.SetTexture("_Mat1", mat1);
            }
        }

        static Texture2D FindTextureBySuffix(string suffix)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D " + suffix);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null && tex.name.EndsWith(suffix)) return tex;
            }
            return null;
        }

        static Texture2D LoadOrCreateDefaultAtlas()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultAtlasPath);
            if (tex != null) return tex;

            EnsureFolder("Assets/MAT2D/Baked");
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            t.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            t.Apply(false, false);
            t.name = "MAT2D_DefaultAtlas";
            AssetDatabase.CreateAsset(t, DefaultAtlasPath);
            return t;
        }

        static T FindFirstAsset<T>() where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) return asset;
            }
            return null;
        }

        static T FindFirstAsset<T>(System.Func<T, bool> predicate) where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null && predicate(asset)) return asset;
            }
            return null;
        }

        static GameObject FindOrCreateChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            return go;
        }

        static void EnsureCamera()
        {
            if (Object.FindObjectOfType<Camera>() != null) return;
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.tag = "MainCamera";
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = "Assets";
            string[] parts = path.Replace("Assets/", "").Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(p))
                {
                    AssetDatabase.CreateFolder(parent, parts[i]);
                }
                parent = p;
            }
        }
    }
}
