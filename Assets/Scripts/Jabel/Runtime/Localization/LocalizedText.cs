using TMPro;
using UnityEngine;

namespace Jabel.Localization
{
    /// <summary>Puts a translated string into a TextMeshPro component and keeps it updated on language change.</summary>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Jabel/Localization/Localized Text")]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private LocalizedString text;
        [Tooltip("Swap the font when the language defines an override.")]
        [SerializeField] private bool applyFontOverride = true;

        private TMP_Text _label;
        private TMP_FontAsset _defaultFont;
        private object[] _args;

        public LocalizedString Text
        {
            get => text;
            set { text = value; Refresh(); }
        }

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            _defaultFont = _label.font;
        }

        private void OnEnable()
        {
            Loc.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => Loc.LanguageChanged -= Refresh;

        /// <summary>Sets format arguments for keys like "Level {0}".</summary>
        public void SetArguments(params object[] args)
        {
            _args = args;
            Refresh();
        }

        public void Refresh()
        {
            if (_label == null) return;
            if (!Loc.IsReady && !text.IsEmpty)
            {
                // Keep the design-time text until localization is initialized.
                return;
            }
            _label.text = _args != null ? text.Resolve(_args) : text.Resolve();

            if (applyFontOverride)
            {
                var info = Loc.CurrentLanguageInfo;
                _label.font = info?.fontOverride != null ? info.fontOverride : _defaultFont;
            }
        }
    }
}
