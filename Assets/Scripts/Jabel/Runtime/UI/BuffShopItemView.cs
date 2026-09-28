using Jabel.Audio;
using Jabel.Buffs;
using Jabel.Core;
using Jabel.Localization;
using Jabel.Numbers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jabel.UI
{
    /// <summary>One row of a shop: icon, name, description, owned count, price and state visuals.</summary>
    [AddComponentMenu("Jabel/UI/Buff Shop Item View")]
    public class BuffShopItemView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text lockText;
        [SerializeField] private Button button;
        [SerializeField] private ButtonJuice juice;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private SoundCue buySound;
        [SerializeField] private SoundCue failSound;
        [SerializeField] private Color affordableColor = new Color(0.55f, 1f, 0.55f);
        [SerializeField] private Color expensiveColor = new Color(1f, 0.45f, 0.45f);
        [SerializeField] private Color lockedColor = new Color(0.8f, 0.8f, 0.8f);

        private ClickerManager _manager;
        private System.Func<int> _amount;
        private BuffAvailability _lastState = (BuffAvailability)(-1);
        private float _appear = 1;
        private Punch _punch;

        public BaseBuff Buff { get; private set; }

        public void Bind(BaseBuff buff, ClickerManager manager, System.Func<int> amountProvider)
        {
            Buff = buff;
            _manager = manager;
            _amount = amountProvider;
            if (icon != null)
            {
                icon.sprite = buff.Icon;
                icon.enabled = buff.Icon != null;
                icon.preserveAspect = true;
            }
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
                button.onClick.AddListener(OnClick);
            }
            RefreshTexts();
            Refresh();
        }

        /// <summary>Plays the "new item" pop-in.</summary>
        public void PlayAppear() => _appear = 0;

        public void RefreshTexts()
        {
            if (Buff == null) return;
            if (title != null) title.text = Buff.DisplayName.Resolve();
            if (description != null) description.text = Buff.Description.Resolve();
        }

        public void Refresh()
        {
            if (Buff == null || _manager == null) return;
            var buffs = _manager.Buffs;
            int amount = Mathf.Max(1, _amount?.Invoke() ?? 1);
            var state = buffs.GetAvailability(Buff, amount);

            int count = buffs.GetCount(Buff);
            if (icon != null)
            {
                var sprite = Buff.IconFor(count);
                if (icon.sprite != sprite) icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
            if (countText != null)
            {
                countText.text = Buff.MaxCount == 1
                    ? (count > 0 ? Loc.Get("jabel.owned") : string.Empty)
                    : (Buff.IsUnlimited ? count.ToString() : $"{count}/{Buff.MaxCount}");
            }

            if (priceText != null)
            {
                if (state == BuffAvailability.MaxedOut)
                {
                    priceText.text = Loc.Get("jabel.maxed");
                    priceText.color = lockedColor;
                }
                else
                {
                    var price = buffs.GetPrice(Buff, amount);
                    var format = _manager.Variables.GetFormat(Buff.Price.currency);
                    string number = NumberFormatter.Format(price, format);
                    string key = "jabel.price." + Buff.Price.currency;
                    priceText.text = (Loc.Has(key) ? Loc.Format(key, number) : number) + (amount > 1 ? $"  x{amount}" : string.Empty);
                    priceText.color = state == BuffAvailability.Available ? affordableColor
                        : state == BuffAvailability.TooExpensive ? expensiveColor : lockedColor;
                }
            }

            if (lockText != null)
            {
                bool locked = state == BuffAvailability.Locked;
                lockText.gameObject.SetActive(locked);
                if (locked) lockText.text = buffs.GetLockReason(Buff);
            }

            if (button != null) button.interactable = state != BuffAvailability.MaxedOut;
            if (juice != null) juice.SetAvailable(state == BuffAvailability.Available);

            if (state != _lastState)
            {
                // Becoming affordable deserves a little attention.
                if (_lastState == BuffAvailability.TooExpensive && state == BuffAvailability.Available) _punch.Kick(0.08f);
                _lastState = state;
            }
        }

        private void OnClick()
        {
            if (Buff == null || _manager == null) return;
            int amount = Mathf.Max(1, _amount?.Invoke() ?? 1);
            int bought = _manager.Buffs.TryBuy(Buff, amount);
            if (bought > 0)
            {
                if (juice != null) juice.Pop();
                JabelAudio.Play(buySound);
            }
            else
            {
                if (juice != null) juice.Shake();
                JabelAudio.Play(failSound);
            }
            Refresh();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_appear < 1)
            {
                _appear = Mathf.Min(1, _appear + dt / 0.35f);
                if (group != null) group.alpha = _appear;
            }
            _punch.Update(dt);
            if (juice == null)
                transform.localScale = Vector3.one * (Ease.OutBack(_appear) * _punch.Value);
        }
    }
}
