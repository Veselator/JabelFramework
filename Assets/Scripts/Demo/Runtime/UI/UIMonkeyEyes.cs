using UnityEngine;
using UnityEngine.UI;

namespace OneKMonkeys
{
    /// <summary>UI counterpart of <see cref="MonkeyEyes"/> for monkey icons: random blinks and glances.</summary>
    [AddComponentMenu("1000 Monkeys/UI Monkey Eyes")]
    public class UIMonkeyEyes : MonoBehaviour
    {
        [SerializeField] private Image leftEye;
        [SerializeField] private Image rightEye;
        [SerializeField] private Sprite[] openSprites;
        [SerializeField] private Sprite closedSprite;
        [Tooltip("Glance distance in canvas units.")]
        [SerializeField] private float lookRadius = 1f;

        private Sprite _open;
        private Vector2 _leftBase, _rightBase;
        private Vector2 _look, _lookTarget;
        private float _blinkTimer, _blinkLeft, _lookTimer;
        private int _pendingBlinks;

        private void Awake()
        {
            if (leftEye != null) _leftBase = leftEye.rectTransform.anchoredPosition;
            if (rightEye != null) _rightBase = rightEye.rectTransform.anchoredPosition;
            if (openSprites != null && openSprites.Length > 0) _open = openSprites[Random.Range(0, openSprites.Length)];
            SetSprite(_open);
            _blinkTimer = Random.Range(1f, 4f);
        }

        private void SetSprite(Sprite sprite)
        {
            if (sprite == null) return;
            if (leftEye != null) leftEye.sprite = sprite;
            if (rightEye != null) rightEye.sprite = sprite;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_blinkLeft > 0)
            {
                _blinkLeft -= dt;
                if (_blinkLeft <= 0)
                {
                    SetSprite(_open);
                    if (_pendingBlinks > 0)
                    {
                        _pendingBlinks--;
                        _blinkTimer = 0.12f;
                    }
                }
            }
            else if ((_blinkTimer -= dt) <= 0)
            {
                SetSprite(closedSprite);
                _blinkLeft = Random.Range(0.08f, 0.14f);
                _blinkTimer = Random.Range(1.5f, 5f);
                if (Random.value < 0.2f) _pendingBlinks = 1;
            }

            if ((_lookTimer -= dt) <= 0)
            {
                _lookTimer = Random.Range(0.6f, 2.5f);
                var dir = Random.insideUnitCircle;
                _lookTarget = new Vector2(dir.x, dir.y * 0.6f) * lookRadius;
            }
            _look = Vector2.Lerp(_look, _lookTarget, 1 - Mathf.Exp(-20f * dt));

            if (leftEye != null) leftEye.rectTransform.anchoredPosition = _leftBase + _look;
            if (rightEye != null) rightEye.rectTransform.anchoredPosition = _rightBase + _look;
        }

#if UNITY_EDITOR
        public void EditorSetup(Image left, Image right, Sprite[] open, Sprite closed, float radius)
        {
            leftEye = left;
            rightEye = right;
            openSprites = open;
            closedSprite = closed;
            lookRadius = radius;
        }
#endif
    }
}
