using System;
using UnityEngine;

namespace Jabel.Audio
{
    /// <summary>
    /// One kind of sound (e.g. "button click") with several variations. Each play picks a random clip
    /// (never the same one twice in a row) and a random pitch, so repeated clicks never sound mechanical.
    /// Play it with <see cref="JabelAudio.Play(SoundCue)"/> or <see cref="Play"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Jabel/Sound Cue", fileName = "SoundCue")]
    public class SoundCue : ScriptableObject
    {
        [Tooltip("Variations; one is chosen at random on every play.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0, 1)] public float volume = 1f;
        [Tooltip("Random pitch range (min, max).")]
        public Vector2 pitch = new Vector2(0.8f, 1.2f);
        [Tooltip("Random volume reduction, 0 = always full volume.")]
        [Range(0, 0.5f)] public float volumeVariation;
        public AudioChannel channel = AudioChannel.Sfx;
        [Tooltip("Never pick the same variation twice in a row.")]
        public bool avoidRepeat = true;
        [Tooltip("Plays closer together than this (seconds) are skipped, so bursts do not stack up into noise.")]
        public float minInterval = 0.03f;
        [Tooltip("How many plays of this cue may overlap; the oldest one is cut when exceeded.")]
        [Min(1)] public int maxVoices = 4;

        [NonSerialized] private int _lastIndex = -1;
        [NonSerialized] private float _lastTime = float.NegativeInfinity;

        public bool HasClips
        {
            get
            {
                if (clips == null) return false;
                foreach (var clip in clips)
                    if (clip != null) return true;
                return false;
            }
        }

        public void Play() => JabelAudio.Play(this);

        /// <summary>Picks the next variation and pitch; false when throttled or empty.</summary>
        public bool TryNext(float now, out AudioClip clip, out float pitchValue, out float volumeValue)
        {
            clip = null;
            pitchValue = 1;
            volumeValue = volume;
            if (!HasClips) return false;

            // Time restarts with every play session (domain reload may be disabled).
            if (now < _lastTime) _lastTime = float.NegativeInfinity;
            if (now - _lastTime < minInterval) return false;
            _lastTime = now;

            int count = clips.Length;
            int index = UnityEngine.Random.Range(0, count);
            if (avoidRepeat && count > 1 && index == _lastIndex) index = (index + 1 + UnityEngine.Random.Range(0, count - 1)) % count;
            // Skip empty slots.
            for (int i = 0; i < count && clips[index] == null; i++) index = (index + 1) % count;
            _lastIndex = index;
            clip = clips[index];

            float min = Mathf.Min(pitch.x, pitch.y), max = Mathf.Max(pitch.x, pitch.y);
            pitchValue = Mathf.Max(0.05f, UnityEngine.Random.Range(min, max));
            volumeValue = volume * (1 - UnityEngine.Random.Range(0, volumeVariation));
            return true;
        }
    }
}
