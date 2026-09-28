using Jabel.Audio;
using Jabel.Core;
using Jabel.Numbers;
using Jabel.Scripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Jabel.UI
{
    public struct ClickFeedbackData
    {
        public Vector3 WorldPosition;
        public Vector2 ScreenPosition;
        public Camera Camera;
        public BigNumber Value;
        public bool IsCritical;
    }

    /// <summary>Anything that reacts visually to a processed click (particles, texts, punches).</summary>
    public interface IClickFeedback
    {
        void Play(ClickFeedbackData data);
    }

    /// <summary>
    /// Clickable surface (UI graphic, or a 2D collider with a Physics2DRaycaster on the camera).
    /// Runs the config's OnClick script (or an override) and plays every IClickFeedback on this object.
    /// </summary>
    [AddComponentMenu("Jabel/UI/Click Area")]
    public class ClickArea : JabelBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        [Tooltip("Register the click on press instead of release: feels snappier and allows fast tapping.")]
        [SerializeField] private bool clickOnPress = true;
        [Tooltip("Optional script replacing ClickerConfig.onClick for this area.")]
        [SerializeField] private JabelScript overrideScript = new JabelScript();
        [SerializeField] private float punch = 0.06f;
        [Tooltip("Played on every processed click (random variation and pitch).")]
        [SerializeField] private SoundCue clickSound;
        [SerializeField] private UnityEvent onClicked = new UnityEvent();

        private IClickFeedback[] _feedbacks;
        private ButtonJuice _juice;
        private Vector3 _baseScale;
        private Punch _punch;

        /// <summary>Optional gate (e.g. "ignore clicks while the camera is being dragged").</summary>
        public System.Func<bool> CanClick { get; set; }

        private void Awake()
        {
            _feedbacks = GetComponents<IClickFeedback>();
            _juice = GetComponent<ButtonJuice>();
            _baseScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (clickOnPress) Process(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!clickOnPress) Process(eventData);
        }

        private void Process(PointerEventData eventData)
        {
            if (!IsBound) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (CanClick != null && !CanClick()) return;

            var camera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
            if (camera == null) camera = Camera.main;

            Vector3 world = eventData.pointerCurrentRaycast.worldPosition;
            if (world == Vector3.zero && camera != null)
            {
                // Overlay canvases report no world position; project onto the object's depth.
                float depth = camera.WorldToScreenPoint(transform.position).z;
                world = camera.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, depth));
            }

            var result = Manager.Click(world, eventData.position, this, overrideScript);
            JabelAudio.Play(clickSound);
            float strength = result.IsCritical ? punch * 2.5f : punch;
            // ButtonJuice owns the scale when present; otherwise punch ourselves.
            if (_juice != null) _juice.Pop(strength);
            else _punch.Kick(strength);

            var data = new ClickFeedbackData
            {
                WorldPosition = world,
                ScreenPosition = eventData.position,
                Camera = camera,
                Value = result.Value,
                IsCritical = result.IsCritical
            };
            foreach (var feedback in _feedbacks) feedback.Play(data);
            onClicked.Invoke();
        }

        private void Update()
        {
            if (!_punch.Active) return;
            _punch.Update(Time.unscaledDeltaTime);
            transform.localScale = _baseScale * _punch.Value;
        }
    }
}
