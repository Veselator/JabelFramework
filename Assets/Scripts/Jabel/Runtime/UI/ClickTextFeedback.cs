using Jabel.Localization;
using Jabel.Numbers;
using UnityEngine;

namespace Jabel.UI
{
    /// <summary>Spawns "+value" floating text at the click point.</summary>
    [AddComponentMenu("Jabel/UI/Click Text Feedback")]
    public class ClickTextFeedback : MonoBehaviour, IClickFeedback
    {
        [SerializeField] private FloatingTextSpawner spawner;
        [Tooltip("Localization key with {0}. Empty = \"+{0}\".")]
        [SerializeField] private LocalizedString format;
        [SerializeField] private NumberFormat numberFormat = NumberFormat.Default;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private Color criticalColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private float criticalScale = 1.6f;

        public void Play(ClickFeedbackData data)
        {
            if (spawner == null) return;
            string number = NumberFormatter.Format(data.Value, numberFormat);
            string text = format.IsEmpty ? "+" + number : format.Resolve(number);
            spawner.SpawnAtScreen(text, data.ScreenPosition, data.IsCritical ? criticalColor : color, data.Camera,
                data.IsCritical ? criticalScale : 1f);
        }

#if UNITY_EDITOR
        public void EditorSetup(FloatingTextSpawner textSpawner, string formatKey, Color textColor)
        {
            spawner = textSpawner;
            format = new LocalizedString(formatKey);
            color = textColor;
        }
#endif
    }
}
