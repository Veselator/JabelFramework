using Jabel.Buffs;
using Jabel.Core;
using Jabel.Localization;
using Jabel.Numbers;
using Jabel.Scripting;
using Jabel.UI;
using TMPro;
using UnityEngine;

namespace OneKMonkeys
{
    /// <summary>
    /// Info plate shown above the hovered monkey: title, level, output now and after the upgrade,
    /// the next effect, the price (green/red) and why an upgrade is locked. Clicking the monkey upgrades it.
    /// Follows the monkey on screen and never blocks clicks.
    /// </summary>
    [AddComponentMenu("1000 Monkeys/Monkey Tooltip")]
    public class MonkeyTooltip : JabelBehaviour
    {
        [SerializeField] private RectTransform plate;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        [SerializeField] private MonkeyLevelTable levels;
        [Tooltip("Characters per activation; local 'level'. Must match the monkey buff's OnTick.")]
        [SerializeField] private JabelFormula charsFormula = new JabelFormula("1");
        [SerializeField] private Vector2 screenOffset = new Vector2(0, 30);
        [SerializeField] private float fadeSpeed = 10f;

        private MonkeyStation _station;
        private float _refresh;
        private Punch _punch;
        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (group != null)
            {
                group.alpha = 0;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        protected override void OnBind() => Loc.LanguageChanged += Refresh;
        protected override void OnUnbind() => Loc.LanguageChanged -= Refresh;

        public void Show(MonkeyStation station)
        {
            _station = station;
            _refresh = 0;
            Refresh();
            Follow();
        }

        /// <summary>Hides the plate if it currently belongs to <paramref name="station"/> (null = any).</summary>
        public void Hide(MonkeyStation station)
        {
            if (station == null || station == _station) _station = null;
        }

        public void HideAll() => _station = null;

        /// <summary>Little bounce after a successful upgrade.</summary>
        public void Pulse() => _punch.Kick(0.12f);

        private BigNumber Chars(int level)
        {
            var ctx = JabelContext.Rent(Manager);
            try
            {
                ctx.SetLocal("level", level);
                return charsFormula.Evaluate(ctx);
            }
            finally
            {
                ctx.Release();
            }
        }

        private double PeriodSeconds(ActiveBuff buff, int level)
        {
            var ctx = JabelContext.Rent(Manager);
            try
            {
                ctx.SetLocal("level", level);
                return System.Math.Max(1, buff.Period.Evaluate(ctx, 1).ToDouble()) * Manager.TickDuration;
            }
            finally
            {
                ctx.Release();
            }
        }

        private string Output(ActiveBuff buff, int level) =>
            Loc.Format("monkey.panel.output", NumberFormatter.Format(Chars(level)), PeriodSeconds(buff, level).ToString("0.##", Loc.Culture));

        private void Refresh()
        {
            if (!IsBound || _station == null || _station.Instance == null) return;
            var instance = _station.Instance;
            var buff = instance.Buff;
            int level = instance.Level;
            bool maxed = level >= buff.MaxLevel;

            if (title != null) title.text = Loc.Format("monkey.panel.title", instance.Index + 1, levels.Get(level).title.Resolve());

            var sb = new System.Text.StringBuilder();
            sb.Append(Loc.Format("monkey.panel.level", level, buff.MaxLevel)).Append('\n');
            sb.Append(Output(buff, level));

            if (maxed)
            {
                sb.Append("\n\n<color=#FFD86B>").Append(Loc.Get("monkey.tooltip.maxed")).Append("</color>");
            }
            else
            {
                var next = levels.Get(level + 1);
                sb.Append("\n<color=#8CFF9A>→ ").Append(Output(buff, level + 1)).Append("</color>");
                sb.Append("\n<size=85%><color=#FFD86B>")
                    .Append(Loc.Format("monkey.panel.next", next.title.Resolve(), EffectNames(next.effects)))
                    .Append("</color></size>");

                var state = Manager.Buffs.GetLevelUpAvailability(instance);
                string price = Loc.Format("hud.money", NumberFormatter.Format(Manager.Buffs.GetLevelUpPrice(instance), NumberFormat.Money));
                string priceColor = state == BuffAvailability.Available ? "#7CFF8A" : "#FF6B6B";
                sb.Append("\n\n<color=").Append(priceColor).Append('>').Append(Loc.Format("monkey.panel.upgrade", price)).Append("</color>");

                if (state == BuffAvailability.Locked)
                    sb.Append("\n<color=#FFA260>").Append(Manager.Buffs.GetLevelUpLockReason(instance)).Append("</color>");
                else if (state == BuffAvailability.TooExpensive)
                    sb.Append("\n<color=#FF8C8C>").Append(Loc.Get("monkey.tooltip.noMoney")).Append("</color>");
                else
                    sb.Append("\n<size=80%><color=#B8C4D6>").Append(Loc.Get("monkey.tooltip.click")).Append("</color></size>");
            }

            if (body != null) body.text = sb.ToString();
        }

        private static string EffectNames(MonkeyFx fx)
        {
            if (fx == MonkeyFx.None) return Loc.Get("fx.None");
            var parts = new System.Collections.Generic.List<string>();
            foreach (MonkeyFx flag in System.Enum.GetValues(typeof(MonkeyFx)))
                if (flag != MonkeyFx.None && (fx & flag) == flag) parts.Add(Loc.Get("fx." + flag));
            return string.Join(", ", parts);
        }

        /// <summary>Places the plate above the monkey, kept inside the screen.</summary>
        private void Follow()
        {
            if (_station == null || plate == null || _canvas == null) return;
            var worldCamera = Camera.main;
            if (worldCamera == null) return;

            var canvasRect = (RectTransform)_canvas.transform;
            Vector2 screen = (Vector2)worldCamera.WorldToScreenPoint(_station.FloatingPoint) + screenOffset;
            var uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out var local)) return;

            var parent = (RectTransform)plate.parent;
            var size = plate.rect.size;
            var bounds = parent.rect;
            float margin = 12f;
            // Pivot is bottom-centre: keep the whole plate inside the parent rect.
            local.x = Mathf.Clamp(local.x, bounds.xMin + size.x * 0.5f + margin, bounds.xMax - size.x * 0.5f - margin);
            local.y = Mathf.Clamp(local.y, bounds.yMin + margin, bounds.yMax - size.y - margin);
            plate.anchoredPosition = local;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            bool visible = _station != null && _station.isActiveAndEnabled;
            if (!visible) _station = null;

            if (visible)
            {
                _refresh -= dt;
                if (_refresh <= 0)
                {
                    _refresh = 0.15f;
                    Refresh();
                }
                Follow();
            }

            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1 : 0, dt * fadeSpeed);
            _punch.Update(dt);
            if (plate != null)
            {
                float appear = group != null ? Mathf.Lerp(0.9f, 1f, Ease.OutCubic(group.alpha)) : 1;
                plate.localScale = Vector3.one * (appear * _punch.Value);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(RectTransform plateRect, CanvasGroup canvasGroup, TMP_Text titleText, TMP_Text bodyText,
            MonkeyLevelTable table, string chars)
        {
            plate = plateRect;
            group = canvasGroup;
            title = titleText;
            body = bodyText;
            levels = table;
            charsFormula = new JabelFormula(chars);
        }
#endif
    }
}
