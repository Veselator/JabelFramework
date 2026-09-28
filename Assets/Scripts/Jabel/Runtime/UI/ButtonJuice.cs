using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jabel.UI
{
    /// <summary>
    /// Makes any UI element feel alive: grows and tilts on hover, squashes on press, punches on success,
    /// wiggles on failure, and dims when unavailable (e.g. too expensive). Works with layout groups
    /// because it only touches scale and rotation.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Jabel/UI/Button Juice")]
    public class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float hoverScale = 1.07f;
        [SerializeField] private float pressScale = 0.92f;
        [SerializeField] private float hoverTilt = 2f;
        [SerializeField] private float response = 16f;
        [Tooltip("Graphic tinted by the hover / unavailable state.")]
        [SerializeField] private Graphic tintTarget;
        [SerializeField] private Color hoverTint = new Color(1.1f, 1.1f, 1.1f, 1f);
        [SerializeField] private Color unavailableTint = new Color(0.6f, 0.6f, 0.65f, 1f);
        [Tooltip("Gentle breathing when available, to attract attention.")]
        [SerializeField] private bool pulseWhenAvailable;

        private Vector3 _baseScale = Vector3.one;
        private Color _baseColor = Color.white;
        private bool _hover, _pressed, _available = true;
        private float _scale = 1;
        private float _tilt;
        private float _shakeTime = 1;
        private Punch _punch;
        private float _pulsePhase;

        public bool Available => _available;

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (tintTarget == null) tintTarget = GetComponent<Graphic>();
            if (tintTarget != null) _baseColor = tintTarget.color;
            _pulsePhase = Random.value * 10;
        }

        private void OnDisable()
        {
            _hover = _pressed = false;
            transform.localScale = _baseScale;
            transform.localRotation = Quaternion.identity;
        }

        public void SetAvailable(bool available) => _available = available;

        /// <summary>Success feedback.</summary>
        public void Pop(float strength = 0.18f) => _punch.Kick(strength);

        /// <summary>Failure feedback ("you can't afford that").</summary>
        public void Shake() => _shakeTime = 0;

        public void OnPointerEnter(PointerEventData eventData) => _hover = true;
        public void OnPointerExit(PointerEventData eventData) { _hover = false; _pressed = false; }
        public void OnPointerDown(PointerEventData eventData) => _pressed = true;
        public void OnPointerUp(PointerEventData eventData) => _pressed = false;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float k = 1 - Mathf.Exp(-response * dt);

            float targetScale = _pressed ? pressScale : (_hover ? hoverScale : 1f);
            if (pulseWhenAvailable && _available && !_hover)
                targetScale *= 1 + 0.025f * Mathf.Sin((Time.unscaledTime + _pulsePhase) * 4f);
            _scale = Mathf.Lerp(_scale, targetScale, k);

            float targetTilt = _hover && !_pressed ? hoverTilt * Mathf.Sin(Time.unscaledTime * 3f + _pulsePhase) : 0;
            _tilt = Mathf.Lerp(_tilt, targetTilt, k);

            float shake = 0;
            if (_shakeTime < 1)
            {
                _shakeTime += dt / 0.4f;
                shake = 9f * Ease.DampedWave(_shakeTime * 0.4f, 45f, 9f);
            }

            _punch.Update(dt);
            transform.localScale = _baseScale * (_scale * _punch.Value);
            transform.localRotation = Quaternion.Euler(0, 0, _tilt + shake);

            if (tintTarget != null)
            {
                Color target = !_available ? _baseColor * unavailableTint : (_hover ? _baseColor * hoverTint : _baseColor);
                target.a = _baseColor.a;
                tintTarget.color = Color.Lerp(tintTarget.color, target, k);
            }
        }
    }
}
