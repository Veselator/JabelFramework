using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jabel.Localization
{
    /// <summary>Button that cycles through the available languages and shows the current one.</summary>
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("Jabel/Localization/Language Button")]
    public class LanguageButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [Tooltip("Show the ISO code (EN/RU) instead of the native name.")]
        [SerializeField] private bool showCode = true;

        private void Awake() => GetComponent<Button>().onClick.AddListener(Loc.NextLanguage);

        private void OnEnable()
        {
            Loc.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => Loc.LanguageChanged -= Refresh;

        private void Refresh()
        {
            if (label == null) return;
            var info = Loc.CurrentLanguageInfo;
            label.text = info == null ? Loc.CurrentLanguage.ToUpperInvariant()
                : (showCode ? info.code.ToUpperInvariant() : info.nativeName);
        }
    }
}
