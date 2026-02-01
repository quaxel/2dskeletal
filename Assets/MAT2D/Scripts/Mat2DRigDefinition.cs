using UnityEngine;

namespace Mat2D
{
    public class Mat2DRigDefinition : MonoBehaviour
    {
        [Tooltip("Optional root for root-space baking. If null, this GameObject is used.")]
        public Transform root;

        [Tooltip("Exactly 6 part transforms. Order must match mesh partIndex 0..5.")]
        public Transform[] parts = new Transform[6];

        [Header("Auto Mapping (optional)")]
        [Tooltip("Optional part names (size 6). Used to auto-resolve transforms by name or sprite name.")]
        public string[] partNames = new string[6];
        public bool matchByTransformName = true;
        public bool matchBySpriteName = true;
        public bool autoResolveOnValidate = false;
        public bool drawGizmos = true;

        void OnValidate()
        {
            if (parts == null || parts.Length != 6)
            {
                parts = new Transform[6];
            }
            if (partNames == null || partNames.Length != 6)
            {
                partNames = new string[6];
            }

            if (autoResolveOnValidate)
            {
                ResolveParts();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Fill Parts (by name or order)")]
        void AutoFillParts()
        {
            ResolveParts();
        }
#endif

        public void ResolveParts()
        {
            var children = GetComponentsInChildren<Transform>(true);
            if (children == null || children.Length == 0) return;

            // 1) Match by provided partNames (transform or sprite name)
            for (int i = 0; i < 6; i++)
            {
                if (parts[i] != null) continue;

                string name = partNames != null && i < partNames.Length ? partNames[i] : null;
                if (string.IsNullOrEmpty(name)) continue;

                Transform found = null;

                if (matchByTransformName)
                {
                    found = FindByTransformName(children, name);
                }
                if (found == null && matchBySpriteName)
                {
                    found = FindBySpriteName(children, name);
                }

                if (found != null)
                {
                    parts[i] = found;
                }
            }

            // 2) Match by name pattern part0..part5 if still missing
            for (int i = 0; i < 6; i++)
            {
                if (parts[i] != null) continue;
                string key = "part" + i;
                var found = FindByTransformNameContains(children, key);
                if (found != null) parts[i] = found;
            }

            // 3) Fallback: alphabetical order (deterministic)
            bool anyMissing = false;
            for (int i = 0; i < 6; i++)
            {
                if (parts[i] == null) { anyMissing = true; break; }
            }
            if (!anyMissing) return;

            System.Array.Sort(children, (a, b) => string.CompareOrdinal(a.name, b.name));
            int idx = 0;
            for (int c = 0; c < children.Length && idx < 6; c++)
            {
                var t = children[c];
                if (t == transform) continue;
                if (parts[idx] != null) { idx++; c--; continue; }
                parts[idx++] = t;
            }
        }

        static Transform FindByTransformName(Transform[] children, string name)
        {
            for (int i = 0; i < children.Length; i++)
            {
                var t = children[i];
                if (t == null) continue;
                if (t.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }
            return null;
        }

        static Transform FindByTransformNameContains(Transform[] children, string key)
        {
            for (int i = 0; i < children.Length; i++)
            {
                var t = children[i];
                if (t == null) continue;
                if (t.name.ToLowerInvariant().Contains(key))
                {
                    return t;
                }
            }
            return null;
        }

        static Transform FindBySpriteName(Transform[] children, string name)
        {
            for (int i = 0; i < children.Length; i++)
            {
                var t = children[i];
                if (t == null) continue;
                var sr = t.GetComponent<SpriteRenderer>();
                if (sr == null || sr.sprite == null) continue;
                if (sr.sprite.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }
            return null;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!drawGizmos || parts == null) return;
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p == null) continue;
                UnityEditor.Handles.Label(p.position, "P" + i);
            }
        }
#endif
    }
}
