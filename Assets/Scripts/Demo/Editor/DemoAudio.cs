using Jabel.Audio;
using Jabel.Editor;
using UnityEditor;
using UnityEngine;

namespace OneKMonkeys.Editor
{
    /// <summary>
    /// Sound cues of the demo. Every kind of click has its own folder of variations under
    /// <c>Assets/Audio/SFX/&lt;Kind&gt;</c> and a cue asset next to it (random variation + pitch 0.8–1.2).
    /// Existing cues keep their tuning; drop new clips into a folder of an empty cue to fill it on the next build.
    /// </summary>
    public static class DemoAudio
    {
        public const string SfxFolder = "Assets/Audio/SFX";
        public const string MusicPath = "Assets/Audio/Music/GameMusic.ogg";
        public const float MusicVolume = 0.8f;

        public sealed class Cues
        {
            public SoundCue Typing;        // keyboard: every click on the code screen
            public SoundCue UiClick;       // mouse: default for every UI button
            public SoundCue Panel;         // light switch: opening / closing panels
            public SoundCue Hire;          // power strip: a new monkey is plugged in
            public SoundCue MonkeyUpgrade; // ratchet: tightening a monkey up a level
            public SoundCue BuyUpgrade;    // pen click: signing for an upgrade
            public SoundCue Denied;        // dull case click: can't afford / locked
            public SoundCue Language;      // controller button: switching language
            public SoundCue Slider;        // small controller clicks: volume ticks
            public SoundCue OpenComputer;  // arcade button: sitting down at the computer
            public SoundCue LevelUp;       // jingle: player level up
            public AudioClip Music;
        }

        public static Cues Create(bool overwrite)
        {
            var cues = new Cues
            {
                Typing = Cue("Typing", 0.7f, overwrite, minInterval: 0.02f, maxVoices: 6),
                UiClick = Cue("UiClick", 0.8f, overwrite),
                Panel = Cue("Panel", 0.8f, overwrite),
                Hire = Cue("Hire", 1f, overwrite),
                MonkeyUpgrade = Cue("MonkeyUpgrade", 0.9f, overwrite),
                BuyUpgrade = Cue("BuyUpgrade", 0.9f, overwrite),
                Denied = Cue("Denied", 0.9f, overwrite),
                Language = Cue("Language", 0.8f, overwrite),
                Slider = Cue("Slider", 0.6f, overwrite, minInterval: 0.05f, maxVoices: 2),
                OpenComputer = Cue("OpenComputer", 0.9f, overwrite),
                LevelUp = Cue("LevelUp", 0.9f, overwrite, minInterval: 0.3f, maxVoices: 2),
                Music = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath)
            };
            if (cues.Music == null) Debug.LogWarning("[1000 Monkeys] Music not found: " + MusicPath);
            return cues;
        }

        private static SoundCue Cue(string kind, float volume, bool overwrite, float minInterval = 0.03f, int maxVoices = 4)
        {
            string folder = SfxFolder + "/" + kind;
            string path = SfxFolder + "/" + kind + ".asset";
            bool fresh = AssetDatabase.LoadAssetAtPath<SoundCue>(path) == null;
            var cue = JabelAudioEditorUtility.CueFromFolder(folder, path, volume, overwrite);
            if (!cue.HasClips) Debug.LogWarning("[1000 Monkeys] No clips for sound cue " + kind + " in " + folder);
            // Tuning is only written for new cues (or from scratch): manual tweaks survive updates.
            if (overwrite || fresh)
            {
                cue.minInterval = minInterval;
                cue.maxVoices = maxVoices;
                EditorUtility.SetDirty(cue);
            }
            return cue;
        }
    }
}
