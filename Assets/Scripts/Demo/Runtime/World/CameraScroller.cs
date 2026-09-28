using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OneKMonkeys
{
    /// <summary>
    /// Horizontal camera for the monkey row: drag with inertia, mouse wheel, keyboard,
    /// soft edges and programmatic focus. Drags never start on UI, and a drag suppresses the click.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("1000 Monkeys/Camera Scroller")]
    public class CameraScroller : MonoBehaviour
    {
        [SerializeField] private float minX;
        [SerializeField] private float maxX = 10;
        [SerializeField] private float dragThresholdPixels = 12f;
        [SerializeField] private float wheelSpeed = 0.02f;
        [SerializeField] private float keySpeed = 14f;
        [SerializeField] private float inertiaDamping = 4f;

        public static CameraScroller Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        /// <summary>True if the current/last press turned into a drag (click handlers should ignore it).</summary>
        public bool WasDragged { get; private set; }
        public bool Locked { get; set; }
        public float MinX => minX;
        public float MaxX => maxX;
        public float X => transform.position.x;
        public float HalfWidth => _camera.orthographicSize * _camera.aspect;

        private Camera _camera;
        private bool _pressing;
        private Vector2 _pressStart;
        private Vector2 _lastPointer;
        private float _velocity;
        private float? _focusTarget;
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private PointerEventData _probe;

        private void Awake()
        {
            Instance = this;
            _camera = GetComponent<Camera>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetBounds(float min, float max)
        {
            minX = min;
            maxX = Mathf.Max(min, max);
        }

        /// <summary>Smoothly moves the camera so <paramref name="x"/> is centered.</summary>
        public void Focus(float x) => _focusTarget = Mathf.Clamp(x, minX, maxX);

        /// <summary>Moves only if <paramref name="x"/> is outside the visible area.</summary>
        public void EnsureVisible(float x, float margin = 2f)
        {
            float half = HalfWidth - margin;
            if (x > X + half) Focus(x - half);
            else if (x < X - half) Focus(x + half);
        }

        private bool PointerOverUI(Vector2 position)
        {
            var system = EventSystem.current;
            if (system == null) return false;
            _probe ??= new PointerEventData(system);
            _probe.position = position;
            _hits.Clear();
            system.RaycastAll(_probe, _hits);
            foreach (var hit in _hits)
                if (hit.module is GraphicRaycaster) return true;
            return false;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float x = transform.position.x;
            float worldPerPixel = _camera.orthographicSize * 2f / Screen.height;

            var pointer = Pointer.current;
            if (!Locked && pointer != null)
            {
                Vector2 pos = pointer.position.ReadValue();
                if (pointer.press.wasPressedThisFrame)
                {
                    WasDragged = false;
                    _pressing = !PointerOverUI(pos);
                    _pressStart = _lastPointer = pos;
                    _velocity = 0;
                }
                else if (_pressing && pointer.press.isPressed)
                {
                    if (!WasDragged && Vector2.Distance(pos, _pressStart) > dragThresholdPixels)
                    {
                        WasDragged = true;
                        _focusTarget = null;
                    }
                    if (WasDragged)
                    {
                        float delta = -(pos.x - _lastPointer.x) * worldPerPixel;
                        x += delta;
                        _velocity = Mathf.Lerp(_velocity, delta / Mathf.Max(dt, 0.001f), 0.5f);
                    }
                    _lastPointer = pos;
                }
                else if (pointer.press.wasReleasedThisFrame)
                {
                    _pressing = false;
                }
            }

            if (!Locked)
            {
                var mouse = Mouse.current;
                if (mouse != null && !_pressing)
                {
                    float wheel = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(wheel) > 0.01f && !PointerOverUI(mouse.position.ReadValue()))
                    {
                        _velocity = 0;
                        _focusTarget = null;
                        x -= wheel * wheelSpeed;
                    }
                }

                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    float dir = 0;
                    if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) dir -= 1;
                    if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) dir += 1;
                    if (dir != 0)
                    {
                        _focusTarget = null;
                        _velocity = dir * keySpeed;
                    }
                    if (keyboard.homeKey.wasPressedThisFrame) Focus(minX);
                    if (keyboard.endKey.wasPressedThisFrame) Focus(maxX);
                }
            }

            if (_focusTarget.HasValue)
            {
                x = Mathf.Lerp(x, _focusTarget.Value, 1 - Mathf.Exp(-6f * dt));
                if (Mathf.Abs(x - _focusTarget.Value) < 0.01f) _focusTarget = null;
            }
            else if (!(_pressing && WasDragged))
            {
                // Inertia.
                x += _velocity * dt;
                _velocity *= Mathf.Exp(-inertiaDamping * dt);
            }

            // Soft edges: allow a little overscroll while dragging, spring back otherwise.
            if (x < minX || x > maxX)
            {
                float edge = x < minX ? minX : maxX;
                if (_pressing && WasDragged) x = edge + (x - edge) * 0.5f;
                else
                {
                    x = Mathf.Lerp(x, edge, 1 - Mathf.Exp(-10f * dt));
                    _velocity = 0;
                }
            }

            var p = transform.position;
            p.x = x;
            transform.position = p;
        }

        /// <summary>0..1 position inside the bounds (for a scrollbar).</summary>
        public float Normalized
        {
            get => maxX > minX ? Mathf.InverseLerp(minX, maxX, X) : 0;
            set
            {
                _focusTarget = null;
                _velocity = 0;
                var p = transform.position;
                p.x = Mathf.Lerp(minX, maxX, Mathf.Clamp01(value));
                transform.position = p;
            }
        }
    }
}
