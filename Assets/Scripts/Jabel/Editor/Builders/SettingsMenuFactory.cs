using Jabel.Audio;
using Jabel.Localization;
using Jabel.UI;
using TMPro;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Jabel.Editor
{
    /// <summary>
    /// Builds the standard settings popup (language selector + master / music / effects volume)
    /// and the gear button that opens it. Shared by the template and game builders.
    /// </summary>
    public static class SettingsMenuFactory
    {
        public const string PopupName = "SettingsPopup";
        public const string ButtonName = "SettingsButton";

        public struct Style
        {
            public Sprite PanelSprite;
            public Color PanelColor;
            public Color ButtonColor;
            public Color AccentColor;
            public Color TitleColor;

            public static Style Default => new Style
            {
                PanelSprite = JabelEditorUtility.RoundedRectSprite(),
                PanelColor = new Color(0.12f, 0.15f, 0.22f, 1f),
                ButtonColor = new Color(0.3f, 0.35f, 0.5f),
                AccentColor = new Color(0.35f, 0.85f, 0.45f),
                TitleColor = new Color(1f, 0.88f, 0.45f)
            };
        }

        public struct Sounds
        {
            public SoundCue Open;
            public SoundCue Close;
            public SoundCue Language;
            public SoundCue Slider;
        }

        private const float PanelWidth = 820;
        private const float RowHeight = 96;
        private const float LabelWidth = 270;
        private const float ControlWidth = 350;
        private const float SelectorWidth = 430;

        /// <summary>Full-screen popup under <paramref name="canvasRoot"/> (should be the last child to draw on top).</summary>
        public static PopupPanel CreatePopup(Transform canvasRoot, Style style, Sounds sounds)
        {
            var holder = JabelUIFactory.CreateRect(PopupName, canvasRoot);
            JabelUIFactory.Stretch(holder);

            var dim = JabelUIFactory.CreateImage(holder, "Backdrop", null, new Color(0, 0, 0, 0.6f));
            JabelUIFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();

            float height = 150 + RowHeight * 4 + 130;
            var panel = JabelUIFactory.CreateImage(holder, "Panel", style.PanelSprite, style.PanelColor, true);
            panel.raycastTarget = true;
            JabelUIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, height));

            var title = JabelUIFactory.CreateText(panel.transform, "Title", "Settings", 50, style.TitleColor, TextAlignmentOptions.Center, FontStyles.Bold);
            JabelUIFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(PanelWidth - 200, 70));
            title.gameObject.AddComponent<LocalizedText>().Text = "jabel.settings.title";

            var closeX = JabelUIFactory.CreateButton(panel.transform, "CloseButton", "X", style.PanelSprite, new Color(0.7f, 0.25f, 0.25f), new Vector2(76, 76), 38);
            JabelUIFactory.Place((RectTransform)closeX.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -22), new Vector2(76, 76));

            // Rows
            float y = -140;
            var language = CreateRow(panel.transform, "LanguageRow", "jabel.settings.language", "Language", y);
            CreateLanguageSelector(language, style, sounds.Language);
            y -= RowHeight;
            foreach (var (channel, key, fallback) in new[]
                     {
                         (AudioChannel.Master, "jabel.settings.master", "Master volume"),
                         (AudioChannel.Music, "jabel.settings.music", "Music"),
                         (AudioChannel.Sfx, "jabel.settings.sfx", "Sound effects")
                     })
            {
                var row = CreateRow(panel.transform, channel + "Row", key, fallback, y);
                CreateVolumeSlider(row, channel, style, sounds.Slider);
                y -= RowHeight;
            }

            var ok = JabelUIFactory.CreateButton(panel.transform, "OkButton", "OK", style.PanelSprite, Opaque(style.AccentColor * 0.8f), new Vector2(260, 84), 38);
            JabelUIFactory.Place((RectTransform)ok.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(260, 84));
            var okLabel = ok.GetComponentInChildren<TMP_Text>();
            okLabel.color = Color.white;
            okLabel.gameObject.AddComponent<LocalizedText>().Text = "jabel.settings.close";

            var popup = holder.gameObject.AddComponent<PopupPanel>();
            popup.EditorSetup(panel.rectTransform, dimGroup, new[] { closeX, ok }, sounds.Open, sounds.Close);
            return popup;
        }

        /// <summary>Gear button that toggles <paramref name="popup"/>.</summary>
        public static Button CreateButton(Transform parent, PopupPanel popup, Style style, Vector2 size, SoundCue clickSound = null)
        {
            var button = JabelUIFactory.CreateButton(parent, ButtonName, null, style.PanelSprite, style.ButtonColor, size);
            AddGearIcon(button);
            Wire(button, popup, clickSound);
            return button;
        }

        /// <summary>Gear icon filling the button (with a small margin).</summary>
        public static Image AddGearIcon(Button button)
        {
            var icon = JabelUIFactory.CreateImage(button.transform, "Icon", JabelEditorUtility.GearSprite(), Color.white);
            icon.preserveAspect = true;
            JabelUIFactory.Stretch(icon.rectTransform, 16, 16, 12, 12);
            return icon;
        }

        public static void Wire(Button button, PopupPanel popup, SoundCue clickSound)
        {
            UnityEventTools.AddPersistentListener(button.onClick, popup.Toggle);
            var juice = button.GetComponent<ButtonJuice>();
            if (juice != null && clickSound != null) JabelEditorUtility.Set(juice, "clickSound", clickSound);
        }

        private static Color Opaque(Color c)
        {
            c.a = 1;
            return c;
        }

        // ------------------------------------------------------------ rows

        private static RectTransform CreateRow(Transform panel, string name, string key, string fallback, float y)
        {
            var row = JabelUIFactory.CreateRect(name, panel);
            JabelUIFactory.Place(row, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(PanelWidth - 80, RowHeight - 16));
            var label = JabelUIFactory.CreateText(row, "Label", fallback, 34, Color.white, TextAlignmentOptions.Left);
            JabelUIFactory.Place(label.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(LabelWidth, RowHeight - 16));
            label.enableAutoSizing = true;
            label.fontSizeMin = 22;
            label.fontSizeMax = 34;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.gameObject.AddComponent<LocalizedText>().Text = key;
            return row;
        }

        private static void CreateLanguageSelector(RectTransform row, Style style, SoundCue sound)
        {
            var area = JabelUIFactory.CreateRect("LanguageSelector", row);
            JabelUIFactory.Place(area, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(SelectorWidth, 72));

            var previous = JabelUIFactory.CreateButton(area, "PreviousButton", "<", style.PanelSprite, style.ButtonColor, new Vector2(72, 72), 36);
            JabelUIFactory.Place((RectTransform)previous.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(72, 72));
            var next = JabelUIFactory.CreateButton(area, "NextButton", ">", style.PanelSprite, style.ButtonColor, new Vector2(72, 72), 36);
            JabelUIFactory.Place((RectTransform)next.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(72, 72));

            var nameButton = JabelUIFactory.CreateButton(area, "LanguageName", "English", style.PanelSprite, Opaque(style.PanelColor * 1.6f), new Vector2(SelectorWidth - 170, 72), 34);
            JabelUIFactory.Place((RectTransform)nameButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SelectorWidth - 170, 72));
            var label = nameButton.GetComponentInChildren<TMP_Text>();
            label.color = style.TitleColor;

            // The selector plays its own sound; the buttons must not add a generic click on top.
            foreach (var b in new[] { previous, next, nameButton })
                JabelEditorUtility.Set(b.GetComponent<ButtonJuice>(), "playClickSound", false);

            area.gameObject.AddComponent<LanguageSelector>().EditorSetup(label, previous, next, nameButton, sound);
        }

        private static void CreateVolumeSlider(RectTransform row, AudioChannel channel, Style style, SoundCue tick)
        {
            var slider = JabelUIFactory.CreateSlider(row, "Slider", new Vector2(ControlWidth, 44),
                new Color(0, 0, 0, 0.45f), style.AccentColor, Color.white);
            JabelUIFactory.Place((RectTransform)slider.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-100, 0), new Vector2(ControlWidth, 44));

            var value = JabelUIFactory.CreateText(row, "Value", "100%", 32, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
            JabelUIFactory.Place(value.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(90, 60));
            value.textWrappingMode = TextWrappingModes.NoWrap;

            slider.gameObject.AddComponent<VolumeSlider>().EditorSetup(channel, value, tick);
        }
    }
}
