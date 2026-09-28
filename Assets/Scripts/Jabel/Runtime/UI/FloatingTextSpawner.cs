using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Jabel.UI
{
    /// <summary>
    /// Pooled "+123" texts that pop, rise and fade. Works with UI texts (TextMeshProUGUI, positions in
    /// screen space) and world texts (TextMeshPro, positions in world space).
    /// </summary>
    [AddComponentMenu("Jabel/UI/Floating Text Spawner")]
    public class FloatingTextSpawner : MonoBehaviour
    {
        [SerializeField] private TMP_Text prefab;
        [SerializeField] private Transform container;
        [SerializeField] private int prewarm = 8;
        [SerializeField] private int maxAlive = 60;
        [SerializeField] private float lifetime = 1.1f;
        [Tooltip("Rise distance: pixels for UI, units for world texts.")]
        [SerializeField] private float rise = 120f;
        [SerializeField] private float horizontalJitter = 30f;
        [SerializeField] private float popScale = 1.35f;

        private sealed class Item
        {
            public TMP_Text Text;
            public Vector3 Start;
            public Vector3 Drift;
            public float Time;
            public float Scale;
            public Color Color;
        }

        private readonly List<Item> _alive = new List<Item>();
        private readonly Stack<Item> _pool = new Stack<Item>();
        private bool _isUI;

        private void Awake()
        {
            if (container == null) container = transform;
            if (prefab == null) return;
            _isUI = prefab is TextMeshProUGUI;
            // Hide scene templates; never touch prefab assets.
            if (prefab.gameObject.scene.IsValid()) prefab.gameObject.SetActive(false);
            for (int i = 0; i < prewarm; i++) _pool.Push(Create());
        }

        private Item Create()
        {
            var text = Instantiate(prefab, container);
            text.gameObject.SetActive(false);
            text.raycastTarget = false;
            return new Item { Text = text };
        }

        /// <summary>Spawns at a world position (world texts) or canvas-space position (UI texts).</summary>
        public void Spawn(string content, Vector3 position, Color color, float scale = 1f)
        {
            if (prefab == null) return;

            Item item;
            if (_alive.Count >= maxAlive)
            {
                // Recycle the oldest instead of dropping feedback entirely.
                item = _alive[0];
                _alive.RemoveAt(0);
            }
            else item = _pool.Count > 0 ? _pool.Pop() : Create();

            item.Text.text = content;
            item.Text.color = color;
            item.Color = color;
            item.Start = position;
            item.Drift = new Vector3(Random.Range(-horizontalJitter, horizontalJitter), rise * Random.Range(0.85f, 1.15f), 0);
            item.Time = 0;
            item.Scale = scale;
            item.Text.transform.SetAsLastSibling();
            item.Text.gameObject.SetActive(true);
            Place(item);
            _alive.Add(item);
        }

        /// <summary>Spawns a UI text at a screen position (converts to the container's space).</summary>
        public void SpawnAtScreen(string content, Vector2 screenPosition, Color color, Camera eventCamera, float scale = 1f)
        {
            if (!_isUI) return;
            var rect = container as RectTransform;
            if (rect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, screenPosition, eventCamera, out var world))
                Spawn(content, world, color, scale);
        }

        private void Place(Item item)
        {
            float t = item.Time / lifetime;
            float move = Ease.OutCubic(t);
            // Pixels for UI are converted to world through the container's scale.
            Vector3 drift = item.Drift * move;
            if (_isUI) drift = container.TransformVector(drift);
            item.Text.transform.position = item.Start + drift;

            float pop = t < 0.15f ? Mathf.Lerp(0.3f, popScale, t / 0.15f) : Mathf.Lerp(popScale, 1f, Ease.OutCubic((t - 0.15f) / 0.3f));
            item.Text.transform.localScale = Vector3.one * (pop * item.Scale);

            var c = item.Color;
            c.a = t < 0.6f ? 1 : 1 - (t - 0.6f) / 0.4f;
            item.Text.color = c;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var item = _alive[i];
                item.Time += dt;
                if (item.Time >= lifetime)
                {
                    item.Text.gameObject.SetActive(false);
                    _alive.RemoveAt(i);
                    _pool.Push(item);
                    continue;
                }
                Place(item);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text textPrefab, Transform root, float riseDistance, float jitter)
        {
            prefab = textPrefab;
            container = root;
            rise = riseDistance;
            horizontalJitter = jitter;
        }
#endif
    }
}
