using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Jabel.Editor
{
    /// <summary>Creates TextMeshPro font assets (and outline materials) from TTF/OTF files.</summary>
    public static class JabelFontUtility
    {
        public const string DefaultCharacters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
            "—–·«»…№€©éèêàçôû";

        /// <summary>
        /// Loads the TMP font asset generated for <paramref name="fontPath"/>, creating it on first use.
        /// Dynamic atlas (missing glyphs are added on demand), pre-filled with Latin + Cyrillic,
        /// with the TMP default font as fallback for anything the font lacks.
        /// </summary>
        public static TMP_FontAsset GetOrCreate(string fontPath, string outputFolder, int samplingSize = 72, int padding = 8)
        {
            string name = Path.GetFileNameWithoutExtension(fontPath) + " SDF";
            string assetPath = JabelEditorUtility.EnsureFolder(outputFolder) + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                Debug.LogError("[Jabel] Font not found: " + fontPath);
                return TMP_Settings.defaultFontAsset;
            }

            var asset = TMP_FontAsset.CreateFontAsset(font, samplingSize, padding, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            asset.name = name;
            AssetDatabase.CreateAsset(asset, assetPath);

            // Atlas texture and material must live inside the font asset to be saved.
            asset.atlasTextures[0].name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            if (TMP_Settings.defaultFontAsset != null)
                asset.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };

            asset.TryAddCharacters(DefaultCharacters, out _);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>Shared outline material preset for a font asset (created next to it once).</summary>
        public static Material OutlineMaterial(TMP_FontAsset font, float width = 0.2f)
        {
            string fontPath = AssetDatabase.GetAssetPath(font);
            if (string.IsNullOrEmpty(fontPath)) return null;
            string path = Path.Combine(Path.GetDirectoryName(fontPath), font.name + " - Outline.mat").Replace('\\', '/');
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(font.material) { name = font.name + " - Outline" };
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.05f, 0.05f, 0.05f, 1f));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
