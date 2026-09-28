using UnityEngine;

namespace Jabel.UI
{
    /// <summary>Panel that slides in from a screen edge with a fade. Hook Toggle/Open/Close to buttons.</summary>
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Jabel/UI/Slide Panel")]
    public class SlidePanel : MonoBehaviour
    {
        [SerializeField] private Vector2 hiddenOffset = new Vector2(700, 0);
        [SerializeField] private float duration = 0.3f;
        [SerializeField] private bool startOpen;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _shown;
        private bool _open;
        private float _t = 1;

        public bool IsOpen => _open;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _shown = _rect.anchoredPosition;
            _open = startOpen;
            Apply(1);
        }

        public void Toggle()
        {
            if (_open) Close();
            else Open();
        }

        public void Open()
        {
            if (_open) return;
            _open = true;
            _t = 0;
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _t = 0;
        }

        private void Update()
        {
            if (_t >= 1) return;
            _t = Mathf.Min(1, _t + Time.unscaledDeltaTime / duration);
            Apply(_t);
        }

        private void Apply(float t)
        {
            float k = _open ? Ease.OutBack(t, 1.1f) : 1 - Ease.InCubic(t);
            _rect.anchoredPosition = Vector2.LerpUnclamped(_shown + hiddenOffset, _shown, k);
            _group.alpha = Mathf.Clamp01(_open ? t * 2 : 1 - t);
            _group.blocksRaycasts = _open;
            _group.interactable = _open;
        }
    }
}
