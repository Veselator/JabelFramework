using Jabel.Audio;
using UnityEditor;
using UnityEngine;

namespace Jabel.Editor
{
    /// <summary>Sound Cue inspector with a preview button (random variation and pitch, like in the game).</summary>
    [CustomEditor(typeof(SoundCue))]
    public class SoundCueEditor : UnityEditor.Editor
    {
        private static AudioSource _preview;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var cue = (SoundCue)target;
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!cue.HasClips))
            {
                if (GUILayout.Button("▶ Preview random variation", GUILayout.Height(28))) Preview(cue);
            }
            if (!cue.HasClips) EditorGUILayout.HelpBox("No clips assigned.", MessageType.Info);
        }

        private static void Preview(SoundCue cue)
        {
            if (!cue.TryNext((float)EditorApplication.timeSinceStartup, out var clip, out float pitch, out float volume)) return;
            if (_preview == null)
            {
                var go = EditorUtility.CreateGameObjectWithHideFlags("SoundCuePreview", HideFlags.HideAndDontSave, typeof(AudioSource));
                _preview = go.GetComponent<AudioSource>();
                _preview.playOnAwake = false;
            }
            _preview.Stop();
            _preview.clip = clip;
            _preview.pitch = pitch;
            _preview.volume = volume;
            _preview.Play();
        }
    }
}
