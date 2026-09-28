using UnityEngine;
using UnityEngine.UI;

namespace OneKMonkeys
{
    /// <summary>Two-way binding between a UI scrollbar and the camera scroller (useful with hundreds of monkeys).</summary>
    [RequireComponent(typeof(Scrollbar))]
    [AddComponentMenu("1000 Monkeys/Row Scrollbar")]
    public class RowScrollbar : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;

        private Scrollbar _bar;
        private bool _updating;

        private void Awake()
        {
            _bar = GetComponent<Scrollbar>();
            _bar.onValueChanged.AddListener(OnChanged);
        }

        private void OnChanged(float value)
        {
            if (_updating || CameraScroller.Instance == null) return;
            CameraScroller.Instance.Normalized = value;
        }

        private void LateUpdate()
        {
            var scroller = CameraScroller.Instance;
            if (scroller == null) return;
            float range = scroller.MaxX - scroller.MinX;
            float view = scroller.HalfWidth * 2;

            _updating = true;
            _bar.size = Mathf.Clamp(view / (range + view), 0.05f, 1f);
            _bar.value = scroller.Normalized;
            _updating = false;

            // Only worth showing once the row is longer than the screen.
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, range > 0.5f ? 1 : 0, Time.unscaledDeltaTime * 3);
        }
    }
}
