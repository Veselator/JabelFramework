using System.Collections.Generic;
using Jabel.Buffs;
using Jabel.Core;
using Jabel.Events;
using Jabel.Localization;
using Jabel.Numbers;
using Jabel.UI;
using UnityEngine;

namespace OneKMonkeys
{
    /// <summary>
    /// Lays out one station per monkey instance to the right of the player's desk, keeps the hire slot
    /// at the end, drives camera bounds, culls off-screen animation and shows income floating texts.
    /// </summary>
    [AddComponentMenu("1000 Monkeys/Monkey Row")]
    public class MonkeyRow : JabelBehaviour
    {
        [SerializeField] private ActiveBuff monkeyBuff;
        [SerializeField] private MonkeyStation stationPrefab;
        [SerializeField] private Transform container;
        [SerializeField] private HireSlot hireSlot;
        [SerializeField] private MonkeyLevelTable levels;
        [SerializeField] private Material monkeyMaterial;
        [SerializeField] private Sprite bodySprite;
        [SerializeField] private float firstX = 4.4f;
        [SerializeField] private float spacing = 4.2f;
        [SerializeField] private float rowY;
        [Tooltip("Leftmost camera position (keeps the player's desk on the left of the screen).")]
        [SerializeField] private float cameraMinX = 5f;
        [SerializeField] private FloatingTextSpawner floatingTexts;
        [SerializeField] private ParticleSystem levelUpParticles;
        [SerializeField] private MonkeyTooltip tooltip;
        [SerializeField] private Color charsColor = new Color(0.6f, 1f, 0.6f);
        [SerializeField] private Color moneyColor = new Color(1f, 0.88f, 0.35f);

        private readonly List<MonkeyStation> _stations = new List<MonkeyStation>();
        private MonkeyMaterials _materials;
        private CameraScroller _scroller;
        private float _cullTimer;
        private int _visibleFrom, _visibleTo = -1;

        public IReadOnlyList<MonkeyStation> Stations => _stations;

        public float SlotX(int index) => firstX + index * spacing;

        protected override void OnBind()
        {
            _materials = new MonkeyMaterials(monkeyMaterial, bodySprite);
            _scroller = CameraScroller.Instance;

            foreach (var instance in Manager.Buffs.GetInstances(monkeyBuff)) Spawn(instance, false);

            Manager.Events.Subscribe<BuffInstanceCreatedEvent>(OnCreated);
            Manager.Events.Subscribe<BuffInstanceLevelUpEvent>(OnLevelUp);
            Manager.Events.Subscribe<CodeWrittenEvent>(OnCodeWritten);
            Manager.Events.Subscribe<RunResetEvent>(OnReset);
            UpdateLayout();
            Cull(true);
        }

        protected override void OnUnbind()
        {
            Manager.Events.Unsubscribe<BuffInstanceCreatedEvent>(OnCreated);
            Manager.Events.Unsubscribe<BuffInstanceLevelUpEvent>(OnLevelUp);
            Manager.Events.Unsubscribe<CodeWrittenEvent>(OnCodeWritten);
            Manager.Events.Unsubscribe<RunResetEvent>(OnReset);
        }

        private void Spawn(BuffInstance instance, bool animate)
        {
            var station = Instantiate(stationPrefab, container);
            station.name = $"Monkey_{instance.Index + 1:0000}";
            station.transform.localPosition = new Vector3(SlotX(instance.Index), rowY, 0);
            station.Bind(instance, levels, _materials, animate);
            station.Clicked = OnStationClicked;
            station.HoverChanged = OnStationHover;
            _stations.Add(station);
        }

        private void OnCreated(BuffInstanceCreatedEvent evt)
        {
            if (evt.Instance.Buff != monkeyBuff) return;
            Spawn(evt.Instance, true);
            UpdateLayout();
            if (_scroller != null) _scroller.EnsureVisible(SlotX(evt.Instance.Index + 1), 2.5f);
            Cull(true);
        }

        private void OnLevelUp(BuffInstanceLevelUpEvent evt)
        {
            if (evt.Instance.Buff != monkeyBuff || evt.Instance.Index >= _stations.Count) return;
            var station = _stations[evt.Instance.Index];
            station.ApplyLevel(true);
            if (levelUpParticles != null)
            {
                var emit = new ParticleSystem.EmitParams { position = station.FloatingPoint, applyShapeToPosition = true };
                levelUpParticles.Emit(emit, 40 + evt.NewLevel * 3);
            }
            if (floatingTexts != null)
            {
                var title = levels.Get(evt.NewLevel).title;
                floatingTexts.Spawn(title.Resolve() + "!", station.FloatingPoint + Vector3.up * 0.6f, moneyColor, 1.4f);
            }
        }

