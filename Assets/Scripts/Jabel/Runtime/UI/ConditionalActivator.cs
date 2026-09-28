using Jabel.Core;
using Jabel.Scripting;
using UnityEngine;

namespace Jabel.UI
{
    /// <summary>
    /// Shows a target while a formula is true: "count('big_screen') > 0", "level >= 5"...
    /// Lets designers wire unlockable scene content without code. Appears with a pop animation.
    /// </summary>
    [AddComponentMenu("Jabel/Conditional Activator")]
    public class ConditionalActivator : JabelBehaviour
    {
        [SerializeField] private JabelFormula condition = new JabelFormula("true");
        [SerializeField] private GameObject target;
        [SerializeField] private bool invert;
        [SerializeField] private bool animate = true;
        [SerializeField] private float checkInterval = 0.2f;

        private float _timer;
        private bool _state;
        private bool _initialized;
        private float _anim = 1;
        private Vector3 _baseScale = Vector3.one;

        protected override void OnBind()
        {
            if (target == null || target == gameObject)
            {
                Debug.LogWarning("[Jabel] ConditionalActivator needs a child target (it cannot disable itself).", this);
                return;
            }
            _baseScale = target.transform.localScale;
            Evaluate(true);
        }

        private void Evaluate(bool immediate)
        {
            if (target == null || target == gameObject) return;
            bool value = condition.Check(Manager) != invert;
            if (_initialized && value == _state) return;

            bool wasInitialized = _initialized;
            _initialized = true;
            _state = value;
            target.SetActive(value);
            if (value && animate && wasInitialized && !immediate) _anim = 0;
        }

        private void Update()
        {
            if (!IsBound) return;
            _timer += Time.unscaledDeltaTime;
            if (_timer >= checkInterval)
            {
                _timer = 0;
                Evaluate(false);
            }

            if (_anim < 1 && target != null)
            {
                _anim = Mathf.Min(1, _anim + Time.unscaledDeltaTime / 0.5f);
                target.transform.localScale = _baseScale * Ease.OutBack(_anim, 2.2f);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(string formula, GameObject targetObject)
        {
            condition = new JabelFormula(formula);
            target = targetObject;
        }
#endif
    }
}
