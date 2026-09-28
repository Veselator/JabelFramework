using Jabel.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace OneKMonkeys
{
    /// <summary>
    /// Switches between the main screen (the monkey row) and the clicker screen (the player's monitor).
    /// The clicker screen zooms in from the player's computer and fades; Escape goes back.
    /// </summary>
    [AddComponentMenu("1000 Monkeys/Screen Navigator")]
    public class ScreenNavigator : MonoBehaviour
    {
        [SerializeField] private CanvasGroup clickerScreen;
        [SerializeField] private RectTransform clickerContent;
        [SerializeField] private CanvasGroup worldOverlay;
        [SerializeField] private float duration = 0.35f;
        [Tooltip("Invoked when the clicker screen opens (e.g. close the upgrades panel and other popups).")]
        [SerializeField] private UnityEvent onClickerOpened = new UnityEvent();

        private bool _open;
        private float _t = 1;

        public bool IsClickerOpen => _open;

        private void Awake()
        {
            if (clickerScreen != null)
            {
                clickerScreen.alpha = 0;
                clickerScreen.blocksRaycasts = false;
                clickerScreen.gameObject.SetActive(false);
            }
        }

        public void OpenClicker()
        {
            if (_open || clickerScreen == null) return;
            _open = true;
            _t = 0;
            clickerScreen.gameObject.SetActive(true);
            clickerScreen.blocksRaycasts = true;
            if (CameraScroller.Instance != null) CameraScroller.Instance.Locked = true;
            onClickerOpened.Invoke();
        }

        public void CloseClicker()
        {
            if (!_open) return;
            _open = false;
            _t = 0;
            clickerScreen.blocksRaycasts = false;
            if (CameraScroller.Instance != null) CameraScroller.Instance.Locked = false;
        }

        private void Update()
        {
            if (_open && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseClicker();
            if (clickerScreen == null || _t >= 1) return;

            _t = Mathf.Min(1, _t + Time.unscaledDeltaTime / duration);
            float k = _open ? Ease.OutBack(_t, 1.2f) : 1 - Ease.InCubic(_t);
            clickerScreen.alpha = Mathf.Clamp01(_open ? _t * 1.5f : 1 - _t);
            if (clickerContent != null) clickerContent.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, k);
            if (worldOverlay != null) worldOverlay.alpha = _open ? 1 - _t : _t;
            if (!_open && _t >= 1) clickerScreen.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        public UnityEvent EditorOnClickerOpened => onClickerOpened;

        public void EditorSetup(CanvasGroup screen, RectTransform content, CanvasGroup overlay)
        {
            clickerScreen = screen;
            clickerContent = content;
            worldOverlay = overlay;
        }
#endif
    }
}