        private void OnCodeWritten(CodeWrittenEvent evt)
        {
            if (evt.IsOffline || evt.Instance == null || evt.Instance.Buff != monkeyBuff) return;
            int index = evt.Instance.Index;
            if (index < _visibleFrom || index > _visibleTo || index >= _stations.Count) return;

            var station = _stations[index];
            float intensity = Mathf.Clamp01((float)(evt.Chars.Log10() / 4.0));
            station.PlayTyping(intensity);

            if (floatingTexts == null) return;
            var at = station.FloatingPoint;
            floatingTexts.Spawn(Loc.Format("monkey.chars", NumberFormatter.Format(evt.Chars)), at, charsColor, 1f);
            if (evt.Money > BigNumber.Zero)
                floatingTexts.Spawn(Loc.Format("hud.plusMoney", NumberFormatter.Format(evt.Money, NumberFormat.Money)),
                    at + new Vector3(0.35f, -0.45f, 0), moneyColor, 0.85f);
        }

        private void OnReset(RunResetEvent evt)
        {
            foreach (var s in _stations) Destroy(s.gameObject);
            _stations.Clear();
            UpdateLayout();
        }

        /// <summary>Click = upgrade. When it is not possible (level too low / not enough money) the station flashes red.</summary>
        private void OnStationClicked(MonkeyStation station)
        {
            if (station.Instance == null) return;
            if (Manager.Buffs.TryLevelUp(station.Instance))
            {
                if (tooltip != null) tooltip.Pulse();
            }
            else station.PlayDenied();
            if (tooltip != null && station.IsHovered) tooltip.Show(station);
        }

        private void OnStationHover(MonkeyStation station, bool hovered)
        {
            if (tooltip == null) return;
            if (hovered) tooltip.Show(station);
            else tooltip.Hide(station);
        }

        private void UpdateLayout()
        {
            int count = _stations.Count;
            bool canHire = monkeyBuff.IsUnlimited || count < monkeyBuff.MaxCount;
            if (hireSlot != null)
            {
                hireSlot.gameObject.SetActive(canHire);
                hireSlot.transform.localPosition = new Vector3(SlotX(count), rowY, 0);
            }
            if (_scroller != null)
            {
                float last = SlotX(canHire ? count : count - 1);
                // Leftmost: the player's desk; rightmost: the hire slot.
                float min = cameraMinX;
                float max = Mathf.Max(min, last - _scroller.HalfWidth + spacing * 0.8f);
                _scroller.SetBounds(min, max);
            }
        }

        private void Update()
        {
            if (!IsBound) return;
            _cullTimer -= Time.deltaTime;
            if (_cullTimer <= 0) Cull(false);
        }

        /// <summary>Only on-screen stations animate: 1000 monkeys cost as much as the ~6 you can see.</summary>
        private void Cull(bool force)
        {
            _cullTimer = 0.2f;
            // Bounds depend on the aspect ratio, which can change at any time (window resize).
            UpdateLayout();
            if (_scroller == null)
            {
                _visibleFrom = 0;
                _visibleTo = _stations.Count - 1;
                return;
            }

            float half = _scroller.HalfWidth + spacing;
            int from = Mathf.Max(0, Mathf.FloorToInt((_scroller.X - half - firstX) / spacing));
            int to = Mathf.Min(_stations.Count - 1, Mathf.CeilToInt((_scroller.X + half - firstX) / spacing));
            if (!force && from == _visibleFrom && to == _visibleTo) return;

            for (int i = 0; i < _stations.Count; i++)
            {
                bool visible = i >= from && i <= to;
                var station = _stations[i];
                if (station.gameObject.activeSelf != visible) station.gameObject.SetActive(visible);
            }
            _visibleFrom = from;
            _visibleTo = to;
        }

#if UNITY_EDITOR
        public void EditorSetup(ActiveBuff buff, MonkeyStation prefab, Transform root, HireSlot slot, MonkeyLevelTable table,
            Material material, Sprite body, FloatingTextSpawner texts, ParticleSystem levelUp, MonkeyTooltip monkeyTooltip,
            float startX, float step, float y)
        {
            monkeyBuff = buff;
            stationPrefab = prefab;
            container = root;
            hireSlot = slot;
            levels = table;
            monkeyMaterial = material;
            bodySprite = body;
            floatingTexts = texts;
            levelUpParticles = levelUp;
            tooltip = monkeyTooltip;
            firstX = startX;
            spacing = step;
            rowY = y;
        }
#endif
    }
}
