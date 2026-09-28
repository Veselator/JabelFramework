using System.Text;
using Jabel.Core;
using Jabel.Events;
using Jabel.Localization;
using Jabel.Numbers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jabel.UI
{
    /// <summary>"While you were away..." popup fed by <see cref="OfflineProgressEvent"/>.</summary>
    [AddComponentMenu("Jabel/UI/Offline Progress Popup")]
    public class OfflineProgressPopup : JabelBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        [SerializeField] private Button closeButton;
        [Tooltip("Optional full-screen dimmer faded with the popup (also blocks clicks behind it).")]
        [SerializeField] private CanvasGroup backdrop;

        private float _anim = 1;
        private bool _visible;

        private void Awake()
        {
            if (panel != null) panel.gameObject.SetActive(false);
            if (backdrop != null) backdrop.gameObject.SetActive(false);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
        }

        protected override void OnBind() => Manager.Events.Subscribe<OfflineProgressEvent>(OnOffline);

        protected override void OnUnbind() => Manager.Events.Unsubscribe<OfflineProgressEvent>(OnOffline);

        private void OnOffline(OfflineProgressEvent evt)
        {
            if (panel == null) return;

            string duration = NumberFormatter.FormatDuration(evt.SecondsAway,
                Loc.Get("time.d"), Loc.Get("time.h"), Loc.Get("time.m"), Loc.Get("time.s"));
            if (title != null) title.text = Loc.Format("jabel.offline.title", duration);

            var sb = new StringBuilder();
            if (evt.Report.Entries.Count == 0) sb.AppendLine(Loc.Get("jabel.offline.nothing"));
            foreach (var entry in evt.Report.Entries)
            {
                var delta = entry.Delta;
                string sign = delta.IsNegative ? "-" : "+";
                string color = delta.IsNegative ? "#FF6B6B" : "#7CFF8A";
                sb.Append($"<color={color}>{sign}{NumberFormatter.Format(BigNumber.Abs(delta), entry.Format)}</color>  {entry.DisplayName}\n");
            }
            if (evt.SecondsSimulated + 1 < evt.SecondsAway)
            {
                string simulated = NumberFormatter.FormatDuration(evt.SecondsSimulated,
                    Loc.Get("time.d"), Loc.Get("time.h"), Loc.Get("time.m"), Loc.Get("time.s"));
                sb.Append("\n<size=75%>").Append(Loc.Format("jabel.offline.capped", simulated)).Append("</size>");
            }
            if (body != null) body.text = sb.ToString();

            panel.gameObject.SetActive(true);
            if (backdrop != null) backdrop.gameObject.SetActive(true);
            _visible = true;
            _anim = 0;
        }

        public void Hide()
        {
            _visible = false;
            _anim = 0;
        }

        private void Update()
        {
            if (panel == null || _anim >= 1) return;
            _anim = Mathf.Min(1, _anim + Time.unscaledDeltaTime / 0.3f);
            if (backdrop != null) backdrop.alpha = _visible ? _anim : 1 - _anim;
            if (_visible) panel.localScale = Vector3.one * Ease.OutBack(_anim);
            else
            {
                panel.localScale = Vector3.one * (1 - Ease.InCubic(_anim));
                if (_anim >= 1)
                {
                    panel.gameObject.SetActive(false);
                    if (backdrop != null) backdrop.gameObject.SetActive(false);
                }
            }
        }
    }
}
