using Jabel.Audio;
using Jabel.Buffs;
using Jabel.Core;
using Jabel.Localization;
using Jabel.Numbers;
using Jabel.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OneKMonkeys
{
    /// <summary>
    /// The empty desk at the end of the row with a "Hire" button and the price of the next monkey.
    /// Shows affordability (price color, button tint, attention pulse) and shakes when you can't pay.
    /// </summary>
    [AddComponentMenu("1000 Monkeys/Hire Slot")]
    public class HireSlot : JabelBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private ActiveBuff monkeyBuff;
        [SerializeField] private SpriteRenderer button;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text price;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Color affordableButton = new Color(0.35f, 0.8f, 0.45f);
        [SerializeField] private Color expensiveButton = new Color(0.5f, 0.5f, 0.55f);
        [SerializeField] private Color affordablePrice = new Color(0.75f, 1f, 0.75f);
        [SerializeField] private Color expensivePrice = new Color(1f, 0.45f, 0.45f);
        [SerializeField] private SoundCue hireSound;
        [SerializeField] private SoundCue deniedSound;

        private bool _hover;
        private float _scale = 1;
        private float _shake = 1;
        private Punch _punch;
        private bool _affordable;
        private float _refresh;
        private Vector3 _baseScale;

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform;
            _baseScale = visualRoot.localScale;
        }

        protected override void OnBind()
        {
            Loc.LanguageChanged += RefreshTexts;
            RefreshTexts();
        }

        protected override void OnUnbind() => Loc.LanguageChanged -= RefreshTexts;

        private void RefreshTexts()
        {
            if (title != null) title.text = Loc.Get("monkey.hire");
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsBound) return;
            if (CameraScroller.Instance != null && CameraScroller.Instance.WasDragged) return;
            if (Manager.Buffs.TryBuy(monkeyBuff) > 0)
            {
                _punch.Kick(0.3f);
                JabelAudio.Play(hireSound);
            }
            else
            {
                _shake = 0;
                JabelAudio.Play(deniedSound);
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => _hover = true;
        public void OnPointerExit(PointerEventData eventData) => _hover = false;

        private void Update()
        {
            if (!IsBound) return;
            float dt = Time.deltaTime;

            _refresh -= dt;
            if (_refresh <= 0)
            {
                _refresh = 0.1f;
                var cost = Manager.Buffs.GetPrice(monkeyBuff);
                _affordable = Manager.Buffs.GetAvailability(monkeyBuff) == BuffAvailability.Available;
                if (price != null)
                {
                    price.text = Loc.Format("hud.money", NumberFormatter.Format(cost, NumberFormat.Money));
                    price.color = _affordable ? affordablePrice : expensivePrice;
                }
            }

            if (button != null)
                button.color = Color.Lerp(button.color, _affordable ? affordableButton * (_hover ? 1.15f : 1f) : expensiveButton, 1 - Mathf.Exp(-10 * dt));

            // Hover grow, attention pulse when affordable, wiggle on failure, punch on success.
            float pulse = _affordable ? 1 + Mathf.Sin(Time.time * 5f) * 0.03f : 1;
            _scale = Mathf.Lerp(_scale, _hover ? 1.08f : 1f, 1 - Mathf.Exp(-14 * dt));
            _punch.Update(dt);
            visualRoot.localScale = _baseScale * (_scale * pulse * _punch.Value);

            float angle = 0;
            if (_shake < 1)
            {
                _shake += dt / 0.45f;
                angle = 10f * Ease.DampedWave(_shake * 0.45f, 40f, 8f);
            }
            visualRoot.localRotation = Quaternion.Euler(0, 0, angle);
        }

#if UNITY_EDITOR
        public void EditorSetup(ActiveBuff buff, SpriteRenderer buttonRenderer, TMP_Text titleText, TMP_Text priceText, Transform root)
        {
            monkeyBuff = buff;
            button = buttonRenderer;
            title = titleText;
            price = priceText;
            visualRoot = root;
        }
#endif
    }
}
