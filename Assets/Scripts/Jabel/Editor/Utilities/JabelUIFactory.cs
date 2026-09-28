using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Jabel.Editor
{
    /// <summary>Code-first uGUI construction helpers used by the scene builders.</summary>
    public static class JabelUIFactory
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        public static TMP_FontAsset DefaultFont => TMP_Settings.defaultFontAsset;

        private static TMP_FontAsset _regularFont, _boldFont;

        /// <summary>Font for normal text. Builders may override it (reset with <see cref="ResetFonts"/>).</summary>
        public static TMP_FontAsset RegularFont
        {
            get => _regularFont != null ? _regularFont : DefaultFont;
            set => _regularFont = value;
        }

        /// <summary>Font for accented text (texts created with FontStyles.Bold). Falls back to faux bold of the regular font.</summary>
        public static TMP_FontAsset BoldFont
        {
            get => _boldFont;
            set => _boldFont = value;
        }

        public static void ResetFonts()
        {
            _regularFont = null;
            _boldFont = null;
        }

        /// <summary>
        /// Picks the font face for a style: bold texts use the dedicated bold font when one is set
        /// (a real bold face instead of synthetic emboldening).
        /// </summary>
        public static void ApplyFont(TMP_Text text, FontStyles style)
        {
            bool bold = (style & FontStyles.Bold) != 0;
            if (bold && _boldFont != null)
            {
                text.font = _boldFont;
                text.fontStyle = style & ~FontStyles.Bold;
            }
            else
            {
                text.font = RegularFont;
                text.fontStyle = style;
            }
        }

        private const string OutlineMaterialPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat";

        /// <summary>
        /// Gives a text a dark outline through a shared outline material preset
        /// (setting outlineWidth in edit mode would instantiate and leak a material).
        /// </summary>
        public static void ApplyOutline(TMP_Text text)
        {
            if (text.font == null) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (material == null || material.mainTexture != text.font.atlasTexture)
                material = JabelFontUtility.OutlineMaterial(text.font);
            if (material != null) text.fontSharedMaterial = material;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        public static Canvas CreateCanvas(string name, Camera camera, int sortingOrder, float planeDistance = 5f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            if (camera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = planeDistance;
            }
            else canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>Anchors at a normalized point with a fixed size.</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image CreateImage(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sliced && sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            var rect = CreateRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            ApplyFont(tmp, style);
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        public static Button CreateButton(Transform parent, string name, string label, Sprite sprite, Color color,
            Vector2 size, float fontSize = 30, bool juice = true)
        {
            var image = CreateImage(parent, name, sprite, color, sliced: true);
            image.raycastTarget = true;
            image.rectTransform.sizeDelta = size;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            // Juice handles hover/press, so the built-in color transition stays subtle.
            var colors = button.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            button.colors = colors;

            if (!string.IsNullOrEmpty(label))
            {
                var text = CreateText(image.transform, "Label", label, fontSize, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                Stretch(text.rectTransform, 8, 8, 4, 4);
            }

            if (juice)
            {
                var j = image.gameObject.AddComponent<Jabel.UI.ButtonJuice>();
                JabelEditorUtility.Set(j, "tintTarget", image);
            }
            return button;
        }

        /// <summary>Vertical scroll list. Returns the content rect (children are laid out automatically).</summary>
        public static RectTransform CreateVerticalScroll(Transform parent, string name, float spacing = 10, int padding = 10)
        {
            var root = CreateRect(name, parent);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40;

            var viewport = CreateRect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        public static LayoutElement Layout(Component c, float preferredHeight = -1, float preferredWidth = -1, float flexibleWidth = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
            if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            return le;
        }

        /// <summary>A burst-only particle system (emits nothing by itself; ClickParticlesFeedback calls Emit).</summary>
        public static ParticleSystem CreateBurstParticles(Transform parent, string name, Material material, Gradient colors,
            float speedMin, float speedMax, float sizeMin, float sizeMax, float lifetime, float gravity, int sortingOrder)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(parent, false);
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new ParticleSystem.MinMaxGradient(colors) { mode = ParticleSystemGradientMode.RandomColor };
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 1000;

            var emission = ps.emission;
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.7f, 0.8f), new Keyframe(1, 0)));

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            color.color = fade;

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-3, 3);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }
    }
}
