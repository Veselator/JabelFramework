using Jabel.Buffs;
using Jabel.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OneKMonkeys
{
    /// <summary>
    /// One workplace: a desk with a computer in front and a monkey behind it.
    /// The monkey sways, breathes and bounces with organic noise, reacts when it types,
    /// celebrates level-ups and wears the shader effects of its level.
    /// </summary>
    [AddComponentMenu("1000 Monkeys/Monkey Station")]
    public class MonkeyStation : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private SpriteRenderer desk;
        [SerializeField] private Transform monkeyPivot;
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private MonkeyEyes eyes;
        [SerializeField] private TMP_Text levelBadge;
        [SerializeField] private SpriteRenderer screenGlow;
        [SerializeField] private Transform floatingAnchor;

        private MonkeyLevelTable _levels;
        private MonkeyMaterials _materials;
        private float _seed;
        private float _energy = 1;
        private Vector3 _pivotBase;
        private Vector3 _deskScale;
        private Punch _typePunch;
        private Punch _levelPunch;
        private float _appear = 1;
        private float _glow;
        private bool _hover;
        private float _hoverScale = 1;
        private Color _glowColor = new Color(0.4f, 1f, 0.5f, 0f);
        private Color _bodyColor = Color.white;
        private Color _deskColor = Color.white;
        private Vector3 _rootScale = Vector3.one;
        private float _denied = 1;

        [Header("Denied feedback")]
        [SerializeField] private Color deniedColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private float deniedScale = 1.18f;
        [SerializeField] private float deniedDuration = 0.7f;

        public BuffInstance Instance { get; private set; }
        public Vector3 FloatingPoint => floatingAnchor != null ? floatingAnchor.position : transform.position + Vector3.up * 2f;
        public System.Action<MonkeyStation> Clicked { get; set; }
        /// <summary>Hover started (true) or ended (false).</summary>
        public System.Action<MonkeyStation, bool> HoverChanged { get; set; }
        public bool IsHovered => _hover;

        private void Awake()
        {
            if (monkeyPivot != null) _pivotBase = monkeyPivot.localPosition;
            if (desk != null)
            {
                _deskScale = desk.transform.localScale;
                _deskColor = desk.color;
            }
            _rootScale = transform.localScale;
        }

        private void OnDisable()
        {
            // Culled or destroyed while hovered: the tooltip must not stay behind.
            if (_hover) HoverChanged?.Invoke(this, false);
            _hover = false;
        }

        /// <summary>
        /// "Can't do that" feedback: monkey and desk flash red and grow, then ease back over <see cref="deniedDuration"/>.
        /// </summary>
        public void PlayDenied()
        {
            _denied = 0;
            if (eyes != null) eyes.Surprise(1f);
        }

        public void Bind(BuffInstance instance, MonkeyLevelTable levels, MonkeyMaterials materials, bool animateAppear)
        {
            Instance = instance;
            _levels = levels;
            _materials = materials;
            _seed = (instance.Seed % 10000) * 0.0137f;
            if (eyes != null) eyes.Setup(instance.Seed);
            ApplyLevel(false);
            if (animateAppear) _appear = 0;
        }

        /// <summary>Applies color, effects and badge of the current level.</summary>
        public void ApplyLevel(bool celebrate)
        {
            if (Instance == null || _levels == null) return;
            int level = Instance.Level;
            var data = _levels.Get(level);
            _energy = data.energy;

            if (body != null)
            {
                _bodyColor = _levels.PickColor(level, Instance.Seed);
                body.color = _bodyColor;
                if (_materials != null) body.sharedMaterial = _materials.Get(data.effects, data.glowColor);
            }
            if (levelBadge != null) levelBadge.text = level.ToString();
            _glowColor = data.glowColor;

            if (celebrate)
            {
                _levelPunch.Kick(0.45f);
                _glow = 1.5f;
                if (eyes != null) eyes.Surprise(1.5f);
            }
        }

        /// <summary>The monkey just typed: bounce, widen eyes, light up the screen.</summary>
        public void PlayTyping(float intensity)
        {
            _typePunch.Kick(Mathf.Lerp(0.08f, 0.22f, intensity));
            _glow = Mathf.Max(_glow, 0.6f + intensity * 0.6f);
            if (eyes != null && intensity > 0.5f) eyes.Surprise(intensity * 0.6f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (CameraScroller.Instance != null && CameraScroller.Instance.WasDragged) return;
            Clicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hover = true;
            HoverChanged?.Invoke(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hover = false;
            HoverChanged?.Invoke(this, false);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;

            _typePunch.Update(dt);
            _levelPunch.Update(dt);
            if (_appear < 1) _appear = Mathf.Min(1, _appear + dt / 0.6f);

            // Organic idle: two noise channels for sway, a slow breath, and a small bob.
            float e = _energy;
            float sway = (Mathf.PerlinNoise(_seed, t * 0.45f) - 0.5f) * 16f * e + Mathf.Sin(t * 1.3f + _seed * 5f) * 2.5f * e;
            float breath = 1 + Mathf.Sin(t * (1.6f + e * 0.4f) + _seed * 3f) * 0.025f * e;
            float bob = (Mathf.PerlinNoise(_seed + 10f, t * 0.8f) - 0.5f) * 0.08f * e;

            float type = _typePunch.Value;       // 1 at rest
            float level = _levelPunch.Value;
            float appear = Ease.OutBack(_appear, 2.5f);

            if (monkeyPivot != null)
            {
                monkeyPivot.localPosition = _pivotBase + new Vector3(0, bob + (type - 1) * 0.4f, 0);
                monkeyPivot.localRotation = Quaternion.Euler(0, 0, sway);
                // Squash & stretch: typing squashes vertically and widens slightly.
                float sx = breath * (1 + (1 - type) * 0.5f) * level;
                float sy = breath * type * level;
                monkeyPivot.localScale = new Vector3(sx, sy, 1) * appear;
            }

            // Denied: instantly red and enlarged, then ease-out back to normal.
            float deniedK = 0;
            if (_denied < 1)
            {
                _denied = Mathf.Min(1, _denied + dt / Mathf.Max(0.01f, deniedDuration));
                deniedK = 1 - Ease.OutCubic(_denied);
                transform.localScale = _rootScale * Mathf.Lerp(1f, deniedScale, deniedK);
                if (body != null) body.color = Color.Lerp(_bodyColor, deniedColor, deniedK);
                if (desk != null) desk.color = Color.Lerp(_deskColor, deniedColor, deniedK);
            }

            _hoverScale = Mathf.Lerp(_hoverScale, _hover ? 1.05f : 1f, 1 - Mathf.Exp(-14 * dt));
            if (desk != null) desk.transform.localScale = _deskScale * (_hoverScale * Mathf.Lerp(0.6f, 1f, appear));

            if (screenGlow != null)
            {
                _glow = Mathf.Max(0, _glow - dt * 2.5f);
                var c = _glowColor;
                c.a = Mathf.Clamp01(_glow) * 0.8f + 0.12f + (_hover ? 0.15f : 0);
                screenGlow.color = c;
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(SpriteRenderer deskRenderer, Transform pivot, SpriteRenderer bodyRenderer, MonkeyEyes monkeyEyes,
            TMP_Text badge, SpriteRenderer glow, Transform anchor)
        {
            desk = deskRenderer;
            monkeyPivot = pivot;
            body = bodyRenderer;
            eyes = monkeyEyes;
            levelBadge = badge;
            screenGlow = glow;
            floatingAnchor = anchor;
        }
#endif
    }
}
