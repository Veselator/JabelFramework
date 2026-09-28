using System.Collections.Generic;
using Jabel.Buffs;
using Jabel.Core;
using Jabel.Events;
using Jabel.Localization;
using UnityEngine;

namespace Jabel.UI
{
    /// <summary>
    /// Data-driven shop: lists buffs from the config filtered by kind/tag, keeps prices and states
    /// up to date and reveals items progressively.
    /// </summary>
    [AddComponentMenu("Jabel/UI/Buff Shop View")]
    public class BuffShopView : JabelBehaviour
    {
        public enum KindFilter { All, Passive, Active }

        [SerializeField] private BuffShopItemView itemPrefab;
        [SerializeField] private RectTransform container;
        [SerializeField] private KindFilter kind = KindFilter.All;
        [Tooltip("Only buffs with this tag. Empty = any.")]
        [SerializeField] private string tagFilter;
        [Tooltip("Push maxed-out items to the end of the list.")]
        [SerializeField] private bool maxedLast = true;
        [SerializeField] private float refreshInterval = 0.15f;

        private readonly Dictionary<BaseBuff, BuffShopItemView> _items = new Dictionary<BaseBuff, BuffShopItemView>();
        private float _timer;
        private int _buyAmount = 1;

        /// <summary>Units bought per click (1, 10, 100...). 0 = max affordable.</summary>
        public int BuyAmount
        {
            get => _buyAmount;
            set { _buyAmount = value; RefreshAll(); }
        }

        protected override void OnBind()
        {
            if (container == null) container = (RectTransform)transform;
            if (itemPrefab != null && itemPrefab.gameObject.scene.IsValid()) itemPrefab.gameObject.SetActive(false);

            Manager.Events.Subscribe<BuffUnlockedEvent>(OnUnlocked);
            Manager.Events.Subscribe<AnyBuffBoughtEvent>(OnBought);
            Loc.LanguageChanged += OnLanguageChanged;
            Rebuild();
        }

        protected override void OnUnbind()
        {
            Manager.Events.Unsubscribe<BuffUnlockedEvent>(OnUnlocked);
            Manager.Events.Unsubscribe<AnyBuffBoughtEvent>(OnBought);
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        private bool Matches(BaseBuff buff)
        {
            if (kind == KindFilter.Passive && buff.IsActive) return false;
            if (kind == KindFilter.Active && !buff.IsActive) return false;
            if (!string.IsNullOrEmpty(tagFilter) && buff.Tag != tagFilter) return false;
            return true;
        }

        private int AmountFor(BaseBuff buff) =>
            _buyAmount > 0 ? _buyAmount : Mathf.Max(1, Manager.Buffs.GetMaxAffordable(buff));

        public void Rebuild()
        {
            if (itemPrefab == null || Manager == null) return;
            foreach (var buff in Manager.Buffs.All)
            {
                if (!Matches(buff)) continue;
                bool visible = Manager.Buffs.IsVisible(buff);
                if (!_items.TryGetValue(buff, out var view))
                {
                    if (!visible) continue;
                    view = Instantiate(itemPrefab, container);
                    view.gameObject.SetActive(true);
                    view.name = "Item_" + buff.Id;
                    var captured = buff;
                    view.Bind(buff, Manager, () => AmountFor(captured));
                    _items[buff] = view;
                    if (IsBound && Time.timeSinceLevelLoad > 0.5f) view.PlayAppear();
                }
                view.gameObject.SetActive(visible);
            }
            Reorder();
        }

        private void Reorder()
        {
            var ordered = new List<BuffShopItemView>(_items.Values);
            ordered.Sort((a, b) =>
            {
                if (maxedLast)
                {
                    bool ma = Manager.Buffs.IsMaxed(a.Buff), mb = Manager.Buffs.IsMaxed(b.Buff);
                    if (ma != mb) return ma ? 1 : -1;
                }
                return a.Buff.SortOrder.CompareTo(b.Buff.SortOrder);
            });
            for (int i = 0; i < ordered.Count; i++) ordered[i].transform.SetSiblingIndex(i);
        }

        private void OnUnlocked(BuffUnlockedEvent evt) => Rebuild();

        private void OnBought(AnyBuffBoughtEvent evt)
        {
            if (!_items.ContainsKey(evt.Data.Buff)) Rebuild();
            else if (Manager.Buffs.IsMaxed(evt.Data.Buff)) Reorder();
            RefreshAll();
        }

        private void OnLanguageChanged()
        {
            foreach (var item in _items.Values)
                if (item != null) item.RefreshTexts();
            RefreshAll();
        }

        public void RefreshAll()
        {
            foreach (var item in _items.Values)
                if (item != null && item.gameObject.activeSelf) item.Refresh();
        }

        private void Update()
        {
            if (!IsBound) return;
            _timer += Time.unscaledDeltaTime;
            if (_timer < refreshInterval) return;
            _timer = 0;
            RefreshAll();
        }

#if UNITY_EDITOR
        public void EditorSetup(BuffShopItemView prefab, RectTransform root, KindFilter filter, string tagOnly)
        {
            itemPrefab = prefab;
            container = root;
            kind = filter;
            tagFilter = tagOnly;
        }
#endif
    }
}
