using Jabel.Core;
using Jabel.Scripting;
using UnityEngine;

namespace OneKMonkeys
{
    /// <summary>
    /// Office wallpaper: a horizontally tiled strip that scrolls with the world (it is not glued to the camera),
    /// sized to the camera height and re-tiled around the view so it is endless at any row length.
    /// The style comes from a formula (bought background upgrades) and changes with a cross-fade.
    /// </summary>
    [AddComponentMenu("1000 Monkeys/Office Background")]
    public class OfficeBackground : JabelBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [Tooltip("Wallpaper per style index (0 = default office).")]
        [SerializeField] private Sprite[] styles;
        [Tooltip("Formula returning the style index, e.g. from bought background upgrades.")]
        [SerializeField] private JabelFormula style = new JabelFormula("0");
        [SerializeField] private SpriteRenderer front;
        [SerializeField] private SpriteRenderer back;
        [SerializeField] private float fadeTime = 0.8f;
        [SerializeField] private float checkInterval = 0.25f;

        private int _current = -1;
        private float _fade = 1;
        private float _timer;

        protected override void OnBind()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            Apply(Evaluate(), instant: true);
        }

        private int Evaluate()
        {
            if (styles == null || styles.Length == 0) return 0;
            return Mathf.Clamp(style.Evaluate(Manager).ToInt(), 0, styles.Length - 1);
        }

        private void Apply(int index, bool instant)
        {
            if (styles == null || styles.Length == 0 || index == _current) return;
            _current = index;
            if (instant || front.sprite == null)
            {
                front.sprite = styles[index];
                back.sprite = null;
                _fade = 1;
            }
            else
            {
                // The old wallpaper stays behind while the new one fades in on top.
                back.sprite = front.sprite;
                front.sprite = styles[index];
                _fade = 0;
            }
            SetAlpha(front, _fade);
            SetAlpha(back, 1);
        }

        private static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            var c = renderer.color;
            c.a = alpha;
            renderer.color = c;
        }

        private void Update()
        {
            if (!IsBound) return;
            _timer += Time.unscaledDeltaTime;
            if (_timer >= checkInterval)
            {
                _timer = 0;
                Apply(Evaluate(), instant: false);
            }

            if (_fade < 1)
            {
                _fade = Mathf.Min(1, _fade + Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));
                SetAlpha(front, Jabel.UI.Ease.InOutCubic(_fade));
                if (_fade >= 1) back.sprite = null;
            }
        }

        private void LateUpdate()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;
            Layout(front);
            Layout(back);
        }

        /// <summary>Fits the strip to the camera height and tiles it across the visible width (plus a margin).</summary>
        private void Layout(SpriteRenderer renderer)
        {
            var sprite = renderer.sprite;
            if (sprite == null) return;

            float viewHeight = targetCamera.orthographicSize * 2f;
            float viewWidth = viewHeight * targetCamera.aspect;
            Vector3 cam = targetCamera.transform.position;

            float spriteHeight = sprite.rect.height / sprite.pixelsPerUnit;
            float spriteWidth = sprite.rect.width / sprite.pixelsPerUnit;
            float scale = viewHeight / spriteHeight;
            float tileWidth = spriteWidth * scale;

            int tiles = Mathf.CeilToInt(viewWidth / tileWidth) + 2;
            renderer.size = new Vector2(tiles * spriteWidth, spriteHeight);
            renderer.transform.localScale = new Vector3(scale, scale, 1);

            // Snap the strip to whole tiles in world space: it scrolls with the office, not with the camera.
            float left = Mathf.Floor((cam.x - viewWidth * 0.5f) / tileWidth) * tileWidth;
            float bottom = cam.y - viewHeight * 0.5f;
            Vector2 pivot = sprite.pivot / sprite.rect.size;
            renderer.transform.position = new Vector3(
                left + pivot.x * renderer.size.x * scale,
                bottom + pivot.y * renderer.size.y * scale,
                renderer.transform.position.z);
        }

#if UNITY_EDITOR
        public void EditorSetup(Camera cameraRef, Sprite[] wallpapers, string styleFormula, SpriteRenderer frontLayer, SpriteRenderer backLayer)
        {
            targetCamera = cameraRef;
            styles = wallpapers;
            style = new JabelFormula(styleFormula);
            front = frontLayer;
            back = backLayer;
        }
#endif
    }
}
