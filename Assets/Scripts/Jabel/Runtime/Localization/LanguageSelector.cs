using Jabel.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jabel.Localization
{
    /// <summary>"‹ Русский ›" selector for settings menus: arrows step through the languages, the label shows the native name.</summary>
    [AddComponentMenu("Jabel/Localization/Language Selector")]
    public class LanguageSelector : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [Tooltip("Clicking the label also switches to the next language.")]
        [SerializeField] private Button labelButton;
        [SerializeField] private SoundCue changeSound;

        private void Awake()
        {
            if (previousButton != null) previousButton.onClick.AddListener(() => Step(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
            if (labelButton != null) labelButton.onClick.AddListener(() => Step(1));
        }

        private void OnEnable()
        {
            Loc.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => Loc.LanguageChanged -= Refresh;

        public void Step(int step)
        {
            Loc.StepLanguage(step);
            JabelAudio.Play(changeSound);
        }

        private void Refresh()
        {
            if (label == null) return;
            var info = Loc.CurrentLanguageInfo;
            label.text = info == null ? Loc.CurrentLanguage.ToUpperInvariant()
                : (string.IsNullOrEmpty(info.nativeName) ? info.code.ToUpperInvariant() : info.nativeName);
        }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text text, Button previous, Button next, Button labelClick, SoundCue sound)
        {
            label = text;
            previousButton = previous;
            nextButton = next;
            labelButton = labelClick;
            changeSound = sound;
        }
#endif
    }
}
