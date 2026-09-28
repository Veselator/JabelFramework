using Jabel.Core;
using Jabel.Scripting;
using UnityEngine;
using UnityEngine.UI;

namespace Jabel.UI
{
    /// <summary>
    /// Filled image driven by "current / max" formulas, with smooth filling and a flash when it wraps (level up).
    /// The image needs a sprite (Unity ignores fillAmount on sprite-less images).
    /// With <see cref="fillRemaining"/> the image covers the part still to go, e.g. a dark overlay that masks
    /// a second, differently coloured label so the text stays readable on both halves of the bar.
    /// </summary>
    [AddComponentMenu("Jabel/UI/Value Progress Bar")]
    public class ValueProgressBar : JabelBehaviour
    {
        [SerializeField] private Image fill;
        [Tooltip("The image shows the remaining part (fillAmount = 1 - progress) instead of the progress.")]
        [SerializeField] private bool fillRemaining;
        [SerializeField] private JabelFormula current = new JabelFormula("0");
        [SerializeField] private JabelFormula maximum = new JabelFormula("1");
        [SerializeField] private float smoothing = 10f;
        [SerializeField] private Image flash;
        [SerializeField] private Color flashColor = new Color(1f, 1f, 0.6f, 0.9f);

        private float _shown;
        private float _flash;

        protected override void OnBind()
        {
            _shown = Target();
            Apply();
        }

        private void Apply()
        {
            if (fill != null) fill.fillAmount = fillRemaining ? 1 - _shown : _shown;
        }

        private float Target()
        {
            var max = maximum.Evaluate(Manager, 1);
            if (max.IsZero) return 0;
            return Mathf.Clamp01((float)(current.Evaluate(Manager) / max).ToDouble());
        }

        private void Update()
        {
            if (!IsBound || fill == null) return;
            float target = Target();
            float dt = Time.unscaledDeltaTime;

            if (target + 0.05f < _shown)
            {
                // Progress wrapped around (level up): celebrate, then refill from zero.
                _shown = 0;
                _flash = 1;
            }
            _shown = Mathf.Lerp(_shown, target, 1 - Mathf.Exp(-smoothing * dt));
            Apply();

            if (flash != null)
            {
                _flash = Mathf.Max(0, _flash - dt * 1.5f);
                var c = flashColor;
                c.a *= _flash;
                flash.color = c;
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(Image fillImage, Image flashImage, string currentFormula, string maxFormula, bool remaining = false)
        {
            fillRemaining = remaining;
            fill = fillImage;
            flash = flashImage;
            current = new JabelFormula(currentFormula);
            maximum = new JabelFormula(maxFormula);
        }
#endif
    }
}
