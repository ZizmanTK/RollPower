using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>Creates and caches coloured materials from one Standard-shader base material.</summary>
    public class Palette
    {
        readonly Material baseMaterial;
        readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        readonly Material glowMaterial;

        /// <summary>Global emission scale: keeps saturated hues from blowing out to white under bloom + ACES.</summary>
        public static float GlowScale = 0.55f;

        public Palette(Material baseMaterial, Material glowMaterial)
        {
            this.baseMaterial = baseMaterial;
            this.glowMaterial = glowMaterial;
        }

        /// <summary>Emissive material (cloned from a base that already has the _EMISSION keyword, so builds keep the variant).</summary>
        public Material Glow(string name, Color color, float intensity = 2f, Color? albedo = null)
        {
            if (cache.TryGetValue(name, out var m)) return m;
            m = new Material(glowMaterial) { name = name };
            SetBase(m, albedo ?? color * 0.3f, 0.6f, 0f);
            m.SetColor("_EmissionColor", color * intensity * GlowScale);
            m.EnableKeyword("_EMISSION");
            cache[name] = m;
            return m;
        }

        public Material Get(string name, Color color, float smoothness = 0.3f, float metallic = 0f)
        {
            if (cache.TryGetValue(name, out var m)) return m;
            m = new Material(baseMaterial) { name = name };
            SetBase(m, color, smoothness, metallic);
            cache[name] = m;
            return m;
        }

        // Works for both URP/Lit (_BaseColor, _Smoothness) and the built-in Standard shader (_Color, _Glossiness).
        static void SetBase(Material m, Color color, float smoothness, float metallic)
        {
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }

    public static class Prim
    {
        static readonly System.Collections.Generic.Dictionary<PrimitiveType, Mesh> meshes = new System.Collections.Generic.Dictionary<PrimitiveType, Mesh>();

        /// <summary>
        /// The built-in mesh for a primitive type. Using it directly (instead of GameObject.CreatePrimitive)
        /// means no collider is ever added: the game has no physics, and WebGL strips the physics module,
        /// where CreatePrimitive logs an error for every object.
        /// </summary>
        public static Mesh MeshFor(PrimitiveType type)
        {
            if (meshes.TryGetValue(type, out var m) && m != null) return m;
            m = Resources.GetBuiltinResource<Mesh>(type + ".fbx");
            if (m == null)
            {
                var tmp = GameObject.CreatePrimitive(type);
                m = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(tmp);
            }
            meshes[type] = m;
            return m;
        }

        /// <summary>Creates a primitive-shaped object (mesh + renderer, no collider).</summary>
        public static GameObject Make(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, Quaternion? localRot = null)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = MeshFor(type);
            go.AddComponent<MeshRenderer>();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, params Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            return go;
        }
    }
}
