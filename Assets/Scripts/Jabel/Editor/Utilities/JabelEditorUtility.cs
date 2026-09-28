using System.IO;
using UnityEditor;
using UnityEngine;

namespace Jabel.Editor
{
    /// <summary>Asset and serialization helpers shared by Jabel's builders.</summary>
    public static class JabelEditorUtility
    {
        public const string GeneratedArtFolder = "Assets/Art/Jabel/Generated";

        /// <summary>Creates a folder path like "Assets/A/B/C" if it does not exist.</summary>
        public static string EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return path;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
            return path;
        }

        /// <summary>Loads an asset or creates it. Existing assets are kept so user tweaks survive rebuilds.</summary>
        public static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (asset != null) return asset;
            EnsureFolder(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Always (re)creates an asset in place, keeping its GUID so references stay valid.</summary>
        public static T CreateOrReplace<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            var fresh = ScriptableObject.CreateInstance<T>();
            if (existing != null)
            {
                EditorUtility.CopySerialized(fresh, existing);
                existing.name = Path.GetFileNameWithoutExtension(path);
                Object.DestroyImmediate(fresh);
                return existing;
            }
            EnsureFolder(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }

        /// <summary>Sets a (possibly private) serialized field by name.</summary>
        public static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Jabel] Field '{field}' not found on {target.GetType().Name}.");
                return;
            }
            switch (value)
            {
                case Object o: prop.objectReferenceValue = o; break;
                case bool b: prop.boolValue = b; break;
                case int i when prop.propertyType == SerializedPropertyType.Enum: prop.enumValueIndex = i; break;
                case int i: prop.intValue = i; break;
                case float f: prop.floatValue = f; break;
                case string s: prop.stringValue = s; break;
                case Color c: prop.colorValue = c; break;
                case Vector2 v2: prop.vector2Value = v2; break;
                case Vector3 v3: prop.vector3Value = v3; break;
                case null: prop.objectReferenceValue = null; break;
                default:
                    if (prop.propertyType == SerializedPropertyType.ManagedReference) prop.managedReferenceValue = value;
                    else if (value.GetType().IsEnum) prop.enumValueIndex = System.Convert.ToInt32(value);
                    else prop.boxedValue = value;
                    break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Soft round dot used for particles (generated once).</summary>
        public static Sprite SoftDotSprite()
        {
            string path = GeneratedArtFolder + "/SoftDot.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            EnsureFolder(GeneratedArtFolder);
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1 - d);
                a = a * a * (3 - 2 * a);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            ConfigureSpriteImporter(path, 64);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Plain white square (generated once). Filled images need a sprite for fillAmount to work.</summary>
        public static Sprite WhiteSprite()
        {
            string path = GeneratedArtFolder + "/White.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            EnsureFolder(GeneratedArtFolder);
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            ConfigureSpriteImporter(path, 100);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Rounded rectangle with a 9-slice border for panels and buttons (generated once).</summary>
        public static Sprite RoundedRectSprite()
        {
            string path = GeneratedArtFolder + "/RoundedRect.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            EnsureFolder(GeneratedArtFolder);
            const int size = 64;
            const float radius = 18f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            ConfigureSpriteImporter(path, 100, new Vector4(22, 22, 22, 22));
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ConfigureSpriteImporter(string path, float ppu, Vector4 border = default)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = ppu;
            importer.spriteBorder = border;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        /// <summary>Unlit sprite material for particle systems under URP (2D or Universal renderer).</summary>
        public static Material ParticleMaterial(string path, Sprite sprite)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            if (sprite != null) material.mainTexture = sprite.texture;
            EnsureFolder(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
