using Jabel.Core;
using Jabel.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OneKMonkeys
{
    /// <summary>The player's own desk: click it to sit down and type (opens the clicker screen).</summary>
    [AddComponentMenu("1000 Monkeys/Player Computer")]
    public class PlayerComputer : JabelBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private ScreenNavigator navigator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer screenGlow;
        [SerializeField] private Transform label;

        private Vector3 _baseScale, _labelBase;
        private float _scale = 1;
        private bool _hover;
        private Punch _punch;
        private float _glow;

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform;
            _baseScale = visualRoot.localScale;
            if (label != null) _labelBase = label.localPosition;
        }

        protected override void OnBind() => Manager.Events.Subscribe<CodeWrittenEvent>(OnWritten);
        protected override void OnUnbind() => Manager.Events.Unsubscribe<CodeWrittenEvent>(OnWritten);

        private void OnWritten(CodeWrittenEvent evt)
        {
            // Player-made code (clicks, typewriter) lights up the player's monitor.
            if (evt.Instance == null && !evt.IsOffline) _glow = 1;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (CameraScroller.Instance != null && CameraScroller.Instance.WasDragged) return;
            _punch.Kick(0.15f);
            if (navigator != null) navigator.OpenClicker();
        }

        public void OnPointerEnter(PointerEventData eventData) => _hover = true;
        public void OnPointerExit(PointerEventData eventData) => _hover = false;

        private void Update()
        {
            float dt = Time.deltaTime;
            _punch.Update(dt);
            _scale = Mathf.Lerp(_scale, _hover ? 1.06f : 1f, 1 - Mathf.Exp(-14 * dt));
            visualRoot.localScale = _baseScale * (_scale * _punch.Value);

            if (label != null)
                label.localPosition = _labelBase + Vector3.up * (Mathf.Sin(Time.time * 2.2f) * 0.08f);

            if (screenGlow != null)
            {
                _glow = Mathf.Max(0, _glow - dt * 3f);
                var c = screenGlow.color;
                c.a = 0.15f + _glow * 0.6f + (_hover ? 0.25f + Mathf.Sin(Time.time * 6f) * 0.1f : 0);
                screenGlow.color = c;
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(ScreenNavigator nav, Transform root, SpriteRenderer glow, Transform youLabel)
        {
            navigator = nav;
            visualRoot = root;
            screenGlow = glow;
            label = youLabel;
        }
#endif
    }
}
