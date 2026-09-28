using Jabel.Core;
using Jabel.Localization;
using Jabel.Numbers;
using Jabel.Scripting;
using TMPro;
using UnityEngine;

namespace Jabel.UI
{
    /// <summary>
    /// Shows a variable, derived value or formula. Animates value changes: the number counts
    /// towards the new value, flashes green on increase / red on decrease and punches its scale.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Jabel/UI/Value Text")]
    public class ValueText : JabelBehaviour
    {
        public enum SourceType { Variable, Formula }

        [Header("Source")]
        [SerializeField] private SourceType source = SourceType.Variable;
        [SerializeField] private string variable = "money";
        [SerializeField] private JabelFormula formula = new JabelFormula();

        [Header("Format")]
        [Tooltip("Localization key with {0} for the number, e.g. 'ui.money' = \"${0}\". Empty = number only.")]
        [SerializeField] private LocalizedString format;
        [SerializeField] private bool useVariableFormat = true;
        [SerializeField] private NumberFormat customFormat = NumberFormat.Default;

        [Header("Animation")]
        [SerializeField] private bool countAnimation = true;
        [SerializeField] private float countDuration = 0.4f;
        [SerializeField] private bool flashOnChange = true;
        [SerializeField] private Color increaseColor = new Color(0.35f, 1f, 0.45f);
        [SerializeField] private Color decreaseColor = new Color(1f, 0.35f, 0.35f);
        [SerializeField] private float flashDuration = 0.6f;
        [SerializeField] private float punchOnIncrease = 0.12f;
        [SerializeField] private float punchOnDecrease = 0.08f;

        private TMP_Text _label;
        private Color _baseColor;
        private Vector3 _baseScale;
        private BigNumber _target, _from, _displayed;
        private float _countTime = 1;
        private Color _flashColor;
        private float _flashTime = 1;
        private Punch _punch;
        private bool _initialized;
        private string _lastText;

        public string Variable
        {
            get => variable;
            set { variable = value; source = SourceType.Variable; _initialized = false; }
        }

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            _baseColor = _label.color;
            _baseScale = transform.localScale;
        }

        protected override void OnBind()
        {
            Loc.LanguageChanged += ForceRender;
            _initialized = false;
        }

        protected override void OnUnbind() => Loc.LanguageChanged -= ForceRender;

        private void ForceRender()
        {
            _lastText = null;
            Render();
        }

        private BigNumber ReadValue()
        {
            if (source == SourceType.Formula) return formula.Evaluate(Manager);
            return Manager.Variables.Get(variable);
        }

        private NumberFormat Format
        {
            get
            {
                if (useVariableFormat && source == SourceType.Variable) return Manager.Variables.GetFormat(variable);
                return customFormat;
            }
        }

        private void Update()
        {
            if (!IsBound) return;
            float dt = Time.unscaledDeltaTime;
            var value = ReadValue();

            if (!_initialized)
            {
                _initialized = true;
                _target = _from = _displayed = value;
                _countTime = 1;
                Render();
            }
            else if (!value.ApproximatelyEquals(_target, 1e-12))
            {
                bool increased = value > _target;
                _from = _displayed;
                _target = value;
                _countTime = countAnimation ? 0 : 1;

                if (flashOnChange)
                {
                    _flashColor = increased ? increaseColor : decreaseColor;
                    _flashTime = 0;
                }
                _punch.Kick(increased ? punchOnIncrease : punchOnDecrease);
            }

            if (_countTime < 1)
            {
                _countTime = Mathf.Min(1, _countTime + dt / Mathf.Max(0.01f, countDuration));
                float k = Ease.OutCubic(_countTime);
                _displayed = _from + (_target - _from) * k;
                if (_countTime >= 1) _displayed = _target;
                Render();
            }

            if (_flashTime < 1)
            {
                _flashTime = Mathf.Min(1, _flashTime + dt / Mathf.Max(0.01f, flashDuration));
                _label.color = Color.Lerp(_flashColor, _baseColor, Ease.InOutCubic(_flashTime));
            }

            _punch.Update(dt);
            transform.localScale = _baseScale * _punch.Value;
        }

        private void Render()
        {
            string number = NumberFormatter.Format(_displayed, Format);
            string text = format.IsEmpty ? number : format.Resolve(number);
            if (text == _lastText) return;
            _lastText = text;
            _label.text = text;
        }

#if UNITY_EDITOR
        public void EditorSetup(SourceType type, string variableKey, string formulaText, string formatKey)
        {
            source = type;
            variable = variableKey;
            formula = new JabelFormula(formulaText ?? string.Empty);
            format = new LocalizedString(formatKey);
        }
#endif
    }
}
