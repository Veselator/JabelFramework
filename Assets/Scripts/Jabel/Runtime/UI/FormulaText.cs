using System.Collections.Generic;
using Jabel.Core;
using Jabel.Localization;
using Jabel.Numbers;
using Jabel.Scripting;
using TMPro;
using UnityEngine;

namespace Jabel.UI
{
    /// <summary>
    /// Text with several live numbers: "Lines {0} / {1}", "{0} / 1000 monkeys".
    /// Each formula fills one placeholder of a localized format. Punches when the text changes.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Jabel/UI/Formula Text")]
    public class FormulaText : JabelBehaviour
    {
        [SerializeField] private LocalizedString format;
        [SerializeField] private List<JabelFormula> values = new List<JabelFormula>();
        [SerializeField] private NumberFormat numberFormat = NumberFormat.Default;
        [SerializeField] private float refreshInterval = 0.1f;
        [SerializeField] private float punch = 0.1f;

        private TMP_Text _label;
        private object[] _args;
        private string _last;
        private float _timer;
        private Punch _punch;
        private Vector3 _baseScale;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            _baseScale = transform.localScale;
        }

        protected override void OnBind()
        {
            Loc.LanguageChanged += ForceRefresh;
            ForceRefresh();
        }

        protected override void OnUnbind() => Loc.LanguageChanged -= ForceRefresh;

        private void ForceRefresh()
        {
            _last = null;
            Refresh(false);
        }

        private void Refresh(bool animate)
        {
            if (_args == null || _args.Length != values.Count) _args = new object[values.Count];
            for (int i = 0; i < values.Count; i++) _args[i] = NumberFormatter.Format(values[i].Evaluate(Manager), numberFormat);
            string text = format.Resolve(_args);
            if (text == _last) return;
            if (animate && _last != null) _punch.Kick(punch);
            _last = text;
            _label.text = text;
        }

        private void Update()
        {
            if (!IsBound) return;
            _timer += Time.unscaledDeltaTime;
            if (_timer >= refreshInterval)
            {
                _timer = 0;
                Refresh(true);
            }
            _punch.Update(Time.unscaledDeltaTime);
            transform.localScale = _baseScale * _punch.Value;
        }

#if UNITY_EDITOR
        public void EditorSetup(string formatKey, params string[] formulas)
        {
            format = new LocalizedString(formatKey);
            values = new List<JabelFormula>();
            foreach (var f in formulas) values.Add(new JabelFormula(f));
        }
#endif
    }
}
