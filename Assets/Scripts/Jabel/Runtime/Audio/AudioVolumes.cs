using System;
using UnityEngine;

namespace Jabel.Audio
{
    public enum AudioChannel
    {
        Master = 0,
        Music = 1,
        Sfx = 2
    }

    /// <summary>
    /// Player volume settings per channel, persisted in PlayerPrefs. Values are linear slider positions (0..1);
    /// <see cref="Effective"/> applies a perceptual curve and the master volume.
    /// Static on purpose: settings menus exist outside of any game session.
    /// </summary>
    public static class AudioVolumes
    {
        private const string PrefsPrefix = "jabel.audio.";
        private static readonly string[] Keys = { "master", "music", "sfx" };
        private static readonly float[] BuiltInDefaults = { 1f, 0.6f, 0.8f };

        private static float[] _values;

        /// <summary>Raised after any channel changes.</summary>
        public static event Action Changed;

        /// <summary>
        /// Sets the defaults used for channels the player has never touched (call before the first read,
        /// e.g. from <see cref="JabelAudio"/>). Saved values always win.
        /// </summary>
        public static void SetDefaults(float master, float music, float sfx)
        {
            BuiltInDefaults[0] = Mathf.Clamp01(master);
            BuiltInDefaults[1] = Mathf.Clamp01(music);
            BuiltInDefaults[2] = Mathf.Clamp01(sfx);
            _values = null;
        }

        public static float Get(AudioChannel channel)
        {
            EnsureLoaded();
            return _values[(int)channel];
        }

        public static void Set(AudioChannel channel, float value, bool save = true)
        {
            EnsureLoaded();
            value = Mathf.Clamp01(value);
            int i = (int)channel;
            if (Mathf.Approximately(_values[i], value)) return;
            _values[i] = value;
            PlayerPrefs.SetFloat(PrefsPrefix + Keys[i], value);
            if (save) PlayerPrefs.Save();
            RaiseChanged();
        }

        /// <summary>Volume multiplier for a source on <paramref name="channel"/> (master included).</summary>
        public static float Effective(AudioChannel channel)
        {
            float master = Curve(Get(AudioChannel.Master));
            return channel == AudioChannel.Master ? master : master * Curve(Get(channel));
        }

        /// <summary>Slider position to amplitude: squared, so the middle of the slider sounds like "half as loud".</summary>
        public static float Curve(float linear) => linear * linear;

        private static void EnsureLoaded()
        {
            if (_values != null) return;
            _values = new float[Keys.Length];
            for (int i = 0; i < Keys.Length; i++)
                _values[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefsPrefix + Keys[i], BuiltInDefaults[i]));
        }

        /// <summary>A destroyed or failing listener must not stop the others (same policy as localization).</summary>
        private static void RaiseChanged()
        {
            var handlers = Changed;
            if (handlers == null) return;
            foreach (var @delegate in handlers.GetInvocationList())
            {
                var handler = (Action)@delegate;
                if (handler.Target is UnityEngine.Object unityObject && unityObject == null)
                {
                    Changed -= handler;
                    continue;
                }
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        /// <summary>Static state must not leak between play sessions when domain reload is disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Changed = null;
            _values = null;
        }
    }
}
