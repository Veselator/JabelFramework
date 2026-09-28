using UnityEditor;
using UnityEngine;

namespace Jabel.Editor
{
    /// <summary>
    /// Web-friendly import settings, applied automatically by folder:
    /// <c>.../Audio/SFX/...</c> — short clicks: mono, decompressed on load (no decode latency), compressed;
    /// <c>.../Audio/Music/...</c> — stereo, streamed / compressed in memory, lower quality.
    /// Only applied when a clip is imported for the first time, so hand-tuned settings survive.
    /// </summary>
    public class JabelAudioImporter : AssetPostprocessor
    {
        public const float SfxQuality = 0.55f;
        public const float MusicQuality = 0.4f;

        private void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            bool sfx = path.Contains("/Audio/SFX/");
            bool music = path.Contains("/Audio/Music/");
            if (!sfx && !music) return;

            var importer = (AudioImporter)assetImporter;
            // importSettingsMissing: first import only (no .meta yet), never overrides manual changes.
            if (!importer.importSettingsMissing) return;
            Apply(importer, music);
        }

        public static void Apply(AudioImporter importer, bool music)
        {
            importer.forceToMono = !music;
            importer.loadInBackground = music;

            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? MusicQuality : SfxQuality;
            settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            settings.preloadAudioData = !music;
            importer.defaultSampleSettings = settings;

            // WebGL always ships AAC; only quality matters there.
            var web = settings;
            web.compressionFormat = AudioCompressionFormat.AAC;
            web.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            importer.SetOverrideSampleSettings("WebGL", web);
        }

        /// <summary>Re-applies the defaults to every clip under Assets/Audio (overwrites manual import settings).</summary>
        [MenuItem("Tools/Jabel/Audio/Reapply Web Import Settings")]
        private static void ReapplyAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                bool sfx = path.Contains("/Audio/SFX/");
                bool music = path.Contains("/Audio/Music/");
                if (!sfx && !music) continue;
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                Apply(importer, music);
                importer.SaveAndReimport();
            }
            Debug.Log("[Jabel] Audio import settings reapplied.");
        }
    }
}
