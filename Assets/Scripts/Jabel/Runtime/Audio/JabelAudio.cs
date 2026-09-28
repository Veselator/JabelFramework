using System.Collections.Generic;
using UnityEngine;

namespace Jabel.Audio
{
    /// <summary>
    /// Plays music and sound cues through a small pool of 2D audio sources, applying the player's
    /// channel volumes (<see cref="AudioVolumes"/>). Put one in the scene to configure music and the default
    /// UI click; if none exists, the first <see cref="Play(SoundCue)"/> creates a bare one on the fly.
    /// No AudioMixer on purpose: it is not supported on WebGL.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Jabel/Audio/Jabel Audio")]
    public class JabelAudio : MonoBehaviour
    {
        [Header("Music")]
        [SerializeField] private AudioClip music;
        [Range(0, 1)] [SerializeField] private float musicVolume = 1f;
        [SerializeField] private float musicFadeIn = 1.5f;
        [SerializeField] private bool playMusicOnStart = true;

        [Header("UI")]
        [Tooltip("Played by every Button with ButtonJuice that has no sound of its own.")]
        [SerializeField] private SoundCue defaultClick;

        [Header("Default volumes (until the player changes them)")]
        [Range(0, 1)] [SerializeField] private float defaultMaster = 1f;
        [Range(0, 1)] [SerializeField] private float defaultMusic = 0.6f;
        [Range(0, 1)] [SerializeField] private float defaultSfx = 0.8f;

        [Header("Pool")]
        [SerializeField] private int voices = 16;

        private static JabelAudio _instance;

        private sealed class Voice
        {
            public AudioSource Source;
            public SoundCue Cue;
            public float BaseVolume;
            public float StartTime;
        }

        private readonly List<Voice> _voices = new List<Voice>();
        private AudioSource _music;
        private float _musicFade = 1;
        private bool _configured;

        public static JabelAudio Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<JabelAudio>();
                if (_instance == null && Application.isPlaying)
                {
                    var go = new GameObject("JabelAudio (auto)");
                    _instance = go.AddComponent<JabelAudio>();
                }
                return _instance;
            }
        }

        public static SoundCue DefaultClick => _instance != null ? _instance.defaultClick : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
            Configure();
        }

        private void Configure()
        {
            if (_configured) return;
            _configured = true;
            AudioVolumes.SetDefaults(defaultMaster, defaultMusic, defaultSfx);

            for (int i = 0; i < Mathf.Max(1, voices); i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0;
                _voices.Add(new Voice { Source = source });
            }

            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.spatialBlend = 0;
        }

        private void OnEnable() => AudioVolumes.Changed += ApplyVolumes;

        private void OnDisable() => AudioVolumes.Changed -= ApplyVolumes;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            if (playMusicOnStart && music != null) PlayMusic(music);
        }

        // ------------------------------------------------------------ API

        /// <summary>Plays a random variation of <paramref name="cue"/>. Safe to call with null.</summary>
        public static void Play(SoundCue cue)
        {
            if (cue == null || !Application.isPlaying) return;
            var audio = Instance;
            if (audio != null) audio.PlayCue(cue);
        }

        /// <summary>Plays a single clip on the effects channel (used by the "Play sound" block).</summary>
        public static void PlayClip(AudioClip clip, float volume = 1f)
        {
            if (clip == null || !Application.isPlaying) return;
            var audio = Instance;
            if (audio != null) audio.PlayOn(null, clip, volume, 1f, AudioChannel.Sfx);
        }

        public void PlayMusic(AudioClip clip, bool fadeIn = true)
        {
            Configure();
            if (clip == null) return;
            if (_music.clip == clip && _music.isPlaying) return;
            _music.clip = clip;
            _musicFade = fadeIn && musicFadeIn > 0 ? 0 : 1;
            ApplyMusicVolume();
            _music.Play();
        }

        public void StopMusic()
        {
            if (_music != null) _music.Stop();
        }

        public void PlayCue(SoundCue cue)
        {
            Configure();
            if (!cue.TryNext(Time.unscaledTime, out var clip, out float pitch, out float volume)) return;
            PlayOn(cue, clip, volume, pitch, cue.channel);
        }

        // ------------------------------------------------------------ internals

        private void PlayOn(SoundCue cue, AudioClip clip, float volume, float pitch, AudioChannel channel)
        {
            Configure();
            var voice = PickVoice(cue);
            voice.Cue = cue;
            voice.BaseVolume = volume;
            voice.StartTime = Time.unscaledTime;
            var source = voice.Source;
            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.volume = volume * AudioVolumes.Effective(channel);
            source.Play();
        }

        /// <summary>A free voice; otherwise the oldest voice of the same cue (voice limit) or the oldest overall.</summary>
        private Voice PickVoice(SoundCue cue)
        {
            Voice free = null, oldestSame = null, oldest = null;
            int sameCount = 0;
            foreach (var voice in _voices)
            {
                bool playing = voice.Source.isPlaying;
                if (!playing)
                {
                    free ??= voice;
                    continue;
                }
                if (cue != null && voice.Cue == cue)
                {
                    sameCount++;
                    if (oldestSame == null || voice.StartTime < oldestSame.StartTime) oldestSame = voice;
                }
                if (oldest == null || voice.StartTime < oldest.StartTime) oldest = voice;
            }
            if (cue != null && oldestSame != null && sameCount >= cue.maxVoices) return oldestSame;
            return free ?? oldest ?? _voices[0];
        }

        private void Update()
        {
            if (_music == null || _musicFade >= 1) return;
            _musicFade = Mathf.Min(1, _musicFade + Time.unscaledDeltaTime / Mathf.Max(0.01f, musicFadeIn));
            ApplyMusicVolume();
        }

        private void ApplyVolumes()
        {
            foreach (var voice in _voices)
                if (voice.Source.isPlaying)
                    voice.Source.volume = voice.BaseVolume * AudioVolumes.Effective(voice.Cue != null ? voice.Cue.channel : AudioChannel.Sfx);
            ApplyMusicVolume();
        }

        private void ApplyMusicVolume()
        {
            if (_music != null) _music.volume = musicVolume * _musicFade * AudioVolumes.Effective(AudioChannel.Music);
        }

#if UNITY_EDITOR
        public void EditorSetup(AudioClip musicClip, float volume, SoundCue click)
        {
            music = musicClip;
            musicVolume = volume;
            defaultClick = click;
        }
#endif
    }
}
