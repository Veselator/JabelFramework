using System;
using System.Collections.Generic;
using Jabel.Localization;
using UnityEngine;

namespace OneKMonkeys
{
    /// <summary>Effect bits of the MonkeyEffects shader (keep in sync with the FX_ defines).</summary>
    [Flags]
    public enum MonkeyFx
    {
        None = 0,
        Outline = 1 << 0,
        Glow = 1 << 1,
        Shine = 1 << 2,
        Pulse = 1 << 3,
        Wobble = 1 << 4,
        Hologram = 1 << 5,
        Rainbow = 1 << 6,
        Sparkles = 1 << 7,
        Gold = 1 << 8,
        Glitch = 1 << 9,
        Chromatic = 1 << 10,
        Electric = 1 << 11,
        Fire = 1 << 12,
        Ghost = 1 << 13,
        Galaxy = 1 << 14,
        Dissolve = 1 << 15,
        RainbowOutline = 1 << 16,
        Halo = 1 << 17,
        InnerGlow = 1 << 18
    }

    /// <summary>Visual identity of every monkey level: title, shader effects and the color range to pick from.</summary>
    [CreateAssetMenu(menuName = "1000 Monkeys/Monkey Level Table", fileName = "MonkeyLevels")]
    public class MonkeyLevelTable : ScriptableObject
    {
        [Serializable]
        public class Level
        {
            public LocalizedString title;
            public MonkeyFx effects;
            [Tooltip("Random color is picked between these two (in HSV).")]
            public Color colorA = new Color(0.55f, 0.38f, 0.25f);
            public Color colorB = new Color(0.75f, 0.55f, 0.35f);
            public Color glowColor = new Color(0.4f, 0.9f, 1f);
            [Tooltip("Idle animation energy multiplier.")]
            public float energy = 1f;
        }

        [SerializeField] private List<Level> levels = new List<Level>();

        public int Count => levels.Count;

        public Level Get(int level) => levels.Count == 0 ? new Level() : levels[Mathf.Clamp(level - 1, 0, levels.Count - 1)];

        /// <summary>Deterministic random color inside the level's range: stable per monkey across sessions.</summary>
        public Color PickColor(int level, int seed)
        {
            var data = Get(level);
            var rng = new System.Random(seed ^ (level * 7919));
            Color.RGBToHSV(data.colorA, out float h1, out float s1, out float v1);
            Color.RGBToHSV(data.colorB, out float h2, out float s2, out float v2);
            float t = (float)rng.NextDouble();
            float h = Mathf.Repeat(Mathf.LerpAngle(h1 * 360f, h2 * 360f, t) / 360f, 1f);
            float s = Mathf.Lerp(s1, s2, (float)rng.NextDouble());
            float v = Mathf.Lerp(v1, v2, (float)rng.NextDouble());
            return Color.HSVToRGB(h, s, v);
        }

#if UNITY_EDITOR
        public void EditorSetup(List<Level> list) => levels = list;
#endif
    }
}
