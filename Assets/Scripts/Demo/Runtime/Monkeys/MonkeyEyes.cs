using UnityEngine;

namespace OneKMonkeys
{
    /// <summary>
    /// Living eyes: random blinks (sometimes double), saccades to look around, and a surprised
    /// widen when the monkey types. Each monkey picks its eye color from the seed.
    /// </summary>
    [AddComponentMenu("1000 Monkeys/Monkey Eyes")]
    public class MonkeyEyes : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer leftEye;
        [SerializeField] private SpriteRenderer rightEye;
        [Tooltip("Open eye variants (pupil colors).")]
        [SerializeField] private Sprite[] openSprites;
        [SerializeField] private Sprite closedSprite;
        [SerializeField] private float lookRadius = 0.035f;

        private Sprite _open;
        private Vector3 _leftBase, _rightBase, _baseScale;
        private float _blinkTimer, _blinkLeft, _lookTimer;
        private Vector3 _lookTarget, _look;
        private int _pendingBlinks;
        private float _surprise;
        private System.Random _rng = new System.Random();

        private void Awake()
        {
            if (leftEye != null) _leftBase = leftEye.transform.localPosition;
            if (rightEye != null) _rightBase = rightEye.transform.localPosition;
            _baseScale = leftEye != null ? leftEye.transform.localScale : Vector3.one;
            _blinkTimer = Random.Range(1f, 4f);
        }

        public void Setup(int seed)
        {
            _rng = new System.Random(seed);
            if (openSprites != null && openSprites.Length > 0) _open = openSprites[_rng.Next(openSprites.Length)];
            SetSprite(_open);
        }

        /// <summary>Wide eyes for a moment (typing burst, level up).</summary>
        public void Surprise(float amount = 1f) => _surprise = Mathf.Max(_surprise, amount);

        private void SetSprite(Sprite sprite)
        {
            if (sprite == null) return;
            if (leftEye != null) leftEye.sprite = sprite;
            if (rightEye != null) rightEye.sprite = sprite;
        }

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        private void Update()
        {
            float dt = Time.deltaTime;

            // Blinking.
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
            else
            {
                _blinkTimer -= dt;
                if (_blinkTimer <= 0)
                {
                    SetSprite(closedSprite);
                    _blinkLeft = Rand(0.08f, 0.14f);
                    _blinkTimer = Rand(1.5f, 5f);
                    if (_rng.NextDouble() < 0.2) _pendingBlinks = 1;
                }
            }

            // Looking around: quick saccades, then hold.
            _lookTimer -= dt;
            if (_lookTimer <= 0)
            {
                _lookTimer = Rand(0.6f, 2.5f);
                var dir = Random.insideUnitCircle;
                _lookTarget = new Vector3(dir.x, dir.y * 0.6f, 0) * lookRadius;
            }
            _look = Vector3.Lerp(_look, _lookTarget, 1 - Mathf.Exp(-20f * dt));

            _surprise = Mathf.Max(0, _surprise - dt * 3f);
            var scale = _baseScale * (1 + _surprise * 0.35f);

            if (leftEye != null)
            {
                leftEye.transform.localPosition = _leftBase + _look;
                leftEye.transform.localScale = scale;
            }
            if (rightEye != null)
            {
                rightEye.transform.localPosition = _rightBase + _look;
                rightEye.transform.localScale = scale;
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(SpriteRenderer left, SpriteRenderer right, Sprite[] open, Sprite closed)
        {
            leftEye = left;
            rightEye = right;
            openSprites = open;
            closedSprite = closed;
        }
#endif
    }
}
