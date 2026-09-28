using System.Collections.Generic;
using System.IO;
using Jabel.Audio;
using UnityEditor;
using UnityEngine;

namespace Jabel.Editor
{
    public static class JabelAudioEditorUtility
    {
        /// <summary>
        /// Creates (or updates) a <see cref="SoundCue"/> holding every clip found in <paramref name="clipFolder"/>.
        /// An existing cue keeps its tuning (volume, pitch...); its clip list is only filled when empty,
        /// unless <paramref name="overwrite"/> is set.
        /// </summary>
        public static SoundCue CueFromFolder(string clipFolder, string cuePath, float volume = 1f, bool overwrite = false)
        {
            var clips = LoadClips(clipFolder);
            var cue = JabelEditorUtility.LoadOrCreate<SoundCue>(cuePath, out bool created);
            if (created || overwrite)
            {
                cue.volume = volume;
                cue.pitch = new Vector2(0.8f, 1.2f);
            }
            if (created || overwrite || !cue.HasClips)
            {
                cue.clips = clips.ToArray();
                EditorUtility.SetDirty(cue);
            }
            return cue;
        }

        public static List<AudioClip> LoadClips(string folder)
        {
            var clips = new List<AudioClip>();
            if (!AssetDatabase.IsValidFolder(folder)) return clips;
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);
            foreach (var path in paths)
            {
                // Direct children only: sub-folders are other cues.
                if (Path.GetDirectoryName(path)?.Replace('\\', '/') != folder.TrimEnd('/')) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) clips.Add(clip);
            }
            return clips;
        }

        /// <summary>Assigns <paramref name="value"/> to an object reference field only when it is empty (keeps manual choices).</summary>
        public static bool SetIfEmpty(Object target, string field, Object value)
        {
            if (target == null || value == null) return false;
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[Jabel] Field '{field}' not found on {target.GetType().Name}.");
                return false;
            }
            if (prop.objectReferenceValue != null) return false;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            return true;
        }
    }
}
