using Jabel.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Jabel.UI
{
    /// <summary>
    /// Modal popup (settings, dialogs): a dimming backdrop plus a panel that pops in with an overshoot.
    /// Put it on a full-screen holder; the holder itself never blocks clicks while closed.
    /// Clicking the backdrop or any close button closes it.
    /// </summary>
    [AddComponentMenu("Jabel/UI/Popup Panel")]
    public class PopupPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [Tooltip("Optional full-screen dimmer (blocks clicks behind the popup).")]
        [SerializeField] private CanvasGroup backdrop;
        [SerializeField] private Button[] closeButtons = new Button[0];
        [SerializeField] private bool closeOnBackdropClick = true;
        [SerializeField] private float duration = 0.28f;
        [SerializeField] private SoundCue openSound;
        [SerializeField] private SoundCue closeSound;

        private bool _open;
        private float _anim = 1;

        public bool IsOpen => _open;

        private void Awake()
        {
            foreach (var button in closeButtons)
                if (button != null) button.onClick.AddListener(Close);
            if (closeOnBackdropClick && backdrop != null)
            {
                var click = backdrop.GetComponent<Button>();
                if (click == null)
                {
                    click = backdrop.gameObject.AddComponent<Button>();
                    click.transition = Selectable.Transition.None;
                }
                click.onClick.AddListener(Close);
            }
            SetVisible(false);
            _open = false;
            _anim = 1;
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
            _anim = 0;
            SetVisible(true);
            JabelAudio.Play(openSound);
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _anim = 0;
            JabelAudio.Play(closeSound);
        }

        private void SetVisible(bool visible)
        {
            if (panel != null) panel.gameObject.SetActive(visible);
            if (backdrop != null) backdrop.gameObject.SetActive(visible);
        }

        private void Update()
        {
            if (_anim >= 1) return;
            _anim = Mathf.Min(1, _anim + Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
            if (backdrop != null) backdrop.alpha = _open ? Ease.OutCubic(_anim) : 1 - _anim;
            if (panel != null)
                panel.localScale = Vector3.one * (_open ? Ease.OutBack(_anim) : 1 - Ease.InCubic(_anim));
            if (!_open && _anim >= 1) SetVisible(false);
        }

#if UNITY_EDITOR
        public void EditorSetup(RectTransform panelRect, CanvasGroup backdropGroup, Button[] close, SoundCue open, SoundCue closed)
        {
            panel = panelRect;
            backdrop = backdropGroup;
            closeButtons = close;
            openSound = open;
            closeSound = closed;
        }
#endif
    }
}
