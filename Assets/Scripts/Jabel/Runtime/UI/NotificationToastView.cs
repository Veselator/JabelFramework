using System.Collections.Generic;
using Jabel.Core;
using Jabel.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jabel.UI
{
    /// <summary>Displays <see cref="NotificationEvent"/>s as toasts that slide in, wait and fade out.</summary>
    [AddComponentMenu("Jabel/UI/Notification Toast View")]
    public class NotificationToastView : JabelBehaviour
    {
        [SerializeField] private RectTransform toastPrefab;
        [SerializeField] private RectTransform container;
        [SerializeField] private float holdTime = 2.2f;
        [SerializeField] private int maxVisible = 3;
        [SerializeField] private float spacing = 8f;
        [SerializeField] private Color infoColor = new Color(0.15f, 0.2f, 0.28f, 0.95f);
        [SerializeField] private Color successColor = new Color(0.12f, 0.45f, 0.22f, 0.95f);
        [SerializeField] private Color warningColor = new Color(0.55f, 0.2f, 0.15f, 0.95f);
        [SerializeField] private Color achievementColor = new Color(0.6f, 0.45f, 0.08f, 0.95f);

        private sealed class Toast
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public float Time;
            public float Y;
        }

        private readonly List<Toast> _toasts = new List<Toast>();
        private readonly Queue<NotificationEvent> _queue = new Queue<NotificationEvent>();

        protected override void OnBind()
        {
            if (container == null) container = (RectTransform)transform;
            if (toastPrefab != null) toastPrefab.gameObject.SetActive(false);
            Manager.Events.Subscribe<NotificationEvent>(OnNotification);
        }

        protected override void OnUnbind() => Manager.Events.Unsubscribe<NotificationEvent>(OnNotification);

        private void OnNotification(NotificationEvent evt) => _queue.Enqueue(evt);

        private void Show(NotificationEvent evt)
        {
            var rect = Instantiate(toastPrefab, container);
            rect.gameObject.SetActive(true);
            var text = rect.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = evt.Text;
            var bg = rect.GetComponent<Image>();
            if (bg != null)
            {
                switch (evt.Style)
                {
                    case NotificationStyle.Success: bg.color = successColor; break;
                    case NotificationStyle.Warning: bg.color = warningColor; break;
                    case NotificationStyle.Achievement: bg.color = achievementColor; break;
                    default: bg.color = infoColor; break;
                }
            }
            var icon = rect.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = evt.Icon;
                icon.gameObject.SetActive(evt.Icon != null);
            }
            if (!rect.TryGetComponent<CanvasGroup>(out var group)) group = rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            _toasts.Insert(0, new Toast { Rect = rect, Group = group, Time = 0, Y = rect.rect.height });
        }

        private void Update()
        {
            if (!IsBound) return;
            if (_queue.Count > 0 && _toasts.Count < maxVisible) Show(_queue.Dequeue());

            float dt = Time.unscaledDeltaTime;
            float y = 0;
            for (int i = 0; i < _toasts.Count; i++)
            {
                var toast = _toasts[i];
                toast.Time += dt;
                float total = holdTime + 0.6f;
                if (toast.Time >= total)
                {
                    Destroy(toast.Rect.gameObject);
                    _toasts.RemoveAt(i--);
                    continue;
                }

                float appear = Ease.OutBack(toast.Time / 0.35f);
                float fade = toast.Time > holdTime ? 1 - (toast.Time - holdTime) / 0.6f : 1;
                toast.Group.alpha = Mathf.Clamp01(Mathf.Min(appear, fade));
                toast.Y = Mathf.Lerp(toast.Y, y, 1 - Mathf.Exp(-14 * dt));
                toast.Rect.anchoredPosition = new Vector2((1 - Mathf.Clamp01(appear)) * 60f, -toast.Y);
                toast.Rect.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(appear));
                y += toast.Rect.rect.height + spacing;
            }
        }
    }
}
